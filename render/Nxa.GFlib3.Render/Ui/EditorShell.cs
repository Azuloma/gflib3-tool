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
    private readonly Localizer _text;
    private readonly FieldScene _scene;

    private bool _showAbout;
    private bool _layoutPending;
    private uint _dockspaceId;
    private int _selectedRoomMesh = -1;
    private int _selectedPlant = -1;

    public EditorShell(Localizer text, FieldScene scene, bool buildDefaultLayout)
    {
        _text = text;
        _scene = scene;
        _layoutPending = buildDefaultLayout;
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
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoScrollWithMouse
            | ImGuiWindowFlags.MenuBar;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        bool open = ImGui.Begin("##GFlib3Host", hostFlags);
        ImGui.PopStyleVar(3);

        if (open)
        {
            DrawMenuBar();
            DrawToolbar();
            DrawDockspace();
        }

        ImGui.End();

        DrawHierarchy();
        DrawProject();
        DrawInspector();
        DrawConsolePanels();
        DrawScene(frame);
        DrawStatusBar(frame);
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

            if (ImGui.BeginMenu(_text.T("menu.view.layout")))
            {
                if (ImGui.MenuItem(_text.T("menu.view.resetLayout")))
                {
                    _layoutPending = true;
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
        Vector2 start = ImGui.GetCursorScreenPos();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        Vector2 end = new(start.X + ImGui.GetWindowWidth(), start.Y + EditorTheme.ToolbarHeight);
        draw.AddRectFilled(start, end, EditorIcons.ToColor(EditorTheme.ToolbarBackground));
        draw.AddLine(start, new Vector2(end.X, start.Y), EditorIcons.ToColor(EditorTheme.Border));
        draw.AddLine(
            new Vector2(start.X, end.Y - 1f),
            end,
            EditorIcons.ToColor(EditorTheme.Border));

        float buttonY = start.Y + MathF.Max(0f, (EditorTheme.ToolbarHeight - ImGui.GetFrameHeight()) * 0.5f);
        ImGui.SetCursorScreenPos(new Vector2(start.X + 6f, buttonY));
        EditorIcons.ToolButton(EditorIcon.Select, _text.T("toolbar.select"), active: true, enabled: false);
        ImGui.SameLine();
        EditorIcons.ToolButton(EditorIcon.Move, _text.T("toolbar.move"), active: false, enabled: false);
        ImGui.SameLine();
        EditorIcons.ToolButton(EditorIcon.Rotate, _text.T("toolbar.rotate"), active: false, enabled: false);
        ImGui.SameLine();
        EditorIcons.ToolButton(EditorIcon.Scale, _text.T("toolbar.scale"), active: false, enabled: false);
        EditorIcons.ToolbarSeparator();
        if (EditorIcons.ToolButton(EditorIcon.Frame, _text.T("toolbar.frame"), active: false, enabled: true))
        {
            FrameRequested = true;
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + EditorTheme.ToolbarHeight));
    }

    private void DrawDockspace()
    {
        _dockspaceId = EditorLayout.DockspaceId;
        Vector2 position = ImGui.GetCursorScreenPos();
        Vector2 size = ImGui.GetContentRegionAvail();
        size.Y = MathF.Max(1f, size.Y - EditorTheme.StatusBarHeight);

        if (_layoutPending)
        {
            EditorLayout.Build(_dockspaceId, position, size);
            _layoutPending = false;
        }

        ImGui.DockSpace(_dockspaceId, size, ImGuiDockNodeFlags.None);
    }

    private void DrawHierarchy()
    {
        if (!ImGui.Begin(EditorLayout.HierarchyWindow))
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
        if (!ImGui.Begin(EditorLayout.ProjectWindow))
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

        if (_scene.Terrain is not null)
        {
            ImGui.Separator();
            float tiling = _scene.TerrainTileScale;
            if (ImGui.SliderFloat(_text.T("project.terrainUv"), ref tiling, 0.25f, 256f, "x%.2f"))
            {
                _scene.TerrainTileScale = tiling;
            }
        }

        ImGui.End();
    }

    private void DrawInspector()
    {
        if (!ImGui.Begin(EditorLayout.InspectorWindow))
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
        if (!ImGui.Begin(EditorLayout.ConsoleWindow, ImGuiWindowFlags.NoCollapse))
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
                ? EditorTheme.Warning
                : message.StartsWith("error:", StringComparison.OrdinalIgnoreCase)
                    ? EditorTheme.Error
                    : EditorTheme.TextPrimary;
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

        ImGui.TextColored(EditorTheme.Warning, $"{_text.T("validation.warnings")}: {warnings.Count}");
        foreach (string warning in warnings)
        {
            ImGui.BulletText(warning);
        }
    }

    private void DrawStatusBar(UiFrameContext frame)
    {
        ImGuiViewportPtr viewport = ImGui.GetMainViewport();
        Vector2 position = new(
            viewport.WorkPos.X,
            viewport.WorkPos.Y + viewport.WorkSize.Y - EditorTheme.StatusBarHeight);
        var size = new Vector2(viewport.WorkSize.X, EditorTheme.StatusBarHeight);
        ImDrawListPtr draw = ImGui.GetForegroundDrawList(viewport);
        draw.AddRectFilled(position, position + size, EditorIcons.ToColor(EditorTheme.StatusBackground));
        draw.AddLine(position, new Vector2(position.X + size.X, position.Y), EditorIcons.ToColor(EditorTheme.Border));

        float x = position.X + 10f;
        float y = position.Y + 4f;
        x = AddStatusText(draw, x, y, frame.Status, EditorTheme.TextPrimary);
        x = AddStatusSeparator(draw, x, y);
        Vector3 camera = frame.Camera.Position;
        x = AddStatusPair(draw, x, y, _text.T("status.camera"), $"{camera.X:F1}, {camera.Y:F1}, {camera.Z:F1}");
        x = AddStatusSeparator(draw, x, y);
        x = AddStatusPair(draw, x, y, _text.T("status.fps"), ImGui.GetIO().Framerate.ToString("F1", CultureInfo.InvariantCulture));
        x = AddStatusSeparator(draw, x, y);
        x = AddStatusPair(draw, x, y, _text.T("status.triangles"), frame.Triangles.ToString("N0", CultureInfo.InvariantCulture));
        x = AddStatusSeparator(draw, x, y);
        AddStatusPair(draw, x, y, _text.T("status.instances"), frame.Instances.ToString("N0", CultureInfo.InvariantCulture));

        string mode = _text.T("toolbar.mode.viewer");
        float modeWidth = ImGui.CalcTextSize(mode).X;
        draw.AddText(
            new Vector2(position.X + size.X - modeWidth - 10f, y),
            EditorIcons.ToColor(EditorTheme.TextDisabled),
            mode);
    }

    private static float AddStatusText(ImDrawListPtr draw, float x, float y, string text, Vector4 color)
    {
        draw.AddText(new Vector2(x, y), EditorIcons.ToColor(color), text);
        return x + ImGui.CalcTextSize(text).X;
    }

    private static float AddStatusSeparator(ImDrawListPtr draw, float x, float y)
    {
        x += 8f;
        x = AddStatusText(draw, x, y, "|", EditorTheme.TextDisabled);
        return x + 8f;
    }

    private static float AddStatusPair(ImDrawListPtr draw, float x, float y, string label, string value)
    {
        x = AddStatusText(draw, x, y, label, EditorTheme.TextMuted);
        x += 4f;
        return AddStatusText(draw, x, y, value, EditorTheme.TextPrimary);
    }

    private void DrawScene(UiFrameContext frame)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        bool open = ImGui.Begin(EditorLayout.SceneWindow, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!open)
        {
            ImGui.End();
            ImGui.PopStyleVar();
            return;
        }

        Vector2 available = ImGui.GetContentRegionAvail();
        int width = Math.Max(1, (int)available.X);
        int height = Math.Max(1, (int)available.Y);
        uint texture = frame.RenderScene(width, height);
        ImGui.Image((nint)texture, available, new Vector2(0, 1), new Vector2(1, 0));

        Vector2 min = ImGui.GetItemRectMin();
        string hint = _text.T("control.hint");
        Vector2 hintSize = ImGui.CalcTextSize(hint);
        Vector2 hintMin = min + new Vector2(8f, 8f);
        Vector2 hintMax = hintMin + hintSize + new Vector2(12f, 8f);
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(hintMin, hintMax, 0xB0202020, 2f);
        draw.AddText(hintMin + new Vector2(6f, 4f), EditorIcons.ToColor(EditorTheme.TextMuted), hint);

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
        ImGui.PopStyleVar();
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



