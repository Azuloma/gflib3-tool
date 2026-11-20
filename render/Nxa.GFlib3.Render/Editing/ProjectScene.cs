// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Editing;

/// <summary>Captures scene edits into a project and applies a project back to a scene.</summary>
public static class ProjectScene
{
    public static EditorProject Capture(FieldScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var files = new List<EditorProjectFile>();
        foreach (PlantInstanceFile source in scene.InstanceFiles)
        {
            string model = ResolveModelName(scene, source);
            var modified = new List<EditorProjectModification>();
            var added = new List<(int Index, Matrix4x4RowMajor Matrix)>();

            foreach (PlantInstance instance in scene.Instances)
            {
                if (!ReferenceEquals(instance.SourceFile, source))
                {
                    continue;
                }

                if (instance.SourceIndex < source.OriginalCount)
                {
                    if (instance.Matrix != instance.OriginalMatrix)
                    {
                        modified.Add(new EditorProjectModification
                        {
                            Index = instance.SourceIndex,
                            Original = instance.OriginalMatrix,
                            Matrix = instance.Matrix,
                        });
                    }
                }
                else
                {
                    added.Add((instance.SourceIndex, instance.Matrix));
                }
            }

            if (modified.Count == 0 && added.Count == 0)
            {
                continue;
            }

            if (source.AddedCount != added.Count)
            {
                throw new InvalidDataException(
                    $"'{source.LogicalPath}' reports {source.AddedCount} added instances "
                    + $"but the scene has {added.Count}.");
            }

            added.Sort((left, right) => left.Index.CompareTo(right.Index));
            var additions = new List<EditorProjectAddition>(added.Count);
            for (int i = 0; i < added.Count; i++)
            {
                long expected = (long)source.OriginalCount + i;
                if (added[i].Index != expected)
                {
                    throw new InvalidDataException(
                        $"'{source.LogicalPath}' has a non-contiguous added index; "
                        + $"expected {expected}, found {added[i].Index}.");
                }

                additions.Add(new EditorProjectAddition { Matrix = added[i].Matrix });
            }

            int[]? cell = null;
            if (source.IsSynthetic)
            {
                if (!PlantCellCatalog.TryParseCell(source.FileName, out TerrainCell parsed))
                {
                    throw new InvalidDataException(
                        $"Synthetic .trins '{source.LogicalPath}' has no parsable cell suffix.");
                }

                cell = [parsed.X, parsed.Z];
            }

            files.Add(new EditorProjectFile
            {
                Path = source.LogicalPath,
                Model = model,
                SourceCount = source.OriginalCount,
                Synthetic = source.IsSynthetic,
                Cell = cell,
                Modified = modified.Count > 0 ? modified : null,
                Added = additions.Count > 0 ? additions : null,
            });
        }

        files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        var project = new EditorProject
        {
            Format = EditorProject.FormatName,
            Version = EditorProject.CurrentVersion,
            Area = scene.AreaName,
            ModelPack = scene.ModelPackName,
            Terrain = scene.TerrainSurface is { } surface
                ? new EditorProjectTerrain
                {
                    CellPrefix = scene.TerrainCellPrefix,
                    LogicalFolder = scene.TerrainLogicalFolder,
                    WorldOriginX = surface.WorldOriginX,
                    WorldExtentX = surface.WorldExtentX,
                    WorldExtentZ = surface.WorldExtentZ,
                }
                : null,
            Files = files,
        };
        EditorProject.Normalize(project);
        return project;
    }

    public static ProjectApplyResult Apply(
        EditorProject project,
        FieldScene scene,
        PlantCellCatalog? cells)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(scene);

        EditorProject.ValidateAgainstScene(project, scene.AreaName, scene.ModelPackName);

        var result = new ProjectApplyResult();
        bool terrainTrusted = CheckTerrain(project, scene, result);
        Dictionary<(PlantInstanceFile File, int Index), PlantInstance> instances =
            BuildInstanceIndex(scene, result);

