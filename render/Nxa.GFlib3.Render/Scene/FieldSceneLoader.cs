// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Bntx;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Core.Formats.Trinity;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>Loads a room model pack plus terrain vegetation into a <see cref="FieldScene"/>.</summary>
public static class FieldSceneLoader
{
    private const string ModelPackFolder = "bin/field/model/pack/";
    private const string BntxMagic = "BNTX";
    private const int MaxTextureSize = 8192;

    public static FieldScene Load(string dumpRoot, string area, string modelPackName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPackName);

        string romfs = Path.Combine(dumpRoot, "RomFS");
        string terrainPackPath = Path.Combine(
            romfs, "bin", "archive", "field", "terrain", area + ".gfpak");
        string modelPackPath = Path.Combine(
            romfs, "bin", "archive", "field", "model", "pack", modelPackName + ".gfpak");

        if (!File.Exists(terrainPackPath))
        {
            throw new FileNotFoundException($"Terrain pack not found: {terrainPackPath}");
        }

        if (!File.Exists(modelPackPath))
        {
            throw new FileNotFoundException($"Model pack not found: {modelPackPath}");
        }

        var scene = new FieldScene
        {
            AreaName = area,
            ModelPackName = modelPackName,
            DumpRoot = dumpRoot,
        };

        using var modelPack = new GfpakPack(modelPackPath, ModelPackFolder);
        using var terrainPack = new GfpakPack(
            terrainPackPath, $"bin/field/terrain/{area}/terrain/");

        LoadRoomModel(modelPack, scene);
        LoadVegetation(terrainPack, scene);

