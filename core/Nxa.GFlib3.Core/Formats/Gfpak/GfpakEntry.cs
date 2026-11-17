// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Compression;

namespace Nxa.GFlib3.Core.Formats.Gfpak;

/// <summary>One entry in a GFLXPACK file table.</summary>
public sealed class GfpakEntry
{
    internal GfpakEntry(
        int index,
        ushort level,
        CompressionType compression,
        int sizeDecompressed,
        int sizeCompressed,
        long offsetPacked,
        uint unused)
    {
        Index = index;
        Level = level;
        Compression = compression;
        SizeDecompressed = sizeDecompressed;
        SizeCompressed = sizeCompressed;
        OffsetPacked = offsetPacked;
        Unused = unused;
    }

    /// <summary>Index into the file table, also the folder-table file index.</summary>
    public int Index { get; }

    /// <summary>Quality level recorded by the packer (9 in the dump).</summary>
    public ushort Level { get; }

    public CompressionType Compression { get; }

    public int SizeDecompressed { get; }

    public int SizeCompressed { get; }

    public long OffsetPacked { get; }

    public uint Unused { get; }
}
