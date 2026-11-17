// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

/// <summary>TRMBF: vertex/index buffer payloads referenced by TRMSH.</summary>
public sealed class TrmbfFile
{
    private TrmbfFile(uint version, TrmbfMeshBuffer[] buffers)
    {
        Version = version;
        Buffers = buffers;
    }

    public uint Version { get; }

    public IReadOnlyList<TrmbfMeshBuffer> Buffers { get; }

    public static TrmbfFile Parse(FlatBufferTable root)
    {
        return new TrmbfFile(root.GetUInt32(0), TrinityParse.TableVector(root, 1, ParseMeshBuffer));
    }

    private static TrmbfMeshBuffer ParseMeshBuffer(FlatBufferTable table)
    {
        return new TrmbfMeshBuffer(
            ParseBuffers(table, 0),
            ParseBuffers(table, 1),
            TrinityParse.TableVector(table, 2, ParseMorphTarget));
    }

    private static ReadOnlyMemory<byte>[] ParseBuffers(FlatBufferTable table, int field)
    {
        if (!table.TryGetVector(field, 4, out FlatBufferVector vector))
        {
            return Array.Empty<ReadOnlyMemory<byte>>();
        }

        var buffers = new ReadOnlyMemory<byte>[vector.Count];
        for (int i = 0; i < buffers.Length; i++)
        {
            buffers[i] = vector.GetTable(i).GetByteVector(0);
        }

        return buffers;
    }

    private static TrmbfMorphTarget ParseMorphTarget(FlatBufferTable table)
    {
        return new TrmbfMorphTarget(ParseBuffers(table, 0));
    }
}

public sealed class TrmbfMeshBuffer
{
    internal TrmbfMeshBuffer(
        IReadOnlyList<ReadOnlyMemory<byte>> indexBuffers,
        IReadOnlyList<ReadOnlyMemory<byte>> vertexBuffers,
        IReadOnlyList<TrmbfMorphTarget> morphTargets)
    {
        IndexBuffers = indexBuffers;
        VertexBuffers = vertexBuffers;
        MorphTargets = morphTargets;
    }

    public IReadOnlyList<ReadOnlyMemory<byte>> IndexBuffers { get; }

    public IReadOnlyList<ReadOnlyMemory<byte>> VertexBuffers { get; }

    public IReadOnlyList<TrmbfMorphTarget> MorphTargets { get; }
}

public sealed class TrmbfMorphTarget
{
    internal TrmbfMorphTarget(IReadOnlyList<ReadOnlyMemory<byte>> buffers)
    {
        Buffers = buffers;
    }

    public IReadOnlyList<ReadOnlyMemory<byte>> Buffers { get; }
}
