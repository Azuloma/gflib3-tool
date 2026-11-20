// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Editing;

/// <summary>
/// Owns the active project path, its saved baseline, and dirty state. The scene
/// itself remains the live edit target; the baseline is a normalized snapshot.
/// </summary>
public sealed class ProjectSession
{
    private byte[] _baseline = [];
    private EditorProject _sceneBaseline;
    private byte[] _sceneBaselineBytes = [];

    public ProjectSession(FieldScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        _sceneBaseline = ProjectScene.Capture(scene);
        _sceneBaselineBytes = EditorProject.Serialize(_sceneBaseline);
        Baseline = _sceneBaseline;
        _baseline = _sceneBaselineBytes;
    }

    public string? ProjectPath { get; private set; }

    public EditorProject Baseline { get; private set; }

    public bool IsDirty { get; private set; }

    public bool HasProject => ProjectPath is not null;

    public void Reset(FieldScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ProjectPath = null;
        _sceneBaseline = ProjectScene.Capture(scene);
        _sceneBaselineBytes = EditorProject.Serialize(_sceneBaseline);
        Baseline = _sceneBaseline;
        _baseline = _sceneBaselineBytes;
        IsDirty = false;
    }

    /// <summary>Removes the project association but keeps its edits as unsaved changes.</summary>
    public void DetachProject(FieldScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ProjectPath = null;
        Baseline = _sceneBaseline;
        _baseline = _sceneBaselineBytes;
        byte[] current = EditorProject.Serialize(ProjectScene.Capture(scene));
        IsDirty = !current.AsSpan().SequenceEqual(_baseline);
    }

    public void MarkEdited() => IsDirty = true;

    /// <summary>Recomputes dirty state against the saved baseline after an undo-like change.</summary>
    public void RefreshDirty(FieldScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        byte[] current = EditorProject.Serialize(ProjectScene.Capture(scene));
        IsDirty = !current.AsSpan().SequenceEqual(_baseline);
    }

    public ProjectApplyResult Open(FieldScene scene, PlantCellCatalog? cells, string path)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);

        EditorProject project = EditorProject.Read(fullPath);
        EditorProject.ValidateAgainstScene(project, scene.AreaName, scene.ModelPackName);
        ProjectApplyResult result = ProjectScene.Apply(project, scene, cells);

        ProjectPath = fullPath;
        Baseline = project;
        _baseline = EditorProject.Serialize(project);
        byte[] actual = EditorProject.Serialize(ProjectScene.Capture(scene));
        IsDirty = !actual.AsSpan().SequenceEqual(_baseline);

        foreach (string warning in result.Warnings)
        {
            scene.Messages.Add($"warning: project '{fullPath}': {warning}");
        }

        scene.Messages.Add(
            $"project: loaded '{fullPath}': {result.FilesApplied} file(s), "
            + $"{result.ModifiedApplied} modified, {result.AddedApplied} added, "
            + $"{result.SyntheticFiles} synthetic.");
        return result;
    }

    public string Save(FieldScene scene, string path)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        EditorProject project = ProjectScene.Capture(scene);
        EditorProject.Write(project, fullPath);

        ProjectPath = fullPath;
        Baseline = project;
        _baseline = EditorProject.Serialize(project);
        IsDirty = false;
        scene.Messages.Add(
            $"project: saved '{fullPath}': {project.FileCount} file(s), "
            + $"{project.ModifiedCount} modified, {project.AddedCount} added, "
            + $"{project.SyntheticFileCount} synthetic.");
        return fullPath;
    }

    /// <summary>Counts instance changes that differ from the saved baseline.</summary>
    public int CountUnsavedChanges(FieldScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!IsDirty)
        {
            return 0;
        }

        EditorProject current = ProjectScene.Capture(scene);
        Dictionary<string, EditorProjectFile> baselineFiles = BuildFileMap(Baseline);
        Dictionary<string, EditorProjectFile> currentFiles = BuildFileMap(current);
        int changes = 0;
        foreach ((string path, EditorProjectFile currentFile) in currentFiles)
        {
            baselineFiles.TryGetValue(path, out EditorProjectFile? baselineFile);
            changes += CountFileChanges(currentFile, baselineFile);
        }

        foreach ((string path, EditorProjectFile baselineFile) in baselineFiles)
        {
            if (!currentFiles.ContainsKey(path))
            {
                changes += CountFileChanges(null, baselineFile);
            }
        }

        return changes;
    }

    private static int CountFileChanges(EditorProjectFile? current, EditorProjectFile? baseline)
    {
        Dictionary<int, EditorProjectModification> baselineModifications =
            (baseline?.Modified ?? []).ToDictionary(change => change.Index);
        Dictionary<int, EditorProjectModification> currentModifications =
            (current?.Modified ?? []).ToDictionary(change => change.Index);

        int changes = 0;
        foreach ((int index, EditorProjectModification modification) in currentModifications)
        {
            if (!baselineModifications.TryGetValue(index, out EditorProjectModification? saved)
                || saved.Matrix != modification.Matrix)
            {
                changes++;
            }
        }

        foreach (int index in baselineModifications.Keys)
        {
            if (!currentModifications.ContainsKey(index))
            {
                changes++;
            }
        }

        List<EditorProjectAddition> baselineAdded = baseline?.Added ?? [];
        List<EditorProjectAddition> currentAdded = current?.Added ?? [];
        int addedCount = Math.Max(baselineAdded.Count, currentAdded.Count);
        for (int i = 0; i < addedCount; i++)
        {
            if (i >= baselineAdded.Count
                || i >= currentAdded.Count
                || baselineAdded[i].Matrix != currentAdded[i].Matrix)
            {
                changes++;
            }
        }

        return changes;
    }

    private static Dictionary<string, EditorProjectFile> BuildFileMap(EditorProject project)
    {
        var files = new Dictionary<string, EditorProjectFile>(StringComparer.Ordinal);
        foreach (EditorProjectFile file in project.Files ?? [])
        {
            files[file.Path] = file;
        }

        return files;
    }
}
