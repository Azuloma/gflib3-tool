// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using System.Numerics;
using ImGuiNET;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

public sealed partial class EditorShell
{
    private void DrawInspector()
    {
        if (!ImGui.Begin(EditorLayout.InspectorWindow))
        {
            ImGui.End();
            return;
        }

        if (!IsSelectionValid())
        {
            ImGui.TextDisabled(_text.T("inspector.empty"));
            ImGui.End();
            return;
        }

        DrawInspectorHeader();
        switch (_selection.Kind)
        {
            case EditorSelectionKind.RoomMesh:
                DrawRoomInspector(_scene.RoomMeshes[_selection.Index]);
                break;
            case EditorSelectionKind.PlacedMesh:
                DrawPlacedMeshInspector(_scene.PlacedMeshes[_selection.Index]);
                break;
            case EditorSelectionKind.PlacedGroup:
                DrawPlacedGroupInspector(_placedGroups[_selection.Index]);
                break;
            case EditorSelectionKind.PlantModel:
                DrawPlantModelInspector(_selection.Index);
                break;
            case EditorSelectionKind.PlantInstance:
                DrawPlantInstanceInspector(_selection.Index);
                break;
            case EditorSelectionKind.Terrain:
                DrawTerrainInspector();
                break;
        }

        ImGui.End();
    }

    private bool IsSelectionValid() => _selection.Kind switch
    {
        EditorSelectionKind.RoomMesh => (uint)_selection.Index < (uint)_scene.RoomMeshes.Count,
        EditorSelectionKind.PlacedMesh => (uint)_selection.Index < (uint)_scene.PlacedMeshes.Count,
        EditorSelectionKind.PlacedGroup => (uint)_selection.Index < (uint)_placedGroups.Count,
        EditorSelectionKind.PlantModel => (uint)_selection.Index < (uint)_scene.Plants.Count,
        EditorSelectionKind.PlantInstance => (uint)_selection.Index < (uint)_scene.Instances.Count,
        EditorSelectionKind.Terrain => _scene.Terrain is not null,
        _ => false,
    };

    private void DrawInspectorHeader()
    {
        ImGui.TextWrapped(SelectionName());
        ImGui.TextDisabled(SelectionKindName());
        if (ImGui.Button(_text.T("inspector.frame"), new Vector2(-1f, 0f)))
        {
            FrameRequested = true;
        }

        ImGui.Separator();
    }

    private string SelectionName() => _selection.Kind switch
    {
        EditorSelectionKind.RoomMesh => _scene.RoomMeshes[_selection.Index].Name,
        EditorSelectionKind.PlacedMesh => _scene.PlacedMeshes[_selection.Index].Name,
        EditorSelectionKind.PlacedGroup => _placedGroups[_selection.Index].Name,
        EditorSelectionKind.PlantModel => _scene.Plants[_selection.Index].Name,
        EditorSelectionKind.PlantInstance => PlantInstanceLabel(_selection.Index),
        EditorSelectionKind.Terrain => _text.T("hierarchy.terrain"),
        _ => _text.T("inspector.empty"),
    };

    private string SelectionKindName() => _selection.Kind switch
    {
        EditorSelectionKind.RoomMesh => _text.T("inspector.kind.roomMesh"),
        EditorSelectionKind.PlacedMesh => _text.T("inspector.kind.sceneObject"),
        EditorSelectionKind.PlacedGroup => _text.T("inspector.kind.sceneGroup"),
        EditorSelectionKind.PlantModel => _text.T("inspector.kind.plantModel"),
        EditorSelectionKind.PlantInstance => _text.T("inspector.kind.plantInstance"),
        EditorSelectionKind.Terrain => _text.T("inspector.kind.terrain"),
        _ => string.Empty,
    };

    private string PlantInstanceLabel(int index)
    {
        PlantInstance instance = _scene.Instances[index];
        string model = (uint)instance.ModelIndex < (uint)_scene.Plants.Count
            ? _scene.Plants[instance.ModelIndex].Name
            : _text.T("common.none");
        return $"{model} #{index}";
    }

    private void DrawRoomInspector(SceneMesh mesh)
    {
        DrawTransformComponent(new SceneTransform(Vector3.Zero, 0f, Vector3.One));
        DrawGeometryComponent([mesh], mesh.Geometry.Bounds);
        DrawMaterialComponent(mesh.MaterialIndex);
    }

