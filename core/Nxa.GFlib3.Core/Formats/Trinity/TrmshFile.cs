// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

/// <summary>TRMSH: mesh declarations and material partitions.</summary>
public sealed class TrmshFile
{
    private TrmshFile(uint version, TrmshMesh[] meshes, string? bufferFilePath)
    {
        Version = version;
        Meshes = meshes;
        BufferFilePath = bufferFilePath;
    }

    public uint Version { get; }

    public IReadOnlyList<TrmshMesh> Meshes { get; }

    /// <summary>Name of the referenced <c>.trmbf</c> file.</summary>
    public string? BufferFilePath { get; }

    public static TrmshFile Parse(FlatBufferTable root)
    {
        return new TrmshFile(
            root.GetUInt32(0),
            TrinityParse.TableVector(root, 1, ParseMesh),
            root.GetString(2));
    }

    private static TrmshMesh ParseMesh(FlatBufferTable table)
    {
        return new TrmshMesh(
            table.GetString(0),
            TrinityParse.Bounds(table, 1),
            (TrIndexFormat)table.GetUInt32(2),
            TrinityParse.TableVector(table, 3, ParseVertexDeclaration),
            TrinityParse.TableVector(table, 4, ParseMeshPart),
            table.GetUInt32(5),
            table.GetUInt32(6),
            table.GetUInt32(7),
            table.GetUInt32(8),
            table.GetSphere(9),
            TrinityParse.TableVector(table, 10, ParseBoneWeight),
            TrinityParse.TableVector(table, 11, ParseVisibilityShape),
            table.GetString(12),
            table.GetUInt32(13),
            TrinityParse.TableVector(table, 14, ParseMorphShape));
    }

    private static TrVertexDeclaration ParseVertexDeclaration(FlatBufferTable table)
    {
        return new TrVertexDeclaration(
            TrinityParse.TableVector(table, 0, ParseVertexElement),
            ParseSizes(table, 1));
    }

    private static TrVertexElement ParseVertexElement(FlatBufferTable table)
    {
        return new TrVertexElement(
            table.GetInt32(0, -1),
            (TrVertexUsage)table.GetUInt32(1),
            table.GetUInt32(2),
            (TrVertexFormat)table.GetUInt32(3),
            table.GetUInt32(4));
    }

    private static uint[] ParseSizes(FlatBufferTable table, int field)
    {
        if (!table.TryGetVector(field, 4, out FlatBufferVector vector))
        {
            return Array.Empty<uint>();
        }

        var sizes = new uint[vector.Count];
        for (int i = 0; i < sizes.Length; i++)
        {
            sizes[i] = vector.GetTable(i).GetUInt32(0);
        }

        return sizes;
    }

    private static TrMeshPart ParseMeshPart(FlatBufferTable table)
    {
        return new TrMeshPart(
            table.GetUInt32(0),
            table.GetUInt32(1),
            table.GetUInt32(2),
            table.GetString(3),
            table.GetInt32(4, -1));
    }

    private static TrBoneWeight ParseBoneWeight(FlatBufferTable table)
    {
        return new TrBoneWeight(table.GetUInt32(0), table.GetFloat(1));
    }

    private static TrVisibilityShape ParseVisibilityShape(FlatBufferTable table)
    {
        return new TrVisibilityShape(table.GetUInt32(0), table.GetString(1));
    }

    private static TrMorphShape ParseMorphShape(FlatBufferTable table)
    {
        int dataCount = table.TryGetVector(0, 4, out FlatBufferVector data) ? data.Count : 0;
        return new TrMorphShape(table.GetString(2), dataCount, ParseMorphMetadata(table));
    }

    private static string[] ParseMorphMetadata(FlatBufferTable table)
    {
        if (!table.TryGetVector(1, 4, out FlatBufferVector vector))
        {
            return Array.Empty<string>();
        }

        var names = new string[vector.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = vector.GetTable(i).GetString(1) ?? string.Empty;
        }

        return names;
    }
}

