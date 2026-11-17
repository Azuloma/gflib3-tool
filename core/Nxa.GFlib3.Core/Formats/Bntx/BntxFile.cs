// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using System.Text;

namespace Nxa.GFlib3.Core.Formats.Bntx;

/// <summary>Parsed BNTX texture container (ResTextureFile).</summary>
public sealed class BntxFile
{
    public const int HeaderSize = 0x58;
    public const int TextureInfoHeaderSize = 0x10;

    private const int MaxTextures = 4096;
    private const int MaxNameLength = 256;
    private const uint MaxDimension = 16384;
    private const uint MaxDepth = 1024;
    private const uint MaxArrayCount = 256;
    private const int MaxMipCount = 16;
    private const int MaxBlockHeightLog2 = 5;
    private const uint MaxAlignment = 65536;

    private readonly byte[] _data;

    private BntxFile(
        byte[] data,
        BntxVersion version,
        uint alignmentShift,
        uint targetAddressSize,
        string name,
        string platform,
        long relocationOffset,
        uint fileSize,
        BntxTexture[] textures)
    {
        _data = data;
        Version = version;
        AlignmentShift = alignmentShift;
        TargetAddressSize = targetAddressSize;
        Name = name;
        Platform = platform;
        RelocationOffset = relocationOffset;
        FileSize = fileSize;
        Textures = textures;
    }

    public BntxVersion Version { get; }

    public uint AlignmentShift { get; }

    public uint TargetAddressSize { get; }

    /// <summary>NUL-terminated container name from the header.</summary>
    public string Name { get; }

    public string Platform { get; }

    public long RelocationOffset { get; }

    public uint FileSize { get; }

    public IReadOnlyList<BntxTexture> Textures { get; }

