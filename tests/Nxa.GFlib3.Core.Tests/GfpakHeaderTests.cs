// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Core.Tests;

public sealed class GfpakHeaderTests
{
    [Fact]
    public void ParsesHeader()
    {
        GfpakHeader header = GfpakHeader.Parse(BuildHeader(version: 0x1000, relocated: 0, files: 2, folders: 1));

        Assert.Equal(0x1000u, header.Version);
        Assert.False(header.IsRelocated);
        Assert.Equal(2, header.FileCount);
        Assert.Equal(1, header.FolderCount);
    }

    [Fact]
    public void ReadsHeaderFromStream()
    {
        using var stream = new MemoryStream(BuildHeader(files: 5, folders: 3));

        GfpakHeader header = GfpakHeader.Read(stream);

        Assert.Equal(5, header.FileCount);
        Assert.Equal(3, header.FolderCount);
        Assert.Equal(GfpakHeader.Size, stream.Position);
    }

    [Fact]
    public void RejectsTruncatedSource()
    {
        byte[] data = BuildHeader()[..(GfpakHeader.Size - 1)];

        var error = Assert.Throws<InvalidDataException>(() => GfpakHeader.Parse(data));
        Assert.Contains("truncated", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTruncatedStream()
    {
        using var stream = new MemoryStream(new byte[8]);

        var error = Assert.Throws<InvalidDataException>(() => GfpakHeader.Read(stream));
        Assert.Contains("truncated", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsBadMagic()
    {
        byte[] data = BuildHeader();
        BinaryPrimitives.WriteUInt64LittleEndian(data, 0xDEADBEEF);

        var error = Assert.Throws<InvalidDataException>(() => GfpakHeader.Parse(data));
        Assert.Contains("GFLXPACK", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0x10, -1, "file count")]
    [InlineData(0x10, GfpakHeader.MaxFileCount + 1, "file count")]
    [InlineData(0x14, -1, "folder count")]
    [InlineData(0x14, GfpakHeader.MaxFolderCount + 1, "folder count")]
    public void RejectsInvalidCounts(int offset, int value, string field)
    {
        byte[] data = BuildHeader();
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);

        var error = Assert.Throws<InvalidDataException>(() => GfpakHeader.Parse(data));
        Assert.Contains(field, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsRealHeaderWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string path = Path.Combine(dump, "RomFS", "bin", "archive", "appli", "map.gfpak");
        if (!File.Exists(path))
        {
            return;
        }

        GfpakHeader header = GfpakHeader.Open(path);
        using GfpakArchive archive = GfpakArchive.Open(path);

        Assert.Equal(archive.Version, header.Version);
        Assert.Equal(archive.IsRelocated, header.IsRelocated);
        Assert.Equal(archive.Entries.Count, header.FileCount);
        Assert.Equal(archive.Folders.Count, header.FolderCount);
    }

    private static byte[] BuildHeader(
        uint version = 0x1000,
        uint relocated = 0,
        int files = 1,
        int folders = 1)
    {
        var data = new byte[GfpakHeader.Size];
        Span<byte> span = data;
        BinaryPrimitives.WriteUInt64LittleEndian(span, GfpakArchive.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x08..], version);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x0C..], relocated);
        BinaryPrimitives.WriteInt32LittleEndian(span[0x10..], files);
        BinaryPrimitives.WriteInt32LittleEndian(span[0x14..], folders);
        return data;
    }
}
