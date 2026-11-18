// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using System.Text;
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
    public const int MaxInstances = 1_000_000;

    private const int RootTableOffset = 20;
    private const int RootVTableOffset = 6;
    private const int RootVTableSize = 14;
    private const int RootObjectSize = 22;
    private const int BufferVTableOffset = 42;
    private const int BufferVTableSize = 6;
    private const int BufferObjectSize = 8;
    private const int BufferTableOffset = 48;
    private const int BufferVectorOffset = 56;
    private const int PayloadOffset = 60;
    private const int StringLengthSize = 4;
    private const int Alignment = 4;

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

    /// <summary>Creates a writable TRINS value with the observed schema.</summary>
    public static TrinsFile Create(
        string typeName,
        uint version,
        uint reserved,
        IReadOnlyList<Matrix4x4RowMajor> instances)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(instances);

        if (!string.Equals(typeName, SrtInstanceBufferType, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported TRINS type '{typeName}'.");
        }

        if (instances.Count > MaxInstances)
        {
            throw new InvalidDataException(
                $"TRINS instance count {instances.Count} exceeds the limit of {MaxInstances}.");
        }

        var copy = new Matrix4x4RowMajor[instances.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = instances[i];
        }

        return new TrinsFile(typeName, version, checked((uint)copy.Length), reserved, copy);
    }

    /// <summary>Returns a new TRINS value with one matrix appended.</summary>
    public TrinsFile WithInstance(Matrix4x4RowMajor instance)
    {
        if (Count >= MaxInstances)
        {
            throw new InvalidDataException(
                $"TRINS instance count {Count} has reached the limit of {MaxInstances}.");
        }

        var instances = new Matrix4x4RowMajor[Instances.Count + 1];
        for (int i = 0; i < Instances.Count; i++)
        {
            instances[i] = Instances[i];
        }

        instances[^1] = instance;
        return new TrinsFile(TypeName, Version, checked(Count + 1), Reserved, instances);
    }

    /// <summary>Rebuilds the observed FlatBuffer layout, including the instance payload.</summary>
    public byte[] ToBytes()
    {
        if (!string.Equals(TypeName, SrtInstanceBufferType, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Unsupported TRINS type '{TypeName}'.");
        }

        if (Reserved != 0)
        {
            throw new NotSupportedException(
                "The TRINS writer supports only the observed layout without a reserved value.");
        }

        if (Count != checked((uint)Instances.Count))
        {
            throw new InvalidDataException(
                $"TRINS count {Count} does not match {Instances.Count} matrices.");
        }

        if (Count > MaxInstances)
        {
            throw new InvalidDataException(
                $"TRINS instance count {Count} exceeds the limit of {MaxInstances}.");
        }

        int typeNameLength = Encoding.UTF8.GetByteCount(TypeName);
        long payloadLength = checked((long)Count * InstanceStride);
        long stringOffset = PayloadOffset + payloadLength;
        long stringEnd = stringOffset + StringLengthSize + typeNameLength;
        long totalLength = (stringEnd + Alignment - 1L) & ~(Alignment - 1L);
        if (totalLength > int.MaxValue)
        {
            throw new InvalidDataException(
                $"TRINS output would be {totalLength} bytes; the maximum is {int.MaxValue}.");
        }

        byte[] data = new byte[(int)totalLength];
        WriteUInt32(data, 0, RootTableOffset);
        WriteUInt16(data, RootVTableOffset, RootVTableSize);
        WriteUInt16(data, RootVTableOffset + 2, RootObjectSize);

        ReadOnlySpan<ushort> fieldOffsets = [4, 8, 12, 0, 16];
        for (int i = 0; i < fieldOffsets.Length; i++)
        {
            WriteUInt16(data, RootVTableOffset + 4 + (i * 2), fieldOffsets[i]);
        }

        WriteInt32(data, RootTableOffset, RootTableOffset - RootVTableOffset);
        WriteUInt32(
            data,
            RootTableOffset + 4,
            checked((uint)(stringOffset - (RootTableOffset + 4))));
        WriteUInt32(data, RootTableOffset + 8, Version);
        WriteUInt32(data, RootTableOffset + 12, Count);
        WriteUInt32(
            data,
            RootTableOffset + 16,
            checked((uint)(BufferTableOffset - (RootTableOffset + 16))));

        WriteUInt16(data, BufferVTableOffset, BufferVTableSize);
        WriteUInt16(data, BufferVTableOffset + 2, BufferObjectSize);
        WriteUInt16(data, BufferVTableOffset + 4, 4);
        WriteInt32(data, BufferTableOffset, BufferTableOffset - BufferVTableOffset);
        WriteUInt32(data, BufferTableOffset + 4, 4);
        WriteUInt32(data, BufferVectorOffset, checked((uint)payloadLength));

        int payloadOffset = PayloadOffset;
        foreach (Matrix4x4RowMajor matrix in Instances)
        {
            WriteMatrix(data, payloadOffset, matrix);
            payloadOffset += InstanceStride;
        }

        WriteUInt32(data, (int)stringOffset, checked((uint)typeNameLength));
        Encoding.UTF8.GetBytes(TypeName, data.AsSpan((int)stringOffset + StringLengthSize));
        return data;
    }

    public static TrinsFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return Parse(File.ReadAllBytes(path));
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Failed to parse TRINS '{path}': {ex.Message}", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new NotSupportedException($"Unsupported TRINS '{path}': {ex.Message}", ex);
        }
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

    private static void WriteMatrix(byte[] data, int offset, Matrix4x4RowMajor matrix)
    {
        WriteRow(data, offset, matrix.Row0);
        WriteRow(data, offset + 16, matrix.Row1);
        WriteRow(data, offset + 32, matrix.Row2);
        WriteRow(data, offset + 48, matrix.Row3);
    }

    private static void WriteRow(byte[] data, int offset, Vector4f row)
    {
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset), row.X);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 4), row.Y);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 8), row.Z);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset + 12), row.W);
    }

    private static void WriteUInt16(byte[] data, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);

    private static void WriteInt32(byte[] data, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset), value);

    private static void WriteUInt32(byte[] data, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
