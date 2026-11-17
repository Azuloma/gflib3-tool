// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using System.Text;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Sarc;

namespace Nxa.GFlib3.Core.Tests;

public sealed class SarcArchiveTests
{
    private const string Name0 = "dir/file0.txt";
    private const string Name1 = "file1.bin";
    private static readonly byte[] File0 = "hello"u8.ToArray();
    private static readonly byte[] File1 = "test"u8.ToArray();

    [Fact]
    public void ReadsSyntheticArchive()
    {
        using var stream = new MemoryStream(BuildArchive());
        using var archive = SarcArchive.Read(stream);

        Assert.Equal(0x100, archive.Version);
        Assert.Equal(0x80u, archive.DataOffset);
        Assert.Equal(2, archive.Entries.Count);

        SarcEntry first = archive.Entries[0];
        Assert.Equal(Name0, first.Name);
        Assert.Equal(SarcHash.Compute(Name0), first.Hash);
        Assert.Equal(File0.Length, first.Size);
        Assert.Equal(File0, archive.ReadEntry(first));

        SarcEntry second = archive.Entries[1];
        Assert.Equal(Name1, second.Name);
        Assert.Equal(File1, archive.ReadEntry(second));

        Assert.True(archive.TryGetEntryByName(Name1, out SarcEntry? byName));
        Assert.Same(second, byName);
        Assert.True(archive.TryGetEntryByHash(SarcHash.Compute(Name0), out SarcEntry? byHash));
        Assert.Same(first, byHash);
        Assert.False(archive.TryGetEntryByName("missing", out _));
    }

    [Fact]
    public void MatchesObservedDumpHash()
    {
        Assert.Equal(0x04432FCAu, SarcHash.Compute("anim/map_icon_qicon_vfx_00_keep.bflan"));
        Assert.Equal(0x045AFF8Fu, SarcHash.Compute("anim/map_app_icon_00_close.bflan"));
    }

    [Theory]
    [InlineData("dir/file0.txt", "dir/file0.txt")]
    [InlineData("a\\b\\c.bin", "a/b/c.bin")]
    public void AcceptsSafeNames(string input, string expected)
    {
        Assert.Equal(Path.Combine(expected.Split('/')), SarcPath.GetSafeRelativePath(input));
    }

    [Theory]
    [InlineData("../evil.txt")]
    [InlineData("a/../../evil.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("C:/windows/file.txt")]
    [InlineData("a//b.txt")]
    [InlineData(".")]
    public void RejectsUnsafeNames(string input)
    {
        Assert.Throws<InvalidDataException>(() => SarcPath.GetSafeRelativePath(input));
    }

    [Fact]
    public void RejectsBadMagic()
    {
        byte[] data = BuildArchive();
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0xDEADBEEF);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => SarcArchive.Read(stream));
    }

    [Fact]
    public void RejectsTruncatedHeader()
    {
        using var stream = new MemoryStream(new byte[8]);
        Assert.Throws<InvalidDataException>(() => SarcArchive.Read(stream));
    }

    [Fact]
    public void RejectsOutOfRangeData()
    {
        byte[] data = BuildArchive();
        int nodeOffset = 0x14 + 0x0C + 0x10;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(nodeOffset + 12), 0x7FFFFFF0);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => SarcArchive.Read(stream));
    }

    [Fact]
    public void ReadsMapSarcWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string gfpak = Path.Combine(dump, "RomFS", "bin", "archive", "appli", "map.gfpak");
        if (!File.Exists(gfpak))
        {
            return;
        }

        using var pack = GfpakArchive.Open(gfpak);
        byte[] payload = pack.ReadEntry(pack.Entries[0]);

        using var stream = new MemoryStream(payload);
        using var archive = SarcArchive.Read(stream);

        Assert.Equal(68, archive.Entries.Count);
        Assert.Equal("anim/map_icon_qicon_vfx_00_keep.bflan", archive.Entries[0].Name);
        Assert.Equal(0x04432FCAu, archive.Entries[0].Hash);
        Assert.Equal("FLAN"u8.ToArray(), archive.ReadEntry(archive.Entries[0])[..4]);

        foreach (SarcEntry entry in archive.Entries)
        {
            Assert.Equal(SarcHash.Compute(entry.Name), entry.Hash);
        }
    }

    private static byte[] BuildArchive()
    {
        const int sfatOffset = 0x14;
        const int sfatHeaderSize = 0x0C;
        const int nodeCount = 2;
        const int nodeOffset = sfatOffset + sfatHeaderSize;
        const int sfntOffset = nodeOffset + (nodeCount * 0x10);
        const int sfntHeaderSize = 0x08;
        const int namesBase = sfntOffset + sfntHeaderSize;
        const int name0Offset = 0x00;
        const int name1Offset = 0x10;
        const uint dataOffset = 0x80;
        const int data0 = 0x00;
        const int data1 = 0x08;

        int total = (int)dataOffset + data1 + File1.Length;
        var data = new byte[total];
        Span<byte> span = data;

        BinaryPrimitives.WriteUInt32LittleEndian(span, SarcArchive.Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x04..], SarcArchive.HeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x06..], 0xFEFF);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x08..], (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x0C..], dataOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x10..], 0x100);

        BinaryPrimitives.WriteUInt32LittleEndian(span[sfatOffset..], SarcArchive.SfatMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(sfatOffset + 4)..], sfatHeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(sfatOffset + 6)..], nodeCount);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(sfatOffset + 8)..], SarcHash.DefaultMultiplier);

        WriteNode(span[nodeOffset..], Name0, name0Offset, data0, File0.Length);
        WriteNode(span[(nodeOffset + 0x10)..], Name1, name1Offset, data1, File1.Length);

        BinaryPrimitives.WriteUInt32LittleEndian(span[sfntOffset..], SarcArchive.SfntMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(sfntOffset + 4)..], sfntHeaderSize);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(sfntOffset + 6)..], 0);

        Encoding.ASCII.GetBytes(Name0).CopyTo(span[(namesBase + name0Offset)..]);
        Encoding.ASCII.GetBytes(Name1).CopyTo(span[(namesBase + name1Offset)..]);

        File0.CopyTo(span[((int)dataOffset + data0)..]);
        File1.CopyTo(span[((int)dataOffset + data1)..]);
        return data;
    }

    private static void WriteNode(Span<byte> span, string name, int nameOffset, int dataStart, int length)
    {
        uint attributes = 0x01000000u | ((uint)nameOffset >> 2);

        BinaryPrimitives.WriteUInt32LittleEndian(span, SarcHash.Compute(name));
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], attributes);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)dataStart);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], (uint)(dataStart + length));
    }
}
