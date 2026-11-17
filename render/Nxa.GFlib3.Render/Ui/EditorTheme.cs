// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using ImGuiNET;

namespace Nxa.GFlib3.Render.Ui;

/// <summary>Unity 2019 dark-skin palette and compact metrics for the editor shell.</summary>
internal static class EditorTheme
{
    public const float UiScale = 1.25f;
    public const int FontSize = 18;

    public static readonly float ToolbarHeight = 28f * UiScale;
    public static readonly float StatusBarHeight = 22f * UiScale;

    public static readonly Vector4 TextPrimary = Hex(0xD2D2D2);
    public static readonly Vector4 TextMuted = Hex(0x9A9A9A);
    public static readonly Vector4 TextDisabled = Hex(0x6A6A6A);
    public static readonly Vector4 IconActive = Hex(0xF0F0F0);
    public static readonly Vector4 Selection = Hex(0x2C5D87);
    public static readonly Vector4 SelectionHovered = Hex(0x356A99);
    public static readonly Vector4 Accent = Hex(0x4A90D9);
    public static readonly Vector4 Warning = Hex(0xD9A441);
    public static readonly Vector4 Error = Hex(0xD95C5C);
    public static readonly Vector4 ControlHovered = Hex(0x3E3E3E);
    public static readonly Vector4 ToolbarBackground = Hex(0x3C3C3C);
    public static readonly Vector4 StatusBackground = Hex(0x2D2D2D);
    public static readonly Vector4 Border = Hex(0x232323);

