// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using ImGuiNET;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Camera;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

public sealed partial class EditorShell
{
    private const float GizmoTargetPixels = 72f;
    private const float MinAxisWorldLength = 0.25f;
    private const float MaxAxisWorldLength = 5000f;
    private const float RotationRingFactor = 0.8f;
    private const float GizmoHitRadius = 9f * EditorTheme.UiScale;
    private const float GizmoAxisThickness = 1.8f;
    private const float GizmoActiveThickness = 3.2f;
    private const int RotationRingSegments = 48;

    private static readonly int[,] BoundsEdges =
    {
        { 0, 1 }, { 1, 3 }, { 3, 2 }, { 2, 0 },
        { 4, 5 }, { 5, 7 }, { 7, 6 }, { 6, 4 },
        { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
    };

    private GizmoMode _tool = GizmoMode.Select;
    private GizmoDrag? _drag;
    private GizmoAxis _hoveredAxis = GizmoAxis.None;

    public int DirtyInstanceCount => _editSession.DirtyCount;

    public bool HasEdits => _editSession.DirtyCount > 0;

    public bool SelectPlantInstance(int index)
    {
        if ((uint)index >= (uint)_scene.Instances.Count)
        {
            return false;
        }

        Select(new EditorSelection(EditorSelectionKind.PlantInstance, index));
        return true;
    }

    private void UpdateEditorShortcuts()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        if (io.WantTextInput)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey._1, false))
        {
            SetTool(GizmoMode.Select);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey._2, false))
        {
            SetTool(GizmoMode.Move);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey._3, false))
        {
            SetTool(GizmoMode.Rotate);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey._4, false))
        {
            SetTool(GizmoMode.Scale);
        }
        else if (ImGui.IsKeyPressed(ImGuiKey._5, false))
        {
            SetTool(GizmoMode.Place);
        }

        if (!io.KeyCtrl)
        {
            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.D, false))
        {
            DuplicateSelected();
            return;
        }

        if (io.KeyShift)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
            {
                Redo();
            }

            return;
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Z, false))
        {
            Undo();
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.Y, false))
        {
            Redo();
        }
    }

    public void SetTool(GizmoMode mode)
    {
        if (mode == GizmoMode.Place && !CanPlace)
        {
            _scene.Messages.Add(
                "warning: select a vegetation instance or model before using Place.");
            return;
        }

        if (_drag is not null)
        {
            CancelDrag();
        }

        _tool = mode;
        if (mode != GizmoMode.Place)
        {
            ClearPlacePreview();
        }
    }

    private void Undo()
    {
        if (_drag is null)
        {
            _editSession.Undo();
            EnsureSceneIndexes();
        }
    }

    private void Redo()
    {
        if (_drag is null)
        {
            _editSession.Redo();
            EnsureSceneIndexes();
        }
    }

    private void CancelDrag()
    {
        if (_drag is GizmoDrag drag)
        {
            _editSession.SetMatrix(drag.InstanceIndex, drag.StartMatrix);
        }

        _drag = null;
    }

    private bool TryGetSelectedInstance(out int index)
    {
        index = _selection.Index;
        return _selection.Kind == EditorSelectionKind.PlantInstance
            && (uint)index < (uint)_scene.Instances.Count;
    }

    private string ToolNameKey => _tool switch
    {
        GizmoMode.Move => "toolbar.move",
        GizmoMode.Rotate => "toolbar.rotate",
        GizmoMode.Scale => "toolbar.scale",
        GizmoMode.Place => "toolbar.place",
        _ => "toolbar.select",
    };

    private bool UpdateGizmoInput(
        FreeFlyCamera camera,
        Matrix4x4 viewProjection,
        Matrix4x4 inverseViewProjection,
        bool hovered,
        Vector2 localMouse,
        Vector2 imageSize)
    {
        if (_drag is GizmoDrag drag)
        {
            if (ImGui.IsKeyPressed(ImGuiKey.Escape, false))
            {
                CancelDrag();
                return true;
            }

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                ApplyDrag(inverseViewProjection, localMouse, imageSize);
                Matrix4x4RowMajor current = _editSession.GetMatrix(drag.InstanceIndex);
                if (drag.Changed)
                {
                    _editSession.Commit(drag.InstanceIndex, drag.StartMatrix, current);
                }

                _drag = null;
                return true;
            }

            ApplyDrag(inverseViewProjection, localMouse, imageSize);
            return true;
        }

        _hoveredAxis = GizmoAxis.None;
        if (!hovered
            || _tool is GizmoMode.Select or GizmoMode.Place
            || !TryGetSelectedInstance(out int index))
        {
            return false;
        }

        if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
        {
            return false;
        }

        SceneTransform transform = SceneGeometry.Decompose(_editSession.GetMatrix(index));
        _hoveredAxis = HitTestGizmo(camera, transform, viewProjection, localMouse, imageSize);
        if (_hoveredAxis == GizmoAxis.None || !ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            return false;
        }

        if (!ScenePicking.TryScreenRay(localMouse, imageSize, inverseViewProjection, out SceneRay ray))
        {
            return false;
        }

        return BeginDrag(index, transform, _hoveredAxis, ray);
    }

    private bool BeginDrag(int index, SceneTransform start, GizmoAxis axis, SceneRay ray)
    {
        float startDistance = 0f;
        float startAngle = 0f;
        if (_tool is GizmoMode.Move or GizmoMode.Scale)
        {
            if (!GizmoMath.TryClosestPointOnAxis(
                    start.Position,
                    GizmoMath.AxisDirection(axis),
                    ray,
                    out startDistance))
            {
                return false;
            }
        }
        else if (_tool == GizmoMode.Rotate)
        {
            if (!GizmoMath.TryIntersectPlane(ray, start.Position, Vector3.UnitY, out Vector3 hit))
            {
                return false;
            }

            startAngle = GizmoMath.AngleDegrees(start.Position, hit);
        }
        else
        {
            return false;
        }

        _drag = new GizmoDrag
        {
            InstanceIndex = index,
            Mode = _tool,
            Axis = axis,
            Start = start,
            StartMatrix = _editSession.GetMatrix(index),
            StartAxisDistance = startDistance,
            StartAngle = startAngle,
        };
        return true;
    }

    private void ApplyDrag(Matrix4x4 inverseViewProjection, Vector2 localMouse, Vector2 imageSize)
    {
        GizmoDrag drag = _drag!;
        if (!ScenePicking.TryScreenRay(localMouse, imageSize, inverseViewProjection, out SceneRay ray))
        {
            return;
        }

        Vector3 axis = GizmoMath.AxisDirection(drag.Axis);
        Matrix4x4RowMajor matrix;

        switch (drag.Mode)
        {
            case GizmoMode.Move:
                if (!GizmoMath.TryClosestPointOnAxis(drag.Start.Position, axis, ray, out float distance))
                {
                    return;
                }

                matrix = GizmoTransforms.Move(
                    drag.Start,
                    drag.Axis,
                    drag.StartAxisDistance,
                    distance,
                    drag.StartMatrix);
                break;

            case GizmoMode.Rotate:
                if (!GizmoMath.TryIntersectPlane(ray, drag.Start.Position, Vector3.UnitY, out Vector3 hit))
                {
                    return;
                }

                float angle = GizmoMath.AngleDegrees(drag.Start.Position, hit);
                matrix = GizmoTransforms.Rotate(drag.Start, drag.StartAngle, angle, drag.StartMatrix);
                break;

            case GizmoMode.Scale:
                if (!GizmoMath.TryClosestPointOnAxis(
                        drag.Start.Position,
                        axis,
                        ray,
                        out float scaleDistance))
                {
                    return;
                }

                matrix = GizmoTransforms.Scale(
                    drag.Start,
                    drag.Axis,
                    drag.StartAxisDistance,
                    scaleDistance,
                    drag.StartMatrix);
                break;

            default:
                return;
        }

        if (_editSession.GetMatrix(drag.InstanceIndex) != matrix)
        {
            _editSession.SetMatrix(drag.InstanceIndex, matrix);
            drag.Changed = true;
        }
    }

    private GizmoAxis HitTestGizmo(
        FreeFlyCamera camera,
        SceneTransform transform,
        Matrix4x4 viewProjection,
        Vector2 mouse,
        Vector2 viewSize)
    {
        if (!GizmoMath.Project(transform.Position, viewProjection, viewSize, out Vector2 pivot))
        {
            return GizmoAxis.None;
        }

        if (_tool == GizmoMode.Rotate)
        {
            return HitTestRotationRing(camera, transform.Position, viewProjection, viewSize, mouse);
        }

        GizmoAxis[] axes = _tool == GizmoMode.Scale
            ? [GizmoAxis.Xz, GizmoAxis.Y]
            : [GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z];
        float length = AxisLength(camera, transform.Position, viewSize.Y);
        GizmoAxis best = GizmoAxis.None;
        float bestDistance = GizmoHitRadius;

        foreach (GizmoAxis axis in axes)
        {
            Vector3 endWorld = transform.Position + (GizmoMath.AxisDirection(axis) * length);
            if (!GizmoMath.Project(endWorld, viewProjection, viewSize, out Vector2 end))
            {
                continue;
            }

            float distance = GizmoMath.DistanceToSegment(mouse, pivot, end);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = axis;
            }
        }

        return best;
    }

    private static GizmoAxis HitTestRotationRing(
        FreeFlyCamera camera,
        Vector3 pivot,
        Matrix4x4 viewProjection,
        Vector2 viewSize,
        Vector2 mouse)
    {
        float radius = RingRadius(camera, pivot, viewSize.Y);
        bool hit = false;
        Vector2? previous = null;

        for (int i = 0; i <= RotationRingSegments; i++)
        {
            float angle = i / (float)RotationRingSegments * MathF.Tau;
            var world = pivot + new Vector3(
                MathF.Cos(angle) * radius,
                0f,
                MathF.Sin(angle) * radius);
            if (!GizmoMath.Project(world, viewProjection, viewSize, out Vector2 point))
            {
                previous = null;
                continue;
            }

            if (previous is Vector2 start
                && GizmoMath.DistanceToSegment(mouse, start, point) < GizmoHitRadius)
            {
                hit = true;
            }

            previous = point;
        }

        return hit ? GizmoAxis.Y : GizmoAxis.None;
    }

    private void DrawSceneOverlay(
        FreeFlyCamera camera,
        Matrix4x4 viewProjection,
        Vector2 imageMin,
        Vector2 imageSize,
        bool hovered,
        Vector2 localMouse)
    {
        ImDrawListPtr draw = ImGui.GetWindowDrawList();
        if (TryGetSelectionBounds(out BoundingBox bounds))
        {
            DrawBoundsWireframe(draw, viewProjection, imageMin, imageSize, bounds, EditorTheme.Accent);
        }

        if (_tool == GizmoMode.Place)
        {
            DrawPlacementMarker(draw, viewProjection, imageMin, imageSize);
            return;
        }

        if (_tool == GizmoMode.Select || !TryGetSelectedInstance(out int index))
        {
            return;
        }

        SceneTransform transform = SceneGeometry.Decompose(_editSession.GetMatrix(index));
        GizmoAxis active = _drag?.Axis ?? _hoveredAxis;
        if (_tool == GizmoMode.Rotate)
        {
            DrawRotationRing(draw, camera, transform.Position, viewProjection, imageMin, imageSize, active == GizmoAxis.Y);
            DrawPivot(draw, transform.Position, viewProjection, imageMin, imageSize, EditorTheme.AxisY);
            return;
        }

        float length = AxisLength(camera, transform.Position, imageSize.Y);
        GizmoAxis[] axes = _tool == GizmoMode.Scale
            ? [GizmoAxis.Xz, GizmoAxis.Y]
            : [GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z];
        Vector2? pivotScreen = null;

        foreach (GizmoAxis axis in axes)
        {
            Vector3 endWorld = transform.Position + (GizmoMath.AxisDirection(axis) * length);
            if (!GizmoMath.Project(transform.Position, viewProjection, imageSize, out Vector2 pivot)
                || !GizmoMath.Project(endWorld, viewProjection, imageSize, out Vector2 end))
            {
                continue;
            }

            pivotScreen ??= pivot;
            Vector4 color = AxisColor(axis);
            uint packed = EditorIcons.ToColor(color);
            bool highlighted = axis == active;
            Vector2 start = imageMin + pivot;
            Vector2 finish = imageMin + end;
            draw.AddLine(
                start,
                finish,
                packed,
                highlighted ? GizmoActiveThickness : GizmoAxisThickness);

            if (_tool == GizmoMode.Move)
            {
                DrawArrowHead(draw, start, finish, packed, highlighted);
            }
            else
            {
                float half = highlighted ? 5f : 4f;
                draw.AddRectFilled(finish - new Vector2(half), finish + new Vector2(half), packed);
            }

            draw.AddText(finish + new Vector2(5f, -8f), packed, AxisLabel(axis));
        }

        if (pivotScreen is Vector2 pivotPixel)
        {
            draw.AddCircleFilled(imageMin + pivotPixel, 3f, EditorIcons.ToColor(EditorTheme.TextPrimary));
        }
    }

    private static void DrawRotationRing(
        ImDrawListPtr draw,
        FreeFlyCamera camera,
        Vector3 pivot,
        Matrix4x4 viewProjection,
        Vector2 imageMin,
        Vector2 imageSize,
        bool highlighted)
    {
        float radius = RingRadius(camera, pivot, imageSize.Y);
        uint color = EditorIcons.ToColor(EditorTheme.AxisY);
        float thickness = highlighted ? GizmoActiveThickness : GizmoAxisThickness;
        Vector2? previous = null;

        for (int i = 0; i <= RotationRingSegments; i++)
        {
            float angle = i / (float)RotationRingSegments * MathF.Tau;
            var world = pivot + new Vector3(
                MathF.Cos(angle) * radius,
                0f,
                MathF.Sin(angle) * radius);
            if (!GizmoMath.Project(world, viewProjection, imageSize, out Vector2 point))
            {
                previous = null;
                continue;
            }

            Vector2 current = imageMin + point;
            if (previous is Vector2 start)
            {
                draw.AddLine(start, current, color, thickness);
            }

            previous = current;
        }
    }

    private static void DrawPivot(
        ImDrawListPtr draw,
        Vector3 pivot,
        Matrix4x4 viewProjection,
        Vector2 imageMin,
        Vector2 imageSize,
        Vector4 color)
    {
        if (GizmoMath.Project(pivot, viewProjection, imageSize, out Vector2 screen))
        {
            draw.AddCircleFilled(imageMin + screen, 3f, EditorIcons.ToColor(color));
        }
    }

    private static void DrawArrowHead(
        ImDrawListPtr draw,
        Vector2 start,
        Vector2 end,
        uint color,
        bool highlighted)
    {
        Vector2 direction = end - start;
        if (direction.LengthSquared() < ParallelEpsilonSquared)
        {
            return;
        }

        direction = Vector2.Normalize(direction);
        var normal = new Vector2(-direction.Y, direction.X);
        float size = highlighted ? 10f : 8f;
        Vector2 tip = end + (direction * 2f);
        draw.AddTriangleFilled(
            tip,
            end - (direction * size) + (normal * size * 0.5f),
            end - (direction * size) - (normal * size * 0.5f),
            color);
    }

    private const float ParallelEpsilonSquared = 1e-6f;

    private void DrawPlacementMarker(
        ImDrawListPtr draw,
        Matrix4x4 viewProjection,
        Vector2 imageMin,
        Vector2 imageSize)
    {
        if (_placePreviewMatrix is not Matrix4x4RowMajor matrix
            || (uint)_placeModelIndex >= (uint)_scene.Plants.Count)
        {
            return;
        }

        BoundingBox bounds = SceneGeometry.OfInstance(_scene.Plants[_placeModelIndex], matrix);
        DrawBoundsWireframe(draw, viewProjection, imageMin, imageSize, bounds, EditorTheme.Accent);
    }

    private static void DrawBoundsWireframe(
        ImDrawListPtr draw,
        Matrix4x4 viewProjection,
        Vector2 imageMin,
        Vector2 imageSize,
        BoundingBox bounds,
        Vector4 color)
    {
        Span<Vector3> corners = stackalloc Vector3[8];
        Span<Vector2> projected = stackalloc Vector2[8];
        Span<bool> visible = stackalloc bool[8];
        for (int corner = 0; corner < 8; corner++)
        {
            corners[corner] = new Vector3(
                (corner & 1) == 0 ? bounds.Min.X : bounds.Max.X,
                (corner & 2) == 0 ? bounds.Min.Y : bounds.Max.Y,
                (corner & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
            visible[corner] = GizmoMath.Project(
                corners[corner],
                viewProjection,
                imageSize,
                out projected[corner]);
        }

        uint packed = EditorIcons.ToColor(color);
        for (int edge = 0; edge < BoundsEdges.GetLength(0); edge++)
        {
            int first = BoundsEdges[edge, 0];
            int second = BoundsEdges[edge, 1];
            if (visible[first] && visible[second])
            {
                draw.AddLine(imageMin + projected[first], imageMin + projected[second], packed, 1.4f);
            }
        }
    }

    private static float AxisLength(FreeFlyCamera camera, Vector3 pivot, float viewHeight) =>
        Math.Clamp(
            GizmoMath.WorldLengthForPixels(
                GizmoTargetPixels,
                camera.Position,
                camera.Forward,
                pivot,
                FreeFlyCamera.FieldOfViewRadians,
                viewHeight),
            MinAxisWorldLength,
            MaxAxisWorldLength);

    private static float RingRadius(FreeFlyCamera camera, Vector3 pivot, float viewHeight) =>
        AxisLength(camera, pivot, viewHeight) * RotationRingFactor;

    private static Vector4 AxisColor(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => EditorTheme.AxisX,
        GizmoAxis.Y => EditorTheme.AxisY,
        GizmoAxis.Z => EditorTheme.AxisZ,
        _ => EditorTheme.AxisXz,
    };

    private static string AxisLabel(GizmoAxis axis) => axis switch
    {
        GizmoAxis.Y => "Y",
        GizmoAxis.Z => "Z",
        GizmoAxis.Xz => "XZ",
        _ => "X",
    };

    private sealed class GizmoDrag
    {
        public required int InstanceIndex { get; init; }

        public required GizmoMode Mode { get; init; }

        public required GizmoAxis Axis { get; init; }

        public required SceneTransform Start { get; init; }

        public required Matrix4x4RowMajor StartMatrix { get; init; }

        public required float StartAxisDistance { get; init; }

        public required float StartAngle { get; init; }

        public bool Changed { get; set; }
    }
}
