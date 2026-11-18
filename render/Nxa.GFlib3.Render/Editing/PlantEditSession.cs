// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Editing;

/// <summary>One undoable vegetation edit.</summary>
public abstract record PlantEditCommand(int InstanceIndex);

/// <summary>One committed instance transform change.</summary>
public sealed record PlantTransformCommand : PlantEditCommand
{
    public PlantTransformCommand(
        int instanceIndex,
        Matrix4x4RowMajor before,
        Matrix4x4RowMajor after)
        : base(instanceIndex)
    {
        Before = before;
        After = after;
    }

    public Matrix4x4RowMajor Before { get; }

    public Matrix4x4RowMajor After { get; }
}

/// <summary>One appended instance, removed again when the edit is undone.</summary>
public sealed record PlantAddCommand : PlantEditCommand
{
    public PlantAddCommand(int instanceIndex, PlantInstance instance)
        : base(instanceIndex)
    {
        ArgumentNullException.ThrowIfNull(instance);
        Instance = instance;
    }

    public PlantInstance Instance { get; }
}

/// <summary>
/// Mutates the scene's instance list and matrices, recording linear undo/redo
/// history. Live drag updates use <see cref="SetMatrix"/>; a drag commits once
/// on release.
/// </summary>
public sealed class PlantEditSession
{
    public const int MaxHistoryEntries = 512;

    private readonly FieldScene _scene;
    private readonly List<PlantEditCommand> _history = [];
    private readonly Dictionary<int, Matrix4x4RowMajor> _originalMatrices = [];
    private readonly HashSet<int> _addedInstances = [];
    private int _position;

    public PlantEditSession(FieldScene scene)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    public IReadOnlyList<PlantEditCommand> History => _history;

    /// <summary>Number of applied history entries; entries after it are redoable.</summary>
    public int HistoryPosition => _position;

    public bool CanUndo => _position > 0;

    public bool CanRedo => _position < _history.Count;

    public int EditCount => _history.Count;

    /// <summary>Instances whose current state differs from the session start.</summary>
    public int DirtyCount
    {
        get
        {
            int dirty = _addedInstances.Count;
            foreach ((int index, Matrix4x4RowMajor original) in _originalMatrices)
            {
                if (_addedInstances.Contains(index))
                {
                    continue;
                }

                if ((uint)index < (uint)_scene.Instances.Count
                    && _scene.Instances[index].Matrix != original)
                {
                    dirty++;
                }
            }

            return dirty;
        }
    }

    public Matrix4x4RowMajor GetMatrix(int index)
    {
        return Instance(index).Matrix;
    }

    /// <summary>Applies a live matrix without recording history.</summary>
    public void SetMatrix(int index, Matrix4x4RowMajor matrix)
    {
        if (!IsFinite(matrix))
        {
            throw new ArgumentException("Instance matrix contains a non-finite value.", nameof(matrix));
        }

        PlantInstance instance = Instance(index);
        if (!_addedInstances.Contains(index))
        {
            _originalMatrices.TryAdd(index, instance.Matrix);
        }

        instance.Matrix = matrix;
    }

    /// <summary>Applies and records a change; clears the redo tail.</summary>
    public void Commit(int index, Matrix4x4RowMajor before, Matrix4x4RowMajor after)
    {
        if (!IsFinite(before) || !IsFinite(after))
        {
            throw new ArgumentException("Instance matrix contains a non-finite value.");
        }

        Instance(index).Matrix = after;
        if (!_addedInstances.Contains(index))
        {
            _originalMatrices.TryAdd(index, before);
        }

        if (before == after)
        {
            return;
        }

        AppendHistory(new PlantTransformCommand(index, before, after));
    }

    /// <summary>Appends a matrix to a scene source file and records one add.</summary>
    public int AddInstance(PlantInstanceFile file, Matrix4x4RowMajor matrix)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (!IsFinite(matrix))
        {
            throw new ArgumentException("Instance matrix contains a non-finite value.", nameof(matrix));
        }

        if (!_scene.InstanceFiles.Contains(file))
        {
            throw new ArgumentException(
                "The TRINS source file does not belong to this scene.",
                nameof(file));
        }

        if ((uint)file.ModelIndex >= (uint)_scene.Plants.Count)
        {
            throw new ArgumentException(
                $"TRINS source model index {file.ModelIndex} is outside the scene plant range.",
                nameof(file));
        }

