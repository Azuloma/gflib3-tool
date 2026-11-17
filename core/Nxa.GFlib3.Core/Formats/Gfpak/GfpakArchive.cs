// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using System.IO.Compression;
using Nxa.GFlib3.Core.Compression;

namespace Nxa.GFlib3.Core.Formats.Gfpak;

/// <summary>Reads GFLXPACK (<c>.gfpak</c>) container archives.</summary>
public sealed class GfpakArchive : IDisposable
{
    public const ulong Magic = 0x4B434150584C4647; // "GFLXPACK"
    public const int HeaderSize = 0x18;
    public const int FileTableEntrySize = 0x18;
    public const int HashEntrySize = 0x10;

    private const int MaxFileCount = 1_000_000;
    private const int MaxFolderCount = 100_000;
    private const int MaxEntrySize = 1 << 30;

    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private readonly long _length;
    private readonly object _sync = new();
    private bool _disposed;

    private GfpakArchive(Stream stream, bool ownsStream)
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

    public uint Version { get; private set; }

    public bool IsRelocated { get; private set; }

    public IReadOnlyList<ulong> PathHashes { get; private set; } = Array.Empty<ulong>();

    public IReadOnlyList<GfpakFolder> Folders { get; private set; } = Array.Empty<GfpakFolder>();

    public IReadOnlyList<GfpakEntry> Entries { get; private set; } = Array.Empty<GfpakEntry>();

