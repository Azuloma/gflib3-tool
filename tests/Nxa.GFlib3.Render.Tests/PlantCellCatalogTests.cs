// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class PlantCellCatalogTests
{
    [Fact]
    public void ResolvesExistingCellFile()
    {
        FieldScene scene = CreateScene(out PlantInstanceFile existing);
        var catalog = new PlantCellCatalog(scene, new TerrainCellGrid(0f, 1024f, 1024f));

        Assert.True(catalog.TryGetFile(0, new TerrainCell(6, 5), out PlantInstanceFile file));
        Assert.Same(existing, file);
        Assert.True(catalog.IsRegistered(existing));
    }

    [Fact]
    public void CreatesPendingSyntheticPathWithoutRegistering()
    {
        FieldScene scene = CreateScene(out _);
        var catalog = new PlantCellCatalog(scene, new TerrainCellGrid(0f, 1024f, 1024f));

        PlantInstanceFile? file = catalog.GetOrCreatePending(0, new TerrainCell(1, 2));

        Assert.NotNull(file);
        Assert.True(file.IsSynthetic);
        Assert.False(catalog.IsRegistered(file));
        Assert.Equal(
            "bin/field/terrain/test/terrain/area00_0_0_plants_tree01_X1Z2_d0.trins",
            file.LogicalPath);
        Assert.DoesNotContain(file, scene.InstanceFiles);
    }

    [Fact]
    public void RegistersSyntheticFileAndReusesIt()
    {
        FieldScene scene = CreateScene(out _);
        var catalog = new PlantCellCatalog(scene, new TerrainCellGrid(200f, 511.5f, 511.5f));
        var cell = new TerrainCell(3, 4);
        PlantInstanceFile file = catalog.GetOrCreatePending(0, cell)!;

        catalog.Register(file, cell);

        Assert.Contains(file, scene.InstanceFiles);
        Assert.True(catalog.IsRegistered(file));
        Assert.True(catalog.TryGetFile(0, cell, out PlantInstanceFile resolved));
        Assert.Same(file, resolved);
        Assert.Same(file, catalog.GetOrCreatePending(0, cell));
    }

    [Fact]
    public void RejectsSyntheticPathsWithoutPrefixOrModel()
    {
        FieldScene scene = CreateScene(out _);
        scene.TerrainCellPrefix = string.Empty;
        var catalog = new PlantCellCatalog(scene, new TerrainCellGrid(0f, 1024f, 1024f));

        Assert.False(catalog.CanCreateSynthetic(0));
        Assert.Null(catalog.GetOrCreatePending(0, new TerrainCell(1, 1)));
        Assert.Null(catalog.GetOrCreatePending(9, new TerrainCell(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => catalog.GetOrCreatePending(0, new TerrainCell(16, 0)));
    }

    [Theory]
    [InlineData("ha_area00", "ha_area00")]
    [InlineData("ha_area00_s02", "ha_area00")]
    [InlineData("ha_area02", "ha_area02")]
    [InlineData("ha_area06", "ha_area06")]
    [InlineData("ha_area11", "ha_area11")]
    public void MapsLoadedInstancesToTheirSourceFilesWhenDumpIsConfigured(
        string area,
        string modelPack)
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, area, modelPack, null);
        TerrainSurface surface = Assert.IsType<TerrainSurface>(scene.TerrainSurface);
        var catalog = new PlantCellCatalog(scene, TerrainCellGrid.FromSurface(surface));
        int checkedInstances = 0;
        foreach (PlantInstance instance in scene.Instances)
        {
            if (instance.SourceFile is not PlantInstanceFile source)
            {
                continue;
            }

            TerrainCell cell = catalog.Grid.CellAt(instance.Matrix.Row3.X, instance.Matrix.Row3.Z);
            Assert.True(
                catalog.TryGetFile(instance.ModelIndex, cell, out PlantInstanceFile file),
                $"{area}: no file for model {instance.ModelIndex} cell ({cell.X}, {cell.Z}).");
            Assert.Equal(source.LogicalPath, file.LogicalPath);
            checkedInstances++;
        }

        Assert.True(checkedInstances > 0);
    }

    [Theory]
    [InlineData("area00_0_0_plants_tree01_X6Z5_d0.trins", 6, 5, true)]
    [InlineData("terrain_1_1_plants_tree01_X15Z0_d0.trins", 15, 0, true)]
    [InlineData("area00_0_0_plants_tree01_X16Z5_d0.trins", 0, 0, false)]
    [InlineData("area00_0_0_plants_tree01.trins", 0, 0, false)]
    public void ParsesCellSuffix(string fileName, int x, int z, bool expected)
    {
        bool parsed = PlantCellCatalog.TryParseCell(fileName, out TerrainCell cell);

        Assert.Equal(expected, parsed);
        if (expected)
        {
            Assert.Equal(new TerrainCell(x, z), cell);
        }
    }

    private static FieldScene CreateScene(out PlantInstanceFile existing)
    {
        var scene = new FieldScene
        {
            AreaName = "test",
            ModelPackName = "test",
            DumpRoot = ".",
            TerrainCellPrefix = "area00_0_0",
            TerrainLogicalFolder = "bin/field/terrain/test/terrain/",
        };
        scene.Plants.Add(new PlantModel { Name = "plants_tree01.trmdl" });
        existing = new PlantInstanceFile
        {
            LogicalPath = "bin/field/terrain/test/terrain/area00_0_0_plants_tree01_X6Z5_d0.trins",
            ModelIndex = 0,
            OriginalCount = 1,
        };
        scene.InstanceFiles.Add(existing);
        return scene;
    }
}
