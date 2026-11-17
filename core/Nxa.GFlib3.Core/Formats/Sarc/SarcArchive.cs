// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using System.Text;

namespace Nxa.GFlib3.Core.Formats.Sarc;

/// <summary>Reads SARC archives as used inside GFlib3 packs.</summary>
public sealed class SarcArchive : IDisposable
{
    public const uint Magic = 0x43524153; // "SARC"
    public const uint SfatMagic = 0x54414653; // "SFAT"
    public const uint SfntMagic = 0x544E4653; // "SFNT"
    public const int HeaderSize = 0x14;
    public const int SfatEntrySize = 0x10;
    public const int SfntHeaderSize = 0x08;

    private const int MaxNodeCount = 1_000_000;
    private const int MaxNameLength = 4096;

    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly long _length;
    private readonly Dictionary<uint, int> _hashIndex = new();
    private readonly object _sync = new();
    private long _logicalLength;
    private bool _disposed;

    private SarcArchive(Stream stream, bool ownsStream)
    {
        _stream = stream;
        _ownsStream = ownsStream;
        _length = stream.Length;

        try
        {
            Parse();
        }
        catch
        {
            if (ownsStream)
            {
                stream.Dispose();
            }

            throw;
        }
    }

    /// <summary>Header size from the SARC header (0x14 in the dump).</summary>
    public ushort HeaderSizeValue { get; private set; }

    public ushort Bom { get; private set; }

    public uint DeclaredFileSize { get; private set; }

    public uint DataOffset { get; private set; }

    public ushort Version { get; private set; }

    public uint HashKey { get; private set; }

    public IReadOnlyList<SarcEntry> Entries { get; private set; } = Array.Empty<SarcEntry>();