        scene.Messages.Insert(0, $"Loaded {area}: {scene.RoomMeshes.Count} room parts, "
            + $"{scene.Plants.Count} plant models, {scene.Instances.Count} instances.");
        return scene;
    }

    private static void LoadRoomModel(GfpakPack pack, FieldScene scene)
    {
        var trmdls = new List<TrinityFile>();

        foreach (GfpakEntry entry in pack.Entries)
        {
            if (entry.SizeDecompressed < 4)
            {
                continue;
            }

            byte[] data = pack.Read(entry);
            if (IsBntx(data))
            {
                pack.ParseBntx(entry);
                continue;
            }

            TrinityFile? file = pack.ParseTrinity(entry, data);
            if (file?.Kind == TrinityKind.Trmdl)
            {
                trmdls.Add(file);
            }
        }

        if (trmdls.Count == 0)
        {
            scene.Messages.Add($"warning: model pack '{pack.ArchivePath}' has no TRMDL.");
            return;
        }

        var seenMeshes = new HashSet<string>(StringComparer.Ordinal);
        foreach (TrinityFile trmdl in trmdls)
        {
            AddModelMeshes(pack, scene, trmdl.Trmdl!, "room", scene.RoomMeshes, seenMeshes);
        }
    }

    private static void LoadVegetation(GfpakPack pack, FieldScene scene)
    {
        TerrainManifest manifest;
        try
        {
            manifest = pack.FindManifest();
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            scene.Messages.Add($"warning: terrain manifest not found: {ex.Message}");
            return;
        }

        foreach (TerrainPlantGroup group in manifest.PlantGroups)
        {
            TrinityFile? trmdl = ResolveTrinity(pack, group.ModelPath, scene, "vegetation");
            if (trmdl?.Kind != TrinityKind.Trmdl)
            {
                scene.Messages.Add($"warning: plant model '{group.ModelPath}' is unavailable.");
                continue;
            }

            var model = new PlantModel { Name = group.ModelPath };
            var seenMeshes = new HashSet<string>(StringComparer.Ordinal);
            AddModelMeshes(pack, scene, trmdl.Trmdl!, group.ModelPath, model.Meshes, seenMeshes);

            if (model.Meshes.Count == 0)
            {
                scene.Messages.Add($"warning: plant model '{group.ModelPath}' has no drawable mesh.");
                continue;
            }

            int modelIndex = scene.Plants.Count;
            scene.Plants.Add(model);

            foreach (string instancePath in group.InstancePaths)
            {
                byte[]? data = pack.TryRead(instancePath);
                if (data is null)
                {
                    scene.Messages.Add($"warning: missing instance file '{instancePath}'.");
                    continue;
                }

                try
                {
                    TrinsFile trins = TrinsFile.Parse(data);
                    foreach (Matrix4x4RowMajor matrix in trins.Instances)
                    {
                        scene.Instances.Add(new PlantInstance(modelIndex, matrix));
                    }
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
                {
                    scene.Messages.Add($"warning: '{instancePath}': {ex.Message}");
                }
            }
        }
    }

    private static void AddModelMeshes(
        GfpakPack pack,
        FieldScene scene,
        TrmdlFile model,
        string owner,
        List<SceneMesh> target,
        HashSet<string> seenMeshes)
    {
        IEnumerable<string> meshPaths = SelectLod0Paths(model, owner, scene);
        foreach (string meshPath in meshPaths)
        {
            string meshKey = pack.LogicalFolder + meshPath;
            if (!seenMeshes.Add(meshKey))
            {
                continue;
            }

            TrinityFile? trmshFile = ResolveTrinity(pack, meshPath, scene, owner);
            if (trmshFile?.Kind != TrinityKind.Trmsh)
            {
                continue;
            }

            TrmshFile trmsh = trmshFile.Trmsh!;
            if (string.IsNullOrWhiteSpace(trmsh.BufferFilePath))
            {
                scene.Messages.Add($"warning: '{meshPath}' has no buffer path.");
                continue;
            }

            TrinityFile? trmbfFile = ResolveTrinity(pack, trmsh.BufferFilePath, scene, owner);
            if (trmbfFile?.Kind != TrinityKind.Trmbf)
            {
                continue;
            }

            TrmbfFile trmbf = trmbfFile.Trmbf!;
            if (trmbf.Buffers.Count != trmsh.Meshes.Count)
            {
                scene.Messages.Add(
                    $"warning: '{meshPath}' has {trmsh.Meshes.Count} meshes but "
                    + $"'{trmsh.BufferFilePath}' has {trmbf.Buffers.Count} buffers.");
            }

            int count = Math.Min(trmbf.Buffers.Count, trmsh.Meshes.Count);
            for (int i = 0; i < count; i++)
            {
                TrinityMeshGeometry geometry;
                try
                {
                    geometry = TrinityMeshBuilder.Build(trmbf.Buffers[i], trmsh.Meshes[i]);
                }
                catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
                {
                    scene.Messages.Add($"warning: '{meshPath}' mesh {i}: {ex.Message}");
                    continue;
                }

                for (int part = 0; part < geometry.Parts.Count; part++)
                {
                    TrinitySubMesh subMesh = geometry.Parts[part];
                    int material = EnsureMaterial(scene, pack, model, subMesh.MaterialName, owner);
                    target.Add(new SceneMesh
                    {
                        Name = geometry.Name + (geometry.Parts.Count > 1 ? $" [{part}]" : string.Empty),
                        Geometry = geometry,
                        PartIndexStart = subMesh.IndexStart,
                        PartIndexCount = subMesh.IndexCount,
                        MaterialIndex = material,
                    });
                }
            }
        }
    }

    private static IEnumerable<string> SelectLod0Paths(TrmdlFile model, string owner, FieldScene scene)
    {
        if (model.Lods.Count == 0 || model.Lods[0].EntryIndices.Count == 0)
        {
            return model.MeshPaths.Take(1);
        }

        var all = new List<string>(model.Lods[0].EntryIndices.Count);
        foreach (uint index in model.Lods[0].EntryIndices)
        {
            if (index >= model.MeshPaths.Count)
            {
                scene.Messages.Add($"warning: '{owner}' LOD entry {index} exceeds {model.MeshPaths.Count} mesh paths.");
                continue;
            }

            all.Add(model.MeshPaths[(int)index]);
        }

        // One LOD group can list every LOD variant; keep the highest-detail paths.
        List<string> highest = all.Where(path => !IsLodVariant(path)).ToList();
        if (highest.Count == 0)
        {
            highest.Add(all[0]);
        }

        return highest;
    }

    private static bool IsLodVariant(string path)
    {
        int marker = path.IndexOf("_lod", StringComparison.OrdinalIgnoreCase);
        if (marker < 0 || marker + 4 >= path.Length)
        {
            return false;
        }

        char level = path[marker + 4];
        return level is >= '1' and <= '9';
    }

    private static int EnsureMaterial(
        FieldScene scene,
        GfpakPack pack,
        TrmdlFile model,
        string? materialName,
        string owner)
    {
        if (string.IsNullOrWhiteSpace(materialName))
        {
            return -1;
        }

        int existing = scene.FindMaterial(materialName);
        if (existing >= 0)
        {
            return existing;
        }

        TrmtrMaterial? source = FindMaterial(pack, model, materialName, scene, owner);
        if (source is null)
        {
            scene.Materials.Add(new SceneMaterial { Name = materialName });
            return scene.Materials.Count - 1;
        }

        int baseColor = FindTexture(scene, pack, source, "BaseColorMap", owner);
        int normal = FindTexture(scene, pack, source, "NormalMap", owner);
        int ao = FindTexture(scene, pack, source, "AOMap", owner);

        scene.Materials.Add(new SceneMaterial
        {
            Name = materialName,
            BaseColorTexture = baseColor,
            NormalTexture = normal,
            AmbientOcclusionTexture = ao,
        });
        return scene.Materials.Count - 1;
    }

    private static TrmtrMaterial? FindMaterial(
        GfpakPack pack,
        TrmdlFile model,
        string materialName,
        FieldScene scene,
        string owner)
    {
        foreach (string materialPath in model.Materials)
        {
            TrinityFile? file = ResolveTrinity(pack, materialPath, scene, owner);
            if (file?.Kind != TrinityKind.Trmtr)
            {
                continue;
            }

            foreach (TrmtrMaterial material in file.Trmtr!.Materials)
            {
                if (string.Equals(material.Name, materialName, StringComparison.Ordinal))
                {
                    return material;
                }
            }
        }

        scene.Messages.Add($"warning: material '{materialName}' not found for '{owner}'.");
        return null;
    }

    private static int FindTexture(
        FieldScene scene,
        GfpakPack pack,
        TrmtrMaterial material,
        string slotName,
        string owner)
    {
        TrmtrTexture? source = material.Textures.FirstOrDefault(
            texture => string.Equals(texture.Name, slotName, StringComparison.OrdinalIgnoreCase));
        if (source?.File is null)
        {
            return -1;
        }

        string fileName = source.File.EndsWith(".bntx", StringComparison.OrdinalIgnoreCase)
            ? source.File
            : source.File + ".bntx";

        int existing = scene.Textures.FindIndex(
            texture => string.Equals(texture.Name, Path.GetFileNameWithoutExtension(fileName), StringComparison.Ordinal));
        if (existing >= 0)
        {
            return existing;
        }

        GfpakEntry? entry = pack.Find(fileName);
        if (entry is null)
        {
            scene.Messages.Add($"warning: texture '{fileName}' for '{owner}' is missing.");
            return -1;
        }

        BntxFile? bntx = pack.ParseBntx(entry);
        BntxTexture? texture = bntx is null || bntx.Textures.Count == 0 ? null : bntx.Textures[0];
        if (texture is null)
        {
            scene.Messages.Add($"warning: texture '{fileName}' could not be parsed.");
            return -1;
        }

        if (texture.Width == 0 || texture.Height == 0
            || texture.Width > MaxTextureSize || texture.Height > MaxTextureSize)
        {
            scene.Messages.Add($"warning: texture '{fileName}' has unsupported size {texture.Width}x{texture.Height}.");
            return -1;
        }

        try
        {
            BntxImage image = BntxTextureDecoder.Decode(texture, 0);
            scene.Textures.Add(new SceneTexture
            {
                Name = texture.Name,
                Image = image,
                IsSrgb = texture.IsSrgb,
            });
            return scene.Textures.Count - 1;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            scene.Messages.Add($"warning: texture '{fileName}' could not be decoded: {ex.Message}");
            return -1;
        }
    }

    private static TrinityFile? ResolveTrinity(
        GfpakPack pack,
        string fileName,
        FieldScene scene,
        string owner)
    {
        GfpakEntry? entry = pack.Find(fileName);
        if (entry is null)
        {
            scene.Messages.Add($"warning: '{fileName}' for '{owner}' is missing.");
            return null;
        }

        TrinityFile? file = pack.ParseTrinity(entry);
        if (file is null)
        {
            scene.Messages.Add($"warning: '{fileName}' is not a Trinity file.");
        }

        return file;
    }

    private static bool IsBntx(byte[] data)
    {
        return data.Length >= 4
            && data[0] == (byte)BntxMagic[0]
            && data[1] == (byte)BntxMagic[1]
            && data[2] == (byte)BntxMagic[2]
            && data[3] == (byte)BntxMagic[3];
    }
}

