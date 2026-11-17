// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Core.Formats.Terrain;

/// <summary>
/// Terrain manifest FlatBuffer. It groups vegetation model files with the
/// <c>.trins</c> placement files that instance them.
/// </summary>
public sealed class TerrainManifest
{
    /// <summary>Smallest manifest observed in the dump (area 11, 1,664 bytes).</summary>
    public const int MinArchiveBytes = 512;

    /// <summary>Largest manifest observed in the dump (area 02, 35,284 bytes).</summary>
    public const int MaxArchiveBytes = 1 << 16;

    private const int MaxGroups = 100_000;
    private const int MaxInstancePaths = 1_000_000;

    private const string ModelExtension = ".trmdl";
    private const string InstanceExtension = ".trins";
    private const string MaterialExtension = ".trmtr";
    private const string DetailMapExtension = ".trtbf";

    private TerrainManifest(
        uint plantGroupCount,
        TerrainPlantGroup[] plantGroups,
        uint detailGroupCount,
        TerrainDetailGroup[] detailGroups,
        string? materialPath,
        string[] detailMapPaths,
        float gridWidth,
        float gridHeight,
        float heightScale,
        (uint X, uint Y, uint Z) cellDimensions)
    {
        PlantGroupCount = plantGroupCount;
        PlantGroups = plantGroups;
        DetailGroupCount = detailGroupCount;
        DetailGroups = detailGroups;
        MaterialPath = materialPath;
        DetailMapPaths = detailMapPaths;
        GridWidth = gridWidth;
        GridHeight = gridHeight;
        HeightScale = heightScale;
        CellDimensions = cellDimensions;
    }

    public uint PlantGroupCount { get; }

    public IReadOnlyList<TerrainPlantGroup> PlantGroups { get; }

    public uint DetailGroupCount { get; }

    public IReadOnlyList<TerrainDetailGroup> DetailGroups { get; }

    /// <summary>Heightmap grid width from field 1 (1025 in observed packs).</summary>
    public float GridWidth { get; }

    /// <summary>Heightmap grid height from field 1 (1025 in observed packs).</summary>
    public float GridHeight { get; }

    /// <summary>
    /// Height extent from field 1; heightmap samples are normalized and scale by
    /// this value (185 for area 00).
    /// </summary>
    public float HeightScale { get; }

    /// <summary>Field 2 values (1024/1024/1025); individual meanings unresolved.</summary>
    public (uint X, uint Y, uint Z) CellDimensions { get; }

    /// <summary>Terrain material (<c>.trmtr</c>) referenced by the manifest.</summary>
    public string? MaterialPath { get; }

    public IReadOnlyList<string> DetailMapPaths { get; }