    public static SarcArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 16,
            FileOptions.RandomAccess);

        return new SarcArchive(stream, ownsStream: true);
    }

    public static SarcArchive Read(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("The stream must be readable and seekable.", nameof(stream));
        }

        return new SarcArchive(stream, ownsStream: !leaveOpen);
    }

    public bool TryGetEntryByHash(uint hash, out SarcEntry? entry)
    {
        if (_hashIndex.TryGetValue(hash, out int index))
        {
            entry = Entries[index];
            return true;
        }

        entry = null;
        return false;
    }

    public bool TryGetEntryByName(string name, out SarcEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(name);

        uint hash = SarcHash.Compute(name, HashKey);
        if (_hashIndex.TryGetValue(hash, out int index))
        {
            SarcEntry candidate = Entries[index];
            if (string.Equals(candidate.Name, name, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public byte[] ReadEntry(SarcEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);

        var data = new byte[entry.Size];
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ReadExactlyAt(entry.AbsoluteOffset, data);
        }

        return data;
    }

    private void Parse()
    {
        if (_length < HeaderSize)
        {
            throw new InvalidDataException("File is too small for a SARC header.");
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        ReadExactlyAt(0, header);

        uint magic = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (magic != Magic)
        {
            throw new InvalidDataException($"Not a SARC file (magic 0x{magic:X8}).");
        }

        HeaderSizeValue = BinaryPrimitives.ReadUInt16LittleEndian(header[0x04..]);
        Bom = BinaryPrimitives.ReadUInt16LittleEndian(header[0x06..]);
        DeclaredFileSize = BinaryPrimitives.ReadUInt32LittleEndian(header[0x08..]);
        DataOffset = BinaryPrimitives.ReadUInt32LittleEndian(header[0x0C..]);
        Version = BinaryPrimitives.ReadUInt16LittleEndian(header[0x10..]);

        if (HeaderSizeValue < HeaderSize || HeaderSizeValue > _length)
        {
            throw new InvalidDataException($"Invalid SARC header size {HeaderSizeValue}.");
        }

        if (Bom != 0xFEFF)
        {
            throw new InvalidDataException($"Invalid SARC byte-order mark 0x{Bom:X4}.");
        }

        if (DeclaredFileSize > _length)
        {
            throw new InvalidDataException(
                $"SARC declares 0x{DeclaredFileSize:X} bytes but only 0x{_length:X} are present.");
        }

        _logicalLength = DeclaredFileSize == 0 ? _length : DeclaredFileSize;

        long sfatOffset = HeaderSizeValue;
        RequireRange(sfatOffset, 0x0C);

        Span<byte> sfat = stackalloc byte[0x0C];
        ReadExactlyAt(sfatOffset, sfat);

        uint sfatMagic = BinaryPrimitives.ReadUInt32LittleEndian(sfat);
        if (sfatMagic != SfatMagic)
        {
            throw new InvalidDataException($"Missing SFAT table (magic 0x{sfatMagic:X8}).");
        }

        ushort sfatHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(sfat[0x04..]);
        ushort nodeCount = BinaryPrimitives.ReadUInt16LittleEndian(sfat[0x06..]);
        HashKey = BinaryPrimitives.ReadUInt32LittleEndian(sfat[0x08..]);

        if (sfatHeaderSize < 0x0C || (int)nodeCount > MaxNodeCount)
        {
            throw new InvalidDataException("Invalid SFAT header.");
        }

        long nodesOffset = checked(sfatOffset + sfatHeaderSize);
        long nodesSize = checked((long)nodeCount * SfatEntrySize);
        RequireRange(nodesOffset, nodesSize);

        long sfntOffset = checked(nodesOffset + nodesSize);
        RequireRange(sfntOffset, SfntHeaderSize);

        Span<byte> sfnt = stackalloc byte[SfntHeaderSize];
        ReadExactlyAt(sfntOffset, sfnt);

        uint sfntMagic = BinaryPrimitives.ReadUInt32LittleEndian(sfnt);
        if (sfntMagic != SfntMagic)
        {
            throw new InvalidDataException($"Missing SFNT table (magic 0x{sfntMagic:X8}).");
        }

        ushort sfntHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(sfnt[0x04..]);
        if (sfntHeaderSize < SfntHeaderSize)
        {
            throw new InvalidDataException($"Invalid SFNT header size {sfntHeaderSize}.");
        }

        long nameTableBase = checked(sfntOffset + sfntHeaderSize);
        long nameTableEnd = DataOffset;
        if (nameTableEnd <= nameTableBase)
        {
            nameTableEnd = _logicalLength;
        }

        RequireRange(nameTableBase, nameTableEnd - nameTableBase);
        var nameTable = new byte[nameTableEnd - nameTableBase];
        ReadExactlyAt(nameTableBase, nameTable);

        var nodes = new byte[nodesSize];
        ReadExactlyAt(nodesOffset, nodes);
        Entries = ReadEntries(nodes, nodeCount, nameTable);
    }

    private SarcEntry[] ReadEntries(byte[] nodes, int nodeCount, byte[] nameTable)
    {
        var entries = new SarcEntry[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            ReadOnlySpan<byte> raw = nodes.AsSpan(i * SfatEntrySize, SfatEntrySize);
            uint hash = BinaryPrimitives.ReadUInt32LittleEndian(raw);
            uint attributes = BinaryPrimitives.ReadUInt32LittleEndian(raw[4..]);
            uint dataStart = BinaryPrimitives.ReadUInt32LittleEndian(raw[8..]);
            uint dataEnd = BinaryPrimitives.ReadUInt32LittleEndian(raw[12..]);

            if (dataEnd < dataStart)
            {
                throw new InvalidDataException($"Entry {i} has an invalid data range.");
            }

            if (dataEnd - dataStart > int.MaxValue)
            {
                throw new InvalidDataException($"Entry {i} is too large.");
            }

            long absolute = (long)DataOffset + dataStart;
            long absoluteEnd = (long)DataOffset + dataEnd;
            if (absoluteEnd > _logicalLength)
            {
                throw new InvalidDataException($"Entry {i} data lies outside the archive.");
            }

            string name = ReadName(nameTable, attributes);

            entries[i] = new SarcEntry(i, name, hash, attributes, dataStart, dataEnd, absolute);
            _hashIndex.TryAdd(hash, i);
        }

        return entries;
    }

    private static string ReadName(byte[] nameTable, uint attributes)
    {
        long nameOffset = (long)(attributes & 0xFFFFFF) << 2;
        if (nameOffset >= nameTable.Length)
        {
            throw new InvalidDataException($"Name offset 0x{nameOffset:X} lies outside the name table.");
        }

        ReadOnlySpan<byte> remainder = nameTable.AsSpan((int)nameOffset);
        int terminator = remainder.IndexOf((byte)0);
        if (terminator < 0 || terminator > MaxNameLength)
        {
            throw new InvalidDataException("SARC entry name is not null-terminated.");
        }

        return Encoding.UTF8.GetString(remainder[..terminator]);
    }

    private void ValidateEntry(SarcEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)entry.Index >= (uint)Entries.Count || !ReferenceEquals(Entries[entry.Index], entry))
        {
            throw new ArgumentException("The entry does not belong to this archive.", nameof(entry));
        }
    }

    private void ReadExactlyAt(long offset, Span<byte> buffer)
    {
        RequireRange(offset, buffer.Length);
        _stream.Position = offset;
        _stream.ReadExactly(buffer);
    }

    private void RequireRange(long offset, long size)
    {
        if (offset < 0 || size < 0 || offset > _length || size > _length - offset)
        {
            throw new InvalidDataException(
                $"Range 0x{offset:X} + 0x{size:X} lies outside the file (length 0x{_length:X}).");
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_ownsStream)
            {
                _stream.Dispose();
            }
        }
    }
}
