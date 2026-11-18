// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Formats.Terrain;

/// <summary>A vegetation cell index in the 16 x 16 terrain grid.</summary>
public readonly record struct TerrainCell(int X, int Z);

/// <summary>
/// Maps world X/Z to the 16 x 16 terrain cells used by `.trins` file names.
/// Cell X grows with world X; cell Z grows toward decreasing world Z.
/// </summary>
public sealed class TerrainCellGrid
{
    public const int CellCount = 16;

    public TerrainCellGrid(float worldOriginX, float worldExtentX, float worldExtentZ)
    {
        if (!float.IsFinite(worldOriginX)
            || !float.IsFinite(worldExtentX)
            || !float.IsFinite(worldExtentZ)
            || worldExtentX <= 0f
            || worldExtentZ <= 0f)
        {
            throw new ArgumentException(
                $"Terrain cell grid needs a finite origin and positive extents; "
                + $"origin X {worldOriginX}, extent {worldExtentX} x {worldExtentZ}.");
        }

        WorldOriginX = worldOriginX;
        WorldExtentX = worldExtentX;
        WorldExtentZ = worldExtentZ;
    }

    public float WorldOriginX { get; }

    public float WorldExtentX { get; }

    public float WorldExtentZ { get; }

    public float WorldMaxX => WorldOriginX + WorldExtentX;

    /// <summary>World units per cell; the manifest grid width divided by 16.</summary>
    public float CellSizeX => (WorldExtentX + 1f) / CellCount;

    public float CellSizeZ => (WorldExtentZ + 1f) / CellCount;

    public static TerrainCellGrid FromSurface(TerrainSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        return new TerrainCellGrid(surface.WorldOriginX, surface.WorldExtentX, surface.WorldExtentZ);
    }

    public bool Contains(float worldX, float worldZ)
    {
        if (!float.IsFinite(worldX) || !float.IsFinite(worldZ))
        {
            return false;
        }

        float localX = worldX - WorldOriginX;
        return localX >= 0f
            && localX <= WorldExtentX
            && worldZ >= 0f
            && worldZ <= WorldExtentZ;
    }

    /// <summary>Returns the cell at a world position, clamped to the grid.</summary>
    public TerrainCell CellAt(float worldX, float worldZ)
    {
        float localX = worldX - WorldOriginX;
        int x = (int)MathF.Floor(localX / CellSizeX);
        int z = (int)MathF.Floor((WorldExtentZ + 1f - worldZ) / CellSizeZ);
        return new TerrainCell(
            Math.Clamp(x, 0, CellCount - 1),
            Math.Clamp(z, 0, CellCount - 1));
    }

    public bool TryGetCell(float worldX, float worldZ, out TerrainCell cell)
    {
        if (!Contains(worldX, worldZ))
        {
            cell = default;
            return false;
        }

        cell = CellAt(worldX, worldZ);
        return true;
    }
}