        foreach (EditorProjectFile projectFile in project.Files ?? [])
        {
            int modelIndex = FindModel(scene, projectFile, result);
            if (modelIndex < 0)
            {
                continue;
            }

            PlantInstanceFile? source = FindSource(scene, projectFile.Path);
            if (projectFile.Synthetic)
            {
                source = ResolveSynthetic(
                    projectFile,
                    scene,
                    cells,
                    modelIndex,
                    source,
                    terrainTrusted,
                    result);
            }
            else if (source is null)
            {
                result.Warnings.Add(
                    $"source .trins '{projectFile.Path}' is not present in the loaded area.");
                continue;
            }

            if (source is null)
            {
                continue;
            }

            if (source.ModelIndex != modelIndex)
            {
                result.Warnings.Add(
                    $"'{projectFile.Path}' belongs to model index {source.ModelIndex}, "
                    + $"not the project's '{projectFile.Model}'.");
                continue;
            }

            int modifiedApplied = ApplyModifications(projectFile, source, instances, result);
            int addedApplied = ApplyAdditions(projectFile, scene, source, modelIndex, result);

            if (modifiedApplied + addedApplied > 0)
            {
                result.FilesApplied++;
                result.ModifiedApplied += modifiedApplied;
                result.AddedApplied += addedApplied;
                if (projectFile.Synthetic)
                {
                    result.SyntheticFiles++;
                }
            }
        }

        if (result.AddedApplied > 0)
        {
            scene.NotifyInstancesChanged();
        }