    public static GfpakArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 16,
            FileOptions.RandomAccess);

        return new GfpakArchive(stream, ownsStream: true);
    }

    /// <summary>Reads an archive from a seekable stream.</summary>
    /// <param name="stream">Backing stream. Must support reading and seeking.</param>
    /// <param name="leaveOpen">Keep <paramref name="stream"/> open when the archive is disposed.</param>
    public static GfpakArchive Read(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new ArgumentException("The stream must be readable and seekable.", nameof(stream));
        }

        return new GfpakArchive(stream, ownsStream: !leaveOpen);
    }

    public ulong GetPathHash(GfpakEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);
        return PathHashes[entry.Index];
    }

    public bool TryGetEntryByPathHash(ulong pathHash, out GfpakEntry? entry)
    {
        int index = -1;
        for (int i = 0; i < PathHashes.Count; i++)
        {
            if (PathHashes[i] == pathHash)
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            entry = null;
            return false;
        }

        entry = Entries[index];
        return true;
    }

    /// <summary>Reads an entry's packed (compressed) bytes.</summary>
    public byte[] ReadCompressed(GfpakEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ValidateEntry(entry);

        var data = new byte[entry.SizeCompressed];
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ReadExactlyAt(entry.OffsetPacked, data);
        }

        return data;
    }

    /// <summary>Reads and decompresses an entry.</summary>
    public byte[] ReadEntry(GfpakEntry entry)
    {
        byte[] packed = ReadCompressed(entry);
        return Decompress(entry, packed);
    }

    private static byte[] Decompress(GfpakEntry entry, byte[] packed)
    {
        switch (entry.Compression)
        {
            case CompressionType.None:
                if (packed.Length != entry.SizeDecompressed)
                {
                    throw new InvalidDataException(
                        $"Entry {entry.Index} is not compressed but its stored size differs from the declared size.");
                }

                return packed;

            case CompressionType.Zlib:
                return DecompressZlib(entry, packed);

            case CompressionType.OodleKraken:
                return OodleKraken.Decompress(packed, entry.SizeDecompressed);

            default:
                throw new NotSupportedException(
                    $"Entry {entry.Index} uses unsupported compression {entry.Compression}.");
        }
    }

    private static byte[] DecompressZlib(GfpakEntry entry, byte[] packed)
    {
        using var input = new MemoryStream(packed, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);

        var result = new byte[entry.SizeDecompressed];
        try
        {
            zlib.ReadExactly(result);
        }
        catch (EndOfStreamException)
        {
            throw new InvalidDataException($"Entry {entry.Index} ended before the declared size.");
        }

        if (zlib.ReadByte() != -1)
        {
            throw new InvalidDataException($"Entry {entry.Index} is larger than the declared size.");
        }

        return result;
    }

    private void Parse()
    {
        if (_length < HeaderSize)
        {
            throw new InvalidDataException("File is too small for a GFLXPACK header.");
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        ReadExactlyAt(0, header);

        ulong magic = BinaryPrimitives.ReadUInt64LittleEndian(header);
        if (magic != Magic)
        {
            throw new InvalidDataException($"Not a GFLXPACK file (magic 0x{magic:X16}).");
        }

        Version = BinaryPrimitives.ReadUInt32LittleEndian(header[0x08..]);
        IsRelocated = BinaryPrimitives.ReadUInt32LittleEndian(header[0x0C..]) != 0;
        int fileCount = BinaryPrimitives.ReadInt32LittleEndian(header[0x10..]);
        int folderCount = BinaryPrimitives.ReadInt32LittleEndian(header[0x14..]);

        if (fileCount is < 0 or > MaxFileCount)
        {
            throw new InvalidDataException($"Invalid file count {fileCount}.");
        }

        if (folderCount is < 0 or > MaxFolderCount)
        {
            throw new InvalidDataException($"Invalid folder count {folderCount}.");
        }

        long pointerBlockSize = checked(16L + (8L * folderCount));
        RequireRange(HeaderSize, pointerBlockSize);

        var pointers = new byte[pointerBlockSize];
        ReadExactlyAt(HeaderSize, pointers);

        long fileTableOffset = BinaryPrimitives.ReadInt64LittleEndian(pointers);
        long pathHashOffset = BinaryPrimitives.ReadInt64LittleEndian(pointers.AsSpan(8));
        var folderOffsets = new long[folderCount];
        for (int i = 0; i < folderCount; i++)
        {
            folderOffsets[i] = BinaryPrimitives.ReadInt64LittleEndian(pointers.AsSpan(16 + (i * 8)));
        }

        PathHashes = ReadPathHashes(pathHashOffset, fileCount);
        Entries = ReadFileTable(fileTableOffset, fileCount);
        Folders = ReadFolders(folderOffsets, fileCount);
    }

    private ulong[] ReadPathHashes(long offset, int fileCount)
    {
        long size = checked((long)fileCount * sizeof(ulong));
        RequireRange(offset, size);

        var buffer = new byte[size];
        ReadExactlyAt(offset, buffer);

        var hashes = new ulong[fileCount];
        for (int i = 0; i < fileCount; i++)
        {
            hashes[i] = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(i * sizeof(ulong)));
        }

        return hashes;
    }

    private GfpakEntry[] ReadFileTable(long offset, int fileCount)
    {
        long size = checked((long)fileCount * FileTableEntrySize);
        RequireRange(offset, size);

        var buffer = new byte[size];
        ReadExactlyAt(offset, buffer);

        var entries = new GfpakEntry[fileCount];
        for (int i = 0; i < fileCount; i++)
        {
            ReadOnlySpan<byte> raw = buffer.AsSpan(i * FileTableEntrySize, FileTableEntrySize);
            ushort level = BinaryPrimitives.ReadUInt16LittleEndian(raw);
            var compression = (CompressionType)raw[2];

            int sizeDecompressed = ReadEntrySize(raw[4..], "decompressed", i);
            int sizeCompressed = ReadEntrySize(raw[8..], "compressed", i);
            long offsetPacked = BinaryPrimitives.ReadUInt32LittleEndian(raw[16..]);
            uint unused = BinaryPrimitives.ReadUInt32LittleEndian(raw[20..]);

            if (sizeCompressed > 0)
            {
                RequireRange(offsetPacked, sizeCompressed);
            }
            else if (offsetPacked > _length)
            {
                throw new InvalidDataException($"Entry {i} has an out-of-range packed offset.");
            }

            entries[i] = new GfpakEntry(
                i,
                level,
                compression,
                sizeDecompressed,
                sizeCompressed,
                offsetPacked,
                unused);
        }

        return entries;
    }

    private static int ReadEntrySize(ReadOnlySpan<byte> raw, string kind, int index)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(raw);
        if (value > MaxEntrySize)
        {
            throw new InvalidDataException($"Entry {index} has an invalid {kind} size {value}.");
        }

        return (int)value;
    }

    private GfpakFolder[] ReadFolders(long[] offsets, int fileCount)
    {
        var folders = new GfpakFolder[offsets.Length];
        Span<byte> info = stackalloc byte[HashEntrySize];
        for (int i = 0; i < offsets.Length; i++)
        {
            long offset = offsets[i];
            RequireRange(offset, HashEntrySize);

            ReadExactlyAt(offset, info);

            ulong nameHash = BinaryPrimitives.ReadUInt64LittleEndian(info);
            int entryCount = BinaryPrimitives.ReadInt32LittleEndian(info[8..]);
            if (entryCount < 0 || entryCount > fileCount)
            {
                throw new InvalidDataException($"Folder {i} has invalid file count {entryCount}.");
            }

            long entriesOffset = checked(offset + HashEntrySize);
            long entriesSize = checked((long)entryCount * HashEntrySize);
            RequireRange(entriesOffset, entriesSize);

            var raw = new byte[entriesSize];
            ReadExactlyAt(entriesOffset, raw);

            var files = new GfpakFolderFile[entryCount];
            for (int j = 0; j < entryCount; j++)
            {
                ReadOnlySpan<byte> entry = raw.AsSpan(j * HashEntrySize, HashEntrySize);
                ulong fileHash = BinaryPrimitives.ReadUInt64LittleEndian(entry);
                int fileIndex = BinaryPrimitives.ReadInt32LittleEndian(entry[8..]);
                if ((uint)fileIndex >= (uint)fileCount)
                {
                    throw new InvalidDataException(
                        $"Folder {i} references file index {fileIndex}, out of range.");
                }

                files[j] = new GfpakFolderFile(fileHash, fileIndex);
            }

            folders[i] = new GfpakFolder(nameHash, files);
        }

        return folders;
    }

    private void ValidateEntry(GfpakEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if ((uint)entry.Index >= (uint)Entries.Count)
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
