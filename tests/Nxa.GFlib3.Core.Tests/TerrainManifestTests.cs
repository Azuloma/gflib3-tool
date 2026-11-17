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
}
