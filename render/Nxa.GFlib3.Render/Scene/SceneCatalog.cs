// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Render.Scene;

/// <summary>Role of a field archive discovered in the dump.</summary>
public enum AssetKind
{
    BaseArea,
    SubArea,
    AreaModel,
    SubAreaModel,
    AuxiliaryModel,
    Other,
}

/// <summary>One <c>.gfpak</c> file discovered by <see cref="SceneCatalog"/>.</summary>
public sealed record AssetPack
{
    public required string Name { get; init; }

    public required AssetKind Kind { get; init; }

    public required string FilePath { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>Same-named counterpart pack that forms a loadable area/model pair.</summary>
    public string? PairName { get; init; }

    public bool IsLoadable => PairName is not null;
}

/// <summary>Area, model, and asset packs discovered in the dump.</summary>
public sealed class SceneCatalog
{
    private const string TerrainFolder = "bin/archive/field/terrain";
    private const string ModelFolder = "bin/archive/field/model/pack";
    private const string AuxiliaryModelName = "resident_bg_parts_model";
    private const int BaseAreaNameLength = 9;  // ha_areaNN
    private const int SubAreaNameLength = 13;  // ha_areaNN_sNN

    private SceneCatalog(
        IReadOnlyList<string> areas,
        IReadOnlyList<string> modelPacks,
        IReadOnlyList<AssetPack> assets,
        string? discoveryWarning)
    {
        Areas = areas;
        ModelPacks = modelPacks;
        Assets = assets;
        DiscoveryWarning = discoveryWarning;
    }

    public IReadOnlyList<string> Areas { get; }

    public IReadOnlyList<string> ModelPacks { get; }

    /// <summary>Every discovered pack, sorted by name and then by kind.</summary>
    public IReadOnlyList<AssetPack> Assets { get; }

    /// <summary>Enumeration failure that left the catalog empty or partial.</summary>
    public string? DiscoveryWarning { get; }

    public static SceneCatalog Discover(string dumpRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpRoot);
        string romfs = Path.Combine(dumpRoot, "RomFS");
        var warnings = new List<string>();
        List<PackFile> terrainFiles = ListPackFiles(
            Path.Combine(romfs, TerrainFolder), warnings);
        List<PackFile> modelFiles = ListPackFiles(
            Path.Combine(romfs, ModelFolder), warnings);
        List<AssetPack> assets = BuildAssets(terrainFiles, modelFiles, warnings);

        List<string> areas = assets
            .Where(asset => asset.Kind is AssetKind.BaseArea or AssetKind.SubArea)
            .Select(asset => asset.Name)
            .ToList();
        List<string> models = assets
            .Where(asset => asset.Kind is AssetKind.AreaModel or AssetKind.SubAreaModel)
            .Select(asset => asset.Name)
            .ToList();
        string? warning = warnings.Count == 0 ? null : string.Join("; ", warnings);
        return new SceneCatalog(areas, models, assets, warning);
    }

    public int IndexOfArea(string? name) => IndexOf(Areas, name);

    public int IndexOfModel(string? name) => IndexOf(ModelPacks, name);

    private static List<AssetPack> BuildAssets(
        IReadOnlyList<PackFile> terrainFiles,
        IReadOnlyList<PackFile> modelFiles,
        List<string> warnings)
    {
        Dictionary<string, AssetPack> terrain = CreateAssetMap(
            terrainFiles, ClassifyTerrain, warnings);
        Dictionary<string, AssetPack> models = CreateAssetMap(
            modelFiles, ClassifyModel, warnings);

        var assets = new List<AssetPack>(terrain.Count + models.Count);
        assets.AddRange(PairAssets(terrain, models));
        assets.AddRange(PairAssets(models, terrain));
        assets.Sort(CompareAssets);
        return assets;
    }

    private static Dictionary<string, AssetPack> CreateAssetMap(
        IReadOnlyList<PackFile> files,
        Func<string, AssetKind> classify,
        List<string> warnings)
    {
        var map = new Dictionary<string, AssetPack>(StringComparer.OrdinalIgnoreCase);
        foreach (PackFile file in files)
        {
            var asset = new AssetPack
            {
                Name = file.Name,
                Kind = classify(file.Name),
                FilePath = file.Path,
                SizeBytes = file.Length,
            };

            if (!map.TryAdd(asset.Name, asset))
            {
                warnings.Add($"duplicate pack name '{asset.Name}' in '{file.Path}'; keeping the first.");
            }
        }

        return map;
    }

