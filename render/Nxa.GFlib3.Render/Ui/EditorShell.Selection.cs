// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

public sealed partial class EditorShell
{
    private sealed class PlacedMeshGroup
    {
        public required string Name { get; init; }

        public List<int> Indices { get; } = [];
    }

    private void BuildSceneIndexes()
    {
        _placedGroups = [];
        var byName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < _scene.PlacedMeshes.Count; i++)
        {
            SceneMesh mesh = _scene.PlacedMeshes[i];
            if (!byName.TryGetValue(mesh.Name, out int groupIndex))
            {
                groupIndex = _placedGroups.Count;
                byName.Add(mesh.Name, groupIndex);
                _placedGroups.Add(new PlacedMeshGroup { Name = mesh.Name });
            }

            _placedGroups[groupIndex].Indices.Add(i);
        }

        _plantInstancesByModel = new List<int>[_scene.Plants.Count];
        for (int i = 0; i < _plantInstancesByModel.Length; i++)
        {
            _plantInstancesByModel[i] = [];
        }

        for (int i = 0; i < _scene.Instances.Count; i++)
        {
            int modelIndex = _scene.Instances[i].ModelIndex;
            if ((uint)modelIndex < (uint)_plantInstancesByModel.Length)
            {
                _plantInstancesByModel[modelIndex].Add(i);
            }
        }
    }

    private void Select(EditorSelection selection) => _selection = selection;

    private bool IsSelected(EditorSelectionKind kind, int index) =>
        _selection.Kind == kind && _selection.Index == index;

    public bool TryGetSelectionBounds(out BoundingBox bounds)
    {
        switch (_selection.Kind)
        {
            case EditorSelectionKind.RoomMesh
                when (uint)_selection.Index < (uint)_scene.RoomMeshes.Count:
                bounds = _scene.RoomMeshes[_selection.Index].Geometry.Bounds;
                return true;
            case EditorSelectionKind.PlacedMesh
                when (uint)_selection.Index < (uint)_scene.PlacedMeshes.Count:
                bounds = SceneGeometry.OfPlacedMesh(_scene.PlacedMeshes[_selection.Index]);
                return true;
            case EditorSelectionKind.PlacedGroup
                when (uint)_selection.Index < (uint)_placedGroups.Count:
                bounds = PlacedGroupBounds(_placedGroups[_selection.Index]);
                return true;
            case EditorSelectionKind.PlantModel
                when (uint)_selection.Index < (uint)_scene.Plants.Count:
                bounds = PlantModelBounds(_selection.Index);
                return true;
            case EditorSelectionKind.PlantInstance
                when (uint)_selection.Index < (uint)_scene.Instances.Count:
                bounds = PlantInstanceBounds(_scene.Instances[_selection.Index]);
                return true;
            case EditorSelectionKind.Terrain when _scene.Terrain is not null:
                bounds = _scene.Terrain.Bounds;
                return true;
            default:
                bounds = default;
                return false;
        }
    }

    private BoundingBox PlacedGroupBounds(PlacedMeshGroup group) => SceneGeometry.Union(
        group.Indices.Select(index => SceneGeometry.OfPlacedMesh(_scene.PlacedMeshes[index])));

    private BoundingBox PlantModelBounds(int modelIndex)
    {
        PlantModel model = _scene.Plants[modelIndex];
        return SceneGeometry.Union(
            _plantInstancesByModel[modelIndex].Select(index =>
                SceneGeometry.OfInstance(model, _scene.Instances[index].Matrix)));
    }

    private BoundingBox PlantInstanceBounds(PlantInstance instance) =>
        (uint)instance.ModelIndex < (uint)_scene.Plants.Count
            ? SceneGeometry.OfInstance(_scene.Plants[instance.ModelIndex], instance.Matrix)
            : default;
}
