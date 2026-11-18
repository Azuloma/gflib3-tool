// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class SceneCatalogTests
{
    [Fact]
    public void ClassifiesAndPairsAreaPacks()
    {
        using var dump = new TempDump();
        dump.AddTerrain("ha_area00");
        dump.AddTerrain("ha_area00_s01");
        dump.AddTerrain("stray");
        dump.AddModel("ha_area00");
        dump.AddModel("ha_area00_s01");
        dump.AddModel("ha_area00_s02");
        dump.AddModel("resident_bg_parts_model");
        dump.AddModel("animation");
        dump.AddModel("collision");

        SceneCatalog catalog = SceneCatalog.Discover(dump.Root);

        Assert.Null(catalog.DiscoveryWarning);
        Assert.Equal(["ha_area00", "ha_area00_s01"], catalog.Areas);
        Assert.Equal(["ha_area00", "ha_area00_s01", "ha_area00_s02"], catalog.ModelPacks);
        Assert.Equal(9, catalog.Assets.Count);

        AssetPack baseTerrain = Single(catalog, "ha_area00", AssetKind.BaseArea);
        Assert.Equal("ha_area00", baseTerrain.PairName);
        Assert.True(baseTerrain.IsLoadable);
        Assert.Equal(3, baseTerrain.SizeBytes);

        AssetPack baseModel = Single(catalog, "ha_area00", AssetKind.AreaModel);
        Assert.Equal("ha_area00", baseModel.PairName);
        Assert.True(baseModel.IsLoadable);

        AssetPack subTerrain = Single(catalog, "ha_area00_s01", AssetKind.SubArea);
        Assert.Equal("ha_area00_s01", subTerrain.PairName);

        AssetPack subModel = Single(catalog, "ha_area00_s01", AssetKind.SubAreaModel);
        Assert.Equal("ha_area00_s01", subModel.PairName);

        AssetPack unpairedModel = Single(catalog, "ha_area00_s02", AssetKind.SubAreaModel);
        Assert.Null(unpairedModel.PairName);
        Assert.False(unpairedModel.IsLoadable);

        Assert.Equal(AssetKind.AuxiliaryModel, Single(catalog, "resident_bg_parts_model", AssetKind.AuxiliaryModel).Kind);
        Assert.Null(Single(catalog, "resident_bg_parts_model", AssetKind.AuxiliaryModel).PairName);
        Assert.Equal(AssetKind.Other, Single(catalog, "animation", AssetKind.Other).Kind);
        Assert.Null(Single(catalog, "animation", AssetKind.Other).PairName);
        Assert.Equal(AssetKind.Other, Single(catalog, "collision", AssetKind.Other).Kind);
        Assert.Equal(AssetKind.Other, Single(catalog, "stray", AssetKind.Other).Kind);
    }

    [Fact]
    public void SortsAssetsByNameThenKind()
    {
        using var dump = new TempDump();
        dump.AddTerrain("ha_area00_s01");
        dump.AddTerrain("ha_area00");
        dump.AddModel("ha_area00");
        dump.AddModel("ha_area00_s01");
        dump.AddModel("resident_bg_parts_model");
        dump.AddModel("animation");

        SceneCatalog catalog = SceneCatalog.Discover(dump.Root);

        (string Name, AssetKind Kind)[] actual = catalog.Assets
            .Select(asset => (asset.Name, asset.Kind))
            .ToArray();
        Assert.Equal(
            [
                ("animation", AssetKind.Other),
                ("ha_area00", AssetKind.BaseArea),
                ("ha_area00", AssetKind.AreaModel),
                ("ha_area00_s01", AssetKind.SubArea),
                ("ha_area00_s01", AssetKind.SubAreaModel),
                ("resident_bg_parts_model", AssetKind.AuxiliaryModel),
            ],
            actual);
    }

    [Fact]
    public void FallsBackToBaseModelForSubAreaTerrain()
    {
        using var dump = new TempDump();
        dump.AddTerrain("ha_area01_s01");
        dump.AddModel("ha_area01");

        SceneCatalog catalog = SceneCatalog.Discover(dump.Root);

        AssetPack terrain = Single(catalog, "ha_area01_s01", AssetKind.SubArea);
        Assert.Equal("ha_area01", terrain.PairName);
        Assert.True(terrain.IsLoadable);
        Assert.Equal(["ha_area01_s01"], catalog.Areas);
        Assert.Equal(["ha_area01"], catalog.ModelPacks);
    }

    [Fact]
    public void AcceptsCaseInsensitiveNamesAndSubAreaSuffixes()
    {
        using var dump = new TempDump();
        dump.AddTerrain("HA_AREA03");
        dump.AddTerrain("ha_area03_s01_a");
        dump.AddModel("ha_area03");
        dump.AddModel("HA_AREA03_S01_A");

        SceneCatalog catalog = SceneCatalog.Discover(dump.Root);

        AssetPack baseTerrain = Single(catalog, "HA_AREA03", AssetKind.BaseArea);
        Assert.Equal("ha_area03", baseTerrain.PairName);
        AssetPack subTerrain = Single(catalog, "ha_area03_s01_a", AssetKind.SubArea);
        Assert.Equal("HA_AREA03_S01_A", subTerrain.PairName);
    }

    [Theory]
    [InlineData("ha_area3")]
    [InlineData("ha_area00_s1")]
    [InlineData("ha_area00_s01x")]
    [InlineData("ha_area00_extra")]
    public void ClassifiesMalformedNamesAsOther(string name)
    {
        using var dump = new TempDump();
        dump.AddTerrain(name);

        SceneCatalog catalog = SceneCatalog.Discover(dump.Root);

        AssetPack asset = Assert.Single(catalog.Assets);
        Assert.Equal(AssetKind.Other, asset.Kind);
        Assert.Null(asset.PairName);
    }

    [Fact]
    public void ReturnsEmptyCatalogForMissingFolders()
    {
        using var dump = new TempDump();

        SceneCatalog catalog = SceneCatalog.Discover(dump.Root);

        Assert.Empty(catalog.Areas);
        Assert.Empty(catalog.ModelPacks);
        Assert.Empty(catalog.Assets);
        Assert.Null(catalog.DiscoveryWarning);
    }

    [Fact]
    public void DiscoversPairedPacksWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        SceneCatalog catalog = SceneCatalog.Discover(dump);

        Assert.Null(catalog.DiscoveryWarning);
        Assert.NotEmpty(catalog.Areas);
        Assert.NotEmpty(catalog.ModelPacks);
        Assert.All(
            catalog.Assets.Where(asset => asset.Kind is AssetKind.BaseArea or AssetKind.SubArea),
            asset => Assert.True(
                asset.IsLoadable,
                $"Terrain pack '{asset.Name}' has no model counterpart."));
        Assert.All(
            catalog.Assets.Where(asset => asset.Kind is AssetKind.AreaModel or AssetKind.SubAreaModel),
            asset => Assert.True(
                asset.IsLoadable,
                $"Model pack '{asset.Name}' has no terrain counterpart."));
        Assert.Contains(
            catalog.Assets,
            asset => asset.Name == "resident_bg_parts_model"
                && asset.Kind == AssetKind.AuxiliaryModel
                && !asset.IsLoadable);
    }

    private static AssetPack Single(SceneCatalog catalog, string name, AssetKind kind)
    {
        return Assert.Single(
            catalog.Assets,
            asset => asset.Name == name && asset.Kind == kind);
    }

    private sealed class TempDump : IDisposable
    {
        private const string TerrainFolder = "RomFS/bin/archive/field/terrain";
        private const string ModelFolder = "RomFS/bin/archive/field/model/pack";

        public TempDump()
        {
            Root = Path.Combine(Path.GetTempPath(), "gflib3-catalog-" + Guid.NewGuid().ToString("N"));
        }

        public string Root { get; }

        public void AddTerrain(string name) => Add(TerrainFolder, name);

        public void AddModel(string name) => Add(ModelFolder, name);

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }

        private void Add(string folder, string name)
        {
            string path = Path.Combine(Root, folder);
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, name + ".gfpak"), [1, 2, 3]);
        }
    }
}
