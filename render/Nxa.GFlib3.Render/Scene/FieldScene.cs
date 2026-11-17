// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Bntx;
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

    public List<SceneMaterial> Materials { get; } = [];

    public List<SceneTexture> Textures { get; } = [];

    public List<PlantModel> Plants { get; } = [];

    public List<PlantInstance> Instances { get; } = [];

    public List<string> Messages { get; } = [];

    public int VertexCount => RoomMeshes.Sum(mesh => mesh.Geometry.Vertices.Length)
        + Plants.Sum(plant => plant.Meshes.Sum(mesh => mesh.Geometry.Vertices.Length));

    public int IndexCount => RoomMeshes.Sum(mesh => mesh.Geometry.Indices.Length)
        + Plants.Sum(plant => plant.Meshes.Sum(mesh => mesh.Geometry.Indices.Length));

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

    public int FindMaterial(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return -1;
        }

        for (int i = 0; i < Materials.Count; i++)
        {
            if (string.Equals(Materials[i].Name, name, StringComparison.Ordinal))
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
}

public sealed class SceneMaterial
{
    public required string Name { get; init; }

    public int BaseColorTexture { get; init; } = -1;

    public int NormalTexture { get; init; } = -1;

    public int AmbientOcclusionTexture { get; init; } = -1;
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
