// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;

namespace Nxa.GFlib3.Core.Formats.Gfpak;

/// <summary>Reads only the fixed 0x18-byte GFLXPACK header.</summary>
public sealed class GfpakHeader
{
    public const int Size = 0x18;
    public const int MaxFileCount = 1_000_000;
    public const int MaxFolderCount = 100_000;

    private GfpakHeader(uint version, bool isRelocated, int fileCount, int folderCount)
    {
        Version = version;
        IsRelocated = isRelocated;
        FileCount = fileCount;
        FolderCount = folderCount;
    }

    public uint Version { get; }

    public bool IsRelocated { get; }

    public int FileCount { get; }

    public int FolderCount { get; }

    /// <summary>Parses a header from the beginning of <paramref name="source"/>.</summary>
    /// <param name="source">Buffer that contains at least <see cref="Size"/> bytes.</param>
    public static GfpakHeader Parse(ReadOnlySpan<byte> source)
    {
        if (source.Length < Size)
        {
            throw new InvalidDataException(
                $"GFLXPACK header is truncated: {source.Length} bytes, expected at least {Size}.");
        }

        ulong magic = BinaryPrimitives.ReadUInt64LittleEndian(source);
        if (magic != GfpakArchive.Magic)
        {
            throw new InvalidDataException($"Not a GFLXPACK file (magic 0x{magic:X16}).");
        }

        uint version = BinaryPrimitives.ReadUInt32LittleEndian(source[0x08..]);
        bool isRelocated = BinaryPrimitives.ReadUInt32LittleEndian(source[0x0C..]) != 0;
        int fileCount = BinaryPrimitives.ReadInt32LittleEndian(source[0x10..]);
        int folderCount = BinaryPrimitives.ReadInt32LittleEndian(source[0x14..]);

        if (fileCount is < 0 or > MaxFileCount)
        {
            throw new InvalidDataException($"Invalid GFLXPACK file count {fileCount}.");
        }

        if (folderCount is < 0 or > MaxFolderCount)
        {
            throw new InvalidDataException($"Invalid GFLXPACK folder count {folderCount}.");
        }

        return new GfpakHeader(version, isRelocated, fileCount, folderCount);
    }

    /// <summary>Reads a header from the current position of <paramref name="stream"/>.</summary>
    /// <param name="stream">Readable stream positioned at the start of the header.</param>
    public static GfpakHeader Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        Span<byte> header = stackalloc byte[Size];
        try
        {
            stream.ReadExactly(header);
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException(
                $"GFLXPACK header is truncated: expected {Size} bytes.",
                ex);
        }

        return Parse(header);
    }

    /// <summary>Reads only the header of the archive at <paramref name="path"/>.</summary>
    public static GfpakHeader Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: Size,
                FileOptions.RandomAccess);
            return Read(stream);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Failed to read gfpak header '{path}': {ex.Message}", ex);
        }
    }
}
