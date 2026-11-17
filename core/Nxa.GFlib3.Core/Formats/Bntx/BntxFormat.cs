// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Formats.Bntx;

/// <summary>
/// Packed BNTX image format: <c>(channel layout &lt;&lt; 8) | data type</c>.
/// Unknown values are carried through so the parser can report them.
/// </summary>
public enum BntxFormat : uint
{
    R8Unorm = 0x0201,
    R5G6B5Unorm = 0x0701,
    R8G8Unorm = 0x0901,
    R16Float = 0x0A05,
    R8G8B8A8Unorm = 0x0B01,
    R32Float = 0x1405,
    R8G8B8A8Srgb = 0x0B06,
    B8G8R8A8Unorm = 0x0C01,
    R10G10B10A2Unorm = 0x0E01,
    Bc1Unorm = 0x1A01,
    Bc1Srgb = 0x1A06,
    Bc2Unorm = 0x1B01,
    Bc2Srgb = 0x1B06,
    Bc3Unorm = 0x1C01,
    Bc3Srgb = 0x1C06,
    Bc4Unorm = 0x1D01,
    Bc4Snorm = 0x1D02,
    Bc5Unorm = 0x1E01,
    Bc5Snorm = 0x1E02,
    Bc6hUf16 = 0x1F01,
    Bc6hSf16 = 0x1F02,
    Bc7Unorm = 0x2001,
    Bc7Srgb = 0x2006,
}

/// <summary>Channel layout of a packed <see cref="BntxFormat"/> value.</summary>
public enum BntxChannelLayout : byte
{
    None = 0x01,
    R8 = 0x02,
    R4G4B4A4 = 0x03,
    R5G5B5A1 = 0x05,
    R5G6B5 = 0x07,
    R8G8 = 0x09,
    R16 = 0x0A,
    R8G8B8A8 = 0x0B,
    B8G8R8A8 = 0x0C,
    R10G10B10A2 = 0x0E,
    R32 = 0x14,
    Bc1 = 0x1A,
    Bc2 = 0x1B,
    Bc3 = 0x1C,
    Bc4 = 0x1D,
    Bc5 = 0x1E,
    Bc6H = 0x1F,
    Bc7 = 0x20,
}

/// <summary>Numeric interpretation of the packed format's low byte.</summary>
public enum BntxDataType : byte
{
    Unorm = 0x01,
    Snorm = 0x02,
    UnsignedInteger = 0x03,
    SignedInteger = 0x04,
    FloatingPoint = 0x05,
    Srgb = 0x06,
    Depth = 0x07,
    UnsignedScaled = 0x08,
    SignedScaled = 0x09,
    UnsignedFloat = 0x0A,
}

/// <summary>Block geometry and byte size for a BNTX image format.</summary>
public readonly record struct BntxFormatLayout(
    BntxChannelLayout Channel,
    BntxDataType DataType,
    int BlockWidth,
    int BlockHeight,
    int BytesPerBlock,
    bool IsCompressed);

public static class BntxFormats
{
    public static BntxFormatLayout Describe(BntxFormat format)
    {
        uint raw = (uint)format;
        BntxChannelLayout channel = (BntxChannelLayout)(raw >> 8);
        BntxDataType dataType = (BntxDataType)(raw & 0xFF);

        (int width, int height, int bytes) = channel switch
        {
            BntxChannelLayout.R8 => (1, 1, 1),
            BntxChannelLayout.R5G6B5 => (1, 1, 2),
            BntxChannelLayout.R8G8 => (1, 1, 2),
            BntxChannelLayout.R16 => (1, 1, 2),
            BntxChannelLayout.R32 => (1, 1, 4),
            BntxChannelLayout.R8G8B8A8 => (1, 1, 4),
            BntxChannelLayout.B8G8R8A8 => (1, 1, 4),
            BntxChannelLayout.R10G10B10A2 => (1, 1, 4),
            BntxChannelLayout.Bc1 => (4, 4, 8),
            BntxChannelLayout.Bc2 => (4, 4, 16),
            BntxChannelLayout.Bc3 => (4, 4, 16),
            BntxChannelLayout.Bc4 => (4, 4, 8),
            BntxChannelLayout.Bc5 => (4, 4, 16),
            BntxChannelLayout.Bc6H => (4, 4, 16),
            BntxChannelLayout.Bc7 => (4, 4, 16),
            _ => throw new NotSupportedException(
                $"BNTX format 0x{raw:X4} uses unsupported channel layout 0x{(byte)channel:X2}."),
        };

        return new BntxFormatLayout(channel, dataType, width, height, bytes, width != 1 || height != 1);
    }

    public static bool IsKnown(BntxFormat format)
    {
        try
        {
            _ = Describe(format);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    public static bool IsSrgb(BntxFormat format)
    {
        return (BntxDataType)((uint)format & 0xFF) == BntxDataType.Srgb;
    }

    public static string GetName(BntxFormat format)
    {
        uint raw = (uint)format;
        BntxChannelLayout channel = (BntxChannelLayout)(raw >> 8);
        BntxDataType dataType = (BntxDataType)(raw & 0xFF);
        if (!IsKnown(format))
        {
            return $"0x{raw:X4}";
        }

        string channelName = channel switch
        {
            BntxChannelLayout.Bc1 => "BC1",
            BntxChannelLayout.Bc2 => "BC2",
            BntxChannelLayout.Bc3 => "BC3",
            BntxChannelLayout.Bc4 => "BC4",
            BntxChannelLayout.Bc5 => "BC5",
            BntxChannelLayout.Bc6H => "BC6H",
            BntxChannelLayout.Bc7 => "BC7",
            _ => channel.ToString(),
        };

        string typeName = dataType switch
        {
            BntxDataType.Unorm => "UNORM",
            BntxDataType.Snorm => "SNORM",
            BntxDataType.FloatingPoint => "FLOAT",
            BntxDataType.Srgb => "SRGB",
            _ => dataType.ToString(),
        };

        return $"{channelName}_{typeName}";
    }
}