    public static void Apply()
    {
        ImGui.StyleColorsDark();
        ImGuiStylePtr style = ImGui.GetStyle();

        style.WindowPadding = new Vector2(6, 6);
        style.FramePadding = new Vector2(4, 2);
        style.CellPadding = new Vector2(4, 2);
        style.ItemSpacing = new Vector2(4, 3);
        style.ItemInnerSpacing = new Vector2(4, 4);
        style.IndentSpacing = 16f;
        style.ScrollbarSize = 12f;
        style.GrabMinSize = 10f;

        style.WindowBorderSize = 0f;
        style.ChildBorderSize = 0f;
        style.PopupBorderSize = 1f;
        style.FrameBorderSize = 0f;
        style.TabBorderSize = 0f;
        style.TabBarBorderSize = 1f;
        style.DockingSeparatorSize = 1f;

        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 2f;
        style.PopupRounding = 0f;
        style.ScrollbarRounding = 0f;
        style.GrabRounding = 2f;
        style.TabRounding = 0f;

        style.WindowMenuButtonPosition = ImGuiDir.None;
        style.ColorButtonPosition = ImGuiDir.Right;
        style.ButtonTextAlign = new Vector2(0.5f, 0.5f);
        style.SelectableTextAlign = new Vector2(0f, 0.5f);
        style.ScaleAllSizes(UiScale);

        style.Colors[(int)ImGuiCol.Text] = TextPrimary;
        style.Colors[(int)ImGuiCol.TextDisabled] = TextDisabled;
        style.Colors[(int)ImGuiCol.WindowBg] = Hex(0x383838);
        style.Colors[(int)ImGuiCol.ChildBg] = Hex(0x383838);
        style.Colors[(int)ImGuiCol.PopupBg] = Hex(0x2D2D2D, 0.99f);
        style.Colors[(int)ImGuiCol.Border] = Border;
        style.Colors[(int)ImGuiCol.BorderShadow] = Vector4.Zero;
        style.Colors[(int)ImGuiCol.FrameBg] = Hex(0x2C2C2C);
        style.Colors[(int)ImGuiCol.FrameBgHovered] = ControlHovered;
        style.Colors[(int)ImGuiCol.FrameBgActive] = Hex(0x4A4A4A);
        style.Colors[(int)ImGuiCol.TitleBg] = Hex(0x3C3C3C);
        style.Colors[(int)ImGuiCol.TitleBgActive] = Hex(0x3C3C3C);
        style.Colors[(int)ImGuiCol.TitleBgCollapsed] = Hex(0x303030);
        style.Colors[(int)ImGuiCol.MenuBarBg] = Hex(0x3C3C3C);
        style.Colors[(int)ImGuiCol.ScrollbarBg] = Hex(0x303030);
        style.Colors[(int)ImGuiCol.ScrollbarGrab] = Hex(0x5A5A5A);
        style.Colors[(int)ImGuiCol.ScrollbarGrabHovered] = Hex(0x6A6A6A);
        style.Colors[(int)ImGuiCol.ScrollbarGrabActive] = Hex(0x7A7A7A);
        style.Colors[(int)ImGuiCol.CheckMark] = Accent;
        style.Colors[(int)ImGuiCol.SliderGrab] = Hex(0x4C7EA8);
        style.Colors[(int)ImGuiCol.SliderGrabActive] = Accent;
        style.Colors[(int)ImGuiCol.Button] = Hex(0x3C3C3C);
        style.Colors[(int)ImGuiCol.ButtonHovered] = Hex(0x4A4A4A);
        style.Colors[(int)ImGuiCol.ButtonActive] = Hex(0x555555);
        style.Colors[(int)ImGuiCol.Header] = Selection;
        style.Colors[(int)ImGuiCol.HeaderHovered] = SelectionHovered;
        style.Colors[(int)ImGuiCol.HeaderActive] = Selection;
        style.Colors[(int)ImGuiCol.Separator] = Hex(0x2A2A2A);
        style.Colors[(int)ImGuiCol.SeparatorHovered] = Hex(0x4A90D9, 0.6f);
        style.Colors[(int)ImGuiCol.SeparatorActive] = Accent;
        style.Colors[(int)ImGuiCol.ResizeGrip] = Vector4.Zero;
        style.Colors[(int)ImGuiCol.ResizeGripHovered] = Hex(0x4A90D9, 0.4f);
        style.Colors[(int)ImGuiCol.ResizeGripActive] = Hex(0x4A90D9, 0.7f);
        style.Colors[(int)ImGuiCol.Tab] = Hex(0x353535);
        style.Colors[(int)ImGuiCol.TabHovered] = Hex(0x454545);
        style.Colors[(int)ImGuiCol.TabActive] = Hex(0x4A4A4A);
        style.Colors[(int)ImGuiCol.TabUnfocused] = Hex(0x353535);
        style.Colors[(int)ImGuiCol.TabUnfocusedActive] = Hex(0x404040);
        style.Colors[(int)ImGuiCol.DockingPreview] = Hex(0x2C5D87, 0.7f);
        style.Colors[(int)ImGuiCol.DockingEmptyBg] = Hex(0x2D2D2D);
        style.Colors[(int)ImGuiCol.TableHeaderBg] = Hex(0x3C3C3C);
        style.Colors[(int)ImGuiCol.TableBorderStrong] = Border;
        style.Colors[(int)ImGuiCol.TableBorderLight] = Hex(0x2A2A2A);
        style.Colors[(int)ImGuiCol.TableRowBg] = Vector4.Zero;
        style.Colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1f, 1f, 1f, 0.03f);
        style.Colors[(int)ImGuiCol.TextSelectedBg] = Hex(0x2C5D87, 0.6f);
        style.Colors[(int)ImGuiCol.DragDropTarget] = Accent;
        style.Colors[(int)ImGuiCol.NavHighlight] = Accent;
        style.Colors[(int)ImGuiCol.NavWindowingHighlight] = new Vector4(1f, 1f, 1f, 0.7f);
        style.Colors[(int)ImGuiCol.ModalWindowDimBg] = new Vector4(0.1f, 0.1f, 0.1f, 0.6f);
    }

    private static Vector4 Hex(uint rgb, float alpha = 1f) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f,
        alpha);
}
