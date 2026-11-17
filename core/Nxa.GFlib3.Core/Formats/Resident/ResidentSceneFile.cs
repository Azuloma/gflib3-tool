// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Core.Formats.Resident;

/// <summary>
/// Scene placement entry from <c>resident_release.gfpak</c>. Each scene names a
/// model resource and carries one or more placement nodes with an SRT transform.
/// </summary>
public sealed class ResidentSceneFile
{
    /// <summary>Floor for archive scans; the smallest placement entry observed is 320 bytes.</summary>
    public const int MinArchiveBytes = 16;

    /// <summary>Largest placement entry observed in the dump (86,928 bytes).</summary>
    public const int MaxArchiveBytes = 1 << 22;

    private const int MinSceneFieldCount = 5;
    private const int MaxSceneFieldCount = 12;
    private const int NodeFieldCount = 10;
    private const int MaxScenes = 10_000;
    private const int MaxNodesPerScene = 1_000;

    private ResidentSceneFile(ResidentScene[] scenes)
    {
        Scenes = scenes;
    }

    public IReadOnlyList<ResidentScene> Scenes { get; }

    public static ResidentSceneFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Parse(File.ReadAllBytes(path));
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Failed to parse resident scene '{path}': {ex.Message}", ex);
        }
    }

    public static ResidentSceneFile Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        FlatBufferTable root = FlatBufferTable.GetRoot(data);
        if (!root.TryGetVector(0, 4, out FlatBufferVector vector))
        {
            throw new InvalidDataException("Scene file has no scene vector.");
        }

        if (vector.Count == 0 || vector.Count > MaxScenes)
        {
            throw new InvalidDataException($"Scene count {vector.Count} is outside 1..{MaxScenes}.");
        }

        var scenes = new ResidentScene[vector.Count];
        for (int i = 0; i < scenes.Length; i++)
        {
            try
            {
                scenes[i] = ParseScene(vector.GetTable(i));
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Scene {i}: {ex.Message}", ex);
            }
        }

        return new ResidentSceneFile(scenes);
    }

    /// <summary>
    /// Attempts a structural parse. Placement entries have a distinct node shape
    /// (position/yaw/scale fields), which rejects door and catalog tables.
    /// </summary>
    public static bool TryParse(byte[] data, out ResidentSceneFile? file)
    {
        try
        {
            file = Parse(data);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException)
        {
            file = null;
            return false;
        }
    }

    /// <summary>Finds every placement entry in a resident archive by structure.</summary>
    public static IReadOnlyList<ResidentSceneArchiveEntry> FindInArchive(GfpakArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var results = new List<ResidentSceneArchiveEntry>();
        foreach (GfpakEntry entry in archive.Entries)
        {
            if (entry.SizeDecompressed < MinArchiveBytes
                || entry.SizeDecompressed > MaxArchiveBytes)
            {
                continue;
            }

            byte[] data = archive.ReadEntry(entry);
            if (TryParse(data, out ResidentSceneFile? file))
            {
                results.Add(new ResidentSceneArchiveEntry(entry, file!));
            }
        }

        return results;
    }

    private static ResidentScene ParseScene(FlatBufferTable table)
    {
        if (table.FieldCount < MinSceneFieldCount || table.FieldCount > MaxSceneFieldCount)
        {
            throw new InvalidDataException(
                $"Scene table has {table.FieldCount} fields; expected {MinSceneFieldCount}..{MaxSceneFieldCount}.");
        }

        string name = table.GetString(0)
            ?? throw new InvalidDataException("Scene has no name.");

        ulong sceneHash = table.GetUInt64(1);
        ulong modelHash = table.GetUInt64(2);
        bool? flag = table.HasField(3) ? table.GetBool(3) : null;
        ResidentSceneNode[] nodes = ReadNodes(table);

        return new ResidentScene(name, sceneHash, modelHash, flag, nodes);
    }

    private static ResidentSceneNode[] ReadNodes(FlatBufferTable table)
    {
        if (!table.TryGetVector(4, 4, out FlatBufferVector vector))
        {
            throw new InvalidDataException("Scene has no placement node vector.");
        }

        if (vector.Count > MaxNodesPerScene)
        {
            throw new InvalidDataException($"Node count {vector.Count} exceeds {MaxNodesPerScene}.");
        }

        var nodes = new ResidentSceneNode[vector.Count];
        for (int i = 0; i < nodes.Length; i++)
        {
            try
            {
                nodes[i] = ParseNode(vector.GetTable(i));
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Node {i}: {ex.Message}", ex);
            }
        }

        return nodes;
    }

    private static ResidentSceneNode ParseNode(FlatBufferTable table)
    {
        if (table.FieldCount != NodeFieldCount)
        {
            throw new InvalidDataException(
                $"Node table has {table.FieldCount} fields; expected {NodeFieldCount}.");
        }

        string target = table.GetString(2) ?? string.Empty;

        FlatBufferTable position = table.GetTable(7)
            ?? throw new InvalidDataException("Placement node has no position.");
        FlatBufferTable rotation = table.GetTable(8)
            ?? throw new InvalidDataException("Placement node has no rotation.");
        FlatBufferTable scale = table.GetTable(9)
            ?? throw new InvalidDataException("Placement node has no scale.");

        return new ResidentSceneNode(
            target,
            new Vector3f(position.GetFloat(0), position.GetFloat(1), position.GetFloat(2)),
            rotation.GetFloat(1),
            new Vector3f(scale.GetFloat(0), scale.GetFloat(1), scale.GetFloat(2)));
    }
}

public sealed class ResidentScene
{
    internal ResidentScene(
        string name,
        ulong sceneHash,
        ulong modelHash,
        bool? flag,
        IReadOnlyList<ResidentSceneNode> nodes)
    {
        Name = name;
        SceneHash = sceneHash;
        ModelHash = modelHash;
        Flag = flag;
        Nodes = nodes;
    }

    /// <summary>Scene name, for example <c>area00_s02_ground_mountain01_tengan</c>.</summary>
    public string Name { get; }

    public ulong SceneHash { get; }

    /// <summary>Resource hash; <c>ground_mountain01_tengan</c> matches the name suffix.</summary>
    public ulong ModelHash { get; }

    /// <summary>Single-byte flag; present on background-model scenes, value 1.</summary>
    public bool? Flag { get; }

    public IReadOnlyList<ResidentSceneNode> Nodes { get; }
}

public sealed class ResidentSceneNode
{
    internal ResidentSceneNode(string targetName, Vector3f position, float yawDegrees, Vector3f scale)
    {
        TargetName = targetName;
        Position = position;
        YawDegrees = yawDegrees;
        Scale = scale;
    }

    /// <summary>Visibility group or event name, for example <c>FEVE_VISIBLE_*</c>.</summary>
    public string TargetName { get; }

    public Vector3f Position { get; }

    /// <summary>Rotation about Y in degrees; absent field means zero.</summary>
    public float YawDegrees { get; }

    public Vector3f Scale { get; }
}

public sealed class ResidentSceneArchiveEntry
{
    internal ResidentSceneArchiveEntry(GfpakEntry entry, ResidentSceneFile file)
    {
        Entry = entry;
        File = file;
    }

    public GfpakEntry Entry { get; }

    public ResidentSceneFile File { get; }
}
