// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using System.Runtime.InteropServices;
using ImGuiNET;

namespace Nxa.GFlib3.Render.Ui;

/// <summary>Builds the default dockspace layout and owns its persistence path.</summary>
internal static class EditorLayout
{
    public const string HierarchyWindow = "Hierarchy###Hierarchy";
    public const string ProjectWindow = "Project###Project";
    public const string SceneWindow = "Scene###Scene";
    public const string InspectorWindow = "Inspector###Inspector";
    public const string ConsoleWindow = "Console###Console";

    private const string DockspaceName = "GFlib3Dockspace";

    public static string IniPath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Nxa", "GFlib3Tool", "editor-layout.ini");
        }
    }

    public static void EnsureIniDirectory()
    {
        string? directory = Path.GetDirectoryName(IniPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public static uint DockspaceId => ImGui.GetID(DockspaceName);

    public static void Build(uint dockspaceId, Vector2 position, Vector2 size)
    {
        DockBuilderNative.RemoveNode(dockspaceId);
        dockspaceId = DockBuilderNative.AddNode(dockspaceId, 0);
        DockBuilderNative.SetNodePos(dockspaceId, position);
        DockBuilderNative.SetNodeSize(dockspaceId, size);

        uint left = DockBuilderNative.SplitNode(dockspaceId, (int)ImGuiDir.Left, 0.20f, out _, out uint center);
        uint right = DockBuilderNative.SplitNode(center, (int)ImGuiDir.Right, 0.275f, out _, out center);
        uint hierarchy = DockBuilderNative.SplitNode(left, (int)ImGuiDir.Up, 0.45f, out _, out uint project);
        uint scene = DockBuilderNative.SplitNode(center, (int)ImGuiDir.Up, 0.68f, out _, out uint console);
        if (left == 0 || right == 0 || hierarchy == 0 || project == 0 || scene == 0 || console == 0)
        {
            throw new InvalidOperationException("Dock layout split produced an invalid node.");
        }

        DockWindow(HierarchyWindow, hierarchy);
        DockWindow(ProjectWindow, project);
        DockWindow(SceneWindow, scene);
        DockWindow(ConsoleWindow, console);
        DockWindow(InspectorWindow, right);
        DockBuilderNative.Finish(dockspaceId);
    }

    private static void DockWindow(string windowName, uint nodeId)
    {
        IntPtr pointer = Marshal.StringToCoTaskMemUTF8(windowName);
        try
        {
            DockBuilderNative.DockWindow(pointer, nodeId);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    // ImGui.NET 1.90.8.1 does not wrap DockBuilder, so call the cimgui exports it ships.
    private static class DockBuilderNative
    {
        private const string Library = "cimgui";

        [DllImport(Library, EntryPoint = "igDockBuilderAddNode", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint AddNode(uint nodeId, int flags);

        [DllImport(Library, EntryPoint = "igDockBuilderRemoveNode", CallingConvention = CallingConvention.Cdecl)]
        public static extern void RemoveNode(uint nodeId);

        [DllImport(Library, EntryPoint = "igDockBuilderSetNodePos", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetNodePos(uint nodeId, Vector2 position);

        [DllImport(Library, EntryPoint = "igDockBuilderSetNodeSize", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SetNodeSize(uint nodeId, Vector2 size);

        [DllImport(Library, EntryPoint = "igDockBuilderSplitNode", CallingConvention = CallingConvention.Cdecl)]
        public static extern uint SplitNode(
            uint nodeId,
            int splitDirection,
            float sizeRatioForNodeAtDirection,
            out uint idAtDirection,
            out uint idAtOppositeDirection);

        [DllImport(Library, EntryPoint = "igDockBuilderDockWindow", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DockWindow(IntPtr windowName, uint nodeId);

        [DllImport(Library, EntryPoint = "igDockBuilderFinish", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Finish(uint nodeId);
    }
}
