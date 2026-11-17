// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Bntx;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Trinity;
using Nxa.GFlib3.Core.Hashing;

namespace Nxa.GFlib3.Core.Formats.Terrain;

/// <summary>
/// Terrain material (<c>TerrainMaterial</c> in a <c>.trmtr</c>) and the
/// heightmap/normal/splat textures it references inside a terrain pack.
/// </summary>
public sealed class TerrainMaterialSet
{
    public const string MaterialName = "TerrainMaterial";
    public const string HeightMapTexture = "HeightMap";
    public const string HeightNormalMapTexture = "HeightNormalMap";
    public const string SplatAlphaTexture = "SplatAlpha";

    /// <summary>Observed terrain materials are 3,192 bytes; keep a wide band.</summary>
    public const int MinMaterialBytes = 256;

    public const int MaxMaterialBytes = 1 << 16;

    private TerrainMaterialSet(
        string folderPath,
        GfpakEntry sourceEntry,
        TrmtrFile file,
        TrmtrMaterial material)
    {
        FolderPath = folderPath;
        SourceEntry = sourceEntry;
        File = file;
        Material = material;
    }

    /// <summary>Logical folder holding the material and its textures, with a trailing slash.</summary>
    public string FolderPath { get; }

    public GfpakEntry SourceEntry { get; }

    public TrmtrFile File { get; }

    public TrmtrMaterial Material { get; }

    public static TerrainMaterialSet FindInArchive(GfpakArchive archive, string folderPath)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        if (!folderPath.EndsWith('/'))
        {
            folderPath += "/";
        }

        foreach (GfpakEntry candidate in archive.Entries)
        {
            if (candidate.SizeDecompressed < MinMaterialBytes
                || candidate.SizeDecompressed > MaxMaterialBytes)
            {
                continue;
            }

            TrinityFile file;
            try
            {
                file = TrinityFile.Parse(archive.ReadEntry(candidate));
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
            {
                continue;
            }

            if (file.Kind != TrinityKind.Trmtr || file.Trmtr is null)
            {
                continue;
            }

            foreach (TrmtrMaterial material in file.Trmtr.Materials)
            {
                if (material.Textures.Any(texture => texture.Name == HeightMapTexture))
                {
                    return new TerrainMaterialSet(folderPath, candidate, file.Trmtr, material);
                }
            }
        }

        throw new InvalidDataException(
            $"No terrain material with a '{HeightMapTexture}' texture among {archive.Entries.Count} entries.");
    }

    public bool TryResolve(GfpakArchive archive, string fileName, out GfpakEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        ulong hash = Fnv1a64.Hash(FolderPath + fileName);
        return archive.TryGetEntryByPathHash(hash, out entry);
    }

    public string? GetTexturePath(string name)
    {
        foreach (TrmtrTexture texture in Material.Textures)
        {
            if (texture.Name == name)
            {
                return texture.File;
            }
        }

        return null;
    }

    public TerrainHeightmap LoadHeightmap(GfpakArchive archive)
    {
        string fileName = GetTexturePath(HeightMapTexture)
            ?? throw new InvalidDataException($"Terrain material has no '{HeightMapTexture}' texture.");
        if (!TryResolve(archive, fileName, out GfpakEntry? entry) || entry is null)
        {
            throw new InvalidDataException(
                $"Terrain heightmap '{FolderPath}{fileName}' is not present in the pack.");
        }

        BntxFile bntx = BntxFile.Parse(archive.ReadEntry(entry));
        return TerrainHeightmap.FromFile(bntx);
    }
}
