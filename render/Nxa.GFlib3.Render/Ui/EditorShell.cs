// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using System.Numerics;
using ImGuiNET;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Camera;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Localization;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

/// <summary>Unity-style dockable editor shell. Stage 4 is read-only.</summary>
public sealed partial class EditorShell
{
    private readonly Localizer _text;
    private readonly SceneCatalog _catalog;

    private FieldScene _scene;
    private PlantEditSession _editSession;
    private bool _showAbout;
    private bool _showControls;
    private bool _layoutPending;
    private uint _dockspaceId;
    private EditorSelection _selection = EditorSelection.None;
    private string _hierarchyFilter = string.Empty;
    private List<PlacedMeshGroup> _placedGroups = [];
    private List<int>[] _plantInstancesByModel = [];
    private int _indexedInstanceRevision = -1;
    private int _areaIndex;
    private int _modelIndex;
    private SceneSelection? _pendingLoad;
    private readonly Dictionary<string, AssetHeaderInfo> _assetHeaders = new(StringComparer.OrdinalIgnoreCase);
    private string _assetFilter = string.Empty;
    private int _assetKindFilter;
    private string _consoleFilter = string.Empty;
    private bool _consoleShowInfo = true;
    private bool _consoleShowWarnings = true;
    private bool _consoleShowErrors = true;
    private bool _consoleAutoScroll = true;
    private int _consoleClearedAt;
    private int _consoleLastCount;

    public EditorShell(Localizer text, FieldScene scene, SceneCatalog catalog, bool buildDefaultLayout)
    {
        _text = text;
        _scene = scene;
        _catalog = catalog;
        _editSession = new PlantEditSession(scene);
        _layoutPending = buildDefaultLayout;
        BuildSceneIndexes();
        SyncPackSelection();
    }

    public bool ExitRequested { get; private set; }

    public bool ResetCameraRequested { get; private set; }

    public bool FrameRequested { get; private set; }

    public SceneSelection? TakePendingLoad()
    {
        SceneSelection? request = _pendingLoad;
        _pendingLoad = null;
        return request;
    }

    public bool TakeResetCameraRequest()
    {
        bool requested = ResetCameraRequested;
        ResetCameraRequested = false;
        return requested;
    }

    public bool TakeFrameRequest()
    {
        bool requested = FrameRequested;
        FrameRequested = false;
        return requested;
    }

    public void RequestFrameSelection() => FrameRequested = true;