public sealed class TrmshMesh
{
    internal TrmshMesh(
        string? name,
        BoundingBox? bounds,
        TrIndexFormat indexFormat,
        IReadOnlyList<TrVertexDeclaration> vertexDeclarations,
        IReadOnlyList<TrMeshPart> meshParts,
        uint field05,
        uint field06,
        uint field07,
        uint field08,
        Sphere clipSphere,
        IReadOnlyList<TrBoneWeight> boneWeights,
        IReadOnlyList<TrVisibilityShape> visibilityShapes,
        string? field12,
        uint field13,
        IReadOnlyList<TrMorphShape> morphShapes)
    {
        Name = name;
        Bounds = bounds;
        IndexFormat = indexFormat;
        VertexDeclarations = vertexDeclarations;
        MeshParts = meshParts;
        Field05 = field05;
        Field06 = field06;
        Field07 = field07;
        Field08 = field08;
        ClipSphere = clipSphere;
        BoneWeights = boneWeights;
        VisibilityShapes = visibilityShapes;
        Field12 = field12;
        Field13 = field13;
        MorphShapes = morphShapes;
    }

    public string? Name { get; }

    public BoundingBox? Bounds { get; }

    public TrIndexFormat IndexFormat { get; }

    public IReadOnlyList<TrVertexDeclaration> VertexDeclarations { get; }

    public IReadOnlyList<TrMeshPart> MeshParts { get; }

    public uint Field05 { get; }

    public uint Field06 { get; }

    public uint Field07 { get; }

    public uint Field08 { get; }

    public Sphere ClipSphere { get; }

    public IReadOnlyList<TrBoneWeight> BoneWeights { get; }

    public IReadOnlyList<TrVisibilityShape> VisibilityShapes { get; }

    public string? Field12 { get; }

    public uint Field13 { get; }

    public IReadOnlyList<TrMorphShape> MorphShapes { get; }
}

public sealed class TrVertexDeclaration
{
    internal TrVertexDeclaration(IReadOnlyList<TrVertexElement> elements, IReadOnlyList<uint> sizes)
    {
        Elements = elements;
        Sizes = sizes;
    }

    public IReadOnlyList<TrVertexElement> Elements { get; }

    public IReadOnlyList<uint> Sizes { get; }

    /// <summary>Vertex stride in bytes (first size entry), or 0 if absent.</summary>
    public uint Stride => Sizes.Count > 0 ? Sizes[0] : 0;
}

public sealed class TrVertexElement
{
    internal TrVertexElement(
        int vertexElementSizeIndex,
        TrVertexUsage usage,
        uint layer,
        TrVertexFormat format,
        uint offset)
    {
        VertexElementSizeIndex = vertexElementSizeIndex;
        Usage = usage;
        Layer = layer;
        Format = format;
        Offset = offset;
    }

    public int VertexElementSizeIndex { get; }

    public TrVertexUsage Usage { get; }

    public uint Layer { get; }

    public TrVertexFormat Format { get; }

    public uint Offset { get; }
}

public sealed class TrMeshPart
{
    internal TrMeshPart(
        uint indexCount,
        uint indexOffset,
        uint field02,
        string? materialName,
        int vertexDeclarationIndex)
    {
        IndexCount = indexCount;
        IndexOffset = indexOffset;
        Field02 = field02;
        MaterialName = materialName;
        VertexDeclarationIndex = vertexDeclarationIndex;
    }

    public uint IndexCount { get; }

    public uint IndexOffset { get; }

    public uint Field02 { get; }

    public string? MaterialName { get; }

    public int VertexDeclarationIndex { get; }
}

public sealed class TrBoneWeight
{
    internal TrBoneWeight(uint rigIndex, float rigWeight)
    {
        RigIndex = rigIndex;
        RigWeight = rigWeight;
    }

    public uint RigIndex { get; }

    public float RigWeight { get; }
}

public sealed class TrVisibilityShape
{
    internal TrVisibilityShape(uint index, string? name)
    {
        Index = index;
        Name = name;
    }

    public uint Index { get; }

    public string? Name { get; }
}

public sealed class TrMorphShape
{
    internal TrMorphShape(string? name, int dataCount, IReadOnlyList<string> metadata)
    {
        Name = name;
        DataCount = dataCount;
        Metadata = metadata;
    }

    public string? Name { get; }

    public int DataCount { get; }

    public IReadOnlyList<string> Metadata { get; }
}
