// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>World-space ray with a normalized direction.</summary>
public readonly record struct SceneRay(Vector3 Origin, Vector3 Direction);

/// <summary>Screen-to-ray conversion and instance picking against model AABBs.</summary>
public static class ScenePicking
{
    private const float MinViewSize = 1e-3f;
    private const float MinDirectionLengthSquared = 1e-12f;
    private const float SlabEpsilon = 1e-8f;

    public static bool TryScreenRay(
        Vector2 screen,
        Vector2 viewSize,
        Matrix4x4 inverseViewProjection,
        out SceneRay ray)
    {
        ray = default;
        if (viewSize.X < MinViewSize || viewSize.Y < MinViewSize)
        {
            return false;
        }

        float ndcX = (screen.X / viewSize.X) * 2f - 1f;
        float ndcY = 1f - (screen.Y / viewSize.Y) * 2f;
        Vector4 near = Vector4.Transform(new Vector4(ndcX, ndcY, -1f, 1f), inverseViewProjection);
        Vector4 far = Vector4.Transform(new Vector4(ndcX, ndcY, 1f, 1f), inverseViewProjection);
        if (MathF.Abs(near.W) < SlabEpsilon || MathF.Abs(far.W) < SlabEpsilon)
        {
            return false;
        }

        near /= near.W;
        far /= far.W;
        var origin = new Vector3(near.X, near.Y, near.Z);
        var direction = new Vector3(far.X - near.X, far.Y - near.Y, far.Z - near.Z);
        if (!float.IsFinite(origin.X)
            || !float.IsFinite(origin.Y)
            || !float.IsFinite(origin.Z)
            || !float.IsFinite(direction.X)
            || !float.IsFinite(direction.Y)
            || !float.IsFinite(direction.Z)
            || direction.LengthSquared() < MinDirectionLengthSquared)
        {
            return false;
        }

        ray = new SceneRay(origin, Vector3.Normalize(direction));
        return true;
    }

    /// <summary>Returns the nearest instance whose model AABB the ray enters.</summary>
    public static bool TryPickPlant(FieldScene scene, SceneRay ray, out int index)
    {
        ArgumentNullException.ThrowIfNull(scene);
        index = -1;
        float bestDistance = float.PositiveInfinity;

        if (scene.Instances.Count == 0 || scene.Plants.Count == 0)
        {
            return false;
        }

        var modelBounds = new BoundingBox[scene.Plants.Count];
        for (int model = 0; model < scene.Plants.Count; model++)
        {
            modelBounds[model] = SceneGeometry.OfModel(scene.Plants[model].Meshes);
        }

        for (int i = 0; i < scene.Instances.Count; i++)
        {
            PlantInstance instance = scene.Instances[i];
            if ((uint)instance.ModelIndex >= (uint)modelBounds.Length)
            {
                continue;
            }

            BoundingBox bounds = modelBounds[instance.ModelIndex];
            if (!IsFinite(bounds))
            {
                continue;
            }

            if (!Matrix4x4.Invert(SceneGeometry.ToMatrix(instance.Matrix), out Matrix4x4 inverse))
            {
                continue;
            }

            Vector3 localOrigin = Vector3.Transform(ray.Origin, inverse);
            Vector3 localDirection = Vector3.TransformNormal(ray.Direction, inverse);
            if (!IntersectsBounds(localOrigin, localDirection, bounds, out float distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                index = i;
            }
        }

        return index >= 0;
    }

    public static bool IntersectsBounds(
        Vector3 origin,
        Vector3 direction,
        BoundingBox bounds,
        out float distance)
    {
        float near = 0f;
        float far = float.PositiveInfinity;
        if (direction.LengthSquared() < MinDirectionLengthSquared
            || !float.IsFinite(direction.X)
            || !float.IsFinite(direction.Y)
            || !float.IsFinite(direction.Z)
            || !ClipAxis(origin.X, direction.X, bounds.Min.X, bounds.Max.X, ref near, ref far)
            || !ClipAxis(origin.Y, direction.Y, bounds.Min.Y, bounds.Max.Y, ref near, ref far)
            || !ClipAxis(origin.Z, direction.Z, bounds.Min.Z, bounds.Max.Z, ref near, ref far)
            || far < near)
        {
            distance = 0f;
            return false;
        }

        distance = near;
        return true;
    }

    private static bool ClipAxis(
        float origin,
        float direction,
        float min,
        float max,
        ref float near,
        ref float far)
    {
        if (MathF.Abs(direction) < SlabEpsilon)
        {
            return origin >= min && origin <= max;
        }

        float first = (min - origin) / direction;
        float second = (max - origin) / direction;
        if (first > second)
        {
            (first, second) = (second, first);
        }

        near = MathF.Max(near, first);
        far = MathF.Min(far, second);
        return true;
    }

    private static bool IsFinite(BoundingBox bounds) =>
        float.IsFinite(bounds.Min.X)
        && float.IsFinite(bounds.Min.Y)
        && float.IsFinite(bounds.Min.Z)
        && float.IsFinite(bounds.Max.X)
        && float.IsFinite(bounds.Max.Y)
        && float.IsFinite(bounds.Max.Z);
}
