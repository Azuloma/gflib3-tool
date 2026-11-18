// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using ImGuiNET;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

public sealed partial class EditorShell
{
    private void DrawHierarchy()
    {
        if (!ImGui.Begin(EditorLayout.HierarchyWindow))
        {
            ImGui.End();
            return;
        }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputTextWithHint(
            "##hierarchyFilter",
            _text.T("hierarchy.filter"),
            ref _hierarchyFilter,
            128);
        ImGui.Separator();

        string filter = _hierarchyFilter.Trim();
        bool any = DrawRoomHierarchy(filter);
        any |= DrawPlacedHierarchy(filter);
        any |= DrawPlantHierarchy(filter);
        any |= DrawTerrainHierarchy(filter);
        if (!any)
        {
            ImGui.TextDisabled(_text.T("hierarchy.empty"));
        }

        ImGui.End();
    }

    private bool DrawRoomHierarchy(string filter)
    {
        int count = _scene.RoomMeshes.Count(mesh => MatchesFilter(mesh.Name, filter));
        if (ImGui.TreeNodeEx(
                $"{_text.T("hierarchy.room")} ({count})",
                ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            for (int i = 0; i < _scene.RoomMeshes.Count; i++)
            {
                SceneMesh mesh = _scene.RoomMeshes[i];
                if (!MatchesFilter(mesh.Name, filter))
                {
                    continue;
                }

                if (ImGui.Selectable(mesh.Name, IsSelected(EditorSelectionKind.RoomMesh, i)))
                {
                    Select(new EditorSelection(EditorSelectionKind.RoomMesh, i));
                }
            }

            ImGui.TreePop();
        }

        return count > 0;
    }

    private bool DrawPlacedHierarchy(string filter)
    {
        int count = _placedGroups.Count(group => MatchesFilter(group.Name, filter));
        if (ImGui.TreeNodeEx(
                $"{_text.T("hierarchy.sceneObjects")} ({count})",
                ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            for (int groupIndex = 0; groupIndex < _placedGroups.Count; groupIndex++)
            {
                PlacedMeshGroup group = _placedGroups[groupIndex];
                if (!MatchesFilter(group.Name, filter))
                {
                    continue;
                }

                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (IsSelected(EditorSelectionKind.PlacedGroup, groupIndex))
                {
                    flags |= ImGuiTreeNodeFlags.Selected;
                }

                string name = group.Name.Length == 0 ? _text.T("common.unnamed") : group.Name;
                bool open = ImGui.TreeNodeEx($"{name} ({group.Indices.Count})", flags);
                if (ImGui.IsItemClicked())
                {
                    Select(new EditorSelection(EditorSelectionKind.PlacedGroup, groupIndex));
                }

                if (open)
                {
                    foreach (int index in group.Indices)
                    {
                        if (ImGui.Selectable(
                                $"#{index}  {PlacedPosition(index)}",
                                IsSelected(EditorSelectionKind.PlacedMesh, index)))
                        {
                            Select(new EditorSelection(EditorSelectionKind.PlacedMesh, index));
                        }
                    }

                    ImGui.TreePop();
                }
            }

            ImGui.TreePop();
        }

        return count > 0;
    }

    private bool DrawPlantHierarchy(string filter)
    {
        int count = _scene.Plants.Count(plant => MatchesFilter(plant.Name, filter));
        if (ImGui.TreeNodeEx(
                $"{_text.T("hierarchy.plants")} ({count})",
                ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
        {
            for (int modelIndex = 0; modelIndex < _scene.Plants.Count; modelIndex++)
            {
                PlantModel plant = _scene.Plants[modelIndex];
                if (!MatchesFilter(plant.Name, filter))
                {
                    continue;
                }

                ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                if (IsSelected(EditorSelectionKind.PlantModel, modelIndex))
                {
                    flags |= ImGuiTreeNodeFlags.Selected;
                }

                List<int> instances = _plantInstancesByModel[modelIndex];
                bool open = ImGui.TreeNodeEx($"{plant.Name} ({instances.Count})", flags);
                if (ImGui.IsItemClicked())
                {
                    Select(new EditorSelection(EditorSelectionKind.PlantModel, modelIndex));
                }

                if (open)
                {
                    foreach (int index in instances)
                    {
                        if (ImGui.Selectable(
                                $"#{index}  {PlantPosition(index)}",
                                IsSelected(EditorSelectionKind.PlantInstance, index)))
                        {
                            Select(new EditorSelection(EditorSelectionKind.PlantInstance, index));
                        }
                    }

                    ImGui.TreePop();
                }
            }

            ImGui.TreePop();
        }

        return count > 0;
    }

    private bool DrawTerrainHierarchy(string filter)
    {
        if (_scene.Terrain is null || !MatchesFilter(_text.T("hierarchy.terrain"), filter))
        {
            return false;
        }

        string label = _scene.TerrainSurface is TerrainSurface surface
            ? $"{_text.T("hierarchy.terrain")} ({surface.Heightmap.Width} x {surface.Heightmap.Height})"
            : _text.T("hierarchy.terrain");
        if (ImGui.Selectable(label, IsSelected(EditorSelectionKind.Terrain, 0)))
        {
            Select(new EditorSelection(EditorSelectionKind.Terrain, 0));
        }

        return true;
    }

    private string PlacedPosition(int index)
    {
        if (_scene.PlacedMeshes[index].Placement is not System.Numerics.Matrix4x4 placement)
        {
            return "0.0, 0.0, 0.0";
        }

        return $"{placement.M41:0.0}, {placement.M42:0.0}, {placement.M43:0.0}";
    }

    private string PlantPosition(int index)
    {
        Matrix4x4RowMajor matrix = _scene.Instances[index].Matrix;
        return $"{matrix.Row3.X:0.0}, {matrix.Row3.Y:0.0}, {matrix.Row3.Z:0.0}";
    }

    private static bool MatchesFilter(string value, string filter) =>
        filter.Length == 0 || value.Contains(filter, StringComparison.OrdinalIgnoreCase);
}
