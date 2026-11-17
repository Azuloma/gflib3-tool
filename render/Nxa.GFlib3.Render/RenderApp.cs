// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Diagnostics;
using System.Numerics;
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
    private readonly FieldScene _scene;
    private readonly FreeFlyCamera _camera = new();
    private readonly Localizer _text = new();

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
    private bool _disposed;
    private string _status = string.Empty;

    public RenderApp(AppOptions options, FieldScene scene)
    {
        _options = options;
        _scene = scene;
        _text.SetLanguage(options.Language);
        _status = _text.T("app.status.ready");
    }

    public void Run()
    {
        WindowOptions options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(1600, 900),
            Title = _text.T("app.title"),
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
    }

    private void OnLoad()
    {
        _window!.Title = _text.T("app.title");
        _gl = GL.GetApi(_window);
        _input = _window.CreateInput();

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
        _shell = new EditorShell(_text, _scene);

        ApplyTheme();
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

        var frame = new UiFrameContext
        {
            Camera = _camera,
            RenderScene = RenderScene,
            Triangles = _renderer.RenderTriangleCount,
            Instances = _renderer.RenderInstanceCount,
            Status = _status,
        };
        _shell.Draw(frame);

        if (_shell.ExitRequested)
        {
            _window.Close();
        }

        if (_shell.ResetCameraRequested)
        {
            FrameCamera();
        }

        if (_shell.FrameRequested)
        {
            FrameSelection();
        }

        _imgui.Render();

        bool screenshotDue = _options.ScreenshotDelaySeconds > 0
            ? _clock.Elapsed.TotalSeconds >= _options.ScreenshotDelaySeconds
            : _frame >= Math.Max(_options.SmokeFrames, 2);
        if (_options.ScreenshotPath is not null && !_screenshotWritten && screenshotDue)
        {
            SaveScreenshot(_options.ScreenshotPath);
            _screenshotWritten = true;
            if (_options.ScreenshotDelaySeconds > 0 && _options.SmokeFrames == 0)
            {
                _window.Close();
            }
        }

        if (_options.SmokeFrames > 0 && _frame >= _options.SmokeFrames)
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
        _renderer!.Render(_scene, _camera, width, height);

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
        FrameCamera();
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
        io.ConfigWindowsMoveFromTitleBarOnly = true;
    }

    private static ImGuiFontConfig? CreateFontConfig()
    {
        string? path = FindJapaneseFont();
        if (path is null)
        {
            return null;
        }

        return new ImGuiFontConfig(path, 17, io => io.Fonts.GetGlyphRangesJapanese());
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

    private static void ApplyTheme()
    {
        ImGui.StyleColorsDark();
        ImGuiStylePtr style = ImGui.GetStyle();
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 3f;
        style.PopupRounding = 3f;
        style.ScrollbarRounding = 2f;
        style.GrabRounding = 2f;
        style.TabRounding = 3f;
        style.WindowBorderSize = 1f;
        style.FrameBorderSize = 0f;
        style.ItemSpacing = new Vector2(8, 4);
        style.FramePadding = new Vector2(8, 4);
        style.IndentSpacing = 18f;
        style.ScrollbarSize = 12f;

        style.Colors[(int)ImGuiCol.WindowBg] = new Vector4(0.12f, 0.12f, 0.13f, 1f);
        style.Colors[(int)ImGuiCol.ChildBg] = new Vector4(0.14f, 0.14f, 0.15f, 1f);
        style.Colors[(int)ImGuiCol.PopupBg] = new Vector4(0.10f, 0.10f, 0.11f, 0.98f);
        style.Colors[(int)ImGuiCol.Border] = new Vector4(0.20f, 0.20f, 0.21f, 1f);
        style.Colors[(int)ImGuiCol.FrameBg] = new Vector4(0.19f, 0.19f, 0.20f, 1f);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.24f, 0.24f, 0.26f, 1f);
        style.Colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.27f, 0.27f, 0.29f, 1f);
        style.Colors[(int)ImGuiCol.TitleBg] = new Vector4(0.10f, 0.10f, 0.11f, 1f);
        style.Colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.13f, 0.13f, 0.14f, 1f);
        style.Colors[(int)ImGuiCol.MenuBarBg] = new Vector4(0.10f, 0.10f, 0.11f, 1f);
        style.Colors[(int)ImGuiCol.Button] = new Vector4(0.21f, 0.21f, 0.23f, 1f);
        style.Colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.27f, 0.27f, 0.29f, 1f);
        style.Colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.31f, 0.31f, 0.34f, 1f);
        style.Colors[(int)ImGuiCol.Header] = new Vector4(0.22f, 0.25f, 0.31f, 1f);
        style.Colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.27f, 0.31f, 0.38f, 1f);
        style.Colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.29f, 0.34f, 0.42f, 1f);
        style.Colors[(int)ImGuiCol.Tab] = new Vector4(0.15f, 0.15f, 0.16f, 1f);
        style.Colors[(int)ImGuiCol.TabHovered] = new Vector4(0.24f, 0.27f, 0.32f, 1f);
        style.Colors[(int)ImGuiCol.TabActive] = new Vector4(0.21f, 0.24f, 0.29f, 1f);
        style.Colors[(int)ImGuiCol.DockingPreview] = new Vector4(0.25f, 0.45f, 0.65f, 0.6f);
    }

    private static Vector3 ToVector(Vector3f value) => new(value.X, value.Y, value.Z);
}








