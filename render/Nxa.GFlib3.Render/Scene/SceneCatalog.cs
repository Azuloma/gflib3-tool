// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Render.Scene;

/// <summary>Area and model pack names discovered in the dump.</summary>
public sealed class SceneCatalog
{
    private const string TerrainFolder = "bin/archive/field/terrain";
    private const string ModelFolder = "bin/archive/field/model/pack";

    private SceneCatalog(
        IReadOnlyList<string> areas,
        IReadOnlyList<string> modelPacks,
        string? discoveryWarning)
    {
        Areas = areas;
        ModelPacks = modelPacks;
        DiscoveryWarning = discoveryWarning;
    }

    public IReadOnlyList<string> Areas { get; }

    public IReadOnlyList<string> ModelPacks { get; }

    /// <summary>Enumeration failure that left the catalog empty or partial.</summary>
    public string? DiscoveryWarning { get; }

    public static SceneCatalog Discover(string dumpRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpRoot);
        string romfs = Path.Combine(dumpRoot, "RomFS");
        var warnings = new List<string>();
        List<string> areas = ListPacks(Path.Combine(romfs, TerrainFolder), warnings);
        List<string> models = ListPacks(Path.Combine(romfs, ModelFolder), warnings);
        string? warning = warnings.Count == 0 ? null : string.Join("; ", warnings);
        return new SceneCatalog(areas, models, warning);
    }

    public int IndexOfArea(string? name) => IndexOf(Areas, name);

    public int IndexOfModel(string? name) => IndexOf(ModelPacks, name);

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

    private static List<string> ListPacks(string folder, List<string> warnings)
    {
        var names = new List<string>();
        if (Directory.Exists(folder))
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*.gfpak"))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (name.Length > 0)
                    {
                        names.Add(name);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"failed to enumerate '{folder}': {ex.Message}");
            }
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }
}
