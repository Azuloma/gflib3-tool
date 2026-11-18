// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Camera;
using Nxa.GFlib3.Render.Localization;
using Nxa.GFlib3.Render.Rendering;
using Nxa.GFlib3.Render.Scene;
using Nxa.GFlib3.Render.Ui;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Nxa.GFlib3.Render;

/// <summary>Owns the window, OpenGL context, ImGui controller, and frame loop.</summary>
internal sealed class RenderApp : IDisposable
{
    private readonly AppOptions _options;
    private readonly SceneCatalog _catalog;
    private readonly FreeFlyCamera _camera = new();
    private readonly Localizer _text = new();

    private FieldScene _scene;

    private IWindow? _window;
    private IInputContext? _input;
    private GL? _gl;
    private ImGuiController? _imgui;
    private SceneRenderer? _renderer;
    private SceneFramebuffer? _framebuffer;
    private EditorShell? _shell;
    private readonly Stopwatch _clock = new();
    private int _frame;
    private bool _screenshotWritten;
    private bool _duplicateApplied;
    private bool _disposed;
    private IntPtr _layoutIniPath;
    private string _status = string.Empty;

    public RenderApp(AppOptions options, FieldScene scene, SceneCatalog catalog)
    {
        _options = options;
        _scene = scene;
        _catalog = catalog;
        _text.SetLanguage(options.Language);
        _status = _text.T("app.status.ready");
    }