        return result;
    }

    private static PlantInstanceFile? ResolveSynthetic(
        EditorProjectFile projectFile,
        FieldScene scene,
        PlantCellCatalog? cells,
        int modelIndex,
        PlantInstanceFile? source,
        bool terrainTrusted,
        ProjectApplyResult result)
    {
        if (!terrainTrusted)
        {
            result.Warnings.Add(
                $"'{projectFile.Path}': synthetic cells cannot be trusted for this area.");
            return null;
        }

        if (projectFile.Cell is not { Length: 2 } cellValues)
        {
            result.Warnings.Add($"'{projectFile.Path}': synthetic file has no cell.");
            return null;
        }

        if (source is not null)
        {
            return source;
        }

        if (cells is null)
        {
            result.Warnings.Add(
                $"'{projectFile.Path}': the loaded area has no terrain cell catalog.");
            return null;
        }

        var cell = new TerrainCell(cellValues[0], cellValues[1]);
        PlantInstanceFile? pending;
        try
        {
            pending = cells.GetOrCreatePending(modelIndex, cell);
        }
        catch (ArgumentException ex)
        {
            result.Warnings.Add($"'{projectFile.Path}': {ex.Message}");
            return null;
        }

        if (pending is null)
        {
            result.Warnings.Add(
                $"'{projectFile.Path}': the area cannot synthesize a .trins for cell "
                + $"({cell.X}, {cell.Z}).");
            return null;
        }

        if (!string.Equals(pending.LogicalPath, projectFile.Path, StringComparison.Ordinal))
        {
            result.Warnings.Add(
                $"'{projectFile.Path}': the cell now maps to '{pending.LogicalPath}'.");
            return null;
        }

        try
        {
            cells.Register(pending, cell);
        }
        catch (InvalidOperationException ex)
        {
            result.Warnings.Add($"'{projectFile.Path}': {ex.Message}");
            return null;
        }

        return pending;
    }

    private static int ApplyModifications(
        EditorProjectFile projectFile,
        PlantInstanceFile source,
        Dictionary<(PlantInstanceFile File, int Index), PlantInstance> instances,
        ProjectApplyResult result)
    {
        if (projectFile.Modified is not { } modifications)
        {
            return 0;
        }

        int applied = 0;
        foreach (EditorProjectModification change in modifications)
        {
            if (change.Index >= source.OriginalCount)
            {
                result.Warnings.Add(
                    $"'{projectFile.Path}': index {change.Index} is outside the source count "
                    + $"{source.OriginalCount}.");
                continue;
            }

            if (!instances.TryGetValue((source, change.Index), out PlantInstance? instance))
            {
                result.Warnings.Add(
                    $"'{projectFile.Path}': index {change.Index} is not present in the scene.");
                continue;
            }

            if (instance.OriginalMatrix != change.Original)
            {
                result.Warnings.Add(
                    $"'{projectFile.Path}': index {change.Index} has a different source matrix; "
                    + "the edit was skipped.");
                continue;
            }

            instance.Matrix = change.Matrix;
            applied++;
        }

        return applied;
    }

    private static int ApplyAdditions(
        EditorProjectFile projectFile,
        FieldScene scene,
        PlantInstanceFile source,
        int modelIndex,
        ProjectApplyResult result)
    {
        if (projectFile.Added is not { } additions)
        {
            return 0;
        }

        if (projectFile.SourceCount != source.OriginalCount)
        {
            result.Warnings.Add(
                $"'{projectFile.Path}': source count {source.OriginalCount} differs from the "
                + $"project's {projectFile.SourceCount}; {additions.Count} addition(s) were skipped.");
            return 0;
        }

        if (source.AddedCount != 0)
        {
            result.Warnings.Add(
                $"'{projectFile.Path}': the scene already has {source.AddedCount} added "
                + $"instance(s); {additions.Count} project addition(s) were skipped.");
            return 0;
        }

        int applied = 0;
        foreach (EditorProjectAddition addition in additions)
        {
            long index = (long)source.OriginalCount + source.AddedCount;
            if (index >= TrinsFile.MaxInstances)
            {
                result.Warnings.Add(
                    $"'{projectFile.Path}': instance limit {TrinsFile.MaxInstances} reached; "
                    + "remaining additions were skipped.");
                break;
            }

            scene.Instances.Add(new PlantInstance(modelIndex, addition.Matrix)
            {
                SourceFile = source,
                SourceIndex = checked((int)index),
            });
            source.AddedCount++;
            applied++;
        }

        return applied;
    }

    private static bool CheckTerrain(
        EditorProject project,
        FieldScene scene,
        ProjectApplyResult result)
    {
        if (project.Terrain is not { } terrain)
        {
            return true;
        }

        if (scene.TerrainSurface is not { } surface)
        {
            result.Warnings.Add(
                "the project has a terrain identity but the loaded area has no terrain.");
            return false;
        }

        bool matches = string.Equals(
                terrain.CellPrefix,
                scene.TerrainCellPrefix,
                StringComparison.Ordinal)
            && string.Equals(
                terrain.LogicalFolder,
                scene.TerrainLogicalFolder,
                StringComparison.Ordinal)
            && terrain.WorldOriginX == surface.WorldOriginX
            && terrain.WorldExtentX == surface.WorldExtentX
            && terrain.WorldExtentZ == surface.WorldExtentZ;
        if (!matches)
        {
            result.Warnings.Add(
                "the project terrain identity differs from the loaded area; "
                + "synthetic cells were skipped.");
            return false;
        }

        return true;
    }

    private static Dictionary<(PlantInstanceFile File, int Index), PlantInstance> BuildInstanceIndex(
        FieldScene scene,
        ProjectApplyResult result)
    {
        var index = new Dictionary<(PlantInstanceFile File, int Index), PlantInstance>();
        foreach (PlantInstance instance in scene.Instances)
        {
            if (instance.SourceFile is not PlantInstanceFile source || instance.SourceIndex < 0)
            {
                continue;
            }

            if (!index.TryAdd((source, instance.SourceIndex), instance))
            {
                result.Warnings.Add(
                    $"'{source.LogicalPath}' has more than one scene instance at index "
                    + $"{instance.SourceIndex}.");
            }
        }

        return index;
    }

    private static int FindModel(
        FieldScene scene,
        EditorProjectFile projectFile,
        ProjectApplyResult result)
    {
        int found = -1;
        for (int i = 0; i < scene.Plants.Count; i++)
        {
            if (!string.Equals(scene.Plants[i].Name, projectFile.Model, StringComparison.Ordinal))
            {
                continue;
            }

            if (found >= 0)
            {
                result.Warnings.Add(
                    $"'{projectFile.Path}': model '{projectFile.Model}' is loaded more than once.");
                return found;
            }

            found = i;
        }

        if (found < 0)
        {
            result.Warnings.Add(
                $"'{projectFile.Path}': model '{projectFile.Model}' is not loaded.");
        }

        return found;
    }

    private static PlantInstanceFile? FindSource(FieldScene scene, string path)
    {
        foreach (PlantInstanceFile file in scene.InstanceFiles)
        {
            if (string.Equals(file.LogicalPath, path, StringComparison.Ordinal))
            {
                return file;
            }
        }

        return null;
    }

    private static string ResolveModelName(FieldScene scene, PlantInstanceFile source)
    {
        if ((uint)source.ModelIndex >= (uint)scene.Plants.Count)
        {
            throw new InvalidDataException(
                $"'{source.LogicalPath}' has model index {source.ModelIndex}, "
                + $"outside the loaded plant range 0..{scene.Plants.Count - 1}.");
        }

        string model = scene.Plants[source.ModelIndex].Name;
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new InvalidDataException($"'{source.LogicalPath}' has no model name.");
        }

        return model;
    }
}

/// <summary>Applied project counters and per-file conflict warnings.</summary>
public sealed class ProjectApplyResult
{
    public int FilesApplied { get; internal set; }

    public int ModifiedApplied { get; internal set; }

    public int AddedApplied { get; internal set; }

    public int SyntheticFiles { get; internal set; }

    public List<string> Warnings { get; } = [];
}
