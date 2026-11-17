// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using System.Numerics;
using ImGuiNET;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Camera;
using Nxa.GFlib3.Render.Localization;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

/// <summary>Unity-style dockable editor shell. Stage 4 is read-only.</summary>
public sealed class EditorShell
{
    private static readonly Vector4 PanelColor = new(0.91f, 0.91f, 0.91f, 1f);
    private static readonly Vector4 WarningColor = new(1f, 0.72f, 0.25f, 1f);
    private static readonly Vector4 ErrorColor = new(1f, 0.40f, 0.40f, 1f);

    private readonly Localizer _text;
    private readonly FieldScene _scene;

    private bool _showAbout;
    private int _selectedRoomMesh = -1;
    private int _selectedPlant = -1;

    public EditorShell(Localizer text, FieldScene scene)
    {
        _text = text;
        _scene = scene;
    }

    public bool ExitRequested { get; private set; }

    public bool ResetCameraRequested { get; private set; }

    public bool FrameRequested { get; private set; }

    public void Draw(UiFrameContext frame)
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos);
        ImGui.SetNextWindowSize(viewport.WorkSize);
        const ImGuiWindowFlags hostFlags = ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoMove
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoBringToFrontOnFocus
            | ImGuiWindowFlags.NoNavFocus
            | ImGuiWindowFlags.NoDocking
            | ImGuiWindowFlags.MenuBar;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        bool open = ImGui.Begin("##GFlib3Host", hostFlags);
        ImGui.PopStyleVar(2);

        if (open)
        {
            DrawMenuBar();
            DrawToolbar();
            ImGui.DockSpace(ImGui.GetID("GFlib3Dockspace"), new Vector2(0, 0), ImGuiDockNodeFlags.PassthruCentralNode);
        }

        ImGui.End();

        DrawHierarchy();
        DrawProject();
        DrawInspector();
        DrawConsolePanels();
        DrawStatus(frame);
        DrawScene(frame);
        DrawAbout();
    }

    private void DrawMenuBar()
    {
        if (!ImGui.BeginMenuBar())
        {
            return;
        }

        if (ImGui.BeginMenu(_text.T("menu.file")))
        {
            ImGui.BeginDisabled();
            ImGui.MenuItem(_text.T("menu.file.open"), "Ctrl+O");
            ImGui.EndDisabled();
            ImGui.Separator();
            if (ImGui.MenuItem(_text.T("menu.file.exit")))
            {
                ExitRequested = true;
            }

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu(_text.T("menu.edit")))
        {
            ImGui.BeginDisabled();
            ImGui.MenuItem(_text.T("menu.edit.undo"), "Ctrl+Z");
            ImGui.MenuItem(_text.T("menu.edit.redo"), "Ctrl+Y");
            ImGui.EndDisabled();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu(_text.T("menu.view")))
        {
            if (ImGui.MenuItem(_text.T("menu.view.resetCamera"), "F"))
            {
                ResetCameraRequested = true;
            }

            if (ImGui.BeginMenu(_text.T("menu.view.language")))
            {
                bool english = _text.Language == "en";
                if (ImGui.MenuItem(_text.T("lang.english"), null, english))
                {
                    _text.SetLanguage("en");
                }

                bool japanese = _text.Language == "ja";
                if (ImGui.MenuItem(_text.T("lang.japanese"), null, japanese))
                {
                    _text.SetLanguage("ja");
                }

                ImGui.EndMenu();
            }

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu(_text.T("menu.help")))
        {
            if (ImGui.MenuItem(_text.T("menu.help.about")))
            {
                _showAbout = true;
                ImGui.OpenPopup("about");
            }

            ImGui.EndMenu();
        }

        ImGui.EndMenuBar();
    }

    private void DrawToolbar()
    {
        ImGui.BeginChild("##toolbar", new Vector2(0, 34), ImGuiChildFlags.None, ImGuiWindowFlags.NoScrollbar);
        ImGui.BeginDisabled();
        ImGui.Button(_text.T("toolbar.select"));
        ImGui.SameLine();
        ImGui.Button(_text.T("toolbar.move"));
        ImGui.SameLine();
        ImGui.Button(_text.T("toolbar.rotate"));
        ImGui.SameLine();
        ImGui.Button(_text.T("toolbar.scale"));
        ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button(_text.T("toolbar.frame")))
        {
            FrameRequested = true;
        }

        ImGui.SameLine();
        ImGui.TextDisabled(_text.T("toolbar.mode.viewer"));
        ImGui.EndChild();
    }

    private void DrawHierarchy()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 64), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(320, 360), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(_text.T("panel.hierarchy")))
        {
            ImGui.End();
            return;
        }

        if (ImGui.TreeNodeEx(_text.T("hierarchy.room"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            for (int i = 0; i < _scene.RoomMeshes.Count; i++)
            {
                bool selected = _selectedRoomMesh == i;
                if (ImGui.Selectable(_scene.RoomMeshes[i].Name, selected))
                {
                    _selectedRoomMesh = i;
                    _selectedPlant = -1;
                }
            }

            ImGui.TreePop();
        }

        if (ImGui.TreeNodeEx(_text.T("hierarchy.plants"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            for (int i = 0; i < _scene.Plants.Count; i++)
            {
                PlantModel plant = _scene.Plants[i];
                int count = _scene.Instances.Count(instance => instance.ModelIndex == i);
                string label = $"{plant.Name}  ({count})";
                bool selected = _selectedPlant == i;
                if (ImGui.Selectable(label, selected))
                {
                    _selectedPlant = i;
                    _selectedRoomMesh = -1;
                }
            }

            ImGui.TreePop();
        }

        ImGui.End();
    }

    private void DrawProject()
    {
        ImGui.SetNextWindowPos(new Vector2(0, 430), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(320, 260), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(_text.T("panel.project")))
        {
            ImGui.End();
            return;
        }

        DrawKeyValue(_text.T("project.area"), _scene.AreaName);
        DrawKeyValue(_text.T("project.modelPack"), _scene.ModelPackName);
        DrawKeyValue(_text.T("project.model"), _scene.RoomModelName);
        DrawKeyValue(_text.T("project.dump"), _scene.DumpRoot);
        ImGui.Separator();
        DrawKeyValue(_text.T("project.meshes"), _scene.RoomMeshes.Count.ToString(CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.vertices"), _scene.VertexCount.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.indices"), _scene.IndexCount.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.instances"), _scene.Instances.Count.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.textures"), _scene.Textures.Count.ToString(CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.warnings"), WarningCount().ToString(CultureInfo.InvariantCulture));
        ImGui.End();
    }

    private void DrawInspector()
    {
        ImGui.SetNextWindowPos(new Vector2(1200, 64), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(360, 700), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(_text.T("panel.inspector")))
        {
            ImGui.End();
            return;
        }

        if (_selectedRoomMesh >= 0 && _selectedRoomMesh < _scene.RoomMeshes.Count)
        {
            DrawRoomInspector(_scene.RoomMeshes[_selectedRoomMesh]);
        }
        else if (_selectedPlant >= 0 && _selectedPlant < _scene.Plants.Count)
        {
            DrawPlantInspector(_selectedPlant);
        }
        else
        {
            ImGui.TextDisabled(_text.T("inspector.empty"));
        }

        ImGui.End();
    }

    private void DrawRoomInspector(SceneMesh mesh)
    {
        DrawKeyValue(_text.T("inspector.name"), mesh.Name);
        DrawKeyValue(_text.T("inspector.kind"), "Room mesh");
        string material = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < _scene.Materials.Count
            ? _scene.Materials[mesh.MaterialIndex].Name
            : _text.T("common.none");
        DrawKeyValue(_text.T("inspector.material"), material);
        DrawKeyValue(_text.T("inspector.vertices"), mesh.Geometry.Vertices.Length.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("inspector.indices"), mesh.Geometry.Indices.Length.ToString("N0", CultureInfo.InvariantCulture));
        DrawBounds(mesh.Geometry.Bounds);

        ImGui.Separator();
        ImGui.TextDisabled(_text.T("inspector.readOnly"));
        ImGui.BeginDisabled();
        var position = Vector3.Zero;
        var rotation = Vector3.Zero;
        var scale = Vector3.One;
        ImGui.DragFloat3("Position", ref position);
        ImGui.DragFloat3("Rotation", ref rotation);
        ImGui.DragFloat3("Scale", ref scale);
        ImGui.EndDisabled();
    }

    private void DrawPlantInspector(int index)
    {
        PlantModel plant = _scene.Plants[index];
        int instances = _scene.Instances.Count(instance => instance.ModelIndex == index);
        DrawKeyValue(_text.T("inspector.name"), plant.Name);
        DrawKeyValue(_text.T("inspector.kind"), _text.T("hierarchy.plants"));
        DrawKeyValue(_text.T("project.instances"), instances.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.meshes"), plant.Meshes.Count.ToString(CultureInfo.InvariantCulture));
        int vertices = plant.Meshes.Sum(mesh => mesh.Geometry.Vertices.Length);
        int indices = plant.Meshes.Sum(mesh => mesh.Geometry.Indices.Length);
        DrawKeyValue(_text.T("inspector.vertices"), vertices.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("inspector.indices"), indices.ToString("N0", CultureInfo.InvariantCulture));

        ImGui.Separator();
        ImGui.TextDisabled(_text.T("inspector.readOnly"));
    }

    private void DrawConsolePanels()
    {
        ImGui.SetNextWindowPos(new Vector2(330, 640), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(700, 230), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(_text.T("panel.console"), ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##bottomTabs"))
        {
            if (ImGui.BeginTabItem(_text.T("panel.console")))
            {
                DrawConsole();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(_text.T("panel.validation")))
            {
                DrawValidation();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(_text.T("panel.history")))
            {
                ImGui.TextDisabled(_text.T("history.empty"));
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawConsole()
    {
        if (_scene.Messages.Count == 0)
        {
            ImGui.TextDisabled(_text.T("console.empty"));
            return;
        }

        foreach (string message in _scene.Messages)
        {
            Vector4 color = message.StartsWith("warning:", StringComparison.OrdinalIgnoreCase)
                ? WarningColor
                : message.StartsWith("error:", StringComparison.OrdinalIgnoreCase)
                    ? ErrorColor
                    : PanelColor;
            ImGui.TextColored(color, message);
        }
    }

    private void DrawValidation()
    {
        var warnings = _scene.Messages
            .Where(message => message.StartsWith("warning:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (warnings.Count == 0)
        {
            ImGui.TextDisabled(_text.T("validation.ok"));
            return;
        }

        ImGui.TextColored(WarningColor, $"{_text.T("validation.warnings")}: {warnings.Count}");
        foreach (string warning in warnings)
        {
            ImGui.BulletText(warning);
        }
    }

    private void DrawStatus(UiFrameContext frame)
    {
        ImGui.SetNextWindowPos(new Vector2(0, 695), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(320, 180), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(_text.T("panel.status"), ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        Vector3 position = frame.Camera.Position;
        DrawKeyValue(_text.T("status.camera"), $"{position.X:F1}, {position.Y:F1}, {position.Z:F1}");
        DrawKeyValue(_text.T("status.fps"), ImGui.GetIO().Framerate.ToString("F1", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("status.mode"), _text.T("toolbar.mode.viewer"));
        DrawKeyValue("Triangles", frame.Triangles.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue("Instances", frame.Instances.ToString("N0", CultureInfo.InvariantCulture));
        ImGui.TextDisabled(frame.Status);
        ImGui.End();
    }

    private void DrawScene(UiFrameContext frame)
    {
        ImGui.SetNextWindowPos(new Vector2(330, 64), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(850, 560), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin(_text.T("panel.scene"), ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.End();
            return;
        }

        Vector2 available = ImGui.GetContentRegionAvail();
        int width = Math.Max(1, (int)available.X);
        int height = Math.Max(1, (int)available.Y);
        uint texture = frame.RenderScene(width, height);
        ImGui.Image((nint)texture, available, new Vector2(0, 1), new Vector2(1, 0));

        Vector2 min = ImGui.GetItemRectMin();
        ImGui.GetWindowDrawList().AddText(min + new Vector2(10, 8), 0xFFC8C8C8, _text.T("control.hint"));

        bool hovered = ImGui.IsItemHovered();
        if (hovered)
        {
            HandleCameraInput(frame.Camera);
            if (ImGui.IsKeyPressed(ImGuiKey.F))
            {
                FrameRequested = true;
            }
        }

        ImGui.End();
    }

    private static void HandleCameraInput(FreeFlyCamera camera)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        bool looking = ImGui.IsMouseDown(ImGuiMouseButton.Right);
        if (looking)
        {
            camera.Look(io.MouseDelta.X, io.MouseDelta.Y);
        }

        Vector3 direction = Vector3.Zero;
        if (ImGui.IsKeyDown(ImGuiKey.W))
        {
            direction.Z += 1;
        }

        if (ImGui.IsKeyDown(ImGuiKey.S))
        {
            direction.Z -= 1;
        }

        if (ImGui.IsKeyDown(ImGuiKey.A))
        {
            direction.X -= 1;
        }

        if (ImGui.IsKeyDown(ImGuiKey.D))
        {
            direction.X += 1;
        }

        if (ImGui.IsKeyDown(ImGuiKey.E))
        {
            direction.Y += 1;
        }

        if (ImGui.IsKeyDown(ImGuiKey.Q))
        {
            direction.Y -= 1;
        }

        bool sprint = ImGui.IsKeyDown(ImGuiKey.LeftShift);
        camera.Move(direction, io.DeltaTime, sprint);
    }

    private void DrawAbout()
    {
        if (_showAbout)
        {
            ImGui.OpenPopup("about");
            _showAbout = false;
        }

        if (ImGui.BeginPopupModal("about", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.Text(_text.T("app.title"));
            ImGui.TextWrapped(_text.T("menu.help.aboutBody"));
            ImGui.Separator();
            if (ImGui.Button("OK"))
            {
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private int WarningCount() => _scene.Messages.Count(
        message => message.StartsWith("warning:", StringComparison.OrdinalIgnoreCase));

    private static void DrawKeyValue(string key, string value)
    {
        ImGui.TextDisabled(key);
        ImGui.SameLine(150);
        ImGui.TextWrapped(value);
    }

    private void DrawBounds(BoundingBox bounds)
    {
        ImGui.TextDisabled(_text.T("inspector.bounds"));
        ImGui.TextWrapped($"min {Format(bounds.Min)}\nmax {Format(bounds.Max)}");
    }

    private static string Format(Vector3f value) => $"{value.X:F2}, {value.Y:F2}, {value.Z:F2}";
}

/// <summary>Per-frame inputs and outputs shared between the app and the shell.</summary>
public sealed class UiFrameContext
{
    public required FreeFlyCamera Camera { get; init; }

    public required Func<int, int, uint> RenderScene { get; init; }

    public required int Triangles { get; init; }

    public required int Instances { get; init; }

    public required string Status { get; init; }
}



