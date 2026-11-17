// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Formats.Bntx;

/// <summary>A single BNTX texture entry (BRTI block).</summary>
public sealed class BntxTexture
{
    private readonly byte[] _data;
    private readonly int _fileSize;

    internal BntxTexture(
        byte[] data,
        int fileSize,
        int index,
        string name,
        BntxFormat format,
        byte flags,
        byte storageDimension,
        ushort tileMode,
        ushort swizzle,
        ushort mipCount,
        ushort sampleCount,
        uint gpuAccess,
        uint width,
        uint height,
        uint depth,
        uint arrayCount,
        int blockHeightLog2,
        uint layout2,
        uint imageSize,
        uint alignment,
        byte channelRed,
        byte channelGreen,
        byte channelBlue,
        byte channelAlpha,
        uint surfaceDimension,
        uint nextBlock,
        uint sectionSize,
        long parentOffset,
        long[] mipOffsets)
    {
        _data = data;
        _fileSize = Math.Min(data.Length, fileSize);
        Index = index;
        Name = name;
        Format = format;
        Flags = flags;
        StorageDimension = storageDimension;
        TileMode = tileMode;
        Swizzle = swizzle;
        MipCount = mipCount;
        SampleCount = sampleCount;
        GpuAccess = gpuAccess;
        Width = width;
        Height = height;
        Depth = depth;
        ArrayCount = arrayCount;
        BlockHeightLog2 = blockHeightLog2;
        Layout2 = layout2;
        ImageSize = imageSize;
        Alignment = alignment;
        ChannelRed = channelRed;
        ChannelGreen = channelGreen;
        ChannelBlue = channelBlue;
        ChannelAlpha = channelAlpha;
        SurfaceDimension = surfaceDimension;
        NextBlock = nextBlock;
        SectionSize = sectionSize;
        ParentOffset = parentOffset;
        MipOffsets = mipOffsets;
    }

    public int Index { get; }

    public string Name { get; }

    public BntxFormat Format { get; }

    public byte Flags { get; }

    public byte StorageDimension { get; }

    /// <summary>0 = block-linear, 1 = pitch-linear (see <c>docs/bntx.md</c>).</summary>
    public ushort TileMode { get; }

    public ushort Swizzle { get; }

    public ushort MipCount { get; }

    public ushort SampleCount { get; }

    public uint GpuAccess { get; }

    public uint Width { get; }

    public uint Height { get; }

    public uint Depth { get; }

    public uint ArrayCount { get; }

    /// <summary>Log2 of the block height in GOB rows (texture layout low bits).</summary>
    public int BlockHeightLog2 { get; }

    public uint Layout2 { get; }

    /// <summary>Total data size across all mips, including per-mip alignment.</summary>
    public uint ImageSize { get; }

    public uint Alignment { get; }

    public byte ChannelRed { get; }

    public byte ChannelGreen { get; }

    public byte ChannelBlue { get; }

    public byte ChannelAlpha { get; }

    public uint SurfaceDimension { get; }

    public uint NextBlock { get; }

    public uint SectionSize { get; }

    public long ParentOffset { get; }

    public IReadOnlyList<long> MipOffsets { get; }

    public bool IsSrgb => BntxFormats.IsSrgb(Format);

    public ReadOnlyMemory<byte> GetMipData(int mip)
    {
        if (mip < 0 || mip >= MipOffsets.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(mip), mip, $"Mip index must be 0..{MipOffsets.Count - 1}.");
        }

        long offset = MipOffsets[mip];
        long end = mip + 1 < MipOffsets.Count ? MipOffsets[mip + 1] : _fileSize;
        if (end > _fileSize)
        {
            end = _fileSize;
        }

        if (offset < 0 || end <= offset || end > _data.Length)
        {
            throw new InvalidDataException(
                $"BNTX '{Name}' mip {mip} range 0x{offset:X}..0x{end:X} is out of bounds.");
        }

        return _data.AsMemory((int)offset, (int)(end - offset));
    }

    public BntxImage DecodeMip(int mip)
    {
        return BntxTextureDecoder.Decode(this, mip);
    }

    public BntxImage DecodeMip0() => DecodeMip(0);

    internal int GetMipWidth(int mip)
    {
        return Math.Max(1, (int)(Width >> mip));
    }

    internal int GetMipHeight(int mip)
    {
        return Math.Max(1, (int)(Height >> mip));
    }
}

/// <summary>Decoded RGBA8 image, rows packed top-to-bottom.</summary>
public sealed record BntxImage(int Width, int Height, byte[] Pixels);

