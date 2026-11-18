// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class GizmoMathTests
{
    [Fact]
    public void FindsClosestPointOnAxis()
    {
        bool found = GizmoMath.TryClosestPointOnAxis(
            Vector3.Zero,
            Vector3.UnitX,
            new SceneRay(new Vector3(5f, 5f, 10f), -Vector3.UnitZ),
            out float distance);

        Assert.True(found);
        Assert.Equal(5f, distance, 3);
    }

    [Fact]
    public void ParallelAxisHasNoClosestPoint()
    {
        bool found = GizmoMath.TryClosestPointOnAxis(
            Vector3.Zero,
            Vector3.UnitX,
            new SceneRay(new Vector3(0f, 1f, 0f), Vector3.UnitX),
            out _);

        Assert.False(found);
    }

    [Fact]
    public void IntersectsRotationPlane()
    {
        bool found = GizmoMath.TryIntersectPlane(
            new SceneRay(new Vector3(0f, 10f, 0f), -Vector3.UnitY),
            Vector3.Zero,
            Vector3.UnitY,
            out Vector3 hit);

        Assert.True(found);
        Assert.Equal(0f, hit.X, 3);
        Assert.Equal(0f, hit.Y, 3);
        Assert.Equal(0f, hit.Z, 3);
    }

    [Fact]
    public void ProjectsWorldPointToViewCenter()
    {
        bool projected = GizmoMath.Project(
            Vector3.Zero,
            Matrix4x4.Identity,
            new Vector2(100f, 50f),
            out Vector2 screen);

        Assert.True(projected);
        Assert.Equal(50f, screen.X, 3);
        Assert.Equal(25f, screen.Y, 3);
    }

    [Fact]
    public void RejectsPointBehindProjection()
    {
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 3f,
            1f,
            0.1f,
            100f);

        bool projected = GizmoMath.Project(
            new Vector3(0f, 0f, 5f),
            projection,
            new Vector2(100f, 100f),
            out _);

        Assert.False(projected);
    }

    [Fact]
    public void MeasuresDistanceToSegment()
    {
        float distance = GizmoMath.DistanceToSegment(
            new Vector2(5f, 5f),
            new Vector2(0f, 0f),
            new Vector2(10f, 0f));

        Assert.Equal(5f, distance, 3);
    }

    [Fact]
    public void MeasuresAngleInXzPlane()
    {
        Assert.Equal(0f, GizmoMath.AngleDegrees(Vector3.Zero, Vector3.UnitX), 3);
        Assert.Equal(90f, GizmoMath.AngleDegrees(Vector3.Zero, Vector3.UnitZ), 3);
        Assert.Equal(180f, MathF.Abs(GizmoMath.AngleDegrees(Vector3.Zero, -Vector3.UnitX)), 3);
    }

    [Fact]
    public void ShortestAngleDeltaWraps()
    {
        Assert.Equal(10f, GizmoMath.ShortestAngleDelta(350f, 360f), 3);
        Assert.Equal(-10f, GizmoMath.ShortestAngleDelta(10f, 0f), 3);
        Assert.Equal(0f, GizmoMath.ShortestAngleDelta(0f, 360f), 3);
    }

    [Fact]
    public void WorldLengthForPixelsScalesWithViewDepth()
    {
        float near = GizmoMath.WorldLengthForPixels(
            100f,
            Vector3.Zero,
            Vector3.UnitZ,
            new Vector3(0f, 0f, 10f),
            MathF.PI / 3f,
            720f);
        float far = GizmoMath.WorldLengthForPixels(
            100f,
            Vector3.Zero,
            Vector3.UnitZ,
            new Vector3(0f, 0f, 20f),
            MathF.PI / 3f,
            720f);

        Assert.True(near > 0f);
        Assert.Equal(2f, far / near, 3);
    }

    [Fact]
    public void ClampsScale()
    {
        Assert.Equal(GizmoMath.MinScale, GizmoMath.ClampScale(0f));
        Assert.Equal(GizmoMath.MaxScale, GizmoMath.ClampScale(1000f));
        Assert.Equal(2f, GizmoMath.ClampScale(2f));
    }
}
