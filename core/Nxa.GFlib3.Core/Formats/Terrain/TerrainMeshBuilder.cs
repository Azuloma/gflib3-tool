// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Terrain;

public sealed record TerrainMeshGeometry(
    TerrainVertex[] Vertices,
    uint[] Indices,
    BoundingBox Bounds);

public readonly record struct TerrainVertex(
    Vector3f Position,
    Vector3f Normal,
    Vector2f TexCoord);

/// <summary>
/// Builds a regular grid mesh from a <see cref="TerrainSurface"/>. Grid X maps to
/// world X and grid Y to world Z; world offset and handedness are not applied.
/// </summary>
public static class TerrainMeshBuilder
{
    public const int DefaultStride = 1;

    private const int MaxVertices = 16 * 1024 * 1024;

    public static TerrainMeshGeometry Build(TerrainSurface surface, int stride = DefaultStride)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentOutOfRangeException.ThrowIfLessThan(stride, 1);

        TerrainHeightmap heightmap = surface.Heightmap;
        int width = heightmap.Width;
        int height = heightmap.Height;
        int columns = SampleCount(width, stride);
        int rows = SampleCount(height, stride);

        long vertexCount = (long)columns * rows;
        if (vertexCount > MaxVertices)
        {
            throw new InvalidDataException($"Terrain mesh would have {vertexCount} vertices; limit is {MaxVertices}.");
        }

        var vertices = new TerrainVertex[vertexCount];
        Vector3f min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3f max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int row = 0; row < rows; row++)
        {
            int y = Math.Min(row * stride, height - 1);
            for (int column = 0; column < columns; column++)
            {
                int x = Math.Min(column * stride, width - 1);
                var position = new Vector3f(x, surface.GetWorldHeight(x, y), y);
                var normal = ComputeNormal(surface, x, y);
                var uv = new Vector2f(
                    width > 1 ? (float)x / (width - 1) : 0,
                    height > 1 ? (float)y / (height - 1) : 0);

                vertices[(row * columns) + column] = new TerrainVertex(position, normal, uv);
                min = new Vector3f(
                    MathF.Min(min.X, position.X),
                    MathF.Min(min.Y, position.Y),
                    MathF.Min(min.Z, position.Z));
                max = new Vector3f(
                    MathF.Max(max.X, position.X),
                    MathF.Max(max.Y, position.Y),
                    MathF.Max(max.Z, position.Z));
            }
        }

        long quadCount = (long)(columns - 1) * (rows - 1);
        var indices = new uint[checked(quadCount * 6)];
        int cursor = 0;
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                uint topLeft = (uint)((row * columns) + column);
                uint topRight = topLeft + 1;
                uint bottomLeft = (uint)(((row + 1) * columns) + column);
                uint bottomRight = bottomLeft + 1;

                indices[cursor++] = topLeft;
                indices[cursor++] = bottomLeft;
                indices[cursor++] = topRight;
                indices[cursor++] = topRight;
                indices[cursor++] = bottomLeft;
                indices[cursor++] = bottomRight;
            }
        }

        return new TerrainMeshGeometry(vertices, indices, new BoundingBox(min, max));
    }

    private static int SampleCount(int extent, int stride)
    {
        return ((extent - 1 + stride - 1) / stride) + 1;
    }

    private static Vector3f ComputeNormal(TerrainSurface surface, int x, int y)
    {
        TerrainHeightmap heightmap = surface.Heightmap;
        int x0 = Math.Max(x - 1, 0);
        int x1 = Math.Min(x + 1, heightmap.Width - 1);
        int y0 = Math.Max(y - 1, 0);
        int y1 = Math.Min(y + 1, heightmap.Height - 1);

        float dx = (surface.GetWorldHeight(x1, y) - surface.GetWorldHeight(x0, y)) / (x1 - x0);
        float dz = (surface.GetWorldHeight(x, y1) - surface.GetWorldHeight(x, y0)) / (y1 - y0);

        var normal = new Vector3f(-dx, 1, -dz);
        float length = MathF.Sqrt((normal.X * normal.X) + (normal.Y * normal.Y) + (normal.Z * normal.Z));
        return length > 0
            ? new Vector3f(normal.X / length, normal.Y / length, normal.Z / length)
            : new Vector3f(0, 1, 0);
    }
}
