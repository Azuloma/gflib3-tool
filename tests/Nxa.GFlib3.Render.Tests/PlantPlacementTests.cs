// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class PlantPlacementTests
{
    [Fact]
    public void DuplicateOffsetsXzAndPreservesRotationRowsExactly()
    {
        Matrix4x4RowMajor source = SceneGeometry.Compose(
            new SceneTransform(new Vector3(10f, 5f, 20f), 30f, new Vector3(2f, 1.5f, 2f)));

        Matrix4x4RowMajor duplicate = PlantPlacement.Duplicate(source, terrain: null);
        SceneTransform transform = SceneGeometry.Decompose(duplicate);

        Assert.Equal(12f, transform.Position.X, 3);
        Assert.Equal(5f, transform.Position.Y, 3);
        Assert.Equal(22f, transform.Position.Z, 3);
        Assert.Equal(30f, transform.YawDegrees, 3);
        Assert.Equal(2f, transform.Scale.X, 3);
        Assert.Equal(1.5f, transform.Scale.Y, 3);
        Assert.Equal(source.Row0, duplicate.Row0);
        Assert.Equal(source.Row1, duplicate.Row1);
        Assert.Equal(source.Row2, duplicate.Row2);
        Assert.Equal(source.Row3.W, duplicate.Row3.W);
    }

    [Fact]
    public void AtTerrainPointUsesUnitScaleAndZeroYaw()
    {
        Matrix4x4RowMajor matrix = PlantPlacement.AtTerrainPoint(new Vector3(100f, 60.5f, 200f));
        SceneTransform transform = SceneGeometry.Decompose(matrix);

        Assert.Equal(new Vector3(100f, 60.5f, 200f), transform.Position);
        Assert.Equal(0f, transform.YawDegrees);
        Assert.Equal(Vector3.One, transform.Scale);
    }

    [Fact]
    public void DuplicateRejectsNonFiniteSource()
    {
        var source = new Matrix4x4RowMajor(
            new Vector4f(1f, 0f, 0f, 0f),
            new Vector4f(0f, float.NaN, 0f, 0f),
            new Vector4f(0f, 0f, 1f, 0f),
            new Vector4f(0f, 0f, 0f, 1f));

        Assert.False(PlantPlacement.IsFinite(source));
        Assert.Throws<ArgumentException>(() => PlantPlacement.Duplicate(source, terrain: null));
    }

    [Fact]
    public void DuplicateSnapsInsideTerrainAndKeepsYOutsideWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, "ha_area00", "ha_area00", null);
        TerrainSurface surface = Assert.IsType<TerrainSurface>(scene.TerrainSurface);

        Matrix4x4RowMajor inside = SceneGeometry.Compose(
            new SceneTransform(new Vector3(445f, 60.78f, 650f), 0f, Vector3.One));
        Matrix4x4RowMajor duplicate = PlantPlacement.Duplicate(inside, surface);
        Assert.Equal(447f, duplicate.Row3.X, 3);
        Assert.Equal(652f, duplicate.Row3.Z, 3);
        Assert.True(surface.TryGetWorldHeight(447f, 652f, out float expected));
        Assert.Equal(expected, duplicate.Row3.Y, 3);

        Matrix4x4RowMajor edge = SceneGeometry.Compose(
            new SceneTransform(new Vector3(1022f, 3f, 1022f), 0f, Vector3.One));
        Matrix4x4RowMajor edgeDuplicate = PlantPlacement.Duplicate(edge, surface);
        Assert.True(surface.TryGetWorldHeight(1024f, 1024f, out float edgeHeight));
        Assert.Equal(edgeHeight, edgeDuplicate.Row3.Y, 3);

        Matrix4x4RowMajor outside = SceneGeometry.Compose(
            new SceneTransform(new Vector3(-10f, 5f, -10f), 0f, Vector3.One));
        Matrix4x4RowMajor outsideDuplicate = PlantPlacement.Duplicate(outside, surface);
        Assert.Equal(5f, outsideDuplicate.Row3.Y, 3);
    }
}