        if (file.OriginalCount < 0 || file.AddedCount < 0)
        {
            throw new ArgumentException(
                "The TRINS source file has a negative instance count.",
                nameof(file));
        }

        long sourceIndex = (long)file.OriginalCount + file.AddedCount;
        if (sourceIndex >= TrinsFile.MaxInstances)
        {
            throw new InvalidDataException(
                $"TRINS source '{file.LogicalPath}' has reached the {TrinsFile.MaxInstances} instance limit.");
        }

        int sceneIndex = _scene.Instances.Count;
        var instance = new PlantInstance(file.ModelIndex, matrix)
        {
            SourceFile = file,
            SourceIndex = (int)sourceIndex,
        };

        _scene.Instances.Add(instance);
        file.AddedCount++;
        _addedInstances.Add(sceneIndex);
        _scene.NotifyInstancesChanged();
        AppendHistory(new PlantAddCommand(sceneIndex, instance));
        return sceneIndex;
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        PlantEditCommand command = _history[_position - 1];
        switch (command)
        {
            case PlantTransformCommand transform:
                Instance(transform.InstanceIndex).Matrix = transform.Before;
                break;
            case PlantAddCommand add:
                RemoveAdded(add);
                break;
        }

        _position--;
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        PlantEditCommand command = _history[_position];
        switch (command)
        {
            case PlantTransformCommand transform:
                Instance(transform.InstanceIndex).Matrix = transform.After;
                break;
            case PlantAddCommand add:
                ReinsertAdded(add);
                break;
        }

        _position++;
        return true;
    }

    private void RemoveAdded(PlantAddCommand command)
    {
        int index = command.InstanceIndex;
        if ((uint)index >= (uint)_scene.Instances.Count
            || !ReferenceEquals(_scene.Instances[index], command.Instance))
        {
            throw new InvalidOperationException(
                $"Cannot undo add for instance {index}; the scene no longer matches the append-only history.");
        }

        PlantInstanceFile? source = command.Instance.SourceFile;
        if (source is not null)
        {
            if (source.AddedCount <= 0)
            {
                throw new InvalidOperationException(
                    $"TRINS source '{source.LogicalPath}' has no appended instance to remove.");
            }

            source.AddedCount--;
        }

        _scene.Instances.RemoveAt(index);
        _addedInstances.Remove(index);
        _scene.NotifyInstancesChanged();
    }

    private void ReinsertAdded(PlantAddCommand command)
    {
        int index = command.InstanceIndex;
        if (index != _scene.Instances.Count)
        {
            throw new InvalidOperationException(
                $"Cannot redo add for instance {index}; the scene has {_scene.Instances.Count} instances.");
        }

        PlantInstanceFile? source = command.Instance.SourceFile;
        if (source is not null)
        {
            if (source.NextIndex != command.Instance.SourceIndex)
            {
                throw new InvalidOperationException(
                    $"TRINS source '{source.LogicalPath}' next index {source.NextIndex} "
                    + $"does not match the redo source index {command.Instance.SourceIndex}.");
            }

            source.AddedCount++;
        }

        _scene.Instances.Insert(index, command.Instance);
        _addedInstances.Add(index);
        _scene.NotifyInstancesChanged();
    }

    private void AppendHistory(PlantEditCommand command)
    {
        if (_position < _history.Count)
        {
            _history.RemoveRange(_position, _history.Count - _position);
        }

        _history.Add(command);
        if (_history.Count > MaxHistoryEntries)
        {
            _history.RemoveAt(0);
        }

        _position = _history.Count;
    }

    private PlantInstance Instance(int index)
    {
        if ((uint)index >= (uint)_scene.Instances.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"Plant instance index is outside 0..{_scene.Instances.Count - 1}.");
        }

        return _scene.Instances[index];
    }

    private static bool IsFinite(Matrix4x4RowMajor matrix) =>
        IsFinite(matrix.Row0)
        && IsFinite(matrix.Row1)
        && IsFinite(matrix.Row2)
        && IsFinite(matrix.Row3);

    private static bool IsFinite(Vector4f row) =>
        float.IsFinite(row.X)
        && float.IsFinite(row.Y)
        && float.IsFinite(row.Z)
        && float.IsFinite(row.W);
}
