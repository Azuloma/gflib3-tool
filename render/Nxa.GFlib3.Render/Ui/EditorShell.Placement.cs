// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Rendering;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

public sealed partial class EditorShell
{
    private int _placeModelIndex = -1;
    private PlantInstanceFile? _placeTargetFile;
    private Matrix4x4RowMajor? _placePreviewMatrix;

    /// <summary>Prospective vegetation draw submitted to the scene renderer.</summary>
    public PlantPreview? Preview { get; private set; }

    public bool CanPlace => HasPlantSelection() && IsPlaceTargetValid();

    public bool CanDuplicate =>
        _drag is null
        && TryGetSelectedInstance(out int index)
        && _scene.Instances[index].SourceFile is not null;

    public bool DuplicatePlantInstance(int index)
    {
        if ((uint)index >= (uint)_scene.Instances.Count)
        {
            _scene.Messages.Add(
                $"warning: cannot duplicate instance {index}; the scene has {_scene.Instances.Count} instances.");
            return false;
        }

        PlantInstance source = _scene.Instances[index];
        if (source.SourceFile is not PlantInstanceFile file)
        {
            _scene.Messages.Add($"warning: cannot duplicate instance {index}; it has no source .trins.");
            return false;
        }

        if (!PlantPlacement.IsFinite(source.Matrix))
        {
            _scene.Messages.Add(
                $"warning: cannot duplicate instance {index} from '{file.LogicalPath}'; "
                + "the source matrix contains a non-finite value.");
            return false;
        }

        Matrix4x4RowMajor matrix;
        try
        {
            matrix = PlantPlacement.Duplicate(source.Matrix, _scene.TerrainSurface);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            _scene.Messages.Add(
                $"warning: cannot duplicate instance {index} from '{file.LogicalPath}': {ex.Message}");
            return false;
        }

        return AddInstance(file, matrix);
    }

    private void DuplicateSelected()
    {
        if (_drag is null && TryGetSelectedInstance(out int index))
        {
            DuplicatePlantInstance(index);
        }
    }

    private bool AddInstance(PlantInstanceFile file, Matrix4x4RowMajor matrix)
    {
        try
        {
            int index = _editSession.AddInstance(file, matrix);
            EnsureSceneIndexes();
            Select(new EditorSelection(EditorSelectionKind.PlantInstance, index));
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            _scene.Messages.Add($"warning: placement into '{file.LogicalPath}' failed: {ex.Message}");
            return false;
        }
    }

    private bool TryPlaceAt(SceneRay ray, float maxDistance)
    {
        if (_scene.TerrainSurface is not TerrainSurface surface
            || !TryGetPlaceTarget(out _, out PlantInstanceFile file)
            || !ScenePicking.TryPickTerrain(surface, ray, maxDistance, out Vector3 point))
        {
            return false;
        }

        return AddInstance(file, PlantPlacement.AtTerrainPoint(point));
    }

    private void UpdatePlacePreview(bool hovered, bool hasRay, SceneRay ray, float maxDistance)
    {
        Preview = null;
        _placePreviewMatrix = null;
        if (_tool != GizmoMode.Place
            || !hovered
            || !hasRay
            || _scene.TerrainSurface is not TerrainSurface surface
            || !TryGetPlaceTarget(out int modelIndex, out _))
        {
            return;
        }

        if (!ScenePicking.TryPickTerrain(surface, ray, maxDistance, out Vector3 point))
        {
            return;
        }

        Matrix4x4RowMajor matrix = PlantPlacement.AtTerrainPoint(point);
        _placePreviewMatrix = matrix;
        Preview = new PlantPreview(modelIndex, matrix);
    }

    private void ClearPlacePreview()
    {
        Preview = null;
        _placePreviewMatrix = null;
    }

    private void ResetPlacementState()
    {
        _placeModelIndex = -1;
        _placeTargetFile = null;
        ClearPlacePreview();
    }

    private void SyncPlaceTargetFromSelection()
    {
        switch (_selection.Kind)
        {
            case EditorSelectionKind.PlantInstance
                when (uint)_selection.Index < (uint)_scene.Instances.Count:
            {
                PlantInstance instance = _scene.Instances[_selection.Index];
                if (instance.SourceFile is PlantInstanceFile source)
                {
                    _placeModelIndex = source.ModelIndex;
                    _placeTargetFile = source;
                }
                else
                {
                    SetPlacementModel(instance.ModelIndex);
                }

                return;
            }

            case EditorSelectionKind.PlantModel
                when (uint)_selection.Index < (uint)_scene.Plants.Count:
                SetPlacementModel(_selection.Index);
                return;

            default:
                if (!IsPlaceTargetValid())
                {
                    ResetPlacementState();
                }

                return;
        }
    }

    private void SetPlacementModel(int modelIndex)
    {
        if ((uint)modelIndex >= (uint)_scene.Plants.Count)
        {
            ResetPlacementState();
            return;
        }

        _placeModelIndex = modelIndex;
        _placeTargetFile = _scene.GetInstanceFiles(modelIndex).FirstOrDefault();
    }

    private void SetPlacementTarget(PlantInstanceFile file)
    {
        if (!_scene.InstanceFiles.Contains(file))
        {
            return;
        }

        _placeModelIndex = file.ModelIndex;
        _placeTargetFile = file;
    }

    private bool TryGetPlaceTarget(out int modelIndex, out PlantInstanceFile file)
    {
        modelIndex = _placeModelIndex;
        file = _placeTargetFile!;
        return HasPlantSelection()
            && (uint)modelIndex < (uint)_scene.Plants.Count
            && _placeTargetFile is not null
            && _placeTargetFile.ModelIndex == modelIndex
            && _scene.InstanceFiles.Contains(_placeTargetFile);
    }

    private bool IsPlaceTargetValid() => TryGetPlaceTarget(out _, out _);

    private bool HasPlantSelection() => _selection.Kind switch
    {
        EditorSelectionKind.PlantInstance =>
            (uint)_selection.Index < (uint)_scene.Instances.Count,
        EditorSelectionKind.PlantModel =>
            (uint)_selection.Index < (uint)_scene.Plants.Count,
        _ => false,
    };
}
