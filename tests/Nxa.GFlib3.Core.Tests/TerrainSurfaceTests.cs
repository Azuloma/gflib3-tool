// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Core.Tests;

public sealed class TerrainSurfaceTests
{
    [Fact]
    public void ReadsArea00HeightmapWhenDumpIsConfigured()
    {
        if (!TryLoad("ha_area00", out GfpakArchive? archive, out TerrainSurface? surface)
            || archive is null || surface is null)
        {
            return;
        }

        using (archive)
        {
            Assert.Equal(1025, surface.Heightmap.Width);
            Assert.Equal(1025, surface.Heightmap.Height);
            Assert.Equal(185f, surface.HeightScale, 3);
            Assert.Equal(1f, surface.WorldUnitsPerSampleX, 3);
            Assert.Equal(1024f, surface.WorldExtentX, 3);

            (float min, float max) = surface.Heightmap.GetRange();
            Assert.Equal(0f, min, 3);
            Assert.Equal(0.854f, max, 3);

            Assert.Equal(60.793f, surface.GetHeightAtSample(445, 650), 2);
        }
    }

    [Fact]
    public void ReadsArea01R32HeightmapWhenDumpIsConfigured()
    {
        if (!TryLoad("ha_area01", out GfpakArchive? archive, out TerrainSurface? surface)
            || archive is null || surface is null)
        {
            return;
        }

        using (archive)
        {
            Assert.Equal(1025, surface.Heightmap.Width);
            Assert.Equal(121.756f, surface.HeightScale, 3);

            (float min, float max) = surface.Heightmap.GetRange();
            Assert.True(min >= 0f);
            Assert.Equal(0.999f, max, 3);
        }
    }

    [Fact]
    public void ReadsArea05HighResolutionHeightmapWhenDumpIsConfigured()
    {
        if (!TryLoad("ha_area05", out GfpakArchive? archive, out TerrainSurface? surface)
            || archive is null || surface is null)
        {
            return;
        }

        using (archive)
        {
            Assert.Equal(2049, surface.Heightmap.Width);
            Assert.Equal(2049, surface.Heightmap.Height);
            Assert.Equal(168f, surface.HeightScale, 3);
            Assert.Equal(0.5f, surface.WorldUnitsPerSampleX, 3);
            Assert.Equal(0.5f, surface.WorldUnitsPerSampleZ, 3);
            Assert.Equal(1024f, surface.WorldExtentX, 3);
            Assert.Equal(1024f, surface.WorldExtentZ, 3);
        }
    }

    [Fact]
    public void MapsArea02PlacementsToHeightmapWorldCoordinatesWhenDumpIsConfigured()
    {
        if (!TryLoad("ha_area02", out GfpakArchive? archive, out TerrainSurface? surface)
            || archive is null || surface is null)
        {
            return;
        }

        using (archive)
        {
            Assert.Equal(2049, surface.Heightmap.Width);
            Assert.Equal(0.5f, surface.WorldUnitsPerSampleX, 3);
            Assert.Equal(1024f, surface.WorldExtentX, 3);

            Assert.True(surface.TryGetWorldHeight(640.058f, 138.576f, out float keystone));
            Assert.Equal(121.931f, keystone, 1);

            Assert.True(surface.TryGetWorldHeight(641.457f, 731.721f, out float tent));
            Assert.Equal(45.491f, tent, 1);

            Assert.True(surface.TryGetWorldHeight(746.033f, 298.152f, out float cave));
            Assert.Equal(92f, cave, 1);

            Assert.True(surface.TryGetWorldHeight(0f, 0f, out _));
            Assert.True(surface.TryGetWorldHeight(surface.WorldExtentX, surface.WorldExtentZ, out _));
            Assert.False(surface.TryGetWorldHeight(-0.125f, 0f, out _));
            Assert.False(surface.TryGetWorldHeight(surface.WorldExtentX + 0.125f, 0f, out _));
            Assert.False(surface.TryGetWorldHeight(-1f, 0f, out _));
            Assert.False(surface.TryGetWorldHeight(surface.WorldExtentX + 1f, 0f, out _));

            TerrainMeshGeometry mesh = TerrainMeshBuilder.Build(surface, 256);
            Assert.Equal(0f, mesh.Bounds.Min.X, 3);
            Assert.Equal(1024f, mesh.Bounds.Max.X, 3);
            Assert.Equal(1024f, mesh.Bounds.Max.Z, 3);
        }
    }

    [Fact]
    public void BuildsTerrainMeshWhenDumpIsConfigured()
    {
        if (!TryLoad("ha_area00", out GfpakArchive? archive, out TerrainSurface? surface)
            || archive is null || surface is null)
        {
            return;
        }

        using (archive)
        {
            TerrainMeshGeometry mesh = TerrainMeshBuilder.Build(surface, 16);

            Assert.Equal(65 * 65, mesh.Vertices.Length);
            Assert.Equal(64 * 64 * 6, mesh.Indices.Length);
            Assert.Equal(0f, mesh.Bounds.Min.X, 3);
            Assert.Equal(1024f, mesh.Bounds.Max.X, 3);
            Assert.Equal(1024f, mesh.Bounds.Max.Z, 3);
            Assert.InRange(mesh.Bounds.Max.Y, 156f, 158f);
        }
    }

    private static bool TryLoad(string pack, out GfpakArchive? archive, out TerrainSurface? surface)
    {
        archive = null;
        surface = null;

        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return false;
        }

        string path = Path.Combine(dump, "RomFS", "bin", "archive", "field", "terrain", pack + ".gfpak");
        if (!File.Exists(path))
        {
            return false;
        }

        archive = GfpakArchive.Open(path);
        TerrainManifest manifest = TerrainManifest.FindInArchive(archive, out _);
        TerrainMaterialSet material = TerrainMaterialSet.FindInArchive(
            archive, $"bin/field/terrain/{pack}/terrain/");
        surface = TerrainSurface.Load(archive, manifest, material);
        return true;
    }
}
