// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>Position, Y yaw, and scale decomposed from a placement matrix.</summary>
public readonly record struct SceneTransform(Vector3 Position, float YawDegrees, Vector3 Scale);

/// <summary>Matrix, bounds, and transform helpers shared by the viewer UI.</summary>
public static class SceneGeometry
{
    public static Matrix4x4 ToMatrix(Matrix4x4RowMajor value) => new(
        value.Row0.X,
        value.Row0.Y,
        value.Row0.Z,
        value.Row0.W,
        value.Row1.X,
        value.Row1.Y,
        value.Row1.Z,
        value.Row1.W,
        value.Row2.X,
        value.Row2.Y,
        value.Row2.Z,
        value.Row2.W,
        value.Row3.X,
        value.Row3.Y,
        value.Row3.Z,
        value.Row3.W);

    public static SceneTransform Decompose(Matrix4x4 matrix)
    {
        if (!Matrix4x4.Decompose(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 position))
        {
            return new SceneTransform(matrix.Translation, 0f, Vector3.One);
        }

        float yaw = MathF.Atan2(
            2f * ((rotation.W * rotation.Y) + (rotation.X * rotation.Z)),
            1f - (2f * ((rotation.Y * rotation.Y) + (rotation.X * rotation.X))));
        return new SceneTransform(position, NormalizeDegrees(yaw * (180f / MathF.PI)), scale);
    }

    public static SceneTransform Decompose(Matrix4x4RowMajor value)
    {
        float scaleXz = MathF.Sqrt((value.Row0.X * value.Row0.X) + (value.Row2.X * value.Row2.X));
        float yaw = MathF.Atan2(value.Row2.X, value.Row0.X);
        var position = new Vector3(value.Row3.X, value.Row3.Y, value.Row3.Z);
        var scale = new Vector3(scaleXz, value.Row1.Y, scaleXz);
        return new SceneTransform(position, NormalizeDegrees(yaw * (180f / MathF.PI)), scale);
    }

    public static BoundingBox TransformBounds(BoundingBox bounds, Matrix4x4 matrix)
    {
        Vector3f min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3f max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int corner = 0; corner < 8; corner++)
        {
            var local = new Vector3(
                (corner & 1) == 0 ? bounds.Min.X : bounds.Max.X,
                (corner & 2) == 0 ? bounds.Min.Y : bounds.Max.Y,
                (corner & 4) == 0 ? bounds.Min.Z : bounds.Max.Z);
            Vector3 world = Vector3.Transform(local, matrix);
            min = Min(min, new Vector3f(world.X, world.Y, world.Z));
            max = Max(max, new Vector3f(world.X, world.Y, world.Z));
        }

        return new BoundingBox(min, max);
    }

    public static BoundingBox Union(BoundingBox a, BoundingBox b) => new(Min(a.Min, b.Min), Max(a.Max, b.Max));

    public static BoundingBox Union(IEnumerable<BoundingBox> bounds)
    {
        BoundingBox result = default;
        bool any = false;
        foreach (BoundingBox boundsItem in bounds)
        {
            result = any ? Union(result, boundsItem) : boundsItem;
            any = true;
        }

        return any
            ? result
            : new BoundingBox(
                new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity));
    }

    public static BoundingBox OfPlacedMesh(SceneMesh mesh) => mesh.Placement is Matrix4x4 placement
        ? TransformBounds(mesh.Geometry.Bounds, placement)
        : mesh.Geometry.Bounds;

    public static BoundingBox OfModel(IReadOnlyList<SceneMesh> meshes) =>
        Union(meshes.Select(mesh => mesh.Geometry.Bounds));

    public static BoundingBox OfInstance(PlantModel model, Matrix4x4RowMajor matrix)
    {
        Matrix4x4 transform = ToMatrix(matrix);
        return TransformBounds(OfModel(model.Meshes), transform);
    }

    private static float NormalizeDegrees(float degrees)
    {
        degrees %= 360f;
        if (degrees <= -180f)
        {
            degrees += 360f;
        }
        else if (degrees > 180f)
        {
            degrees -= 360f;
        }

        return degrees;
    }

    private static Vector3f Min(Vector3f a, Vector3f b) => new(
        Math.Min(a.X, b.X),
        Math.Min(a.Y, b.Y),
        Math.Min(a.Z, b.Z));

    private static Vector3f Max(Vector3f a, Vector3f b) => new(
        Math.Max(a.X, b.X),
        Math.Max(a.Y, b.Y),
        Math.Max(a.Z, b.Z));
}