    public void RequestLoad(string area, string modelPack)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPack);
        _pendingLoad = new SceneSelection(area, modelPack);
    }

    public void SetScene(FieldScene scene)
    {
        _scene = scene;
        _editSession = new PlantEditSession(scene);
        _drag = null;
        _hoveredAxis = GizmoAxis.None;
        _selection = EditorSelection.None;
        ResetPlacementState();
        _consoleClearedAt = 0;
        _consoleLastCount = 0;
        BuildSceneIndexes();
        SyncPackSelection();
    }

    public void Draw(UiFrameContext frame)
    {
        UpdateEditorShortcuts();
        EnsureSceneIndexes();

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
            if (ImGui.MenuItem(_text.T("menu.edit.undo"), "Ctrl+Z", false, _editSession.CanUndo))
            {
                Undo();
            }

            if (ImGui.MenuItem(_text.T("menu.edit.redo"), "Ctrl+Y", false, _editSession.CanRedo))
            {
                Redo();
            }

            ImGui.Separator();
            if (ImGui.MenuItem(_text.T("menu.edit.duplicate"), "Ctrl+D", false, CanDuplicate))
            {
                DuplicateSelected();
            }

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
            if (ImGui.MenuItem(_text.T("menu.help.controls")))
            {
                _showControls = true;
                ImGui.OpenPopup("controls");
            }

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
        ImGui.SetCursorScreenPos(new Vector2(start.X + (6f * EditorTheme.UiScale), buttonY));
        if (EditorIcons.ToolButton(
                EditorIcon.Select,
                _text.T("toolbar.select"),
                active: _tool == GizmoMode.Select,
                enabled: true))
        {
            SetTool(GizmoMode.Select);
        }

        ImGui.SameLine();
        if (EditorIcons.ToolButton(
                EditorIcon.Move,
                _text.T("toolbar.move"),
                active: _tool == GizmoMode.Move,
                enabled: true))
        {
            SetTool(GizmoMode.Move);
        }

        ImGui.SameLine();
        if (EditorIcons.ToolButton(
                EditorIcon.Rotate,
                _text.T("toolbar.rotate"),
                active: _tool == GizmoMode.Rotate,
                enabled: true))
        {
            SetTool(GizmoMode.Rotate);
        }

        ImGui.SameLine();
        if (EditorIcons.ToolButton(
                EditorIcon.Scale,
                _text.T("toolbar.scale"),
                active: _tool == GizmoMode.Scale,
                enabled: true))
        {
            SetTool(GizmoMode.Scale);
        }

        EditorIcons.ToolbarSeparator();
        if (EditorIcons.ToolButton(
                EditorIcon.Place,
                _text.T("toolbar.place"),
                active: _tool == GizmoMode.Place,
                enabled: CanPlace))
        {
            SetTool(GizmoMode.Place);
        }

        ImGui.SameLine();
        if (EditorIcons.ToolButton(
                EditorIcon.Duplicate,
                _text.T("toolbar.duplicate"),
                active: false,
                enabled: CanDuplicate))
        {
            DuplicateSelected();
        }

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

    private void DrawConsolePanels()
    {
        if (!ImGui.Begin(EditorLayout.ConsoleWindow, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##bottomTabs"))
        {
            if (EditorIcons.BeginTabItem(
                    EditorIcon.PanelConsole,
                    _text.T("panel.console"),
                    "consoleTab"))
            {
                DrawConsole();
                ImGui.EndTabItem();
            }

            if (EditorIcons.BeginTabItem(
                    EditorIcon.PanelValidation,
                    _text.T("panel.validation"),
                    "validationTab"))
            {
                DrawValidation();
                ImGui.EndTabItem();
            }

            if (EditorIcons.BeginTabItem(
                    EditorIcon.PanelHistory,
                    _text.T("panel.history"),
                    "historyTab"))
            {
                DrawHistory();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawConsole()
    {
        int messageCount = _scene.Messages.Count;
        bool newMessages = messageCount != _consoleLastCount;
        _consoleLastCount = messageCount;
        _consoleClearedAt = Math.Min(_consoleClearedAt, messageCount);

        bool copyRequested = DrawConsoleToolbar();
        var visible = new List<(string Message, ConsoleLevel Level)>();
        for (int i = _consoleClearedAt; i < messageCount; i++)
        {
            string message = _scene.Messages[i];
            ConsoleLevel level = ConsoleLog.Classify(message);
            if (ConsoleLog.Matches(
                    level,
                    message,
                    _consoleFilter,
                    _consoleShowInfo,
                    _consoleShowWarnings,
                    _consoleShowErrors))
            {
                visible.Add((message, level));
            }
        }

        if (copyRequested)
        {
            IEnumerable<string> lines = visible.Select(item => item.Message);
            ImGui.SetClipboardText(string.Join(Environment.NewLine, lines));
        }

        ImGui.BeginChild("##consoleLog", new Vector2(0f, 0f));
        if (visible.Count == 0)
        {
            ImGui.TextDisabled(_text.T(messageCount == 0 ? "console.empty" : "console.noMatch"));
        }
        else
        {
            foreach ((string message, ConsoleLevel level) in visible)
            {
                Vector4 color = level switch
                {
                    ConsoleLevel.Warning => EditorTheme.Warning,
                    ConsoleLevel.Error => EditorTheme.Error,
                    _ => EditorTheme.TextPrimary,
                };
                ImGui.TextColored(color, message);
            }

            if (_consoleAutoScroll && newMessages)
            {
                ImGui.SetScrollHereY(1f);
            }
        }

        ImGui.EndChild();
    }

    private bool DrawConsoleToolbar()
    {
        ImGui.SetNextItemWidth(170f * EditorTheme.UiScale);
        ImGui.InputTextWithHint(
            "##consoleFilter",
            _text.T("console.filter"),
            ref _consoleFilter,
            128);
        ImGui.SameLine();
        ImGui.Checkbox(_text.T("console.info"), ref _consoleShowInfo);
        ImGui.SameLine();
        ImGui.Checkbox(_text.T("console.warning"), ref _consoleShowWarnings);
        ImGui.SameLine();
        ImGui.Checkbox(_text.T("console.error"), ref _consoleShowErrors);
        ImGui.SameLine();

        if (ImGui.Button(_text.T("console.clear")))
        {
            _consoleClearedAt = _scene.Messages.Count;
        }

        ImGui.SameLine();
        bool copyRequested = ImGui.Button(_text.T("console.copy"));
        ImGui.SameLine();
        ImGui.Checkbox(_text.T("console.autoScroll"), ref _consoleAutoScroll);
        ImGui.Separator();
        return copyRequested;
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

        float x = position.X + (10f * EditorTheme.UiScale);
        float y = position.Y + (4f * EditorTheme.UiScale);
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

        string mode = _text.T(ToolNameKey);
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
        x += 8f * EditorTheme.UiScale;
        x = AddStatusText(draw, x, y, "|", EditorTheme.TextDisabled);
        return x + (8f * EditorTheme.UiScale);
    }

    private static float AddStatusPair(ImDrawListPtr draw, float x, float y, string label, string value)
    {
        x = AddStatusText(draw, x, y, label, EditorTheme.TextMuted);
        x += 4f * EditorTheme.UiScale;
        return AddStatusText(draw, x, y, value, EditorTheme.TextPrimary);
    }

    private void DrawScene(UiFrameContext frame)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        bool open = ImGui.Begin(EditorLayout.SceneWindow, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!open)
        {
            if (_drag is not null)
            {
                CancelDrag();
            }

            ClearPlacePreview();
            ImGui.End();
            ImGui.PopStyleVar();
            return;
        }

        Vector2 available = ImGui.GetContentRegionAvail();
        int width = Math.Max(1, (int)available.X);
        int height = Math.Max(1, (int)available.Y);
        uint texture = frame.RenderScene(width, height);
        ImGui.Image((nint)texture, available, new Vector2(0, 1), new Vector2(1, 0));

        bool hovered = ImGui.IsItemHovered();
        Vector2 imageMin = ImGui.GetItemRectMin();
        var imageSize = new Vector2(width, height);
        Vector2 localMouse = ImGui.GetIO().MousePos - imageMin;
        float farPlane = SceneGeometry.RecommendedFarPlane(_scene, frame.Camera.Position);
        Matrix4x4 viewProjection = frame.Camera.View
            * FreeFlyCamera.Projection((float)width / height, farPlane);

        if (Matrix4x4.Invert(viewProjection, out Matrix4x4 inverseViewProjection))
        {
            bool hasRay = ScenePicking.TryScreenRay(
                localMouse,
                imageSize,
                inverseViewProjection,
                out SceneRay ray);
            UpdatePlacePreview(hovered, hasRay, ray, farPlane);
            bool gizmoConsumed = UpdateGizmoInput(
                frame.Camera,
                viewProjection,
                inverseViewProjection,
                hovered,
                localMouse,
                imageSize);
            if (!gizmoConsumed
                && hovered
                && ImGui.IsMouseClicked(ImGuiMouseButton.Left)
                && !ImGui.IsMouseDown(ImGuiMouseButton.Right)
                && !ImGui.GetIO().WantTextInput
                && hasRay)
            {
                if (_tool == GizmoMode.Place)
                {
                    TryPlaceAt(ray, farPlane);
                }
                else
                {
                    Select(ScenePicking.TryPickPlant(_scene, ray, out int index)
                        ? new EditorSelection(EditorSelectionKind.PlantInstance, index)
                        : EditorSelection.None);
                }
            }

            DrawSceneOverlay(frame.Camera, viewProjection, imageMin, imageSize, hovered, localMouse);
        }
        else
        {
            ClearPlacePreview();
        }

        if (hovered && _drag is null)
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
        DrawControls();
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

    private void DrawControls()
    {
        if (_showControls)
        {
            ImGui.OpenPopup("controls");
            _showControls = false;
        }

        if (!ImGui.BeginPopupModal("controls", ImGuiWindowFlags.AlwaysAutoResize))
        {
            return;
        }

        ImGui.Text(_text.T("menu.help.controls"));
        ImGui.Separator();
        foreach (string line in _text.T("menu.help.controlsBody").Split('\n'))
        {
            ImGui.TextWrapped(line);
        }

        ImGui.Separator();
        if (ImGui.Button("OK"))
        {
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private int WarningCount() => _scene.Messages.Count(
        message => message.StartsWith("warning:", StringComparison.OrdinalIgnoreCase));

    private static void DrawKeyValue(string key, string value)
    {
        ImGui.TextDisabled(key);
        ImGui.SameLine(150 * EditorTheme.UiScale);
        ImGui.TextWrapped(value);
    }

    private static string Format(Vector3f value) => $"{value.X:F2}, {value.Y:F2}, {value.Z:F2}";
}

/// <summary>An area/model pack pair requested for loading.</summary>
public sealed record SceneSelection(string Area, string ModelPack);

/// <summary>Per-frame inputs and outputs shared between the app and the shell.</summary>
public sealed class UiFrameContext
{
    public required FreeFlyCamera Camera { get; init; }

    public required Func<int, int, uint> RenderScene { get; init; }

    public required int Triangles { get; init; }

    public required int Instances { get; init; }

    public required string Status { get; init; }
}