    public static BntxFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Parse(File.ReadAllBytes(path));
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Failed to parse BNTX '{path}': {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException($"Unsupported BNTX '{path}': {ex.Message}", ex);
        }
    }

    public static BntxFile Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.Length < HeaderSize)
        {
            throw new InvalidDataException(
                $"BNTX header needs 0x{HeaderSize:X} bytes; the buffer has 0x{data.Length:X}.");
        }

        if (!data.AsSpan(0, 4).SequenceEqual("BNTX"u8))
        {
            throw new InvalidDataException("Missing BNTX magic.");
        }

        ReadOnlySpan<byte> bom = data.AsSpan(0x0C, 2);
        if (!bom.SequenceEqual(new byte[] { 0xFF, 0xFE }))
        {
            throw new NotSupportedException(
                $"BNTX byte-order mark 0x{BinaryPrimitives.ReadUInt16LittleEndian(bom):X4} is not little-endian.");
        }

        var version = new BntxVersion(
            Minor: data[0x09],
            Patch: data[0x08],
            Major: BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x0A)));

        uint alignmentShift = data[0x0E];
        if (alignmentShift > 20)
        {
            throw new InvalidDataException($"BNTX alignment shift {alignmentShift} is out of range.");
        }

        uint targetAddressSize = data[0x0F];
        if (targetAddressSize is not (32 or 64))
        {
            throw new InvalidDataException($"BNTX target address size {targetAddressSize} is not 32 or 64.");
        }

        string name = ReadHeaderName(data, BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x10)));

        string platform = Encoding.ASCII.GetString(data, 0x20, 4);
        uint textureCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x24));
        if (textureCount > MaxTextures)
        {
            throw new InvalidDataException($"BNTX texture count {textureCount} exceeds the limit.");
        }

        long infoArray = ReadOffset(data, 0x28, "texture-info array");
        CheckRange(data, infoArray, (long)textureCount * sizeof(ulong), "texture-info array");

        long dataBlock = ReadOffset(data, 0x30, "texture data block");
        CheckRange(data, dataBlock, TextureInfoHeaderSize, "texture data block");

        long relocationOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x18));
        uint fileSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0x1C));
        if (fileSize > data.Length)
        {
            throw new InvalidDataException(
                $"BNTX file size 0x{fileSize:X} exceeds the buffer length 0x{data.Length:X}.");
        }

        var textures = new BntxTexture[(int)textureCount];
        for (int i = 0; i < textures.Length; i++)
        {
            long pointer = ReadOffset(data, infoArray + (i * sizeof(ulong)), $"texture {i} pointer");
            textures[i] = ParseTexture(data, pointer, i, fileSize);
        }

        return new BntxFile(
            data,
            version,
            alignmentShift,
            targetAddressSize,
            name,
            platform,
            relocationOffset,
            fileSize,
            textures);
    }

    private static BntxTexture ParseTexture(byte[] data, long block, int index, uint fileSize)
    {
        string context = $"texture {index}";
        CheckRange(data, block, TextureInfoHeaderSize, context);

        if (!data.AsSpan((int)block, 4).SequenceEqual("BRTI"u8))
        {
            throw new InvalidDataException($"BNTX {context} at 0x{block:X} is missing BRTI magic.");
        }

        uint nextBlock = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)block + 0x04));
        uint sectionSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)block + 0x08));
        if (nextBlock < TextureInfoHeaderSize || block + nextBlock > data.Length)
        {
            throw new InvalidDataException(
                $"BNTX {context} next-block offset 0x{nextBlock:X} is out of range.");
        }

        if (sectionSize < TextureInfoHeaderSize)
        {
            throw new InvalidDataException(
                $"BNTX {context} section size 0x{sectionSize:X} is smaller than the block header.");
        }

        long info = block + TextureInfoHeaderSize;
        CheckRange(data, info, 0x90, context);

        byte flags = data[info];
        byte storageDimension = data[info + 0x01];
        ushort tileMode = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)info + 0x02));
        ushort swizzle = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)info + 0x04));
        ushort mipCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)info + 0x06));
        ushort sampleCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)info + 0x08));
        var format = (BntxFormat)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x0C));
        uint gpuAccess = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x10));
        uint width = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x14));
        uint height = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x18));
        uint depth = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x1C));
        uint arrayCount = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x20));
        uint layout = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x24));
        uint layout2 = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x28));
        uint imageSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x40));
        uint alignment = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x44));
        byte channelRed = data[info + 0x48];
        byte channelGreen = data[info + 0x49];
        byte channelBlue = data[info + 0x4A];
        byte channelAlpha = data[info + 0x4B];
        uint surfaceDimension = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)info + 0x4C));

        long nameOffset = ReadOffset(data, info + 0x50, context + " name");
        long parentOffset = ReadOffset(data, info + 0x58, context + " parent");
        long mipArray = ReadOffset(data, info + 0x60, context + " mip array");

        if (width is 0 or > MaxDimension || height is 0 or > MaxDimension)
        {
            throw new InvalidDataException(
                $"BNTX {context} dimensions {width}x{height} are out of range.");
        }

        if (depth is 0 or > MaxDepth || arrayCount is 0 or > MaxArrayCount)
        {
            throw new InvalidDataException(
                $"BNTX {context} depth {depth} / array count {arrayCount} are out of range.");
        }

        if (mipCount is 0 or > MaxMipCount)
        {
            throw new InvalidDataException($"BNTX {context} mip count {mipCount} is out of range.");
        }

        if (tileMode > 1)
        {
            throw new NotSupportedException($"BNTX {context} tile mode {tileMode} is not supported.");
        }

        int blockHeightLog2 = (int)(layout & 7);
        if (blockHeightLog2 > MaxBlockHeightLog2)
        {
            throw new InvalidDataException(
                $"BNTX {context} block-height log2 {blockHeightLog2} is out of range.");
        }

        if (alignment == 0 || alignment > MaxAlignment || (alignment & (alignment - 1)) != 0)
        {
            throw new InvalidDataException($"BNTX {context} alignment {alignment} is not a power of two.");
        }

        if (imageSize > data.Length)
        {
            throw new InvalidDataException(
                $"BNTX {context} image size 0x{imageSize:X} exceeds the buffer length 0x{data.Length:X}.");
        }

        string textureName = ReadName(data, nameOffset, context);
        CheckRange(data, mipArray, (long)mipCount * sizeof(ulong), context + " mip array");

        var mipOffsets = new long[mipCount];
        for (int mip = 0; mip < mipCount; mip++)
        {
            long offset = ReadOffset(data, mipArray + (mip * sizeof(ulong)), $"{context} mip {mip}");
            if (offset <= 0 || offset >= data.Length)
            {
                throw new InvalidDataException(
                    $"BNTX {context} mip {mip} offset 0x{offset:X} is out of range.");
            }

            if (mip > 0 && offset <= mipOffsets[mip - 1])
            {
                throw new InvalidDataException(
                    $"BNTX {context} mip {mip} offset 0x{offset:X} does not advance over mip {mip - 1}.");
            }

            mipOffsets[mip] = offset;
        }

        return new BntxTexture(
            data,
            (int)fileSize,
            index,
            textureName,
            format,
            flags,
            storageDimension,
            tileMode,
            swizzle,
            mipCount,
            sampleCount,
            gpuAccess,
            width,
            height,
            depth,
            arrayCount,
            blockHeightLog2,
            layout2,
            imageSize,
            alignment,
            channelRed,
            channelGreen,
            channelBlue,
            channelAlpha,
            surfaceDimension,
            nextBlock,
            sectionSize,
            parentOffset,
            mipOffsets);
    }

    private static string ReadHeaderName(byte[] data, long offset)
    {
        if (offset <= 0 || offset >= data.Length)
        {
            throw new InvalidDataException($"BNTX header name offset 0x{offset:X} is out of range.");
        }

        int end = Array.IndexOf(data, (byte)0, (int)offset, Math.Min(MaxNameLength, data.Length - (int)offset));
        if (end < 0)
        {
            throw new InvalidDataException($"BNTX header name at 0x{offset:X} is not NUL-terminated.");
        }

        return Encoding.UTF8.GetString(data, (int)offset, end - (int)offset);
    }

    private static string ReadName(byte[] data, long offset, string context)
    {
        CheckRange(data, offset, sizeof(ushort), context + " name");
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan((int)offset));
        if (length > MaxNameLength)
        {
            throw new InvalidDataException($"BNTX {context} name length {length} exceeds the limit.");
        }

        CheckRange(data, offset + sizeof(ushort), length, context + " name");
        return Encoding.UTF8.GetString(data, (int)offset + sizeof(ushort), length);
    }

    private static long ReadOffset(byte[] data, long offset, string context)
    {
        CheckRange(data, offset, sizeof(ulong), context);
        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan((int)offset));
        if (value > int.MaxValue)
        {
            throw new InvalidDataException($"BNTX {context} offset 0x{value:X} exceeds the address range.");
        }

        return (long)value;
    }

    private static void CheckRange(byte[] data, long offset, long length, string context)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
        {
            throw new InvalidDataException(
                $"BNTX {context} range 0x{offset:X}+0x{length:X} exceeds the buffer length 0x{data.Length:X}.");
        }
    }
}

public readonly record struct BntxVersion(ushort Major, byte Minor, byte Patch)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}

