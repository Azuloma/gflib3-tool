// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class SceneGeometryTests
{
    [Fact]
    public void DecomposesRowMajorTrinsMatrix()
    {
        var matrix = new Matrix4x4RowMajor(
            new Vector4f(-0.38041f, 0f, -0.97888f, 0f),
            new Vector4f(0f, 1.06524f, 0f, 0f),
            new Vector4f(0.97888f, 0f, -0.38041f, 0f),
            new Vector4f(445.1664f, 60.78404f, 650.67f, 1f));

        SceneTransform transform = SceneGeometry.Decompose(matrix);

        Assert.Equal(445.1664f, transform.Position.X, 3);
        Assert.Equal(60.78404f, transform.Position.Y, 3);
        Assert.Equal(650.67f, transform.Position.Z, 3);
        Assert.Equal(111.24f, transform.YawDegrees, 1);
        Assert.Equal(1.0502f, transform.Scale.X, 3);
        Assert.Equal(1.06524f, transform.Scale.Y, 3);
    }

    [Fact]
    public void DecomposesComposedSceneMatrix()
    {
        Matrix4x4 matrix = Matrix4x4.CreateScale(2f, 3f, 2f)
            * Matrix4x4.CreateRotationY(30f * (MathF.PI / 180f))
            * Matrix4x4.CreateTranslation(1f, 2f, 3f);

        SceneTransform transform = SceneGeometry.Decompose(matrix);

        Assert.Equal(1f, transform.Position.X, 3);
        Assert.Equal(2f, transform.Position.Y, 3);
        Assert.Equal(3f, transform.Position.Z, 3);
        Assert.Equal(30f, transform.YawDegrees, 2);
        Assert.Equal(2f, transform.Scale.X, 3);
        Assert.Equal(3f, transform.Scale.Y, 3);
        Assert.Equal(2f, transform.Scale.Z, 3);
    }

    [Fact]
    public void RoundTripsRowMajorMatrix()
    {
        var matrix = new Matrix4x4RowMajor(
            new Vector4f(0f, 0f, -2f, 0f),
            new Vector4f(0f, 3f, 0f, 0f),
            new Vector4f(2f, 0f, 0f, 0f),
            new Vector4f(4f, 5f, 6f, 1f));

        SceneTransform transform = SceneGeometry.Decompose(matrix);
        Matrix4x4 converted = SceneGeometry.ToMatrix(matrix);

        Assert.Equal(4f, converted.M41, 3);
        Assert.Equal(5f, converted.M42, 3);
        Assert.Equal(6f, converted.M43, 3);
        Assert.Equal(90f, transform.YawDegrees, 2);
        Assert.Equal(2f, transform.Scale.X, 3);
        Assert.Equal(3f, transform.Scale.Y, 3);
    }

    [Fact]
    public void TransformsBoundsWithTranslationAndYaw()
    {
        var bounds = new BoundingBox(new Vector3f(-1f, 0f, -1f), new Vector3f(1f, 2f, 1f));
        Matrix4x4 matrix = Matrix4x4.CreateRotationY(MathF.PI / 2f) * Matrix4x4.CreateTranslation(10f, 0f, 0f);

        BoundingBox world = SceneGeometry.TransformBounds(bounds, matrix);

        Assert.Equal(9f, world.Min.X, 3);
        Assert.Equal(11f, world.Max.X, 3);
        Assert.Equal(-1f, world.Min.Z, 3);
        Assert.Equal(1f, world.Max.Z, 3);
        Assert.Equal(0f, world.Min.Y, 3);
        Assert.Equal(2f, world.Max.Y, 3);
    }

    [Fact]
    public void UnionsBounds()
    {
        var first = new BoundingBox(new Vector3f(-2f, 0f, -2f), new Vector3f(0f, 1f, 0f));
        var second = new BoundingBox(new Vector3f(1f, -1f, 1f), new Vector3f(3f, 2f, 4f));

        BoundingBox union = SceneGeometry.Union(first, second);

        Assert.Equal(-2f, union.Min.X, 3);
        Assert.Equal(-1f, union.Min.Y, 3);
        Assert.Equal(-2f, union.Min.Z, 3);
        Assert.Equal(3f, union.Max.X, 3);
        Assert.Equal(2f, union.Max.Y, 3);
        Assert.Equal(4f, union.Max.Z, 3);
    }
}
