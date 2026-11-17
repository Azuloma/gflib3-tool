// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.Formats.Bntx;
using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Core.Tests;

public sealed class BntxFileTests
{
    private const int DataOffset = 0x200;

    [Fact]
    public void ParsesSyntheticRgbaTexture()
    {
        byte[] data = BuildRgbaTexture(tileMode: 1);
        BntxFile file = BntxFile.Parse(data);

        Assert.Equal(new BntxVersion(4, 1, 0), file.Version);
        Assert.Equal("NX  ", file.Platform);
        Assert.Equal("Fixture", file.Name);
        Assert.Single(file.Textures);

        BntxTexture texture = file.Textures[0];
        Assert.Equal("FixtureTex", texture.Name);
        Assert.Equal(BntxFormat.R8G8B8A8Unorm, texture.Format);
        Assert.Equal(4u, texture.Width);
        Assert.Equal(4u, texture.Height);
        Assert.Equal(1, texture.MipCount);
        Assert.Equal(DataOffset, texture.MipOffsets[0]);

        BntxImage image = texture.DecodeMip0();
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(64, image.Pixels.Length);

        for (int i = 0; i < 16; i++)
        {
            int offset = i * 4;
            Assert.Equal(i * 10, image.Pixels[offset]);
            Assert.Equal((i * 10) + 1, image.Pixels[offset + 1]);
            Assert.Equal((i * 10) + 2, image.Pixels[offset + 2]);
            Assert.Equal(255, image.Pixels[offset + 3]);
        }
    }

    [Fact]
    public void AppliesChannelSources()
    {
        byte[] data = BuildRgbaTexture(tileMode: 1, red: 2, green: 2, blue: 2, alpha: 1);
        BntxImage image = BntxFile.Parse(data).Textures[0].DecodeMip0();

        Assert.Equal(image.Pixels[0], image.Pixels[1]);
        Assert.Equal(image.Pixels[0], image.Pixels[2]);
        Assert.Equal(255, image.Pixels[3]);
    }

    [Fact]
    public void RejectsBadMagic()
    {
        byte[] data = BuildRgbaTexture(tileMode: 1);
        "XTNB"u8.CopyTo(data);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BntxFile.Parse(data));
        Assert.Contains("magic", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTruncatedHeader()
    {
        Assert.Throws<InvalidDataException>(() => BntxFile.Parse(new byte[16]));
    }

    [Fact]
    public void RejectsUnsupportedTileMode()
    {
        byte[] data = BuildRgbaTexture(tileMode: 2);
        Assert.Throws<NotSupportedException>(() => BntxFile.Parse(data));
    }

    [Fact]
    public void RejectsOutOfRangeMipPointer()
    {
        byte[] data = BuildRgbaTexture(tileMode: 1);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x140), 0x7FFFFFFF);

