// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Bntx;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Resident;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Core.Formats.Trinity;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>Loads a room model pack plus terrain vegetation into a <see cref="FieldScene"/>.</summary>
public static class FieldSceneLoader
{
    private const string ModelPackFolder = "bin/field/model/pack/";
    private const string ResidentPackFile = "resident_release.gfpak";
    private const string BntxMagic = "BNTX";
    private const int MaxTextureSize = 8192;
    private const int MaxResourceTrimDepth = 4;
    private const int MaxModelEntryBytes = 1 << 20;

    public static FieldScene Load(
        string dumpRoot,
        string area,
        string modelPackName,
        string? meshFilter,
        int terrainStride = 1)
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

        LoadRoomModel(modelPack, scene, meshFilter);
        LoadTerrain(terrainPack, scene, terrainStride);
        LoadVegetation(terrainPack, scene);
        LoadScenePlacements(dumpRoot, area, modelPack, scene);

        scene.Messages.Insert(0, $"Loaded {area}: {scene.RoomMeshes.Count} room parts, "
            + $"{scene.PlacedMeshes.Count} placed parts, {scene.Plants.Count} plant models, "
            + $"{scene.Instances.Count} instances.");
        return scene;
    }

    private static void LoadRoomModel(GfpakPack pack, FieldScene scene, string? meshFilter)
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

        TrinityFile selected = SelectModel(trmdls, meshFilter, scene);
        string modelName = selected.Trmdl!.MeshPaths.Count > 0
            ? selected.Trmdl.MeshPaths[0]
            : Path.GetFileName(pack.ArchivePath);
        scene.RoomModelName = modelName;
        scene.Messages.Add($"model: {modelName}");

        var seenMeshes = new HashSet<string>(StringComparer.Ordinal);
        AddModelMeshes(pack, scene, selected.Trmdl, modelName, scene.RoomMeshes, seenMeshes);
    }

    private static TrinityFile SelectModel(List<TrinityFile> models, string? filter, FieldScene scene)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            foreach (TrinityFile model in models)
            {
                if (model.Trmdl!.MeshPaths.Any(
                    path => path.Contains("room_", StringComparison.OrdinalIgnoreCase)))
                {
                    return model;
                }
            }

            return models[0];
        }

        foreach (TrinityFile model in models)
        {
            TrmdlFile trmdl = model.Trmdl!;
            bool matches = trmdl.MeshPaths.Any(
                path => path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                || trmdl.Materials.Any(
                    material => material.Contains(filter, StringComparison.OrdinalIgnoreCase));
            if (matches)
            {
                return model;
            }
        }

        scene.Messages.Add($"warning: no model matched '{filter}'; using the first model.");
        return models[0];
    }

    private static void LoadTerrain(GfpakPack pack, FieldScene scene, int stride)
    {
        try
        {
            TerrainManifest manifest = pack.FindManifest();
            TerrainMaterialSet material = TerrainMaterialSet.FindInArchive(pack.Archive, pack.LogicalFolder);
            TerrainSurface surface = TerrainSurface.Load(pack.Archive, manifest, material);
            TerrainMeshGeometry mesh = TerrainMeshBuilder.Build(surface, Math.Max(stride, 1));

            scene.TerrainSurface = surface;
            scene.Terrain = mesh;
            scene.Messages.Add(
                $"terrain: {surface.Heightmap.Width}x{surface.Heightmap.Height}, "
                + $"scale {surface.HeightScale:0.###}, {mesh.Vertices.Length:N0} vertices.");
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
        {
            scene.Messages.Add($"warning: terrain unavailable: {ex.Message}");
        }
    }

    private static void LoadScenePlacements(string dumpRoot, string area, GfpakPack pack, FieldScene scene)
    {
        string path = Path.Combine(
            dumpRoot, "RomFS", "bin", "archive", "field", ResidentPackFile);
        if (!File.Exists(path))
        {
            scene.Messages.Add($"warning: {ResidentPackFile} not found.");
            return;
        }

        string areaToken = area.StartsWith("ha_", StringComparison.OrdinalIgnoreCase)
            ? area[3..]
            : area;
        List<TrmdlFile> models = BuildModelIndex(pack);
        var cache = new Dictionary<string, List<SceneMesh>>(StringComparer.OrdinalIgnoreCase);
        int sceneCount = 0;
        int placedParts = 0;
        int unavailable = 0;

        try
        {
            using GfpakArchive resident = GfpakArchive.Open(path);
            IReadOnlyList<ResidentSceneArchiveEntry> tables = ResidentSceneFile.FindInArchive(resident);

            foreach (ResidentSceneArchiveEntry table in tables)
            {
                foreach (ResidentScene sceneDefinition in table.File.Scenes)
                {
                    if (!TryGetResourceName(sceneDefinition.Name, areaToken, out string resource))
                    {
                        continue;
                    }

                    sceneCount++;
                    if (!IsFieldModelResource(resource))
                    {
                        continue;
                    }

                    if (!cache.TryGetValue(resource, out List<SceneMesh>? meshes))
                    {
                        meshes = LoadResourceMeshes(pack, scene, models, resource);
                        cache[resource] = meshes;
                    }

                    if (meshes.Count == 0)
                    {
                        unavailable++;
                        continue;
                    }

                    foreach (ResidentSceneNode node in sceneDefinition.Nodes)
                    {
                        if (node.Scale.X == 0 && node.Scale.Y == 0 && node.Scale.Z == 0)
                        {
                            continue;
                        }

                        Matrix4x4 transform = Compose(node);
                        foreach (SceneMesh source in meshes)
                        {
                            scene.PlacedMeshes.Add(new SceneMesh
                            {
                                Name = sceneDefinition.Name,
                                Geometry = source.Geometry,
                                PartIndexStart = source.PartIndexStart,
                                PartIndexCount = source.PartIndexCount,
                                MaterialIndex = source.MaterialIndex,
                                Placement = transform,
                            });
                            placedParts++;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
        {
            scene.Messages.Add($"warning: scene placement unavailable: {ex.Message}");
            return;
        }

        scene.Messages.Add(
            $"scene: {sceneCount} area scenes, {placedParts} placed parts, "
            + $"{unavailable} field models unavailable ({Path.GetFileName(path)}).");
    }

    private static List<TrmdlFile> BuildModelIndex(GfpakPack pack)
    {
        var models = new List<TrmdlFile>();
        foreach (GfpakEntry entry in pack.Entries)
        {
            if (entry.SizeDecompressed < 32 || entry.SizeDecompressed > MaxModelEntryBytes)
            {
                continue;
            }

            TrinityFile? file = pack.ParseTrinity(entry);
            if (file?.Kind == TrinityKind.Trmdl && file.Trmdl is not null)
            {
                models.Add(file.Trmdl);
            }
        }

        return models;
    }

    private static List<SceneMesh> LoadResourceMeshes(
        GfpakPack pack,
        FieldScene scene,
        List<TrmdlFile> models,
        string resource)
    {
        List<string> candidates = BuildResourceCandidates(resource);
        foreach (TrmdlFile model in models)
        {
            if (!MatchesResource(model, candidates))
            {
                continue;
            }

            var meshes = new List<SceneMesh>();
            AddModelMeshes(pack, scene, model, resource, meshes, new HashSet<string>(StringComparer.Ordinal));
            if (meshes.Count > 0)
            {
                return meshes;
            }
        }

        return [];
    }

    private static bool IsFieldModelResource(string resource)
    {
        string[] prefixes =
        [
            "npc_",
            "poke_",
            "vis_",
            "emitter_",
            "effect_",
            "gimmick_",
            "objects_eff_",
            "sub_",
            "event_",
            "fsys_",
            "weve_",
            "wpha_",
        ];

        foreach (string prefix in prefixes)
        {
            if (resource.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return !resource.Contains("_npc_", StringComparison.OrdinalIgnoreCase)
            && !resource.Contains("_poke_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesResource(TrmdlFile model, List<string> candidates)
    {
        foreach (string candidate in candidates)
        {
            foreach (string meshPath in model.MeshPaths)
            {
                if (meshPath.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            foreach (string material in model.Materials)
            {
                if (material.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<string> BuildResourceCandidates(string resource)
    {
        var candidates = new List<string>();
        string current = resource;
        for (int depth = 0; depth <= MaxResourceTrimDepth; depth++)
        {
            AddCandidate(candidates, current);

            int separator = current.LastIndexOf('_');
            if (separator <= 0 || !current[(separator + 1)..].All(char.IsDigit))
            {
                break;
            }

            current = current[..separator];
        }

        return candidates;
    }

    private static void AddCandidate(List<string> candidates, string value)
    {
        if (value.Length == 0)
        {
            return;
        }

        candidates.Add(value);
        string normalized = value
            .Replace("bg_parts_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("bgparts_", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(normalized, value, StringComparison.Ordinal)
            && normalized.Length > 0)
        {
            candidates.Add(normalized);
        }
    }

    private static bool TryGetResourceName(string name, string areaToken, out string resource)
    {
        resource = string.Empty;
        string prefix = areaToken + "_";
        string rest;
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            rest = name[prefix.Length..];
        }
        else if (name.StartsWith("ha_" + prefix, StringComparison.OrdinalIgnoreCase))
        {
            rest = name[("ha_".Length + prefix.Length)..];
        }
        else
        {
            return false;
        }

        if (rest.Length > 3
            && (rest[0] == 's' || rest[0] == 'S')
            && char.IsDigit(rest[1])
            && char.IsDigit(rest[2])
            && rest[3] == '_')
        {
            rest = rest[4..];
        }

        if (rest.Length == 0)
        {
            return false;
        }

        resource = rest;
        return true;
    }

    private static Matrix4x4 Compose(ResidentSceneNode node)
    {
        float radians = node.YawDegrees * (MathF.PI / 180f);
        return Matrix4x4.CreateScale(node.Scale.X, node.Scale.Y, node.Scale.Z)
            * Matrix4x4.CreateRotationY(radians)
            * Matrix4x4.CreateTranslation(node.Position.X, node.Position.Y, node.Position.Z);
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