    public static TerrainManifest Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Parse(File.ReadAllBytes(path));
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Failed to parse terrain manifest '{path}': {ex.Message}", ex);
        }
    }

    public static TerrainManifest Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Parse(FlatBufferTable.GetRoot(data));
    }

    public static TerrainManifest Parse(FlatBufferTable root)
    {
        uint plantCount = root.GetUInt32(3);
        TerrainPlantGroup[] plantGroups = ReadPlantGroups(root, plantCount);
        uint detailCount = root.GetUInt32(5);
        TerrainDetailGroup[] detailGroups = ReadDetailGroups(root, detailCount);

        FlatBufferTable? material = root.GetTable(7);
        string? materialPath = material?.GetString(0);
        string[] detailMaps = ReadStringVector(root, 8);

        FlatBufferTable? dimensions = root.GetTable(1);
        float gridWidth = dimensions?.GetFloat(0) ?? 0;
        float gridHeight = dimensions?.GetFloat(1) ?? 0;
        float heightScale = dimensions?.GetFloat(2) ?? 0;

        FlatBufferTable? cells = root.GetTable(2);
        (uint X, uint Y, uint Z) cellDimensions = (
            cells?.GetUInt32(0) ?? 0,
            cells?.GetUInt32(1) ?? 0,
            cells?.GetUInt32(2) ?? 0);

        return new TerrainManifest(
            plantCount,
            plantGroups,
            detailCount,
            detailGroups,
            materialPath,
            detailMaps,
            gridWidth,
            gridHeight,
            heightScale,
            cellDimensions);
    }

    /// <summary>
    /// Attempts a structural parse. Used to locate the unnamed manifest entry
    /// inside a terrain pack.
    /// </summary>
    public static bool TryParse(byte[] data, out TerrainManifest? manifest)
    {
        try
        {
            manifest = Parse(data);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
        {
            manifest = null;
            return false;
        }
    }

    /// <summary>
    /// Finds the manifest entry in a terrain pack. The entry has no resolved
    /// file name; detection is structural and bounded to <see cref="MinArchiveBytes"/>
    /// through <see cref="MaxArchiveBytes"/> of decompressed data.
    /// </summary>
    public static TerrainManifest FindInArchive(GfpakArchive archive, out GfpakEntry entry)
    {
        ArgumentNullException.ThrowIfNull(archive);

        foreach (GfpakEntry candidate in archive.Entries)
        {
            if (candidate.SizeDecompressed < MinArchiveBytes
                || candidate.SizeDecompressed > MaxArchiveBytes)
            {
                continue;
            }

            byte[] data = archive.ReadEntry(candidate);
            if (!TryParse(data, out TerrainManifest? manifest)
                || !LooksLikeVegetationManifest(manifest!))
            {
                continue;
            }

            entry = candidate;
            return manifest!;
        }

        throw new InvalidDataException(
            $"No terrain manifest with plant groups found among {archive.Entries.Count} entries "
            + $"(decompressed size {MinArchiveBytes}..{MaxArchiveBytes}).");
    }

    private static bool LooksLikeVegetationManifest(TerrainManifest manifest)
    {
        if (manifest.PlantGroups.Count == 0)
        {
            return false;
        }

        foreach (TerrainPlantGroup group in manifest.PlantGroups)
        {
            if (!group.ModelPath.EndsWith(ModelExtension, StringComparison.Ordinal))
            {
                return false;
            }

            foreach (string instancePath in group.InstancePaths)
            {
                if (!instancePath.EndsWith(InstanceExtension, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        if (manifest.MaterialPath is not null
            && !manifest.MaterialPath.EndsWith(MaterialExtension, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (string detailMap in manifest.DetailMapPaths)
        {
            if (!detailMap.EndsWith(DetailMapExtension, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static TerrainPlantGroup[] ReadPlantGroups(FlatBufferTable root, uint declaredCount)
    {
        if (declaredCount > MaxGroups)
        {
            throw new InvalidDataException($"Terrain manifest plant group count {declaredCount} exceeds the limit.");
        }

        if (!root.TryGetVector(4, 4, out FlatBufferVector vector))
        {
            if (declaredCount != 0)
            {
                throw new InvalidDataException(
                    $"Terrain manifest declares {declaredCount} plant groups but has no group vector.");
            }

            return Array.Empty<TerrainPlantGroup>();
        }

        if (vector.Count != declaredCount)
        {
            throw new InvalidDataException(
                $"Terrain manifest declares {declaredCount} plant groups but the vector holds {vector.Count}.");
        }

        var groups = new TerrainPlantGroup[vector.Count];
        int instancePaths = 0;
        for (int i = 0; i < groups.Length; i++)
        {
            try
            {
                groups[i] = ParsePlantGroup(vector.GetTable(i));
                instancePaths = checked(instancePaths + groups[i].InstancePaths.Count);
                if (instancePaths > MaxInstancePaths)
                {
                    throw new InvalidDataException($"Terrain manifest instance path total exceeds {MaxInstancePaths}.");
                }
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Plant group {i}: {ex.Message}", ex);
            }
        }

        return groups;
    }

    private static TerrainPlantGroup ParsePlantGroup(FlatBufferTable table)
    {
        string modelPath = table.GetString(0)
            ?? throw new InvalidDataException("Plant group has no model path.");
        string[] instancePaths = ReadStringVector(table, 1);
        return new TerrainPlantGroup(modelPath, instancePaths);
    }

    private static TerrainDetailGroup[] ReadDetailGroups(FlatBufferTable root, uint declaredCount)
    {
        if (declaredCount > MaxGroups)
        {
            throw new InvalidDataException($"Terrain manifest detail group count {declaredCount} exceeds the limit.");
        }

        if (!root.TryGetVector(6, 4, out FlatBufferVector vector))
        {
            if (declaredCount != 0)
            {
                throw new InvalidDataException(
                    $"Terrain manifest declares {declaredCount} detail groups but has no group vector.");
            }

            return Array.Empty<TerrainDetailGroup>();
        }

        if (vector.Count != declaredCount)
        {
            throw new InvalidDataException(
                $"Terrain manifest declares {declaredCount} detail groups but the vector holds {vector.Count}.");
        }

        var groups = new TerrainDetailGroup[vector.Count];
        for (int i = 0; i < groups.Length; i++)
        {
            try
            {
                groups[i] = ParseDetailGroup(vector.GetTable(i));
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Detail group {i}: {ex.Message}", ex);
            }
        }

        return groups;
    }

    private static TerrainDetailGroup ParseDetailGroup(FlatBufferTable table)
    {
        int parameterCount = 0;
        for (int field = 1; table.HasField(field); field++)
        {
            parameterCount++;
            if (field == int.MaxValue)
            {
                throw new InvalidDataException("Detail group field count overflowed.");
            }
        }

        return new TerrainDetailGroup(table.GetString(0), parameterCount);
    }

    private static string[] ReadStringVector(FlatBufferTable table, int field)
    {
        if (!table.TryGetVector(field, 4, out FlatBufferVector vector))
        {
            return Array.Empty<string>();
        }

        if (vector.Count > MaxInstancePaths)
        {
            throw new InvalidDataException($"Terrain manifest string vector {field} holds {vector.Count} entries.");
        }

        var result = new string[vector.Count];
        for (int i = 0; i < result.Length; i++)
        {
            try
            {
                result[i] = vector.GetString(i);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Field {field}, entry {i}: {ex.Message}", ex);
            }
        }

        return result;
    }
}

public sealed class TerrainPlantGroup
{
    internal TerrainPlantGroup(string modelPath, IReadOnlyList<string> instancePaths)
    {
        ModelPath = modelPath;
        InstancePaths = instancePaths;
    }

    /// <summary>Vegetation model file, for example <c>plants_tree02.trmdl</c>.</summary>
    public string ModelPath { get; }

    /// <summary><c>.trins</c> placement files that instance <see cref="ModelPath"/>.</summary>
    public IReadOnlyList<string> InstancePaths { get; }
}

public sealed class TerrainDetailGroup
{
    internal TerrainDetailGroup(string? modelPath, int parameterCount)
    {
        ModelPath = modelPath;
        ParameterCount = parameterCount;
    }

    /// <summary>Detail model file when present; detail placement rules are unresolved.</summary>
    public string? ModelPath { get; }

    /// <summary>Number of trailing scalar parameters (fields 1 and later).</summary>
    public int ParameterCount { get; }
}
