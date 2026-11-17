// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Terrain;

/// <summary>
/// TRINS placement file. The only observed type is <c>SrtInstanceBuffer</c>,
/// whose payload is a packed array of row-major 4×4 transform matrices.
/// </summary>
public sealed class TrinsFile
{
    public const string SrtInstanceBufferType = "SrtInstanceBuffer";
    public const int InstanceStride = 64;

    private const int MaxInstances = 1_000_000;

    private TrinsFile(
        string typeName,
        uint version,
        uint count,
        uint reserved,
        Matrix4x4RowMajor[] instances)
    {
        TypeName = typeName;
        Version = version;
        Count = count;
        Reserved = reserved;
        Instances = instances;
    }

    public string TypeName { get; }

    public uint Version { get; }

    public uint Count { get; }

    public uint Reserved { get; }

    public IReadOnlyList<Matrix4x4RowMajor> Instances { get; }

    public static TrinsFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllBytes(path));
    }

    public static TrinsFile Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Parse(FlatBufferTable.GetRoot(data));
    }

    public static TrinsFile Parse(FlatBufferTable root)
    {
        string typeName = root.GetString(0)
            ?? throw new InvalidDataException("TRINS root has no type name.");
        if (!string.Equals(typeName, SrtInstanceBufferType, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported TRINS type '{typeName}'.");
        }

        uint version = root.GetUInt32(1);
        uint count = root.GetUInt32(2);
        uint reserved = root.GetUInt32(3);

        if (count > MaxInstances)
        {
            throw new InvalidDataException($"TRINS instance count {count} exceeds the limit.");
        }

        FlatBufferTable buffer = root.GetTable(4)
            ?? throw new InvalidDataException("TRINS root has no instance buffer.");
        ReadOnlyMemory<byte> payload = buffer.GetByteVector(0);

        long expected = checked((long)count * InstanceStride);
        if (payload.Length != expected)
        {
            throw new InvalidDataException(
                $"TRINS payload is 0x{payload.Length:X} bytes; expected 0x{expected:X} for {count} instances.");
        }

        var instances = new Matrix4x4RowMajor[(int)count];
        ReadOnlySpan<byte> span = payload.Span;
        for (int i = 0; i < instances.Length; i++)
        {
            ReadOnlySpan<byte> item = span.Slice(i * InstanceStride, InstanceStride);
            instances[i] = new Matrix4x4RowMajor(
                ReadRow(item, 0),
                ReadRow(item, 16),
                ReadRow(item, 32),
                ReadRow(item, 48));
        }

        return new TrinsFile(typeName, version, count, reserved, instances);
    }

    private static Vector4f ReadRow(ReadOnlySpan<byte> data, int offset)
    {
        return new Vector4f(
            BinaryPrimitives.ReadSingleLittleEndian(data[offset..]),
            BinaryPrimitives.ReadSingleLittleEndian(data[(offset + 4)..]),
            BinaryPrimitives.ReadSingleLittleEndian(data[(offset + 8)..]),
            BinaryPrimitives.ReadSingleLittleEndian(data[(offset + 12)..]));
    }
}
