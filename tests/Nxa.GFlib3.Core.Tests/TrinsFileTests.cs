// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Core.Tests;

public sealed class TrinsFileTests
{
    [Fact]
    public void RejectsEmptyBuffer()
    {
        Assert.Throws<InvalidDataException>(() => TrinsFile.Parse(Array.Empty<byte>()));
    }

    [Fact]
    public void RejectsOutOfRangeRootOffset()
    {
        var data = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(data, 0x7FFFFFFF);

        Assert.Throws<InvalidDataException>(() => TrinsFile.Parse(data));
    }

    [Fact]
    public void ParsesTerrainInstancesWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "terrain", "ha_area00.gfpak");
        if (!File.Exists(path))
        {
            return;
        }

        using var archive = GfpakArchive.Open(path);
        int files = 0;
        int instances = 0;

        foreach (GfpakEntry entry in archive.Entries)
        {
            byte[] data = archive.ReadEntry(entry);
            if (data.AsSpan().IndexOf("SrtInstanceBuffer"u8) < 0)
            {
                continue;
            }

            TrinsFile trins = TrinsFile.Parse(data);
            Assert.Equal(TrinsFile.SrtInstanceBufferType, trins.TypeName);
            Assert.Equal(1u, trins.Version);
            Assert.Equal(trins.Count, (uint)trins.Instances.Count);

            foreach (var matrix in trins.Instances)
            {
                Assert.Equal(0f, matrix.Row0.Y);
                Assert.Equal(0f, matrix.Row0.W);
                Assert.Equal(0f, matrix.Row1.X);
                Assert.Equal(0f, matrix.Row1.Z);
                Assert.Equal(0f, matrix.Row1.W);
                Assert.Equal(0f, matrix.Row2.Y);
                Assert.Equal(0f, matrix.Row2.W);
                Assert.Equal(1f, matrix.Row3.W);
            }

            files++;
            instances += trins.Instances.Count;
        }

        Assert.Equal(25, files);
        Assert.Equal(60, instances);
    }
}
