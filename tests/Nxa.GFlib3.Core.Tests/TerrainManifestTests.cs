// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Core.Tests;

public sealed class TerrainManifestTests
{
    [Fact]
    public void RejectsEmptyBuffer()
    {
        Assert.Throws<InvalidDataException>(() => TerrainManifest.Parse(Array.Empty<byte>()));
    }

    [Fact]
    public void RejectsOutOfRangeRootOffset()
    {
        var data = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(data, 0x7FFFFFFF);

        Assert.Throws<InvalidDataException>(() => TerrainManifest.Parse(data));
    }

    [Fact]
    public void ParsesMatchingWorldXRange()
    {
        TerrainManifest manifest = TerrainManifest.Parse(CreateManifest(1025f, 200f, 1225f));

        Assert.Equal(1025f, manifest.GridWidth, 3);
        Assert.Equal(200f, manifest.WorldMinX, 3);
        Assert.Equal(1225f, manifest.WorldMaxX, 3);
    }

    [Fact]
    public void RejectsWorldXRangeThatDoesNotMatchGridWidth()
    {
        Assert.Throws<InvalidDataException>(
            () => TerrainManifest.Parse(CreateManifest(1025f, 200f, 300f)));
    }

    [Theory]
    [InlineData("ha_area00", 0f, 1025f)]
    [InlineData("ha_area00_s02", 200f, 712.5f)]
    [InlineData("ha_area11", 0f, 2048f)]
    public void ReadsWorldXRangeWhenDumpIsConfigured(string pack, float minX, float maxX)
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "terrain", pack + ".gfpak");
        if (!File.Exists(path))
        {
            return;
        }

        using var archive = GfpakArchive.Open(path);
        TerrainManifest manifest = TerrainManifest.FindInArchive(archive, out _);

        Assert.Equal(minX, manifest.WorldMinX, 3);
        Assert.Equal(maxX, manifest.WorldMaxX, 3);
    }

    [Theory]
    [InlineData("ha_area00", 3, 25, 11, 3, "area00_0_0.trmtr")]
    [InlineData("ha_area01", 9, 217, 20, 5, "area01_0_0.trmtr")]
    [InlineData("ha_area02", 11, 574, 14, 4, "area02_0_0.trmtr")]
    [InlineData("ha_area03", 6, 145, 16, 4, "area03_0_0.trmtr")]
    [InlineData("ha_area04", 5, 232, 11, 3, "area04_0_0.trmtr")]
    [InlineData("ha_area05", 6, 195, 7, 2, "area05_0_0.trmtr")]
    [InlineData("ha_area06", 5, 19, 4, 1, "terrain_1_1.trmtr")]
    [InlineData("ha_area11", 4, 14, 3, 1, "terrain_1_1.trmtr")]
    public void ReadsTerrainPacksWhenDumpIsConfigured(
        string pack,
        int plantGroups,
        int instancePaths,
        int detailGroups,
        int detailMaps,
        string material)
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "terrain", pack + ".gfpak");
        if (!File.Exists(path))
        {
            return;
        }

        using var archive = GfpakArchive.Open(path);
        TerrainManifest manifest = TerrainManifest.FindInArchive(archive, out GfpakEntry entry);

        Assert.InRange(entry.SizeDecompressed, TerrainManifest.MinArchiveBytes, TerrainManifest.MaxArchiveBytes);
        Assert.Equal((uint)plantGroups, manifest.PlantGroupCount);
        Assert.Equal(plantGroups, manifest.PlantGroups.Count);
        Assert.Equal(instancePaths, manifest.PlantGroups.Sum(group => group.InstancePaths.Count));
        Assert.Equal((uint)detailGroups, manifest.DetailGroupCount);
        Assert.Equal(detailGroups, manifest.DetailGroups.Count);
        Assert.Equal(detailMaps, manifest.DetailMapPaths.Count);
        Assert.Equal(material, manifest.MaterialPath);

        foreach (TerrainPlantGroup group in manifest.PlantGroups)
        {
            Assert.EndsWith(".trmdl", group.ModelPath, StringComparison.Ordinal);
            Assert.NotEmpty(group.InstancePaths);
            Assert.All(group.InstancePaths, item => Assert.EndsWith(".trins", item, StringComparison.Ordinal));
        }

        Assert.All(manifest.DetailMapPaths, item => Assert.EndsWith(".trtbf", item, StringComparison.Ordinal));
    }

    private static byte[] CreateManifest(float gridWidth, float worldMinX, float worldMaxX)
    {
        var data = new byte[0x6C];
        BinaryPrimitives.WriteInt32LittleEndian(data, 0x20);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x04), 26);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x06), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x0A), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x1C), 8);

        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x20), 0x1C);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x24), 0x1C);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x28), 0x38);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x30), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x32), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x34), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x36), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x38), 12);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x40), 0x10);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0x44), gridWidth);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0x48), gridWidth);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0x4C), 185f);

        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x50), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x52), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x54), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x56), 8);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x60), 0x10);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0x64), worldMinX);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0x68), worldMaxX);
        return data;
    }
}
