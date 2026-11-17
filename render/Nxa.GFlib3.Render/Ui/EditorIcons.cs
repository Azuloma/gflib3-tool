// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using ImGuiNET;

namespace Nxa.GFlib3.Render.Ui;

internal enum EditorIcon
{
    Select,
    Move,
    Rotate,
    Scale,
    Frame,
}

/// <summary>Draws the compact editor toolbar icons as vector shapes.</summary>
internal static class EditorIcons
{
    public static bool ToolButton(EditorIcon icon, string tooltip, bool active, bool enabled)
    {
        float height = ImGui.GetFrameHeight();
        var size = new Vector2(height, height);
        ImGui.PushID((int)icon);
        ImGui.InvisibleButton("##tool", size);

        bool hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        ImDrawListPtr draw = ImGui.GetWindowDrawList();

        if (active || hovered)
        {
            Vector4 background = active ? EditorTheme.Selection : EditorTheme.ControlHovered;
            draw.AddRectFilled(min, max, ToColor(background), 2f);
        }

        Vector4 color = enabled
            ? active ? EditorTheme.IconActive : EditorTheme.TextPrimary
            : EditorTheme.TextDisabled;
        Draw(draw, icon, min + new Vector2(2f, 2f), size - new Vector2(4f, 4f), ToColor(color));

        if (hovered)
        {
            ImGui.SetTooltip(tooltip);
        }

        bool clicked = enabled && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        ImGui.PopID();
        return clicked;
    }

