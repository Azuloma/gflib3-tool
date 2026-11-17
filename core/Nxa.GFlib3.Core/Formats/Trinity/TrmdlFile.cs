// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

/// <summary>TRMDL: top-level model definition referencing mesh, material, skeleton.</summary>
public sealed class TrmdlFile
{
    private TrmdlFile(
        uint version,
        string[] meshPaths,
        string? skeletonPath,
        string[] materials,
        TrModelLod[] lods,
        BoundingBox? bounds,
        Vector4f field06,
        string? trlttPath,
        uint field08,
        uint field09)
    {
        Version = version;
        MeshPaths = meshPaths;
        SkeletonPath = skeletonPath;
        Materials = materials;
        Lods = lods;
        Bounds = bounds;
        Field06 = field06;
        TrlttPath = trlttPath;
        Field08 = field08;
        Field09 = field09;
    }

    public uint Version { get; }

    public IReadOnlyList<string> MeshPaths { get; }

    public string? SkeletonPath { get; }

    public IReadOnlyList<string> Materials { get; }

    public IReadOnlyList<TrModelLod> Lods { get; }

    public BoundingBox? Bounds { get; }

    public Vector4f Field06 { get; }

    public string? TrlttPath { get; }

    public uint Field08 { get; }

    public uint Field09 { get; }

    public static TrmdlFile Parse(FlatBufferTable root)
    {
        FlatBufferTable? skeleton = root.GetTable(2);

        return new TrmdlFile(
            root.GetUInt32(0),
            TrinityParse.TableVector(root, 1, static table => table.GetString(0) ?? string.Empty),
            skeleton?.GetString(0),
            TrinityParse.StringVector(root, 3),
            TrinityParse.TableVector(root, 4, ParseLod),
            TrinityParse.Bounds(root, 5),
            root.GetVector4f(6),
            root.GetString(7),
            root.GetUInt32(8),
            root.GetUInt32(9));
    }

    private static TrModelLod ParseLod(FlatBufferTable table)
    {
        return new TrModelLod(
            TrinityParse.TableVector(table, 0, static entry => entry.GetUInt32(0)),
            table.GetString(1));
    }
}

public sealed class TrModelLod
{
    internal TrModelLod(IReadOnlyList<uint> entryIndices, string? type)
    {
        EntryIndices = entryIndices;
        Type = type;
    }

    public IReadOnlyList<uint> EntryIndices { get; }

    public string? Type { get; }
}
