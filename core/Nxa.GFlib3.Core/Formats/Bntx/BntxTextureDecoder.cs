// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using BCnEncoder.Decoder;
using BCnEncoder.Shared;

namespace Nxa.GFlib3.Core.Formats.Bntx;

/// <summary>Decodes a BNTX mip level to tightly packed RGBA8.</summary>
public static class BntxTextureDecoder
{
    private const long MaxPixelCount = 64L * 1024 * 1024;

    public static BntxImage Decode(BntxTexture texture, int mip)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (texture.Depth != 1 || texture.ArrayCount != 1)
        {
            throw new NotSupportedException(
                $"BNTX '{texture.Name}' has depth {texture.Depth} / array count {texture.ArrayCount}; " +
                "only 2D single-surface textures are supported.");
        }

        if (texture.SampleCount != 1)
        {
            throw new NotSupportedException(
                $"BNTX '{texture.Name}' has {texture.SampleCount} samples; multisampled textures are not supported.");
        }

        BntxFormatLayout layout = BntxFormats.Describe(texture.Format);
        int width = texture.GetMipWidth(mip);
        int height = texture.GetMipHeight(mip);
        long pixelCount = (long)width * height;
        if (pixelCount > MaxPixelCount)
        {
            throw new InvalidDataException(
                $"BNTX '{texture.Name}' mip {mip} has {pixelCount} pixels, exceeding the limit.");
        }

        int blocksWide = (width + layout.BlockWidth - 1) / layout.BlockWidth;
        int blocksHigh = (height + layout.BlockHeight - 1) / layout.BlockHeight;
        int tightSize = checked(blocksWide * blocksHigh * layout.BytesPerBlock);
        byte[] tight = new byte[tightSize];

        ExpandToTight(texture, mip, tight, blocksWide, blocksHigh, layout.BytesPerBlock, layout);

