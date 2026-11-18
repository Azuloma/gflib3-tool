// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Bntx;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Core.Formats.Trinity;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>CPU-side field area: room geometry, materials, textures, and vegetation.</summary>
public sealed class FieldScene
{
    public required string AreaName { get; init; }

    public required string ModelPackName { get; init; }

    public string RoomModelName { get; set; } = string.Empty;

    public required string DumpRoot { get; init; }

    public List<SceneMesh> RoomMeshes { get; } = [];

    /// <summary>Scene-table instances with a placement transform.</summary>
    public List<SceneMesh> PlacedMeshes { get; } = [];

    public TerrainMeshGeometry? Terrain { get; set; }

    public TerrainSurface? TerrainSurface { get; set; }

    /// <summary>Seven blended terrain albedo layers (SplatColor0..6).</summary>
    public List<SceneTexture> TerrainLayers { get; } = [];

    /// <summary>RGBA weights for layers 0 E (SplatAlpha array slice 0).</summary>
    public SceneTexture? TerrainControl0 { get; set; }

    /// <summary>RGBA weights for layers 4 E (SplatAlpha array slice 1).</summary>
    public SceneTexture? TerrainControl1 { get; set; }

    public float[] TerrainLayerScales { get; } = [1f, 1f, 1f, 1f, 1f, 1f, 1f];

    /// <summary>Live multiplier on <see cref="TerrainLayerScales"/> from the UI/CLI.</summary>
    public float TerrainTileScale { get; set; } = 128f;

    public bool HasTerrainSplat => TerrainLayers.Count == 7
        && TerrainControl0 is not null
        && TerrainControl1 is not null;

    public List<SceneMaterial> Materials { get; } = [];

    public List<SceneTexture> Textures { get; } = [];

    public List<PlantModel> Plants { get; } = [];

    public List<PlantInstance> Instances { get; } = [];

    public List<string> Messages { get; } = [];

    public int VertexCount => RoomMeshes.Sum(mesh => mesh.Geometry.Vertices.Length)
        + PlacedMeshes.Sum(mesh => mesh.Geometry.Vertices.Length)
        + Plants.Sum(plant => plant.Meshes.Sum(mesh => mesh.Geometry.Vertices.Length))
        + (Terrain?.Vertices.Length ?? 0);

    public int IndexCount => RoomMeshes.Sum(mesh => mesh.Geometry.Indices.Length)
        + PlacedMeshes.Sum(mesh => mesh.Geometry.Indices.Length)
        + Plants.Sum(plant => plant.Meshes.Sum(mesh => mesh.Geometry.Indices.Length))
        + (Terrain?.Indices.Length ?? 0);

    public BoundingBox ComputeBounds()
    {
        Vector3f min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3f max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        bool any = false;

        foreach (SceneMesh mesh in RoomMeshes)
        {
            min = Min(min, mesh.Geometry.Bounds.Min);
            max = Max(max, mesh.Geometry.Bounds.Max);
            any = true;
        }

        foreach (SceneMesh mesh in PlacedMeshes)
        {
            BoundingBox bounds = mesh.Placement is Matrix4x4 placement
                ? SceneGeometry.TransformBounds(mesh.Geometry.Bounds, placement)
                : mesh.Geometry.Bounds;
            min = Min(min, bounds.Min);
            max = Max(max, bounds.Max);
            any = true;
        }

        if (Terrain is not null)
        {
            min = Min(min, Terrain.Bounds.Min);
            max = Max(max, Terrain.Bounds.Max);
            any = true;
        }

        foreach (PlantInstance instance in Instances)
        {
            Vector3f center = new(instance.Matrix.Row3.X, instance.Matrix.Row3.Y, instance.Matrix.Row3.Z);
            min = Min(min, center);
            max = Max(max, center);
            any = true;
        }

        if (!any)
        {
            return new BoundingBox(new Vector3f(-1, -1, -1), new Vector3f(1, 1, 1));
        }

        return new BoundingBox(min, max);
    }

    public int FindMaterialByKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return -1;
        }

        for (int i = 0; i < Materials.Count; i++)
        {
            if (string.Equals(Materials[i].Key, key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
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

public sealed class SceneMesh
{
    public required string Name { get; init; }

    public required TrinityMeshGeometry Geometry { get; init; }

    public required int PartIndexStart { get; init; }

    public required int PartIndexCount { get; init; }

    public required int MaterialIndex { get; init; }

    /// <summary>Scene-table transform; null means identity (room geometry).</summary>
    public Matrix4x4? Placement { get; init; }
}

public sealed class SceneMaterial
{
    public string Key { get; init; } = string.Empty;

    public required string Name { get; init; }

    public int BaseColorTexture { get; init; } = -1;

    public int NormalTexture { get; init; } = -1;

    public int AmbientOcclusionTexture { get; init; } = -1;

    /// <summary>Whether the material's shader uses the vertex color as albedo tint.</summary>
    public bool UseVertexColor { get; init; }

    /// <summary>Multiplier for untextured surfaces; water gets a blue fallback.</summary>
    public Vector3 Tint { get; init; } = Vector3.One;
}

public sealed class SceneTexture
{
    public required string Name { get; init; }

    public required BntxImage Image { get; init; }

    public required bool IsSrgb { get; init; }
}

public sealed class PlantModel
{
    public required string Name { get; init; }

    public List<SceneMesh> Meshes { get; } = [];
}

public sealed record PlantInstance(int ModelIndex, Matrix4x4RowMajor Matrix);
