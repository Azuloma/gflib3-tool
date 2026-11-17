// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Core.Formats.Terrain;

/// <summary>
/// Heightmap plus the manifest height extent. Heightmap samples are normalized
/// to 0..1; multiply by <see cref="HeightScale"/> for world units.
/// </summary>
public sealed class TerrainSurface
{
    private TerrainSurface(TerrainHeightmap heightmap, float heightScale)
    {
        Heightmap = heightmap;
        HeightScale = heightScale;
    }

    public TerrainHeightmap Heightmap { get; }

    public float HeightScale { get; }

    public float GetWorldHeight(int x, int y) => Heightmap.GetHeight(x, y) * HeightScale;

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

        // The manifest grid value is per-cell (1025) while multi-cell packs carry
        // 2049x2049 heightmaps, so dimensions are validated on the texture only.
        return new TerrainSurface(heightmap, manifest.HeightScale);
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