        Assert.Throws<InvalidDataException>(() => BntxFile.Parse(data));
    }

    [Fact]
    public void RejectsUnsupportedAstcDecode()
    {
        byte[] data = BuildRgbaTexture(tileMode: 1, format: 0x2D01);
        BntxTexture texture = BntxFile.Parse(data).Textures[0];
        Assert.False(BntxFormats.IsKnown(texture.Format));
        Assert.Throws<NotSupportedException>(() => texture.DecodeMip0());
    }

    [Fact]
    public void ParsesAreaPackTexturesWhenDumpIsConfigured()
    {
        string? packPath = DumpPack("archive", "field", "model", "pack", "ha_area00_s07.gfpak");
        if (packPath is null)
        {
            return;
        }

        using var pack = GfpakArchive.Open(packPath);
        Assert.Equal(22, pack.Entries.Count);

        for (int i = 5; i < 22; i++)
        {
            BntxFile file = BntxFile.Parse(pack.ReadEntry(pack.Entries[i]));
            Assert.Single(file.Textures);
            Assert.StartsWith("room_area00_s07", file.Textures[0].Name, StringComparison.Ordinal);
        }

        BntxTexture emissive = BntxFile.Parse(pack.ReadEntry(pack.Entries[21])).Textures[0];
        Assert.Equal("room_area00_s07_emv", emissive.Name);
        Assert.Equal(BntxFormat.Bc7Srgb, emissive.Format);
        Assert.Equal(64u, emissive.Width);
        Assert.Equal(7, emissive.MipCount);
        Assert.Equal(7680u, emissive.ImageSize);
        Assert.True(emissive.IsSrgb);

        BntxImage emissiveImage = emissive.DecodeMip0();
        Assert.Equal(64, emissiveImage.Width);
        Assert.Contains(emissiveImage.Pixels, static value => value != 0);

        BntxTexture albedo = BntxFile.Parse(pack.ReadEntry(pack.Entries[5])).Textures[0];
        Assert.Equal(BntxFormat.Bc7Srgb, albedo.Format);
        Assert.Equal(1024u, albedo.Width);
        Assert.Equal(11, albedo.MipCount);
        Assert.Equal(1_400_320u, albedo.ImageSize);

        BntxTexture normal = BntxFile.Parse(pack.ReadEntry(pack.Entries[19])).Textures[0];
        Assert.Equal(BntxFormat.Bc5Unorm, normal.Format);
        Assert.Equal(256u, normal.Width);
        BntxImage normalImage = normal.DecodeMip0();
        Assert.Contains(normalImage.Pixels, static value => value != 0);

        BntxTexture mask = BntxFile.Parse(pack.ReadEntry(pack.Entries[20])).Textures[0];
        Assert.Equal(BntxFormat.Bc4Unorm, mask.Format);
        Assert.Equal(256u, mask.Width);
        BntxImage maskImage = mask.DecodeMip0();
        Assert.Equal(maskImage.Pixels[0], maskImage.Pixels[1]);
        Assert.Equal(maskImage.Pixels[0], maskImage.Pixels[2]);
        Assert.Equal(255, maskImage.Pixels[3]);
    }

    [Fact]
    public void DecodesBlockLinearHeightmapWhenDumpIsConfigured()
    {
        string? packPath = DumpPack("archive", "field", "terrain", "ha_area00.gfpak");
        if (packPath is null)
        {
            return;
        }

        using var pack = GfpakArchive.Open(packPath);
        BntxFile file = BntxFile.Parse(pack.ReadEntry(pack.Entries[10]));
        BntxTexture texture = file.Textures[0];

        Assert.Equal("area00_0_0_heightmap", texture.Name);
        Assert.Equal(BntxFormat.R16Float, texture.Format);
        Assert.Equal(1025u, texture.Width);
        Assert.Equal(1025u, texture.Height);
        Assert.Equal(0, texture.TileMode);
        Assert.Equal(4, texture.BlockHeightLog2);
        Assert.Equal(2_433_024u, texture.ImageSize);

        BntxImage image = texture.DecodeMip0();
        Assert.Equal(1025, image.Width);

        long horizontal = 0;
        long vertical = 0;
        for (int y = 1; y < 256; y += 8)
        {
            for (int x = 1; x < 256; x += 8)
            {
                int offset = ((y * image.Width) + x) * 4;
                horizontal += Math.Abs(image.Pixels[offset] - image.Pixels[offset + 4]);
                vertical += Math.Abs(image.Pixels[offset] - image.Pixels[(offset + (image.Width * 4)) + 0]);
            }
        }

        Assert.True(
            vertical < horizontal * 2 + 64,
            $"block-linear decode looks discontinuous (horizontal {horizontal}, vertical {vertical}).");
    }

    [Fact]
    public void DecodesSplatAlphaArraySlicesWhenDumpIsConfigured()
    {
        string? packPath = DumpPack("archive", "field", "terrain", "ha_area00.gfpak");
        if (packPath is null)
        {
            return;
        }

        using var pack = GfpakArchive.Open(packPath);
        BntxTexture texture = BntxFile.Parse(pack.ReadEntry(pack.Entries[37])).Textures[0];

        Assert.Equal("area00_0_0_splat_alpha0", texture.Name);
        Assert.Equal(BntxFormat.Bc7Unorm, texture.Format);
        Assert.Equal(2u, texture.ArrayCount);
        Assert.Equal(1024u, texture.Width);

        BntxImage slice0 = BntxTextureDecoder.Decode(texture, 0, 0);
        BntxImage slice1 = BntxTextureDecoder.Decode(texture, 0, 1);
        Assert.Equal(1024, slice0.Width);
        Assert.Equal(1024, slice1.Width);
        Assert.True(MeanRed(slice0) > 50f);
        Assert.True(MeanRed(slice1) < 50f);
        Assert.Throws<ArgumentOutOfRangeException>(() => BntxTextureDecoder.Decode(texture, 0, 2));
    }

    private static float MeanRed(BntxImage image)
    {
        long sum = 0;
        for (int i = 0; i < image.Pixels.Length; i += 4)
        {
            sum += image.Pixels[i];
        }

        return (float)sum / (image.Pixels.Length / 4);
    }

    private static byte[] BuildRgbaTexture(
        ushort tileMode,
        uint format = 0x0B01,
        byte red = 2,
        byte green = 3,
        byte blue = 4,
        byte alpha = 5)
    {
        byte[] data = new byte[0x400];
        Span<byte> span = data;

        "BNTX"u8.CopyTo(span);
        span[0x08] = 0x00;
        span[0x09] = 0x01;
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x0A..], 4);
        span[0x0C] = 0xFF;
        span[0x0D] = 0xFE;
        span[0x0E] = 12;
        span[0x0F] = 64;
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x10..], 0x58);
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x16..], 0x58);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x18..], 0x200);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x1C..], 0x280);
        "NX  "u8.CopyTo(span[0x20..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x24..], 1);
        BinaryPrimitives.WriteUInt64LittleEndian(span[0x28..], 0x90);
        BinaryPrimitives.WriteUInt64LittleEndian(span[0x30..], 0x1F0);

        "Fixture"u8.CopyTo(span[0x58..]);

        BinaryPrimitives.WriteUInt64LittleEndian(span[0x90..], 0xA0);
        WriteTextureName(span, 0x60, "FixtureTex");
        "BRTI"u8.CopyTo(span[0xA0..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0xA4..], 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0xA8..], 0x10);

        int info = 0xB0;
        span[info] = 9;
        span[info + 1] = 2;
        BinaryPrimitives.WriteUInt16LittleEndian(span[(info + 0x02)..], tileMode);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(info + 0x06)..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(info + 0x08)..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x0C)..], format);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x10)..], 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x14)..], 4);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x18)..], 4);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x1C)..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x20)..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x40)..], 128);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x44)..], 512);
        span[info + 0x48] = red;
        span[info + 0x49] = green;
        span[info + 0x4A] = blue;
        span[info + 0x4B] = alpha;
        BinaryPrimitives.WriteUInt32LittleEndian(span[(info + 0x4C)..], 1);
        BinaryPrimitives.WriteUInt64LittleEndian(span[(info + 0x50)..], 0x60);
        BinaryPrimitives.WriteUInt64LittleEndian(span[(info + 0x60)..], 0x140);

        BinaryPrimitives.WriteUInt64LittleEndian(span[0x140..], DataOffset);

        for (int i = 0; i < 16; i++)
        {
            int offset = DataOffset + (i / 4 * 32) + (i % 4 * 4);
            span[offset] = (byte)(i * 10);
            span[offset + 1] = (byte)((i * 10) + 1);
            span[offset + 2] = (byte)((i * 10) + 2);
            span[offset + 3] = 255;
        }

        return data;
    }

    private static void WriteTextureName(Span<byte> span, int offset, string name)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(name);
        BinaryPrimitives.WriteUInt16LittleEndian(span[offset..], (ushort)bytes.Length);
        bytes.CopyTo(span[(offset + 2)..]);
    }

    private static string? DumpPack(params string[] relative)
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return null;
        }

        string path = Path.Combine([dump, "RomFS", "bin", ..relative]);
        return File.Exists(path) ? path : null;
    }
}
