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
    private PlantCellCatalog? _plantCells;
    private int _placeModelIndex = -1;
    private PlantInstanceFile? _placeTargetFile;
    private PlantInstanceFile? _manualPlaceTarget;
    private bool _placeTargetPending;
    private bool _placeFollowCell = true;
    private TerrainCell? _placeCell;
    private Matrix4x4RowMajor? _placePreviewMatrix;

    /// <summary>Prospective vegetation draw submitted to the scene renderer.</summary>
    public PlantPreview? Preview { get; private set; }

    private TerrainCell? DisplayedTargetCell
    {
        get
        {
            if (_placeFollowCell)
            {
                return _placeCell;
            }

            return _placeTargetFile is not null
                && PlantCellCatalog.TryParseCell(_placeTargetFile.FileName, out TerrainCell cell)
                    ? cell
                    : null;
        }
    }

    public bool CanPlace
    {
        get
        {
            if (!TryGetPlaceModel(out int modelIndex)
                || _scene.TerrainSurface is null
                || _plantCells is null)
            {
                return false;
            }

            return _placeFollowCell
                ? _plantCells.CanCreateSynthetic(modelIndex)
                : IsPlaceTargetValid();
        }
    }

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
        if (file.IsSynthetic && _plantCells is not null && !_plantCells.IsRegistered(file))
        {
            if (!PlantCellCatalog.TryParseCell(file.FileName, out TerrainCell cell))
            {
                _scene.Messages.Add(
                    $"warning: synthetic .trins '{file.LogicalPath}' has no parsable cell suffix.");
                return false;
            }

            try
            {
                _plantCells.Register(file, cell);
            }
            catch (InvalidOperationException ex)
            {
                _scene.Messages.Add($"warning: {ex.Message}");
                return false;
            }
        }

        try
        {
            int index = _editSession.AddInstance(file, matrix);
            _project.MarkEdited();
            _placeTargetFile = file;
            _manualPlaceTarget = file;
            _placeTargetPending = false;
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
            || _plantCells is null
            || !ScenePicking.TryPickTerrain(surface, ray, maxDistance, out Vector3 point)
            || !_plantCells.Grid.TryGetCell(point.X, point.Z, out TerrainCell cell))
        {
            return false;
        }

        PlantInstanceFile? file;
        if (_placeFollowCell)
        {
            if (!TryGetPlaceModel(out int modelIndex))
            {
                return false;
            }

            file = _plantCells.GetOrCreatePending(modelIndex, cell);
        }
        else if (!TryGetPlaceTarget(out _, out PlantInstanceFile target))
        {
            return false;
        }
        else
        {
            file = target;
        }

        if (file is null)
        {
            _scene.Messages.Add(
                $"warning: no .trins target for cell ({cell.X}, {cell.Z}).");
            return false;
        }

        if (file.IsSynthetic && !_plantCells.IsRegistered(file))
        {
            try
            {
                _plantCells.Register(file, cell);
            }
            catch (InvalidOperationException ex)
            {
                _scene.Messages.Add($"warning: {ex.Message}");
                return false;
            }
        }

        _placeCell = _placeFollowCell ? cell : null;
        _placeTargetFile = file;
        _manualPlaceTarget = file;
        _placeTargetPending = false;
        return AddInstance(file, PlantPlacement.AtTerrainPoint(point));
    }

    private void UpdatePlacePreview(bool hovered, bool hasRay, SceneRay ray, float maxDistance)
    {
        ClearPlacePreview();
        if (_tool != GizmoMode.Place
            || !hovered
            || !hasRay
            || _scene.TerrainSurface is not TerrainSurface surface
            || _plantCells is null
            || !TryGetPlaceModel(out int modelIndex))
        {
            return;
        }

        if (!ScenePicking.TryPickTerrain(surface, ray, maxDistance, out Vector3 point)
            || !_plantCells.Grid.TryGetCell(point.X, point.Z, out TerrainCell cell))
        {
            return;
        }

        if (_placeFollowCell)
        {
            PlantInstanceFile? file = _plantCells.GetOrCreatePending(modelIndex, cell);
            if (file is null)
            {
                return;
            }

            _placeCell = cell;
            _placeTargetFile = file;
            _placeTargetPending = !_plantCells.IsRegistered(file);
        }
        else if (!TryGetPlaceTarget(out _, out _))
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
        _placeCell = null;
        _placeTargetPending = false;
        _placeTargetFile = _placeFollowCell ? null : _manualPlaceTarget;
    }

    private void ResetPlacementState()
    {
        _placeModelIndex = -1;
        _placeTargetFile = null;
        _manualPlaceTarget = null;
        _placeTargetPending = false;
        _placeFollowCell = true;
        _placeCell = null;
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
                    _manualPlaceTarget = source;
                    _placeTargetFile = _placeFollowCell ? null : source;
                    _placeTargetPending = false;
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
        _manualPlaceTarget = _scene.GetInstanceFiles(modelIndex).FirstOrDefault();
        _placeTargetFile = _placeFollowCell ? null : _manualPlaceTarget;
        _placeTargetPending = false;
        _placeCell = null;
    }

    private void SetPlacementTarget(PlantInstanceFile file)
    {
        if (!_scene.InstanceFiles.Contains(file))
        {
            return;
        }

        _placeFollowCell = false;
        _placeModelIndex = file.ModelIndex;
        _manualPlaceTarget = file;
        _placeTargetFile = file;
        _placeTargetPending = false;
        ClearPlacePreview();
    }

    private void SetPlacementFollowCell(bool follow)
    {
        _placeFollowCell = follow;
        _placeTargetPending = false;
        _placeCell = null;
        if (follow)
        {
            _placeTargetFile = null;
        }
        else
        {
            _placeTargetFile = _manualPlaceTarget;
            if ((_placeTargetFile is null || !_scene.InstanceFiles.Contains(_placeTargetFile))
                && (uint)_placeModelIndex < (uint)_scene.Plants.Count)
            {
                _placeTargetFile = _scene.GetInstanceFiles(_placeModelIndex).FirstOrDefault();
                _manualPlaceTarget = _placeTargetFile;
            }
        }

        ClearPlacePreview();
    }

    private bool TryGetPlaceModel(out int modelIndex)
    {
        modelIndex = _placeModelIndex;
        return HasPlantSelection() && (uint)modelIndex < (uint)_scene.Plants.Count;
    }

    private bool TryGetPlaceTarget(out int modelIndex, out PlantInstanceFile file)
    {
        modelIndex = _placeModelIndex;
        file = _placeTargetFile!;
        if (!TryGetPlaceModel(out modelIndex)
            || _placeTargetFile is null
            || _placeTargetFile.ModelIndex != modelIndex)
        {
            return false;
        }

        if (_scene.InstanceFiles.Contains(_placeTargetFile))
        {
            return true;
        }

        return _placeTargetFile.IsSynthetic && _placeFollowCell;
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

    private static PlantCellCatalog? CreatePlantCellCatalog(FieldScene scene) =>
        scene.TerrainSurface is TerrainSurface surface
            ? new PlantCellCatalog(scene, TerrainCellGrid.FromSurface(surface))
            : null;
}
