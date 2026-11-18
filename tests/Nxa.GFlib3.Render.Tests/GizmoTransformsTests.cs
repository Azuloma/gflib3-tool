// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class GizmoTransformsTests
{
    private static readonly Matrix4x4RowMajor Observed = new(
        new Vector4f(-0.38041f, 0f, -0.97888f, 0f),
        new Vector4f(0f, 1.06524f, 0f, 0f),
        new Vector4f(0.97888f, 0f, -0.38041f, 0f),
        new Vector4f(445.1664f, 60.78404f, 650.67f, 1f));

    [Fact]
    public void ZeroMoveKeepsRawFallbackMatrix()
    {
        SceneTransform start = SceneGeometry.Decompose(Observed);

        Matrix4x4RowMajor result = GizmoTransforms.Move(
            start,
            GizmoAxis.X,
            12f,
            12f,
            Observed);

        Assert.Equal(Observed, result);
    }

    [Fact]
    public void ZeroRotationKeepsRawFallbackMatrix()
    {
        SceneTransform start = SceneGeometry.Decompose(Observed);

        Matrix4x4RowMajor result = GizmoTransforms.Rotate(start, 33f, 33f, Observed);

        Assert.Equal(Observed, result);
    }

    [Fact]
    public void ZeroScaleKeepsRawFallbackMatrix()
    {
        SceneTransform start = SceneGeometry.Decompose(Observed);

        Matrix4x4RowMajor result = GizmoTransforms.Scale(
            start,
            GizmoAxis.Xz,
            12f,
            12f,
            Observed);

        Assert.Equal(Observed, result);
    }

    [Fact]
    public void YScaleLeavesXzScaleUnchanged()
    {
        var start = new SceneTransform(
            new Vector3(1f, 2f, 3f),
            25f,
            new Vector3(2f, 3f, 2f));

        Matrix4x4RowMajor result = GizmoTransforms.Scale(
            start,
            GizmoAxis.Y,
            10f,
            20f,
            SceneGeometry.Compose(start));

        SceneTransform scaled = SceneGeometry.Decompose(result);
        Assert.Equal(2f, scaled.Scale.X, 3);
        Assert.Equal(6f, scaled.Scale.Y, 3);
    }

    [Fact]
    public void XzScaleLeavesYScaleUnchanged()
    {
        var start = new SceneTransform(
            new Vector3(1f, 2f, 3f),
            25f,
            new Vector3(2f, 3f, 2f));

        Matrix4x4RowMajor result = GizmoTransforms.Scale(
            start,
            GizmoAxis.Xz,
            10f,
            5f,
            SceneGeometry.Compose(start));

        SceneTransform scaled = SceneGeometry.Decompose(result);
        Assert.Equal(1f, scaled.Scale.X, 3);
        Assert.Equal(3f, scaled.Scale.Y, 3);
    }

    [Fact]
    public void MoveAppliesAxisDelta()
    {
        var start = new SceneTransform(Vector3.Zero, 0f, Vector3.One);

        Matrix4x4RowMajor result = GizmoTransforms.Move(
            start,
            GizmoAxis.X,
            4f,
            9f,
            SceneGeometry.Compose(start));

        Assert.Equal(5f, result.Row3.X, 3);
        Assert.Equal(0f, result.Row3.Y, 3);
        Assert.Equal(0f, result.Row3.Z, 3);
    }

    [Fact]
    public void RotationDecreasesYawForPositiveXzAngle()
    {
        var start = new SceneTransform(Vector3.Zero, 0f, Vector3.One);

        Matrix4x4RowMajor result = GizmoTransforms.Rotate(
            start,
            0f,
            30f,
            SceneGeometry.Compose(start));

        Assert.Equal(-30f, SceneGeometry.Decompose(result).YawDegrees, 3);
    }
}