    private static IEnumerable<AssetPack> PairAssets(
        Dictionary<string, AssetPack> own,
        Dictionary<string, AssetPack> counterparts)
    {
        foreach (AssetPack asset in own.Values)
        {
            string? pair = FindCounterpart(asset, counterparts);
            yield return asset with { PairName = pair };
        }
    }

    private static string? FindCounterpart(
        AssetPack asset,
        Dictionary<string, AssetPack> counterparts)
    {
        AssetKind? expected = CounterpartKind(asset.Kind);
        if (expected is not null
            && counterparts.TryGetValue(asset.Name, out AssetPack? candidate)
            && candidate is not null
            && candidate.Kind == expected)
        {
            return candidate.Name;
        }

        // Some sub-area terrain packs have no same-named model pack and render
        // with the base area model instead.
        if (asset.Kind == AssetKind.SubArea
            && counterparts.TryGetValue(asset.Name[..BaseAreaNameLength], out AssetPack? baseModel)
            && baseModel is not null
            && baseModel.Kind == AssetKind.AreaModel)
        {
            return baseModel.Name;
        }

        return null;
    }

    private static AssetKind? CounterpartKind(AssetKind kind) => kind switch
    {
        AssetKind.BaseArea => AssetKind.AreaModel,
        AssetKind.SubArea => AssetKind.SubAreaModel,
        AssetKind.AreaModel => AssetKind.BaseArea,
        AssetKind.SubAreaModel => AssetKind.SubArea,
        _ => null,
    };

    private static int CompareAssets(AssetPack left, AssetPack right)
    {
        int byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        return byName != 0 ? byName : left.Kind.CompareTo(right.Kind);
    }

    private static AssetKind ClassifyTerrain(string name)
    {
        if (IsBaseAreaName(name))
        {
            return AssetKind.BaseArea;
        }

        return IsSubAreaName(name) ? AssetKind.SubArea : AssetKind.Other;
    }

    private static AssetKind ClassifyModel(string name)
    {
        if (string.Equals(name, AuxiliaryModelName, StringComparison.OrdinalIgnoreCase))
        {
            return AssetKind.AuxiliaryModel;
        }

        if (IsBaseAreaName(name))
        {
            return AssetKind.AreaModel;
        }

        return IsSubAreaName(name) ? AssetKind.SubAreaModel : AssetKind.Other;
    }

    private static bool IsBaseAreaName(string name)
    {
        return name.Length == BaseAreaNameLength
            && name.StartsWith("ha_area", StringComparison.OrdinalIgnoreCase)
            && char.IsAsciiDigit(name[7])
            && char.IsAsciiDigit(name[8]);
    }

    private static bool IsSubAreaName(string name)
    {
        return name.Length >= SubAreaNameLength
            && name.StartsWith("ha_area", StringComparison.OrdinalIgnoreCase)
            && char.IsAsciiDigit(name[7])
            && char.IsAsciiDigit(name[8])
            && name[9] == '_'
            && name[10] is 's' or 'S'
            && char.IsAsciiDigit(name[11])
            && char.IsAsciiDigit(name[12])
            && (name.Length == SubAreaNameLength || name[SubAreaNameLength] == '_');
    }

    private static int IndexOf(IReadOnlyList<string> names, string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }

        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<PackFile> ListPackFiles(string folder, List<string> warnings)
    {
        var files = new List<PackFile>();
        if (!Directory.Exists(folder))
        {
            return files;
        }

        try
        {
            foreach (string path in Directory.EnumerateFiles(folder, "*.gfpak"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.Length == 0)
                {
                    continue;
                }

                long length;
                try
                {
                    length = new FileInfo(path).Length;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"failed to stat '{path}': {ex.Message}");
                    continue;
                }

                files.Add(new PackFile(name, path, length));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"failed to enumerate '{folder}': {ex.Message}");
        }

        return files;
    }

    private readonly record struct PackFile(string Name, string Path, long Length);
}
