// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>World-space ray with a normalized direction.</summary>
public readonly record struct SceneRay(Vector3 Origin, Vector3 Direction);

/// <summary>Screen-to-ray conversion and instance picking against model AABBs.</summary>
public static class ScenePicking
{
    private const float MinViewSize = 1e-3f;
    private const float MinDirectionLengthSquared = 1e-12f;
    private const float SlabEpsilon = 1e-8f;
    private const float MinTerrainPickStep = 0.25f;
    private const int TerrainPickRefineSteps = 12;

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

    /// <summary>Returns the first terrain surface point under the ray.</summary>
    public static bool TryPickTerrain(
        TerrainSurface surface,
        SceneRay ray,
        float maxDistance,
        out Vector3 point)
    {
        ArgumentNullException.ThrowIfNull(surface);
        point = default;
        if (!float.IsFinite(maxDistance) || maxDistance <= 0f || !IsFinite(ray.Origin))
        {
            return false;
        }

        float directionLength = ray.Direction.Length();
        if (!float.IsFinite(directionLength) || directionLength < MinDirectionLengthSquared)
        {
            return false;
        }

        Vector3 direction = ray.Direction / directionLength;
        var bounds = new BoundingBox(
            new Vector3f(0f, surface.MinHeight, 0f),
            new Vector3f(surface.WorldExtentX, surface.MaxHeight, surface.WorldExtentZ));
        if (!ClipRayToBounds(ray.Origin, direction, bounds, out float entry, out float exit))
        {
            return false;
        }

        float start = MathF.Max(entry, 0f);
        float end = MathF.Min(exit, maxDistance);
        if (end < start
            || !TrySampleTerrain(surface, ray.Origin, direction, start, out float previousDifference)
            || previousDifference < 0f)
        {
            return false;
        }

        if (previousDifference == 0f)
        {
            return RefineTerrainHit(surface, ray.Origin, direction, start, start, out point);
        }

        float step = MathF.Max(
            MinTerrainPickStep,
            MathF.Min(surface.WorldUnitsPerSampleX, surface.WorldUnitsPerSampleZ));
        float previousDistance = start;
        for (float distance = start + step; ; distance += step)
        {
            bool last = distance >= end;
            if (last)
            {
                distance = end;
            }

            if (TrySampleTerrain(surface, ray.Origin, direction, distance, out float difference))
            {
                if (difference <= 0f && previousDifference > 0f)
                {
                    return RefineTerrainHit(
                        surface,
                        ray.Origin,
                        direction,
                        previousDistance,
                        distance,
                        out point);
                }

                previousDistance = distance;
                previousDifference = difference;
            }

            if (last)
            {
                return false;
            }
        }
    }

    public static bool IntersectsBounds(
        Vector3 origin,
        Vector3 direction,
        BoundingBox bounds,
        out float distance)
    {
        if (!ClipRayToBounds(origin, direction, bounds, out float near, out float _))
        {
            distance = 0f;
            return false;
        }

        distance = near;
        return true;
    }

    private static bool ClipRayToBounds(
        Vector3 origin,
        Vector3 direction,
        BoundingBox bounds,
        out float near,
        out float far)
    {
        near = 0f;
        far = float.PositiveInfinity;
        if (direction.LengthSquared() < MinDirectionLengthSquared
            || !IsFinite(origin)
            || !IsFinite(bounds)
            || !ClipAxis(origin.X, direction.X, bounds.Min.X, bounds.Max.X, ref near, ref far)
            || !ClipAxis(origin.Y, direction.Y, bounds.Min.Y, bounds.Max.Y, ref near, ref far)
            || !ClipAxis(origin.Z, direction.Z, bounds.Min.Z, bounds.Max.Z, ref near, ref far)
            || far < near)
        {
            near = 0f;
            far = 0f;
            return false;
        }

        return true;
    }

    private static bool RefineTerrainHit(
        TerrainSurface surface,
        Vector3 origin,
        Vector3 direction,
        float aboveDistance,
        float belowDistance,
        out Vector3 point)
    {
        point = default;
        for (int i = 0; i < TerrainPickRefineSteps; i++)
        {
            float middle = (aboveDistance + belowDistance) * 0.5f;
            if (!TrySampleTerrain(surface, origin, direction, middle, out float difference))
            {
                break;
            }

            if (difference > 0f)
            {
                aboveDistance = middle;
            }
            else
            {
                belowDistance = middle;
            }
        }

        point = origin + (direction * belowDistance);
        if (!surface.TryGetWorldHeight(point.X, point.Z, out float height))
        {
            return false;
        }

        point.Y = height;
        return true;
    }

    private static bool TrySampleTerrain(
        TerrainSurface surface,
        Vector3 origin,
        Vector3 direction,
        float distance,
        out float difference)
    {
        Vector3 sample = origin + (direction * distance);
        if (!surface.TryGetWorldHeight(sample.X, sample.Z, out float height))
        {
            difference = 0f;
            return false;
        }

        difference = sample.Y - height;
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

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X)
        && float.IsFinite(value.Y)
        && float.IsFinite(value.Z);

    private static bool IsFinite(BoundingBox bounds) =>
        float.IsFinite(bounds.Min.X)
        && float.IsFinite(bounds.Min.Y)
        && float.IsFinite(bounds.Min.Z)
        && float.IsFinite(bounds.Max.X)
        && float.IsFinite(bounds.Max.Y)
        && float.IsFinite(bounds.Max.Z);
}