        return layout.IsCompressed
            ? DecodeBlockCompressed(texture, tight, width, height, layout)
            : DecodeUncompressed(texture, tight, width, height, layout);
    }

    private static void ExpandToTight(
        BntxTexture texture,
        int mip,
        Span<byte> tight,
        int blocksWide,
        int blocksHigh,
        int bytesPerBlock,
        BntxFormatLayout layout)
    {
        ReadOnlySpan<byte> source = texture.GetMipData(mip).Span;
        int pitch = TegraBlockLinear.GetPitch(blocksWide, bytesPerBlock, texture.TileMode);

        for (int blockY = 0; blockY < blocksHigh; blockY++)
        {
            for (int blockX = 0; blockX < blocksWide; blockX++)
            {
                long sourceOffset = texture.TileMode == 0
                    ? TegraBlockLinear.GetBlockOffset(blockX, blockY, blocksWide, bytesPerBlock, texture.BlockHeightLog2)
                    : ((long)blockY * pitch) + ((long)blockX * bytesPerBlock);

                if (sourceOffset < 0 || sourceOffset + bytesPerBlock > source.Length)
                {
                    throw new InvalidDataException(
                        $"BNTX '{texture.Name}' mip {mip} block ({blockX},{blockY}) needs " +
                        $"0x{sourceOffset:X}+0x{bytesPerBlock:X} bytes; payload is 0x{source.Length:X}.");
                }

                source.Slice((int)sourceOffset, bytesPerBlock)
                    .CopyTo(tight.Slice(((blockY * blocksWide) + blockX) * bytesPerBlock, bytesPerBlock));
            }
        }
    }

    private static BntxImage DecodeBlockCompressed(
        BntxTexture texture,
        byte[] tight,
        int width,
        int height,
        BntxFormatLayout layout)
    {
        CompressionFormat format = layout.Channel switch
        {
            BntxChannelLayout.Bc1 => CompressionFormat.Bc1,
            BntxChannelLayout.Bc2 => CompressionFormat.Bc2,
            BntxChannelLayout.Bc3 => CompressionFormat.Bc3,
            BntxChannelLayout.Bc4 => CompressionFormat.Bc4,
            BntxChannelLayout.Bc5 => CompressionFormat.Bc5,
            BntxChannelLayout.Bc6H => layout.DataType == BntxDataType.Snorm
                ? CompressionFormat.Bc6S
                : CompressionFormat.Bc6U,
            BntxChannelLayout.Bc7 => CompressionFormat.Bc7,
            _ => throw new NotSupportedException(
                $"BNTX '{texture.Name}' format 0x{(uint)texture.Format:X4} is not block-compressed."),
        };

        ColorRgba32[] pixels = new BcDecoder().DecodeRaw(tight, width, height, format);
        if (pixels.Length != width * height)
        {
            throw new InvalidDataException(
                $"BNTX '{texture.Name}' decoder returned {pixels.Length} pixels for {width}x{height}.");
        }

        byte[] output = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            ColorRgba32 pixel = pixels[i];
            int offset = i * 4;
            output[offset] = ApplyChannel(texture.ChannelRed, pixel.r, pixel.g, pixel.b, pixel.a);
            output[offset + 1] = ApplyChannel(texture.ChannelGreen, pixel.r, pixel.g, pixel.b, pixel.a);
            output[offset + 2] = ApplyChannel(texture.ChannelBlue, pixel.r, pixel.g, pixel.b, pixel.a);
            output[offset + 3] = ApplyChannel(texture.ChannelAlpha, pixel.r, pixel.g, pixel.b, pixel.a);
        }

        return new BntxImage(width, height, output);
    }

    private static BntxImage DecodeUncompressed(
        BntxTexture texture,
        byte[] tight,
        int width,
        int height,
        BntxFormatLayout layout)
    {
        int pixelCount = checked(width * height);
        byte[] output = new byte[checked(pixelCount * 4)];

        for (int i = 0; i < pixelCount; i++)
        {
            ReadOnlySpan<byte> pixel = tight.AsSpan(i * layout.BytesPerBlock, layout.BytesPerBlock);
            (byte r, byte g, byte b, byte a) = layout.Channel switch
            {
                BntxChannelLayout.R8 => (pixel[0], (byte)0, (byte)0, (byte)255),
                BntxChannelLayout.R8G8 => (pixel[0], pixel[1], (byte)0, (byte)255),
                BntxChannelLayout.R5G6B5 => DecodeR5G6B5(pixel),
                BntxChannelLayout.R16 => DecodeR16(pixel, layout.DataType),
                BntxChannelLayout.R8G8B8A8 => (pixel[0], pixel[1], pixel[2], pixel[3]),
                BntxChannelLayout.B8G8R8A8 => (pixel[2], pixel[1], pixel[0], pixel[3]),
                BntxChannelLayout.R10G10B10A2 => DecodeR10G10B10A2(pixel),
                _ => throw new NotSupportedException(
                    $"BNTX '{texture.Name}' format 0x{(uint)texture.Format:X4} is not an uncompressed format."),
            };

            int offset = i * 4;
            output[offset] = ApplyChannel(texture.ChannelRed, r, g, b, a);
            output[offset + 1] = ApplyChannel(texture.ChannelGreen, r, g, b, a);
            output[offset + 2] = ApplyChannel(texture.ChannelBlue, r, g, b, a);
            output[offset + 3] = ApplyChannel(texture.ChannelAlpha, r, g, b, a);
        }

        return new BntxImage(width, height, output);
    }

    private static byte ApplyChannel(byte source, byte r, byte g, byte b, byte a)
    {
        return source switch
        {
            0 => 0,
            1 => 255,
            2 => r,
            3 => g,
            4 => b,
            5 => a,
            _ => throw new NotSupportedException($"BNTX channel source {source} is not supported."),
        };
    }

    private static (byte R, byte G, byte B, byte A) DecodeR5G6B5(ReadOnlySpan<byte> pixel)
    {
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(pixel);
        int r = (value >> 11) & 0x1F;
        int g = (value >> 5) & 0x3F;
        int b = value & 0x1F;
        return (
            (byte)((r << 3) | (r >> 2)),
            (byte)((g << 2) | (g >> 4)),
            (byte)((b << 3) | (b >> 2)),
            255);
    }

    private static (byte R, byte G, byte B, byte A) DecodeR16(ReadOnlySpan<byte> pixel, BntxDataType dataType)
    {
        ushort raw = BinaryPrimitives.ReadUInt16LittleEndian(pixel);
        if (dataType != BntxDataType.FloatingPoint)
        {
            throw new NotSupportedException($"BNTX R16 data type 0x{(byte)dataType:X2} is not supported.");
        }

        float value = (float)BitConverter.UInt16BitsToHalf(raw);
        byte gray = float.IsNaN(value) ? (byte)0 : (byte)Math.Clamp((int)MathF.Round(value * 255f), 0, 255);
        return (gray, gray, gray, 255);
    }

    private static (byte R, byte G, byte B, byte A) DecodeR10G10B10A2(ReadOnlySpan<byte> pixel)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(pixel);
        return (
            Scale10(value & 0x3FF),
            Scale10((value >> 10) & 0x3FF),
            Scale10((value >> 20) & 0x3FF),
            (byte)(((value >> 30) & 0x3) * 85));
    }

    private static byte Scale10(uint value)
    {
        return (byte)(((value * 255) + 511) / 1023);
    }
}