    public void Run()
    {
        WindowOptions options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1600, 900),
            Title = _text.T("app.title"),
            WindowState = WindowState.Maximized,
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3)),
            VSync = true,
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += OnClosing;
        _window.Run();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shell = null;
        _renderer?.Dispose();
        _renderer = null;
        _framebuffer?.Dispose();
        _framebuffer = null;
        _imgui?.Dispose();
        _imgui = null;
        _input?.Dispose();
        _input = null;
        if (_layoutIniPath != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(_layoutIniPath);
            _layoutIniPath = IntPtr.Zero;
        }
    }

    private void OnLoad()
    {
        _window!.Title = _text.T("app.title");
        _gl = GL.GetApi(_window);
        _input = _window.CreateInput();

        EditorLayout.EnsureIniDirectory();
        bool hasSavedLayout = File.Exists(EditorLayout.IniPath);
        _layoutIniPath = Marshal.StringToCoTaskMemUTF8(EditorLayout.IniPath);
        ImGuiFontConfig? font = CreateFontConfig();
        _imgui = new ImGuiController(_gl, _window, _input, font, ConfigureIo);

        _renderer = new SceneRenderer(
            _gl,
            _scene,
            !_options.NoPlants,
            !_options.NoTextures,
            !_options.NoRoom,
            !_options.NoTerrain,
            !_options.NoScene);
        _framebuffer = new SceneFramebuffer(_gl);
        _shell = new EditorShell(_text, _scene, _catalog, !hasSavedLayout);
        if (_options.SelectInstance is int selectIndex && !_shell.SelectPlantInstance(selectIndex))
        {
            Console.Error.WriteLine(
                $"warning: --select-instance {selectIndex} is outside the loaded instance range.");
        }

        _shell.SetTool(_options.StartTool);

        if (_options.FrameSelection)
        {
            _shell.RequestFrameSelection();
        }

        EditorTheme.Apply();
        FrameCamera();
        _clock.Restart();
    }

    private void OnRender(double delta)
    {
        if (_gl is null || _imgui is null || _renderer is null || _framebuffer is null || _shell is null)
        {
            return;
        }

        _frame++;
        Vector2D<int> size = _window!.FramebufferSize;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)Math.Max(size.X, 1), (uint)Math.Max(size.Y, 1));
        _gl.ClearColor(0.05f, 0.05f, 0.06f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _imgui.Update((float)delta);

        if (_options.ReloadArea is not null && _frame == 2)
        {
            _shell.RequestLoad(_options.ReloadArea, ResolveReloadModel(_options.ReloadArea));
        }

        int duplicateFrame = _options.ReloadArea is null ? 2 : 3;
        if (_options.DuplicateInstance is int duplicateIndex
            && _frame == duplicateFrame
            && !_duplicateApplied)
        {
            _duplicateApplied = true;
            if (!_shell.DuplicatePlantInstance(duplicateIndex))
            {
                Console.Error.WriteLine(
                    $"warning: --duplicate-instance {duplicateIndex} could not be applied.");
            }
        }

        var frame = new UiFrameContext
        {
            Camera = _camera,
            RenderScene = RenderScene,
            Triangles = _renderer.RenderTriangleCount,
            Instances = _renderer.RenderInstanceCount,
            Status = _status,
        };
        _shell.Draw(frame);

        bool reloadAttempted = false;
        if (_shell.TakePendingLoad() is SceneSelection request)
        {
            ReloadScene(request);
            reloadAttempted = true;
        }

        if (_shell.ExitRequested)
        {
            _window.Close();
        }

        if (_shell.TakeResetCameraRequest())
        {
            FrameCamera();
        }

        if (_shell.TakeFrameRequest())
        {
            FrameSelection();
        }

        _imgui.Render();

        bool screenshotDue = _options.ScreenshotDelaySeconds > 0
            ? _clock.Elapsed.TotalSeconds >= _options.ScreenshotDelaySeconds
            : _frame >= Math.Max(_options.SmokeFrames, 2);
        if (!reloadAttempted && _options.ScreenshotPath is not null && !_screenshotWritten && screenshotDue)
        {
            SaveScreenshot(_options.ScreenshotPath);
            _screenshotWritten = true;
            if (_options.ScreenshotDelaySeconds > 0 && _options.SmokeFrames == 0)
            {
                _window.Close();
            }
        }

        if (!reloadAttempted && _options.SmokeFrames > 0 && _frame >= _options.SmokeFrames)
        {
            _window.Close();
        }
    }

    private void OnClosing()
    {
        Dispose();
    }

    private uint RenderScene(int width, int height)
    {
        SceneFramebuffer framebuffer = _framebuffer!;
        framebuffer.Resize(width, height);
        framebuffer.Bind();
        _renderer!.Preview = _shell!.Preview;
        _renderer.Render(_scene, _camera, width, height);

        Vector2D<int> size = _window!.FramebufferSize;
        _gl!.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)Math.Max(size.X, 1), (uint)Math.Max(size.Y, 1));
        return framebuffer.ColorTexture;
    }

    private void FrameCamera()
    {
        BoundingBox bounds = _scene.Terrain?.Bounds
            ?? (_options.NoPlants ? RoomBounds() : VegetationBounds());
        Vector3 min = ToVector(bounds.Min);
        Vector3 max = ToVector(bounds.Max);
        Vector3 center = (min + max) * 0.5f;
        float radius = MathF.Max((max - center).Length(), 1f);
        _camera.Frame(center, radius);
    }

    private BoundingBox VegetationBounds()
    {
        if (_scene.Instances.Count == 0)
        {
            return _scene.ComputeBounds();
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;

        foreach (PlantInstance instance in _scene.Instances)
        {
            minX = Math.Min(minX, instance.Matrix.Row3.X);
            maxX = Math.Max(maxX, instance.Matrix.Row3.X);
            minY = Math.Min(minY, instance.Matrix.Row3.Y);
            maxY = Math.Max(maxY, instance.Matrix.Row3.Y);
            minZ = Math.Min(minZ, instance.Matrix.Row3.Z);
            maxZ = Math.Max(maxZ, instance.Matrix.Row3.Z);
        }

        const float margin = 12f;
        const float plantHeight = 12f;
        return new BoundingBox(
            new Vector3f(minX - margin, minY - 1f, minZ - margin),
            new Vector3f(maxX + margin, maxY + plantHeight, maxZ + margin));
    }

    private BoundingBox RoomBounds()
    {
        if (_scene.RoomMeshes.Count == 0)
        {
            return _scene.ComputeBounds();
        }

        Vector3f min = _scene.RoomMeshes[0].Geometry.Bounds.Min;
        Vector3f max = _scene.RoomMeshes[0].Geometry.Bounds.Max;
        foreach (SceneMesh mesh in _scene.RoomMeshes)
        {
            min = new Vector3f(
                Math.Min(min.X, mesh.Geometry.Bounds.Min.X),
                Math.Min(min.Y, mesh.Geometry.Bounds.Min.Y),
                Math.Min(min.Z, mesh.Geometry.Bounds.Min.Z));
            max = new Vector3f(
                Math.Max(max.X, mesh.Geometry.Bounds.Max.X),
                Math.Max(max.Y, mesh.Geometry.Bounds.Max.Y),
                Math.Max(max.Z, mesh.Geometry.Bounds.Max.Z));
        }

        return new BoundingBox(min, max);
    }

    private void FrameSelection()
    {
        if (_shell!.TryGetSelectionBounds(out BoundingBox bounds))
        {
            Vector3 min = ToVector(bounds.Min);
            Vector3 max = ToVector(bounds.Max);
            if (IsFinite(min)
                && IsFinite(max)
                && min.X <= max.X
                && min.Y <= max.Y
                && min.Z <= max.Z)
            {
                Vector3 center = (min + max) * 0.5f;
                float radius = (max - center).Length();
                if (float.IsFinite(radius) && radius > 0f)
                {
                    _camera.Frame(center, MathF.Max(radius, 1f));
                    return;
                }
            }
        }

        FrameCamera();
    }

    private string ResolveReloadModel(string area)
    {
        if (_options.ReloadModel is not null)
        {
            return _options.ReloadModel;
        }

        int index = _catalog.IndexOfModel(area);
        return index >= 0 ? _catalog.ModelPacks[index] : _scene.ModelPackName;
    }

    private void ReloadScene(SceneSelection request)
    {
        _status = _text.T("app.status.loading");
        int discardedInstances = _shell?.DirtyInstanceCount ?? 0;
        try
        {
            FieldScene scene = FieldSceneLoader.Load(
                _scene.DumpRoot,
                request.Area,
                request.ModelPack,
                _options.MeshFilter,
                _options.TerrainStride,
                _options.TerrainTileScale);
            var renderer = new SceneRenderer(
                _gl!,
                scene,
                !_options.NoPlants,
                !_options.NoTextures,
                !_options.NoRoom,
                !_options.NoTerrain,
                !_options.NoScene);

            _renderer?.Dispose();
            _renderer = renderer;
            _scene = scene;
            _shell!.SetScene(scene);
            if (discardedInstances > 0)
            {
                scene.Messages.Add(
                    $"warning: reload discarded {discardedInstances} edited instance(s).");
            }

            FrameCamera();
            _status = $"{_text.T("app.status.loaded")}: {request.Area}";
            Console.WriteLine(
                $"switched to {request.Area} with {request.ModelPack}: "
                + $"{scene.RoomMeshes.Count} room parts, {scene.PlacedMeshes.Count} placed parts, "
                + $"{scene.Instances.Count} instances.");
        }
        catch (Exception ex) when (ex is IOException
            or InvalidDataException
            or NotSupportedException
            or DllNotFoundException
            or InvalidOperationException
            or UnauthorizedAccessException
            or ArgumentException)
        {
            _scene.Messages.Add(
                $"error: failed to load {request.Area} with {request.ModelPack}: {ex.Message}");
            _status = $"{_text.T("app.status.loadError")}: {request.Area}";
            Console.Error.WriteLine(_scene.Messages[^1]);
        }
    }

    private void SaveScreenshot(string path)
    {
        if (_gl is null || _window is null)
        {
            return;
        }

        Vector2D<int> size = _window.FramebufferSize;
        int width = Math.Max(size.X, 1);
        int height = Math.Max(size.Y, 1);
        byte[] pixels = new byte[width * height * 3];

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.ReadBuffer(ReadBufferMode.Back);
        _gl.PixelStore(PixelStoreParameter.PackAlignment, 1);
        unsafe
        {
            fixed (byte* pointer = pixels)
            {
                _gl.ReadPixels(
                    0,
                    0,
                    (uint)width,
                    (uint)height,
                    PixelFormat.Rgb,
                    PixelType.UnsignedByte,
                    pointer);
            }
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        WriteBmp(path, width, height, pixels);
    }

    private static void WriteBmp(string path, int width, int height, byte[] rgb)
    {
        int rowStride = (width * 3 + 3) & ~3;
        int imageSize = rowStride * height;
        int fileSize = 54 + imageSize;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((short)0);
        writer.Write((short)0);
        writer.Write(54);
        writer.Write(40);
        writer.Write(width);
        writer.Write(height);
        writer.Write((short)1);
        writer.Write((short)24);
        writer.Write(0);
        writer.Write(imageSize);
        writer.Write(2835);
        writer.Write(2835);
        writer.Write(0);
        writer.Write(0);

        byte[] row = new byte[rowStride];
        for (int y = 0; y < height; y++)
        {
            Array.Clear(row);
            for (int x = 0; x < width; x++)
            {
                int source = ((y * width) + x) * 3;
                int target = x * 3;
                row[target] = rgb[source + 2];
                row[target + 1] = rgb[source + 1];
                row[target + 2] = rgb[source];
            }

            writer.Write(row);
        }
    }

    private void ConfigureIo()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        io.ConfigDockingWithShift = false;
        io.ConfigWindowsMoveFromTitleBarOnly = true;
        io.ConfigWindowsResizeFromEdges = true;
        unsafe
        {
            io.NativePtr->IniFilename = (byte*)_layoutIniPath;
        }
    }

    private static ImGuiFontConfig? CreateFontConfig()
    {
        string? path = FindJapaneseFont();
        if (path is null)
        {
            return null;
        }

        return new ImGuiFontConfig(
            path,
            EditorTheme.FontSize,
            io => io.Fonts.GetGlyphRangesJapanese());
    }

    private static string? FindJapaneseFont()
    {
        string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string[] candidates =
        [
            "meiryo.ttc",
            "msgothic.ttc",
            "YuGothM.ttc",
            "YuGothic.ttf",
            "NotoSansCJKjp-Regular.otf",
        ];

        foreach (string candidate in candidates)
        {
            string path = Path.Combine(fonts, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static Vector3 ToVector(Vector3f value) => new(value.X, value.Y, value.Z);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}








