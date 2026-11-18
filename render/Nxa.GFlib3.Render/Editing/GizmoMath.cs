// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Editing;

public enum GizmoMode
{
    Select,
    Move,
    Rotate,
    Scale,
}

public enum GizmoAxis
{
    None,
    X,
    Y,
    Z,
    Xz,
}

/// <summary>ImGui-independent projection, hit-test, and drag math for the gizmo.</summary>
public static class GizmoMath
{
    public const float MinScale = 0.01f;
    public const float MaxScale = 100f;
    public const float ParallelEpsilon = 1e-4f;
    public const float MinViewSize = 1e-3f;
    public const float MinCameraDepth = 0.1f;

    private static readonly Vector3 AxisXz = Vector3.Normalize(new Vector3(1f, 0f, 1f));

    public static Vector3 AxisDirection(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => Vector3.UnitX,
        GizmoAxis.Y => Vector3.UnitY,
        GizmoAxis.Z => Vector3.UnitZ,
        GizmoAxis.Xz => AxisXz,
        _ => Vector3.Zero,
    };

    /// <summary>Projects a world point to pixels relative to the view rectangle.</summary>
    public static bool Project(
        Vector3 world,
        Matrix4x4 viewProjection,
        Vector2 viewSize,
        out Vector2 screen)
    {
        screen = default;
        if (viewSize.X < MinViewSize || viewSize.Y < MinViewSize)
        {
            return false;
        }

        Vector4 clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);
        if (clip.W <= ParallelEpsilon)
        {
            return false;
        }

        float x = clip.X / clip.W;
        float y = clip.Y / clip.W;
        screen = new Vector2(
            ((x * 0.5f) + 0.5f) * viewSize.X,
            (0.5f - (y * 0.5f)) * viewSize.Y);
        return true;
    }

    /// <summary>
    /// World length that projects to roughly <paramref name="pixels"/> at the
    /// pivot's view depth, so handles keep a stable size as the panel resizes.
    /// </summary>
    public static float WorldLengthForPixels(
        float pixels,
        Vector3 cameraPosition,
        Vector3 cameraForward,
        Vector3 pivot,
        float verticalFieldOfView,
        float viewHeight)
    {
        if (pixels <= 0f || viewHeight < 1f)
        {
            return 0f;
        }

        float depth = MathF.Max(Vector3.Dot(pivot - cameraPosition, cameraForward), MinCameraDepth);
        float worldPerPixel = 2f * depth * MathF.Tan(verticalFieldOfView * 0.5f) / viewHeight;
        return pixels * worldPerPixel;
    }

    public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)    {
        Vector2 segment = end - start;
        float lengthSquared = segment.LengthSquared();
        if (lengthSquared < ParallelEpsilon)
        {
            return Vector2.Distance(point, start);
        }

        float t = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.Distance(point, start + (segment * t));
    }

    /// <summary>
    /// Closest point on the axis line to the mouse ray. The returned distance is
    /// measured in world units from <paramref name="origin"/> along the axis.
    /// </summary>
    public static bool TryClosestPointOnAxis(
        Vector3 origin,
        Vector3 axis,
        SceneRay ray,
        out float distance)
    {
        distance = 0f;
        float aa = axis.LengthSquared();
        float bb = Vector3.Dot(axis, ray.Direction);
        float cc = ray.Direction.LengthSquared();
        float denominator = (aa * cc) - (bb * bb);
        if (MathF.Abs(denominator) < ParallelEpsilon)
        {
            return false;
        }

        Vector3 offset = origin - ray.Origin;
        float dd = Vector3.Dot(axis, offset);
        float ee = Vector3.Dot(ray.Direction, offset);
        distance = ((bb * ee) - (cc * dd)) / denominator;
        return float.IsFinite(distance);
    }

    public static bool TryIntersectPlane(
        SceneRay ray,
        Vector3 point,
        Vector3 normal,
        out Vector3 hit)
    {
        hit = default;
        float denominator = Vector3.Dot(normal, ray.Direction);
        if (MathF.Abs(denominator) < ParallelEpsilon)
        {
            return false;
        }

        float t = Vector3.Dot(point - ray.Origin, normal) / denominator;
        if (!float.IsFinite(t) || t < 0f)
        {
            return false;
        }

        hit = ray.Origin + (ray.Direction * t);
        return true;
    }

    /// <summary>Angle in the XZ plane, degrees, measured from +X toward +Z.</summary>
    public static float AngleDegrees(Vector3 center, Vector3 point)
    {
        float radians = MathF.Atan2(point.Z - center.Z, point.X - center.X);
        return radians * (180f / MathF.PI);
    }

    public static float ClampScale(float value) => Math.Clamp(value, MinScale, MaxScale);

    public static float ShortestAngleDelta(float fromDegrees, float toDegrees)
    {
        float delta = (toDegrees - fromDegrees) % 360f;
        if (delta > 180f)
        {
            delta -= 360f;
        }
        else if (delta <= -180f)
        {
            delta += 360f;
        }

        return delta;
    }
}
