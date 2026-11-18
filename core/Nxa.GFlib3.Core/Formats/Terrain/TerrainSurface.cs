// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Core.Formats.Terrain;

/// <summary>
/// Heightmap plus the manifest height extent and world X origin. Heightmap
/// samples are normalized to 0..1; multiply by <see cref="HeightScale"/> for
/// world units.
/// </summary>
public sealed class TerrainSurface
{
    private TerrainSurface(
        TerrainHeightmap heightmap,
        float heightScale,
        float worldUnitsPerSampleX,
        float worldUnitsPerSampleZ,
        float minHeight,
        float maxHeight,
        float worldOriginX)
    {
        Heightmap = heightmap;
        HeightScale = heightScale;
        WorldUnitsPerSampleX = worldUnitsPerSampleX;
        WorldUnitsPerSampleZ = worldUnitsPerSampleZ;
        MinHeight = minHeight;
        MaxHeight = maxHeight;
        WorldOriginX = worldOriginX;
    }

    public TerrainHeightmap Heightmap { get; }

    public float HeightScale { get; }

    /// <summary>Lowest surface height in world units.</summary>
    public float MinHeight { get; }

    /// <summary>Highest surface height in world units.</summary>
    public float MaxHeight { get; }

    /// <summary>
    /// World distance between heightmap samples. The manifest grid is 1024
    /// units across, so 1025² maps to 1 and 2049² maps to 0.5.
    /// </summary>
    public float WorldUnitsPerSampleX { get; }

    public float WorldUnitsPerSampleZ { get; }

    public float WorldExtentX => (Heightmap.Width - 1) * WorldUnitsPerSampleX;

    public float WorldExtentZ => (Heightmap.Height - 1) * WorldUnitsPerSampleZ;

    /// <summary>World X origin from the terrain manifest; zero in base areas.</summary>
    public float WorldOriginX { get; }

    public float WorldMaxX => WorldOriginX + WorldExtentX;

    public float GetHeightAtSample(int x, int y) => Heightmap.GetHeight(x, y) * HeightScale;

    /// <summary>Samples the height nearest to a world X/Z position.</summary>
    public bool TryGetWorldHeight(float worldX, float worldZ, out float height)
    {
        height = 0f;
        if (!float.IsFinite(worldX) || !float.IsFinite(worldZ))
        {
            return false;
        }

        float localX = worldX - WorldOriginX;
        if (localX < 0f || worldZ < 0f
            || localX > WorldExtentX || worldZ > WorldExtentZ)
        {
            return false;
        }

        int x = (int)MathF.Round(localX / WorldUnitsPerSampleX);
        int y = (int)MathF.Round(worldZ / WorldUnitsPerSampleZ);
        if ((uint)x >= (uint)Heightmap.Width || (uint)y >= (uint)Heightmap.Height)
        {
            return false;
        }

        height = GetHeightAtSample(x, y);
        return true;
    }

    /// <summary>Builds a surface from a manifest and a decoded heightmap grid.</summary>
    public static TerrainSurface Create(TerrainManifest manifest, TerrainHeightmap heightmap)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(heightmap);

        if (manifest.HeightScale <= 0 || !float.IsFinite(manifest.HeightScale))
        {
            throw new InvalidDataException(
                $"Terrain manifest height extent {manifest.HeightScale} is not a positive finite value.");
        }

        if (heightmap.Width < 2 || heightmap.Height < 2)
        {
            throw new InvalidDataException(
                $"Terrain heightmap {heightmap.Width}x{heightmap.Height} has no sample spacing.");
        }

        if (manifest.GridWidth <= 1 || manifest.GridHeight <= 1
            || !float.IsFinite(manifest.GridWidth) || !float.IsFinite(manifest.GridHeight))
        {
            throw new InvalidDataException(
                $"Terrain manifest grid {manifest.GridWidth}x{manifest.GridHeight} is not a valid world extent.");
        }

        float unitsPerSampleX = (manifest.GridWidth - 1) / (heightmap.Width - 1);
        float unitsPerSampleZ = (manifest.GridHeight - 1) / (heightmap.Height - 1);
        if (unitsPerSampleX <= 0 || unitsPerSampleZ <= 0
            || !float.IsFinite(unitsPerSampleX) || !float.IsFinite(unitsPerSampleZ))
        {
            throw new InvalidDataException(
                $"Terrain sample spacing {unitsPerSampleX}x{unitsPerSampleZ} is not positive and finite.");
        }

        (float min, float max) = heightmap.GetRange();
        return new TerrainSurface(
            heightmap,
            manifest.HeightScale,
            unitsPerSampleX,
            unitsPerSampleZ,
            min * manifest.HeightScale,
            max * manifest.HeightScale,
            manifest.WorldMinX);
    }

    public static TerrainSurface Load(
        GfpakArchive archive,
        TerrainManifest manifest,
        TerrainMaterialSet material)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(material);

        return Create(manifest, material.LoadHeightmap(archive));
    }
}