    public static void ToolbarSeparator()
    {
        ImGui.SameLine(0f, 6f);
        float height = ImGui.GetFrameHeight();
        ImGui.InvisibleButton("##toolbarSeparator", new Vector2(1f, height));
        Vector2 min = ImGui.GetItemRectMin();
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(min.X, min.Y + 3f),
            new Vector2(min.X, min.Y + height - 3f),
            ToColor(EditorTheme.TextDisabled));
        ImGui.SameLine(0f, 6f);
    }

    public static void Draw(ImDrawListPtr draw, EditorIcon icon, Vector2 min, Vector2 size, uint color)
    {
        switch (icon)
        {
            case EditorIcon.Select:
                DrawSelect(draw, min, size, color);
                break;
            case EditorIcon.Move:
                DrawMove(draw, min, size, color);
                break;
            case EditorIcon.Rotate:
                DrawRotate(draw, min, size, color);
                break;
            case EditorIcon.Scale:
                DrawScale(draw, min, size, color);
                break;
            case EditorIcon.Frame:
                DrawFrame(draw, min, size, color);
                break;
        }
    }

    public static uint ToColor(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);

    private static void DrawSelect(ImDrawListPtr draw, Vector2 min, Vector2 size, uint color)
    {
        Vector2[] points =
        [
            At(min, size, 0.28f, 0.10f),
            At(min, size, 0.28f, 0.82f),
            At(min, size, 0.44f, 0.65f),
            At(min, size, 0.57f, 0.92f),
            At(min, size, 0.69f, 0.86f),
            At(min, size, 0.56f, 0.61f),
            At(min, size, 0.77f, 0.59f),
        ];

        draw.AddConcavePolyFilled(ref points[0], points.Length, color);
    }

    private static void DrawMove(ImDrawListPtr draw, Vector2 min, Vector2 size, uint color)
    {
        const float thickness = 1.4f;
        draw.AddLine(At(min, size, 0.5f, 0.14f), At(min, size, 0.5f, 0.86f), color, thickness);
        draw.AddLine(At(min, size, 0.14f, 0.5f), At(min, size, 0.86f, 0.5f), color, thickness);
        draw.AddTriangleFilled(At(min, size, 0.5f, 0.06f), At(min, size, 0.40f, 0.25f), At(min, size, 0.60f, 0.25f), color);
        draw.AddTriangleFilled(At(min, size, 0.5f, 0.94f), At(min, size, 0.40f, 0.75f), At(min, size, 0.60f, 0.75f), color);
        draw.AddTriangleFilled(At(min, size, 0.06f, 0.5f), At(min, size, 0.25f, 0.40f), At(min, size, 0.25f, 0.60f), color);
        draw.AddTriangleFilled(At(min, size, 0.94f, 0.5f), At(min, size, 0.75f, 0.40f), At(min, size, 0.75f, 0.60f), color);
    }

    private static void DrawRotate(ImDrawListPtr draw, Vector2 min, Vector2 size, uint color)
    {
        const int segments = 18;
        const float start = 0.70f;
        const float end = 5.40f;
        Vector2 center = At(min, size, 0.5f, 0.5f);
        float radius = size.X * 0.30f;
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float angle = start + ((end - start) * i / segments);
            points[i] = new Vector2(
                center.X + (MathF.Cos(angle) * radius),
                center.Y + (MathF.Sin(angle) * radius));
        }

        draw.AddPolyline(ref points[0], points.Length, color, ImDrawFlags.None, 1.4f);

        var tangent = new Vector2(-MathF.Sin(end), MathF.Cos(end));
        var normal = new Vector2(MathF.Cos(end), MathF.Sin(end));
        Vector2 tip = points[^1] + (tangent * (size.X * 0.11f));
        Vector2 baseCenter = points[^1] - (tangent * (size.X * 0.01f));
        draw.AddTriangleFilled(
            tip,
            baseCenter + (normal * (size.X * 0.07f)),
            baseCenter - (normal * (size.X * 0.07f)),
            color);
    }

    private static void DrawScale(ImDrawListPtr draw, Vector2 min, Vector2 size, uint color)
    {
        const float thickness = 1.4f;
        draw.AddRect(At(min, size, 0.14f, 0.58f), At(min, size, 0.42f, 0.86f), color, 0f, ImDrawFlags.None, thickness);
        draw.AddRect(At(min, size, 0.58f, 0.14f), At(min, size, 0.86f, 0.42f), color, 0f, ImDrawFlags.None, thickness);
        draw.AddLine(At(min, size, 0.34f, 0.66f), At(min, size, 0.64f, 0.36f), color, thickness);
        Vector2 tip = At(min, size, 0.72f, 0.28f);
        Vector2 tangent = new(0.7071f, -0.7071f);
        Vector2 normal = new(0.7071f, 0.7071f);
        draw.AddTriangleFilled(
            tip,
            tip - (tangent * (size.X * 0.16f)) + (normal * (size.X * 0.07f)),
            tip - (tangent * (size.X * 0.16f)) - (normal * (size.X * 0.07f)),
            color);
    }

    private static void DrawFrame(ImDrawListPtr draw, Vector2 min, Vector2 size, uint color)
    {
        const float thickness = 1.4f;
        const float a = 0.16f;
        const float b = 0.34f;
        draw.AddLine(At(min, size, a, b), At(min, size, a, a), color, thickness);
        draw.AddLine(At(min, size, a, a), At(min, size, b, a), color, thickness);
        draw.AddLine(At(min, size, 1f - a, b), At(min, size, 1f - a, a), color, thickness);
        draw.AddLine(At(min, size, 1f - a, a), At(min, size, 1f - b, a), color, thickness);
        draw.AddLine(At(min, size, a, 1f - b), At(min, size, a, 1f - a), color, thickness);
        draw.AddLine(At(min, size, a, 1f - a), At(min, size, b, 1f - a), color, thickness);
        draw.AddLine(At(min, size, 1f - a, 1f - b), At(min, size, 1f - a, 1f - a), color, thickness);
        draw.AddLine(At(min, size, 1f - a, 1f - a), At(min, size, 1f - b, 1f - a), color, thickness);
        draw.AddRectFilled(At(min, size, 0.44f, 0.44f), At(min, size, 0.56f, 0.56f), color, 0.5f);
    }

    private static Vector2 At(Vector2 min, Vector2 size, float x, float y) =>
        new(min.X + (x * size.X), min.Y + (y * size.Y));
}
