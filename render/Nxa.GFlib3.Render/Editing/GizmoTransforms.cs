// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Editing;

/// <summary>
/// Converts drag deltas into `.trins` matrices. A negligible delta returns the
/// fallback matrix unchanged so press/release without motion records no edit.
/// </summary>
public static class GizmoTransforms
{
    public const float MinMoveDistance = 0.01f;
    public const float MinRotationDegrees = 0.01f;
    public const float MinScaleDelta = 0.0001f;

    public static Matrix4x4RowMajor Move(
        SceneTransform start,
        GizmoAxis axis,
        float startDistance,
        float distance,
        Matrix4x4RowMajor fallback)
    {
        float delta = distance - startDistance;
        if (!float.IsFinite(delta) || MathF.Abs(delta) < MinMoveDistance)
        {
            return fallback;
        }

        Vector3 position = start.Position + (GizmoMath.AxisDirection(axis) * delta);
        return SceneGeometry.Compose(start with { Position = position });
    }

    public static Matrix4x4RowMajor Rotate(
        SceneTransform start,
        float startAngle,
        float angle,
        Matrix4x4RowMajor fallback)
    {
        float delta = GizmoMath.ShortestAngleDelta(startAngle, angle);
        if (!float.IsFinite(delta) || MathF.Abs(delta) < MinRotationDegrees)
        {
            return fallback;
        }

        float yaw = SceneGeometry.NormalizeDegrees(start.YawDegrees - delta);
        return SceneGeometry.Compose(start with { YawDegrees = yaw });
    }

    public static Matrix4x4RowMajor Scale(
        SceneTransform start,
        GizmoAxis axis,
        float startDistance,
        float distance,
        Matrix4x4RowMajor fallback)
    {
        if (MathF.Abs(startDistance) < MinMoveDistance)
        {
            return fallback;
        }

        float ratio = distance / startDistance;
        if (!float.IsFinite(ratio) || MathF.Abs(ratio - 1f) < MinScaleDelta)
        {
            return fallback;
        }

        float xzRatio = axis == GizmoAxis.Xz ? ratio : 1f;
        float yRatio = axis == GizmoAxis.Y ? ratio : 1f;
        float scaleXz = GizmoMath.ClampScale(start.Scale.X * xzRatio);
        float scaleY = GizmoMath.ClampScale(start.Scale.Y * yRatio);
        var scaled = new SceneTransform(
            start.Position,
            start.YawDegrees,
            new Vector3(scaleXz, scaleY, scaleXz));
        return SceneGeometry.Compose(scaled);
    }
}
