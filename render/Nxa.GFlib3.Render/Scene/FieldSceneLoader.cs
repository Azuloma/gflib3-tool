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

    public static FieldScene Load(
        string dumpRoot,
        string area,
        string modelPackName,
        string? meshFilter,
        int terrainStride = 1,
        float terrainTileScale = 128f)
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
        using GfpakPack? baseModelPack = OpenBaseModelPack(romfs, area, modelPackName);
        using var terrainPack = new GfpakPack(
            terrainPackPath, $"bin/field/terrain/{area}/terrain/");

        string areaMatch = NormalizeAreaId(area);
        bool isBaseArea = areaMatch.Length == 6;
        bool isBaseAreaModelPack = isBaseArea
            && string.Equals(area, modelPackName, StringComparison.OrdinalIgnoreCase);
        bool exactModelPack = string.Equals(area, modelPackName, StringComparison.OrdinalIgnoreCase);
        bool previewRoom = meshFilter is not null || isBaseArea;
        if (isBaseAreaModelPack && meshFilter is null)
        {
            scene.Messages.Add("room: not loaded for a base area pack (use --mesh or a sub-pack).");
        }
        else if (previewRoom)
        {
            LoadRoomModel(modelPack, scene, meshFilter, areaMatch, exactModelPack);
        }

        LoadTerrain(terrainPack, scene, terrainStride, terrainTileScale);
        LoadVegetation(terrainPack, scene);
        LoadScenePlacements(dumpRoot, romfs, area, modelPack, baseModelPack, scene);

        // A sub-pack room is normally placed by the resident scene table; only
        // fall back to an origin preview when no room placement exists.
        if (!previewRoom && !HasPlacedRoom(scene))
        {
            LoadRoomModel(modelPack, scene, null, areaMatch, exactModelPack);
        }

        scene.Messages.Insert(0, $"Loaded {area}: {scene.RoomMeshes.Count} room parts, "
            + $"{scene.PlacedMeshes.Count} placed parts, {scene.Plants.Count} plant models, "
            + $"{scene.Instances.Count} instances.");
        return scene;
    }

    private static bool HasPlacedRoom(FieldScene scene)
    {
        return scene.PlacedMeshes.Any(
            mesh => mesh.Name.StartsWith("room_", StringComparison.OrdinalIgnoreCase));
    }

    private static List<GfpakPack> OpenAreaModelPacks(
        string romfs,
        string areaMatch,
        IReadOnlyList<GfpakPack> alreadyOpen)
    {
        var result = new List<GfpakPack>();
        string folder = Path.Combine(romfs, "bin", "archive", "field", "model", "pack");
        if (!Directory.Exists(folder))
        {
            return result;
        }

        var openPaths = new HashSet<string>(
            alreadyOpen.Select(pack => Path.GetFullPath(pack.ArchivePath)),
            StringComparer.OrdinalIgnoreCase);
        var candidates = new List<string>(Directory.EnumerateFiles(folder, "*.gfpak"));
        string shared = Path.Combine(folder, "resident_bg_parts_model.gfpak");
        if (File.Exists(shared))
        {
            candidates.Add(shared);
        }

        string prefix = "ha_" + areaMatch;
        try
        {
            foreach (string file in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Order())
            {
                string name = Path.GetFileNameWithoutExtension(file);
                bool isShared = string.Equals(name, "resident_bg_parts_model", StringComparison.OrdinalIgnoreCase);
                bool isBaseAreaPack = name.Length == 9
                    && name.StartsWith("ha_area", StringComparison.OrdinalIgnoreCase);
                if (!isShared
                    && !isBaseAreaPack
                    && !name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fullPath = Path.GetFullPath(file);
                if (!openPaths.Add(fullPath))
                {
                    continue;
                }

                try
                {
                    result.Add(new GfpakPack(fullPath, ModelPackFolder));
                }
                catch (Exception ex) when (ex is IOException
                    or InvalidDataException
                    or NotSupportedException
                    or UnauthorizedAccessException)
                {
                    // A damaged optional sub-pack must not hide the rest of the area.
                }
            }
        }
        catch
        {
            // Acquisition boundary: release packs opened before an unexpected failure.
            foreach (GfpakPack pack in result)
            {
                pack.Dispose();
            }

            throw;
        }

        return result;
    }

    private static GfpakPack? OpenBaseModelPack(string romfs, string area, string modelPackName)
    {
        if (string.Equals(area, modelPackName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string path = Path.Combine(
            romfs, "bin", "archive", "field", "model", "pack", area + ".gfpak");
        return File.Exists(path) ? new GfpakPack(path, ModelPackFolder) : null;
    }

    private static void LoadRoomModel(
        GfpakPack pack,
        FieldScene scene,
        string? meshFilter,
        string areaMatch,
        bool exactModelPack)
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

        string? requiredRoom = areaMatch.Length > 6 && !exactModelPack
            ? "room_" + areaMatch
            : null;
        TrinityFile? selected = SelectModel(trmdls, meshFilter, requiredRoom, scene);
        if (selected is null)
        {
            return;
        }

        string modelName = selected.Trmdl!.MeshPaths.Count > 0
            ? selected.Trmdl.MeshPaths[0]
            : Path.GetFileName(pack.ArchivePath);
        scene.RoomModelName = modelName;
        scene.Messages.Add($"model: {modelName}");

        var seenMeshes = new HashSet<string>(StringComparer.Ordinal);
        AddModelMeshes(pack, scene, selected.Trmdl, modelName, scene.RoomMeshes, seenMeshes);
    }

    private static TrinityFile? SelectModel(
        List<TrinityFile> models,
        string? filter,
        string? requiredRoom,
        FieldScene scene)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
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

        if (!string.IsNullOrWhiteSpace(requiredRoom))
        {
            string requiredMesh = requiredRoom + ".trmsh";
            foreach (TrinityFile model in models)
            {
                if (model.Trmdl!.MeshPaths.Any(
                    path => string.Equals(Path.GetFileName(path), requiredMesh, StringComparison.OrdinalIgnoreCase)))
                {
                    return model;
                }
            }

            scene.Messages.Add($"room: no '{requiredMesh}' in the model pack; skipped.");
            return null;
        }

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

    private static void LoadTerrain(GfpakPack pack, FieldScene scene, int stride, float tileScale)
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
                + $"{surface.WorldUnitsPerSampleX:0.###}x{surface.WorldUnitsPerSampleZ:0.###} units/sample, "
                + $"extent {surface.WorldExtentX:0.#}x{surface.WorldExtentZ:0.#}, "
                + $"scale {surface.HeightScale:0.###}, {mesh.Vertices.Length:N0} vertices.");

            LoadTerrainSplat(pack, scene, material, tileScale);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException)
        {
            scene.Messages.Add($"warning: terrain unavailable: {ex.Message}");
        }
    }

    private static void LoadTerrainSplat(GfpakPack pack, FieldScene scene, TerrainMaterialSet material, float tileScale)
    {
        string[] layerNames =
        [
            "SplatColor0",
            "SplatColor1",
            "SplatColor2",
            "SplatColor3",
            "SplatColor4",
            "SplatColor5",
            "SplatColor6",
        ];

        var layers = new List<SceneTexture>(layerNames.Length);
        for (int i = 0; i < layerNames.Length; i++)
        {
            string? file = material.GetTexturePath(layerNames[i]);
            if (file is null)
            {
                scene.Messages.Add($"warning: terrain layer '{layerNames[i]}' is not declared.");
                return;
            }

            BntxTexture? texture = ResolveTerrainTexture(pack, material, file, layerNames[i], scene);
            if (texture is null)
            {
                return;
            }

            try
            {
                BntxImage image = BntxTextureDecoder.Decode(texture, 0);
                layers.Add(new SceneTexture
                {
                    Name = texture.Name,
                    Image = image,
                    IsSrgb = texture.IsSrgb,
                });
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
            {
                scene.Messages.Add($"warning: terrain layer '{file}' could not be decoded: {ex.Message}");
                return;
            }
        }

        string? splatFile = material.GetTexturePath(TerrainMaterialSet.SplatAlphaTexture);
        if (splatFile is null)
        {
            scene.Messages.Add("warning: terrain SplatAlpha is not declared.");
            return;
        }

        BntxTexture? splat = ResolveTerrainTexture(
            pack, material, splatFile, TerrainMaterialSet.SplatAlphaTexture, scene);
        if (splat is null)
        {
            return;
        }

        if (splat.ArrayCount < 2)
        {
            scene.Messages.Add($"warning: terrain SplatAlpha has {splat.ArrayCount} array slices; expected 2.");
            return;
        }

        try
        {
            scene.TerrainControl0 = new SceneTexture
            {
                Name = splat.Name + " [0]",
                Image = BntxTextureDecoder.Decode(splat, 0, 0),
                IsSrgb = false,
            };
            scene.TerrainControl1 = new SceneTexture
            {
                Name = splat.Name + " [1]",
                Image = BntxTextureDecoder.Decode(splat, 0, 1),
                IsSrgb = false,
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            scene.Messages.Add($"warning: terrain SplatAlpha could not be decoded: {ex.Message}");
            return;
        }

        for (int i = 0; i < layers.Count; i++)
        {
            TrmtrVec4Parameter? parameter = material.Material.Vec4Parameters.FirstOrDefault(
                item => item.Name == $"SplatST{i}");
            float scale = parameter?.Value.X ?? 1f;
            if (scale <= 0)
            {
                scale = 1f;
            }

            scene.TerrainLayerScales[i] = scale;
        }

        scene.TerrainTileScale = tileScale;
        scene.TerrainLayers.AddRange(layers);
        scene.Messages.Add(
            $"terrain splat: {layers.Count} layers, 2 control slices, UV x{tileScale:0.###}.");
    }

    private static BntxTexture? ResolveTerrainTexture(
        GfpakPack pack,
        TerrainMaterialSet material,
        string fileName,
        string slot,
        FieldScene scene)
    {
        if (!material.TryResolve(pack.Archive, fileName, out GfpakEntry? entry) || entry is null)
        {
            scene.Messages.Add($"warning: terrain texture '{fileName}' ({slot}) is missing.");
            return null;
        }

        BntxFile? bntx = pack.ParseBntx(entry);
        if (bntx is null || bntx.Textures.Count == 0)
        {
            scene.Messages.Add($"warning: terrain texture '{fileName}' ({slot}) could not be parsed.");
            return null;
        }

        return bntx.Textures[0];
    }

    private static void LoadScenePlacements(
        string dumpRoot,
        string romfs,
        string area,
        GfpakPack selectedPack,
        GfpakPack? basePack,
        FieldScene scene)
    {
        string path = Path.Combine(
            dumpRoot, "RomFS", "bin", "archive", "field", ResidentPackFile);
        if (!File.Exists(path))
        {
            scene.Messages.Add($"warning: {ResidentPackFile} not found.");
            return;
        }

        string areaMatch = NormalizeAreaId(area);
        bool isBaseArea = areaMatch.Length == 6;
        var scenePacks = new List<GfpakPack>(2);
        if (basePack is not null)
        {
            scenePacks.Add(basePack);
        }

        scenePacks.Add(selectedPack);
        List<GfpakPack> extraPacks = OpenAreaModelPacks(romfs, areaMatch, scenePacks);
        scenePacks.AddRange(extraPacks);

        try
        {
        var cache = new Dictionary<ulong, CatalogModel>();
        int sceneCount = 0;
        int placedParts = 0;
        int emptyModels = 0;
        int unresolved = 0;
        int unresolvedReported = 0;

        try
        {
            using GfpakArchive resident = GfpakArchive.Open(path);
            ResidentModelCatalog catalog;
            try
            {
                catalog = ResidentModelCatalog.FindInArchive(resident, out GfpakEntry catalogEntry);
                scene.Messages.Add(
                    $"model catalog: {catalog.Entries.Count} entries (resident entry {catalogEntry.Index}).");
            }
            catch (InvalidDataException ex)
            {
                scene.Messages.Add($"warning: model catalog unavailable: {ex.Message}");
                return;
            }

            IReadOnlyList<ResidentSceneArchiveEntry> tables = ResidentSceneFile.FindInArchive(resident);

            foreach (ResidentSceneArchiveEntry table in tables)
            {
                (string? Area, bool HasRoomModel) ownership = ResolveTableOwnership(table, catalog);
                string? tableArea = ownership.Area;
                bool includeWholeTable = tableArea is not null && AreaMatchesTable(tableArea, areaMatch, isBaseArea);
                bool includeNamedBaseOnly = isBaseArea
                    && !includeWholeTable
                    && ownership.HasRoomModel
                    && tableArea is not null
                    && tableArea.Length > 6
                    && tableArea.StartsWith(areaMatch, StringComparison.OrdinalIgnoreCase);
                if (tableArea is not null && !includeWholeTable && !includeNamedBaseOnly)
                {
                    continue;
                }

                foreach (ResidentScene sceneDefinition in table.File.Scenes)
                {
                    if (!catalog.TryGet(sceneDefinition.ModelHash, out ResidentModelCatalogEntry catalogEntry))
                    {
                        continue;
                    }

                    // Vista shells duplicate the heightmap at low detail; the viewer draws the terrain grid.
                    if (catalogEntry.PackedModelFileName.Contains("_vista", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (isBaseArea
                        && catalogEntry.PackedModelFileName.StartsWith("room_", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    SceneAreaMatch match;
                    if (tableArea is null)
                    {
                        match = MatchGenericScene(
                            sceneDefinition, areaMatch, isBaseArea, scene.TerrainSurface);
                    }
                    else if (!includeWholeTable)
                    {
                        if (!TryGetAreaId(sceneDefinition.Name, out string nameArea)
                            || !AreaMatches(nameArea, areaMatch, isBaseArea: true))
                        {
                            continue;
                        }

                        match = SceneAreaMatch.Table;
                    }
                    else
                    {
                        match = SceneAreaMatch.Table;
                    }

                    if (match == SceneAreaMatch.None)
                    {
                        continue;
                    }

                    sceneCount++;
                    if (!cache.TryGetValue(sceneDefinition.ModelHash, out CatalogModel model))
                    {
                        model = LoadCatalogMeshes(catalogEntry, scenePacks, scene);
                        cache[sceneDefinition.ModelHash] = model;
                    }

                    if (model.Meshes.Count == 0)
                    {
                        if (model.Empty)
                        {
                            emptyModels++;
                            continue;
                        }

                        unresolved++;
                        if (unresolvedReported < 10)
                        {
                            scene.Messages.Add(
                                $"warning: model '{catalogEntry.PackedModelFileName}' "
                                + $"(0x{catalogEntry.Hash:X16}) was not found.");
                            unresolvedReported++;
                        }

                        continue;
                    }

                    string displayName = sceneDefinition.Name == "none" || sceneDefinition.Name.Length == 0
                        ? Path.GetFileNameWithoutExtension(catalogEntry.PackedModelFileName)
                        : sceneDefinition.Name;

                    foreach (ResidentSceneNode node in sceneDefinition.Nodes)
                    {
                        if (node.Scale.X == 0 && node.Scale.Y == 0 && node.Scale.Z == 0)
                        {
                            continue;
                        }

                        if (match == SceneAreaMatch.Terrain
                            && !IsNodeNearTerrain(node, scene.TerrainSurface))
                        {
                            continue;
                        }

                        Matrix4x4 transform = Compose(node);
                        foreach (SceneMesh source in model.Meshes)
                        {
                            scene.PlacedMeshes.Add(new SceneMesh
                            {
                                Name = displayName,
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
            + $"{emptyModels} empty models, {unresolved} unresolved models ({Path.GetFileName(path)}).");
        if (unresolved > 0)
        {
            scene.Messages.Add($"warning: {unresolved} area scenes have no resolved model.");
        }
        }
        finally
        {
            foreach (GfpakPack pack in extraPacks)
            {
                pack.Dispose();
            }
        }
    }

    private static string NormalizeAreaId(string area)
    {
        string value = area.StartsWith("ha_", StringComparison.OrdinalIgnoreCase)
            ? area[3..]
            : area;
        return value.Length >= 10 && value[6] == '_' ? value[..10] : value;
    }

    private static (string? Area, bool HasRoomModel) ResolveTableOwnership(
        ResidentSceneArchiveEntry table,
        ResidentModelCatalog catalog)
    {
        var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool hasRoomModel = false;

        foreach (ResidentScene scene in table.File.Scenes)
        {
            ResidentModelCatalogEntry? entry = null;
            if (catalog.TryGet(scene.ModelHash, out ResidentModelCatalogEntry found))
            {
                entry = found;
                if (found.PackedModelFileName.StartsWith("room_", StringComparison.OrdinalIgnoreCase)
                    && TryGetAreaId(found.PackedModelFileName, out string roomArea))
                {
                    hasRoomModel = true;
                    AddAreaScore(scores, roomArea, 10);
                }
            }

            if (TryGetAreaId(scene.Name, out string nameArea))
            {
                AddAreaScore(scores, nameArea, 2);
                continue;
            }

            if (entry is not null)
            {
                if (TryGetAreaId(entry.PackedModelPath, out string packedArea))
                {
                    AddAreaScore(scores, packedArea, 1);
                }
                else if (TryGetAreaId(entry.ModelPath, out string modelArea))
                {
                    AddAreaScore(scores, modelArea, 1);
                }
            }
        }

        if (scores.Count == 0)
        {
            return (null, hasRoomModel);
        }

        string best = scores
            .OrderByDescending(pair => pair.Value)
            .ThenByDescending(pair => pair.Key.Length)
            .First()
            .Key;

        // A sub-area token without an interior room is an outdoor feature pack
        // (for example area00_s02's mountain), so it belongs to the base area.
        if (best.Length > 6 && best[6] == '_' && !hasRoomModel)
        {
            best = best[..6];
        }

        return (best, hasRoomModel);
    }

    private static void AddAreaScore(Dictionary<string, int> scores, string areaId, int weight)
    {
        scores.TryGetValue(areaId, out int current);
        scores[areaId] = current + weight;
    }

    private static bool AreaMatchesTable(string tableArea, string areaMatch, bool isBaseArea)
    {
        return isBaseArea
            ? tableArea.Length == 6 && string.Equals(tableArea, areaMatch, StringComparison.OrdinalIgnoreCase)
            : string.Equals(tableArea, areaMatch, StringComparison.OrdinalIgnoreCase);
    }

    private static SceneAreaMatch MatchGenericScene(
        ResidentScene scene,
        string areaMatch,
        bool isBaseArea,
        TerrainSurface? terrain)
    {
        if (TryGetAreaId(scene.Name, out string nameArea))
        {
            return AreaMatches(nameArea, areaMatch, isBaseArea)
                ? SceneAreaMatch.Named
                : SceneAreaMatch.None;
        }

        if (isBaseArea && terrain is not null && NodesNearTerrain(scene.Nodes, terrain))
        {
            return SceneAreaMatch.Terrain;
        }

        return SceneAreaMatch.None;
    }

    private static bool AreaMatches(string sceneArea, string areaMatch, bool isBaseArea)
    {
        return isBaseArea
            ? sceneArea.StartsWith(areaMatch, StringComparison.OrdinalIgnoreCase)
            : string.Equals(sceneArea, areaMatch, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetAreaId(string? text, out string areaId)
    {
        areaId = string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        int index = text.IndexOf("area", StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            int digit = index + 4;
            if (digit + 1 < text.Length && char.IsDigit(text[digit]) && char.IsDigit(text[digit + 1]))
            {
                areaId = text.Substring(index, 6);
                int sub = digit + 2;
                if (sub + 3 < text.Length
                    && text[sub] == '_'
                    && (text[sub + 1] == 's' || text[sub + 1] == 'S')
                    && char.IsDigit(text[sub + 2])
                    && char.IsDigit(text[sub + 3]))
                {
                    areaId = text.Substring(index, 10);
                }

                return true;
            }

            index = text.IndexOf("area", index + 4, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool NodesNearTerrain(IReadOnlyList<ResidentSceneNode> nodes, TerrainSurface terrain)
    {
        foreach (ResidentSceneNode node in nodes)
        {
            if (IsNodeNearTerrain(node, terrain))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNodeNearTerrain(ResidentSceneNode node, TerrainSurface? terrain)
    {
        if (terrain is null
            || (node.Position.X == 0 && node.Position.Y == 0 && node.Position.Z == 0))
        {
            return false;
        }

        const float tolerance = 12f;
        return terrain.TryGetWorldHeight(node.Position.X, node.Position.Z, out float height)
            && MathF.Abs(node.Position.Y - height) <= tolerance;
    }

    private enum SceneAreaMatch
    {
        None,
        Named,
        Table,
        Terrain,
    }

    private readonly record struct CatalogModel(List<SceneMesh> Meshes, bool Empty);

    private readonly record struct MeshBuildSummary(bool Empty, bool Failed);

    private static CatalogModel LoadCatalogMeshes(
        ResidentModelCatalogEntry entry,
        IReadOnlyList<GfpakPack> packs,
        FieldScene scene)
    {
        bool emptyModel = false;
        bool failedModel = false;

        string fileName = entry.PackedModelFileName;
        foreach (GfpakPack pack in packs)
        {
            GfpakEntry? modelEntry = pack.Find(fileName)
                ?? pack.FindPath(entry.PackedModelPath)
                ?? pack.FindPath(entry.ModelPath);
            if (modelEntry is null)
            {
                continue;
            }

            TrinityFile? file = pack.ParseTrinity(modelEntry);
            if (file?.Kind != TrinityKind.Trmdl || file.Trmdl is null)
            {
                scene.Messages.Add(
                    $"warning: model '{fileName}' in '{pack.ArchivePath}' is not a TRMDL.");
                failedModel = true;
                continue;
            }

            var meshes = new List<SceneMesh>();
            MeshBuildSummary summary = AddModelMeshes(
                pack, scene, file.Trmdl, fileName, meshes, new HashSet<string>(StringComparer.Ordinal));
            if (meshes.Count > 0)
            {
                return new CatalogModel(meshes, Empty: false);
            }

            emptyModel |= summary.Empty;
            failedModel |= summary.Failed;
        }

        return new CatalogModel([], emptyModel && !failedModel);
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

    private static MeshBuildSummary AddModelMeshes(
        GfpakPack pack,
        FieldScene scene,
        TrmdlFile model,
        string owner,
        List<SceneMesh> target,
        HashSet<string> seenMeshes)
    {
        bool anyPath = false;
        bool emptyMesh = false;
        bool failed = false;

        IEnumerable<string> meshPaths = SelectLod0Paths(model, owner, scene);
        foreach (string meshPath in meshPaths)
        {
            anyPath = true;
            string meshKey = pack.LogicalFolder + meshPath;
            if (!seenMeshes.Add(meshKey))
            {
                continue;
            }

            TrinityFile? trmshFile = ResolveTrinity(pack, meshPath, scene, owner);
            if (trmshFile?.Kind != TrinityKind.Trmsh)
            {
                failed = true;
                continue;
            }

            TrmshFile trmsh = trmshFile.Trmsh!;
            if (trmsh.Meshes.Count == 0)
            {
                emptyMesh = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(trmsh.BufferFilePath))
            {
                scene.Messages.Add($"warning: '{meshPath}' has no buffer path.");
                failed = true;
                continue;
            }

            TrinityFile? trmbfFile = ResolveTrinity(pack, trmsh.BufferFilePath, scene, owner);
            if (trmbfFile?.Kind != TrinityKind.Trmbf)
            {
                failed = true;
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
            if (count < trmsh.Meshes.Count)
            {
                failed = true;
            }

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
                    failed = true;
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

        return new MeshBuildSummary(emptyMesh || !anyPath, failed);
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

        TrmtrMaterial? source = FindMaterial(pack, model, materialName, scene, owner, out string key);
        int existing = scene.FindMaterialByKey(key);
        if (existing >= 0)
        {
            return existing;
        }

        if (source is null)
        {
            scene.Materials.Add(new SceneMaterial { Key = key, Name = materialName });
            return scene.Materials.Count - 1;
        }

        int baseColor = FindTexture(scene, pack, source, "BaseColorMap", owner);
        int normal = FindTexture(scene, pack, source, "NormalMap", owner);
        int ao = FindTexture(scene, pack, source, "AOMap", owner);

        scene.Materials.Add(new SceneMaterial
        {
            Key = key,
            Name = materialName,
            BaseColorTexture = baseColor,
            NormalTexture = normal,
            AmbientOcclusionTexture = ao,
            UseVertexColor = ReadShaderBool(source, "EnableVertexBaseColor"),
            Tint = baseColor < 0 && materialName.Contains("water", StringComparison.OrdinalIgnoreCase)
                ? new Vector3(0.18f, 0.32f, 0.45f)
                : Vector3.One,
        });
        return scene.Materials.Count - 1;
    }

    private static bool ReadShaderBool(TrmtrMaterial material, string name)
    {
        foreach (TrmtrShader shader in material.Shaders)
        {
            foreach (TrmtrStringParameter parameter in shader.Values)
            {
                if (string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(parameter.Value, "True", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        return false;
    }

    private static TrmtrMaterial? FindMaterial(
        GfpakPack pack,
        TrmdlFile model,
        string materialName,
        FieldScene scene,
        string owner,
        out string key)
    {
        foreach (string materialPath in model.Materials)
        {
            TrinityFile? file = ResolveTrinity(pack, materialPath, scene, owner);
            if (file?.Kind != TrinityKind.Trmtr)
            {
                continue;
            }

            IReadOnlyList<TrmtrMaterial> materials = file.Trmtr!.Materials;
            for (int i = 0; i < materials.Count; i++)
            {
                TrmtrMaterial material = materials[i];
                if (string.Equals(material.Name, materialName, StringComparison.Ordinal))
                {
                    key = $"{pack.ArchivePath}|{materialPath}|{i}|{materialName}";
                    return material;
                }
            }
        }

        key = $"{pack.ArchivePath}|?|{materialName}";
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


