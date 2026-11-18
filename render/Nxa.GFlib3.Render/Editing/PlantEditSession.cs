// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Editing;

/// <summary>One committed instance transform change.</summary>
public readonly record struct PlantTransformChange(
    int InstanceIndex,
    Matrix4x4RowMajor Before,
    Matrix4x4RowMajor After);

/// <summary>
/// Mutates the scene's instance matrices and records linear undo/redo history.
/// Live drag updates use <see cref="SetMatrix"/>; a drag commits once on release.
/// </summary>
public sealed class PlantEditSession
{
    public const int MaxHistoryEntries = 512;

    private readonly FieldScene _scene;
    private readonly List<PlantTransformChange> _history = [];
    private readonly Dictionary<int, Matrix4x4RowMajor> _originalMatrices = [];
    private int _position;

    public PlantEditSession(FieldScene scene)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    public IReadOnlyList<PlantTransformChange> History => _history;

    /// <summary>Number of applied history entries; entries after it are redoable.</summary>
    public int HistoryPosition => _position;

    public bool CanUndo => _position > 0;

    public bool CanRedo => _position < _history.Count;

    public int EditCount => _history.Count;

    /// <summary>Instances whose current matrix differs from the session start.</summary>
    public int DirtyCount
    {
        get
        {
            int dirty = 0;
            foreach ((int index, Matrix4x4RowMajor original) in _originalMatrices)
            {
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
        _originalMatrices.TryAdd(index, instance.Matrix);
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
        _originalMatrices.TryAdd(index, before);
        if (before == after)
        {
            return;
        }

        if (_position < _history.Count)
        {
            _history.RemoveRange(_position, _history.Count - _position);
        }

        _history.Add(new PlantTransformChange(index, before, after));
        if (_history.Count > MaxHistoryEntries)
        {
            _history.RemoveAt(0);
        }

        _position = _history.Count;
    }

    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        PlantTransformChange change = _history[_position - 1];
        Instance(change.InstanceIndex).Matrix = change.Before;
        _position--;
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        PlantTransformChange change = _history[_position];
        Instance(change.InstanceIndex).Matrix = change.After;
        _position++;
        return true;
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