    private void DrawPlacedMeshInspector(SceneMesh mesh)
    {
        Matrix4x4 placement = mesh.Placement ?? Matrix4x4.Identity;
        DrawTransformComponent(SceneGeometry.Decompose(placement));
        DrawGeometryComponent([mesh], SceneGeometry.OfPlacedMesh(mesh));
        DrawMaterialComponent(mesh.MaterialIndex);
    }

    private void DrawPlacedGroupInspector(PlacedMeshGroup group)
    {
        if (EditorIcons.CollapsingHeader(EditorIcon.ComponentInstances, _text.T("inspector.component.instances"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawKeyValue(_text.T("project.meshes"), group.Indices.Count.ToString(CultureInfo.InvariantCulture));
            DrawBounds(_text.T("inspector.boundsWorld"), PlacedGroupBounds(group));
        }

        List<SceneMesh> meshes = group.Indices.Select(index => _scene.PlacedMeshes[index]).ToList();
        BoundingBox worldBounds = PlacedGroupBounds(group);
        DrawGeometryComponent(meshes, worldBounds);

        if (EditorIcons.CollapsingHeader(EditorIcon.ComponentMaterial, _text.T("inspector.component.material"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            List<string> materials = meshes
                .Select(mesh => MaterialName(mesh.MaterialIndex))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            for (int i = 0; i < Math.Min(materials.Count, 8); i++)
            {
                ImGui.BulletText(materials[i]);
            }

            if (materials.Count > 8)
            {
                ImGui.TextDisabled($"+{materials.Count - 8}");
            }
        }
    }

    private void DrawPlantModelInspector(int modelIndex)
    {
        PlantModel model = _scene.Plants[modelIndex];
        List<int> instances = _plantInstancesByModel[modelIndex];
        if (EditorIcons.CollapsingHeader(EditorIcon.ComponentInstances, _text.T("inspector.component.instances"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawKeyValue(_text.T("project.instances"), instances.Count.ToString("N0", CultureInfo.InvariantCulture));
            DrawKeyValue(_text.T("project.meshes"), model.Meshes.Count.ToString(CultureInfo.InvariantCulture));
        }

        DrawGeometryComponent(model.Meshes, null);
        DrawMaterialComponent(model.Meshes.Count > 0 ? model.Meshes[0].MaterialIndex : -1);
    }

    private void DrawPlantInstanceInspector(int index)
    {
        PlantInstance instance = _scene.Instances[index];
        if ((uint)instance.ModelIndex >= (uint)_scene.Plants.Count)
        {
            ImGui.TextDisabled(_text.T("common.none"));
            return;
        }

        PlantModel model = _scene.Plants[instance.ModelIndex];
        DrawTransformComponent(SceneGeometry.Decompose(instance.Matrix));
        DrawGeometryComponent(model.Meshes, SceneGeometry.OfInstance(model, instance.Matrix));
        DrawMaterialComponent(model.Meshes.Count > 0 ? model.Meshes[0].MaterialIndex : -1);

        if (EditorIcons.CollapsingHeader(EditorIcon.ComponentInstances, _text.T("inspector.component.instances")))
        {
            DrawKeyValue(_text.T("inspector.instanceIndex"), index.ToString(CultureInfo.InvariantCulture));
            DrawKeyValue(_text.T("inspector.model"), model.Name);
        }
    }

    private void DrawTerrainInspector()
    {
        if (_scene.TerrainSurface is not TerrainSurface surface)
        {
            ImGui.TextDisabled(_text.T("common.none"));
            return;
        }

        if (EditorIcons.CollapsingHeader(EditorIcon.ComponentTerrain, _text.T("inspector.component.terrain"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawKeyValue(
                _text.T("inspector.heightmap"),
                $"{surface.Heightmap.Width} x {surface.Heightmap.Height}");
            DrawKeyValue(
                _text.T("inspector.sampleSpacing"),
                $"{surface.WorldUnitsPerSampleX:0.###}, {surface.WorldUnitsPerSampleZ:0.###}");
            DrawKeyValue(
                _text.T("inspector.worldExtent"),
                $"{surface.WorldExtentX:0.##}, {surface.WorldExtentZ:0.##}");
            DrawKeyValue(_text.T("inspector.heightScale"), surface.HeightScale.ToString("0.###", CultureInfo.InvariantCulture));
            DrawKeyValue(_text.T("inspector.splatLayers"), _scene.TerrainLayers.Count.ToString(CultureInfo.InvariantCulture));
            DrawKeyValue(_text.T("inspector.terrainUv"), _scene.TerrainTileScale.ToString("0.##", CultureInfo.InvariantCulture));
        }

        DrawBounds(_text.T("inspector.boundsWorld"), _scene.Terrain!.Bounds);
    }

    private void DrawTransformComponent(SceneTransform transform)
    {
        if (!EditorIcons.CollapsingHeader(EditorIcon.ComponentTransform, _text.T("inspector.component.transform"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.BeginDisabled();
        Vector3 position = transform.Position;
        float yaw = transform.YawDegrees;
        Vector3 scale = transform.Scale;
        ImGui.DragFloat3(_text.T("inspector.position"), ref position);
        ImGui.DragFloat(_text.T("inspector.yaw"), ref yaw, 0.1f, -180f, 180f, "%.2f");
        ImGui.DragFloat3(_text.T("inspector.scale"), ref scale);
        ImGui.EndDisabled();
    }

    private void DrawGeometryComponent(List<SceneMesh> meshes, BoundingBox? worldBounds)
    {
        if (!EditorIcons.CollapsingHeader(EditorIcon.ComponentGeometry, _text.T("inspector.component.geometry"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        int vertices = meshes.Sum(mesh => mesh.Geometry.Vertices.Length);
        int indices = meshes.Sum(mesh => mesh.Geometry.Indices.Length);
        int triangles = meshes.Sum(mesh => mesh.PartIndexCount / 3);
        DrawKeyValue(_text.T("project.meshes"), meshes.Count.ToString(CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("inspector.vertices"), vertices.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("inspector.indices"), indices.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("inspector.triangles"), triangles.ToString("N0", CultureInfo.InvariantCulture));
        DrawBounds(_text.T("inspector.boundsLocal"), SceneGeometry.OfModel(meshes));
        if (worldBounds is BoundingBox world && meshes.Count > 0)
        {
            DrawBounds(_text.T("inspector.boundsWorld"), world);
        }
    }

    private void DrawMaterialComponent(int materialIndex)
    {
        if (!EditorIcons.CollapsingHeader(EditorIcon.ComponentMaterial, _text.T("inspector.component.material"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        if ((uint)materialIndex >= (uint)_scene.Materials.Count)
        {
            ImGui.TextDisabled(_text.T("common.none"));
            return;
        }

        SceneMaterial material = _scene.Materials[materialIndex];
        DrawKeyValue(_text.T("inspector.name"), material.Name);
        DrawKeyValue(_text.T("inspector.baseColor"), TextureName(material.BaseColorTexture));
        DrawKeyValue(_text.T("inspector.normalMap"), TextureName(material.NormalTexture));
        DrawKeyValue(_text.T("inspector.aoMap"), TextureName(material.AmbientOcclusionTexture));
        DrawKeyValue(_text.T("inspector.vertexColor"), material.UseVertexColor ? _text.T("common.yes") : _text.T("common.no"));
        DrawKeyValue(
            _text.T("inspector.tint"),
            $"{material.Tint.X:0.##}, {material.Tint.Y:0.##}, {material.Tint.Z:0.##}");
    }

    private string MaterialName(int materialIndex) =>
        (uint)materialIndex < (uint)_scene.Materials.Count
            ? _scene.Materials[materialIndex].Name
            : _text.T("common.none");

    private string TextureName(int textureIndex) =>
        (uint)textureIndex < (uint)_scene.Textures.Count
            ? _scene.Textures[textureIndex].Name
            : _text.T("common.none");

    private static void DrawBounds(string label, BoundingBox bounds)
    {
        if (!float.IsFinite(bounds.Min.X) || !float.IsFinite(bounds.Max.X))
        {
            return;
        }

        ImGui.TextDisabled(label);
        ImGui.TextWrapped($"min {Format(bounds.Min)}\nmax {Format(bounds.Max)}");
    }
}
