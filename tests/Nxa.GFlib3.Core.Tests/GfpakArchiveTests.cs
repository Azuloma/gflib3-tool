// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.Compression;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Hashing;

namespace Nxa.GFlib3.Core.Tests;

public sealed class GfpakArchiveTests
{
    private static readonly byte[] File0 = "hello"u8.ToArray();
    private static readonly byte[] File1 = "world!"u8.ToArray();

    [Fact]
    public void ReadsSyntheticArchive()
    {
        using var stream = new MemoryStream(BuildArchive());
        using var archive = GfpakArchive.Read(stream);

        Assert.Equal(0x1000u, archive.Version);
        Assert.False(archive.IsRelocated);
        Assert.Equal(2, archive.Entries.Count);
        Assert.Single(archive.Folders);
        Assert.Equal(2, archive.PathHashes.Count);
        Assert.Equal(Fnv1a64.Hash("folder/file0.bin"), archive.PathHashes[0]);
        Assert.Equal(Fnv1a64.Hash("folder/file1.bin"), archive.PathHashes[1]);

        GfpakFolder folder = archive.Folders[0];
        Assert.Equal(Fnv1a64.Hash("folder"), folder.NameHash);
        Assert.Equal(2, folder.Files.Count);
        Assert.Equal(0, folder.Files[0].FileIndex);
        Assert.Equal(1, folder.Files[1].FileIndex);

        GfpakEntry first = archive.Entries[0];
        Assert.Equal(CompressionType.None, first.Compression);
        Assert.Equal(File0.Length, first.SizeCompressed);
        Assert.Equal(File0.Length, first.SizeDecompressed);
        Assert.Equal(9, first.Level);

        Assert.Equal(File0, archive.ReadEntry(first));
        Assert.Equal(File1, archive.ReadEntry(archive.Entries[1]));
    }

    [Fact]
    public void ResolvesEntriesByPathHash()
    {
        using var stream = new MemoryStream(BuildArchive());
        using var archive = GfpakArchive.Read(stream);

        ulong hash = Fnv1a64.Hash("folder/file1.bin");
        Assert.True(archive.TryGetEntryByPathHash(hash, out GfpakEntry? entry));
        Assert.NotNull(entry);
        Assert.Equal(1, entry.Index);
        Assert.Equal(hash, archive.GetPathHash(entry));

        Assert.False(archive.TryGetEntryByPathHash(0xDEADBEEF, out _));
    }

    [Fact]
    public void RejectsBadMagic()
    {
        byte[] data = BuildArchive();
        BinaryPrimitives.WriteUInt64LittleEndian(data, 0xDEADBEEF);

        using var stream = new MemoryStream(data);
        var error = Assert.Throws<InvalidDataException>(() => GfpakArchive.Read(stream));
        Assert.Contains("GFLXPACK", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsTruncatedHeader()
    {
        using var stream = new MemoryStream(new byte[8]);
        Assert.Throws<InvalidDataException>(() => GfpakArchive.Read(stream));
    }

    [Fact]
    public void RejectsOutOfRangeEntryOffset()
    {
        byte[] data = BuildArchive();
        int fileTable = 0x70;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(fileTable + 0x10), 0x7FFFFFF0);

        using var stream = new MemoryStream(data);
        Assert.Throws<InvalidDataException>(() => GfpakArchive.Read(stream));
    }

    [Fact]
    public void DecompressesRealArchiveWhenDumpIsConfigured()
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

        using var archive = GfpakArchive.Open(path);
        byte[] first = archive.ReadEntry(archive.Entries[0]);

        Assert.True(first.Length > 4);
        Assert.Equal("SARC"u8.ToArray(), first[..4]);
    }

    private static byte[] BuildArchive()
    {
        const int pointerBlock = 0x18;
        const int pathHashes = 0x30;
        const int folderTable = 0x40;
        const int fileTable = 0x70;
        const int dataStart = 0xA0;
        const int data0 = dataStart;
        const int data1 = dataStart + 0x10;

        var data = new byte[data1 + File1.Length];
        Span<byte> span = data;

        BinaryPrimitives.WriteUInt64LittleEndian(span, GfpakArchive.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x08..], 0x1000);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x0C..], 0);
        BinaryPrimitives.WriteInt32LittleEndian(span[0x10..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(span[0x14..], 1);

        BinaryPrimitives.WriteInt64LittleEndian(span[pointerBlock..], fileTable);
        BinaryPrimitives.WriteInt64LittleEndian(span[(pointerBlock + 8)..], pathHashes);
        BinaryPrimitives.WriteInt64LittleEndian(span[(pointerBlock + 16)..], folderTable);

        BinaryPrimitives.WriteUInt64LittleEndian(span[pathHashes..], Fnv1a64.Hash("folder/file0.bin"));
        BinaryPrimitives.WriteUInt64LittleEndian(span[(pathHashes + 8)..], Fnv1a64.Hash("folder/file1.bin"));

        BinaryPrimitives.WriteUInt64LittleEndian(span[folderTable..], Fnv1a64.Hash("folder"));
        BinaryPrimitives.WriteInt32LittleEndian(span[(folderTable + 8)..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(folderTable + 12)..], 0xCC);

        int folderEntry = folderTable + 0x10;
        BinaryPrimitives.WriteUInt64LittleEndian(span[folderEntry..], Fnv1a64.Hash("file0.bin"));
        BinaryPrimitives.WriteInt32LittleEndian(span[(folderEntry + 8)..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(folderEntry + 12)..], 0xCC);

        folderEntry += 0x10;
        BinaryPrimitives.WriteUInt64LittleEndian(span[folderEntry..], Fnv1a64.Hash("file1.bin"));
        BinaryPrimitives.WriteInt32LittleEndian(span[(folderEntry + 8)..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(folderEntry + 12)..], 0xCC);

        WriteFileEntry(span[fileTable..], 0, File0.Length, data0);
        WriteFileEntry(span[(fileTable + GfpakArchive.FileTableEntrySize)..], 1, File1.Length, data1);

        File0.CopyTo(span[data0..]);
        File1.CopyTo(span[data1..]);
        return data;
    }

    private static void WriteFileEntry(Span<byte> span, int index, int size, int offset)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(span, 9);
        span[2] = (byte)CompressionType.None;
        span[3] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], (uint)size);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], (uint)size);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], 0xCC);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)offset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[20..], 0);
    }
}
