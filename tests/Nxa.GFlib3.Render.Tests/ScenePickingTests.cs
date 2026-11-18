// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Trinity;
using Nxa.GFlib3.Render.Camera;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class ScenePickingTests
{
    [Fact]
    public void RayFromViewCenterPointsForward()
    {
        bool created = ScenePicking.TryScreenRay(
            new Vector2(50f, 25f),
            new Vector2(100f, 50f),
            Matrix4x4.Identity,
            out SceneRay ray);

        Assert.True(created);
        Assert.Equal(0f, ray.Origin.X, 3);
        Assert.Equal(0f, ray.Origin.Y, 3);
        Assert.Equal(-1f, ray.Origin.Z, 3);
        Assert.Equal(0f, ray.Direction.X, 3);
        Assert.Equal(0f, ray.Direction.Y, 3);
        Assert.Equal(1f, ray.Direction.Z, 3);
    }

    [Fact]
    public void RejectsDegenerateViewSize()
    {
        bool created = ScenePicking.TryScreenRay(
            Vector2.Zero,
            new Vector2(0f, 0f),
            Matrix4x4.Identity,
            out _);

        Assert.False(created);
    }

    [Fact]
    public void RejectsZeroDirectionRay()
    {
        bool hit = ScenePicking.IntersectsBounds(
            Vector3.Zero,
            Vector3.Zero,
            new BoundingBox(new Vector3f(-1f, -1f, -1f), new Vector3f(1f, 1f, 1f)),
            out _);

        Assert.False(hit);
    }

    [Fact]
    public void CameraProjectionAndRayRoundTrip()
    {
        var camera = new FreeFlyCamera
        {
            Position = new Vector3(4f, 6f, 18f),
            Yaw = MathF.PI,
            Pitch = -0.2f,
        };
        var viewSize = new Vector2(1280f, 720f);
        Matrix4x4 viewProjection = camera.View
            * FreeFlyCamera.Projection(viewSize.X / viewSize.Y, 5000f);
        Assert.True(Matrix4x4.Invert(viewProjection, out Matrix4x4 inverse));
        var pivot = new Vector3(3f, 1f, 5f);

        Assert.True(GizmoMath.Project(pivot, viewProjection, viewSize, out Vector2 screen));
        Assert.True(ScenePicking.TryScreenRay(screen, viewSize, inverse, out SceneRay ray));

        Vector3 offset = pivot - ray.Origin;
        float distance = Vector3.Cross(offset, ray.Direction).Length();
        Assert.True(distance < 0.01f, $"Ray missed the pivot by {distance:0.###}.");
    }

    [Fact]
    public void PicksNearestInstanceAlongRay()
    {
        FieldScene scene = CreateScene();
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 0f)));
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 5f)));

        bool hit = ScenePicking.TryPickPlant(
            scene,
            new SceneRay(new Vector3(0f, 1f, -10f), Vector3.UnitZ),
            out int index);

        Assert.True(hit);
        Assert.Equal(0, index);
    }

    [Fact]
    public void PicksNextInstanceWhenNearOneIsBehindRay()
    {
        FieldScene scene = CreateScene();
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 0f)));
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 5f)));

        bool hit = ScenePicking.TryPickPlant(
            scene,
            new SceneRay(new Vector3(0f, 1f, 3f), Vector3.UnitZ),
            out int index);

        Assert.True(hit);
        Assert.Equal(1, index);
    }

    [Fact]
    public void PicksTranslatedInstance()
    {
        FieldScene scene = CreateScene();
        scene.Instances.Add(new PlantInstance(0, At(10f, 0f, 0f)));

        bool hit = ScenePicking.TryPickPlant(
            scene,
            new SceneRay(new Vector3(0f, 1f, 0f), Vector3.UnitX),
            out int index);

        Assert.True(hit);
        Assert.Equal(0, index);
    }

    [Fact]
    public void MissesWhenRayPassesOutsideBounds()
    {
        FieldScene scene = CreateScene();
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 0f)));

        bool hit = ScenePicking.TryPickPlant(
            scene,
            new SceneRay(new Vector3(0f, 10f, -10f), Vector3.UnitZ),
            out int index);

        Assert.False(hit);
        Assert.Equal(-1, index);
    }

    [Fact]
    public void SkipsModelsWithoutMeshes()
    {
        var scene = new FieldScene
        {
            AreaName = "test",
            ModelPackName = "test",
            DumpRoot = ".",
        };
        scene.Plants.Add(new PlantModel { Name = "empty" });
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 0f)));

        bool hit = ScenePicking.TryPickPlant(
            scene,
            new SceneRay(new Vector3(0f, 1f, -10f), Vector3.UnitZ),
            out _);

        Assert.False(hit);
    }

    [Fact]
    public void BoundsBehindRayDoNotHit()
    {
        var bounds = new BoundingBox(new Vector3f(-1f, 0f, -1f), new Vector3f(1f, 2f, 1f));

        bool hit = ScenePicking.IntersectsBounds(
            new Vector3(0f, 1f, 10f),
            Vector3.UnitZ,
            bounds,
            out _);

        Assert.False(hit);
    }

    [Fact]
    public void PicksRotatedAndNonUniformlyScaledInstance()
    {
        FieldScene scene = CreateScene();
        scene.Instances.Add(new PlantInstance(
            0,
            SceneGeometry.Compose(new SceneTransform(
                new Vector3(10f, 0f, 0f),
                90f,
                new Vector3(2f, 3f, 2f)))));

        Assert.True(ScenePicking.TryPickPlant(
            scene,
            new SceneRay(new Vector3(10f, 1f, -10f), Vector3.UnitZ),
            out int index));
        Assert.Equal(0, index);

        Assert.False(ScenePicking.TryPickPlant(
            scene,
            new SceneRay(new Vector3(0f, 1f, -10f), Vector3.UnitZ),
            out _));
    }

    private static FieldScene CreateScene()
    {
        var scene = new FieldScene
        {
            AreaName = "test",
            ModelPackName = "test",
            DumpRoot = ".",
        };

        var model = new PlantModel { Name = "plant" };
        model.Meshes.Add(new SceneMesh
        {
            Name = "plant",
            Geometry = new TrinityMeshGeometry(
                "plant",
                [],
                [],
                [],
                new BoundingBox(new Vector3f(-1f, 0f, -1f), new Vector3f(1f, 2f, 1f))),
            PartIndexStart = 0,
            PartIndexCount = 0,
            MaterialIndex = -1,
        });
        scene.Plants.Add(model);
        return scene;
    }

    private static Matrix4x4RowMajor At(float x, float y, float z) =>
        SceneGeometry.Compose(new SceneTransform(new Vector3(x, y, z), 0f, Vector3.One));
}
