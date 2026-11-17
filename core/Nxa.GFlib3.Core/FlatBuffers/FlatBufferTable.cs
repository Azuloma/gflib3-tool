// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using System.Text;

namespace Nxa.GFlib3.Core.FlatBuffers;

/// <summary>
/// Minimal, bounds-checked FlatBuffer table reader. Trinity files are
/// FlatBuffers; this avoids a generated dependency and treats input as hostile.
/// </summary>
public readonly struct FlatBufferTable
{
    private readonly byte[] _data;
    private readonly int _position;

    internal FlatBufferTable(byte[] data, int position)
    {
        _data = data;
        _position = position;
    }

    public static FlatBufferTable GetRoot(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 4)
        {
            throw new InvalidDataException("FlatBuffer is too small.");
        }

        int root = BinaryPrimitives.ReadInt32LittleEndian(data);
        if (root <= 0 || root >= data.Length)
        {
            throw new InvalidDataException($"FlatBuffer root offset 0x{root:X} is out of range.");
        }

        return new FlatBufferTable(data, root);
    }

    public bool HasField(int index) => GetFieldPosition(index) >= 0;

    public byte GetByte(int index, byte defaultValue = 0)
    {
        int position = GetFieldPosition(index);
        return position < 0 ? defaultValue : ReadByte(position);
    }

    public sbyte GetSByte(int index, sbyte defaultValue = 0)
    {
        int position = GetFieldPosition(index);
        return position < 0 ? defaultValue : unchecked((sbyte)ReadByte(position));
    }

    public bool GetBool(int index, bool defaultValue = false)
    {
        int position = GetFieldPosition(index);
        return position < 0 ? defaultValue : ReadByte(position) != 0;
    }

    public ushort GetUInt16(int index, ushort defaultValue = 0)
    {
        int position = GetFieldPosition(index);
        return position < 0 ? defaultValue : ReadUInt16(position);
    }

    public int GetInt32(int index, int defaultValue = 0)
    {
        int position = GetFieldPosition(index);
        return position < 0 ? defaultValue : ReadInt32(position);
    }

    public uint GetUInt32(int index, uint defaultValue = 0)
    {
        int position = GetFieldPosition(index);
        return position < 0 ? defaultValue : ReadUInt32(position);
    }

    public float GetFloat(int index, float defaultValue = 0)
    {
        int position = GetFieldPosition(index);
        return position < 0 ? defaultValue : ReadFloat(position);
    }

    public string? GetString(int index)
    {
        int position = GetFieldPosition(index);
        if (position < 0)
        {
            return null;
        }

        int target = position + ReadOffset(position);
        int length = ReadLength(target);
        Require(target + 4, length);
        return Encoding.UTF8.GetString(_data, target + 4, length);
    }

    public FlatBufferTable? GetTable(int index)
    {
        int position = GetFieldPosition(index);
        if (position < 0)
        {
            return null;
        }

        int target = position + ReadOffset(position);
        if (target <= 0 || target + 4 > _data.Length)
        {
            throw new InvalidDataException($"FlatBuffer table at 0x{target:X} is out of range.");
        }

        return new FlatBufferTable(_data, target);
    }

    public bool TryGetVector(int index, int elementSize, out FlatBufferVector vector)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elementSize, 1);

        int position = GetFieldPosition(index);
        if (position < 0)
        {
            vector = default;
            return false;
        }

        int target = position + ReadOffset(position);
        int count = ReadLength(target);
        long size = checked((long)count * elementSize);
        Require(target + 4, size);
        vector = new FlatBufferVector(_data, target + 4, count, elementSize);
        return true;
    }

    /// <summary>Reads an inline <see cref="Vector2f"/> struct field.</summary>
    public Vector2f GetVector2f(int index)
    {
        int position = GetFieldPosition(index);
        if (position < 0)
        {
            return default;
        }

        Require(position, 8);
        return new Vector2f(ReadFloat(position), ReadFloat(position + 4));
    }

    /// <summary>Reads an inline <see cref="Vector3f"/> struct field.</summary>
    public Vector3f GetVector3f(int index)
    {
        int position = GetFieldPosition(index);
        if (position < 0)
        {
            return default;
        }

        Require(position, 12);
        return new Vector3f(ReadFloat(position), ReadFloat(position + 4), ReadFloat(position + 8));
    }

    /// <summary>Reads an inline <see cref="Vector4f"/> struct field.</summary>
    public Vector4f GetVector4f(int index)
    {
        int position = GetFieldPosition(index);
        if (position < 0)
        {
            return default;
        }

        Require(position, 16);
        return new Vector4f(
            ReadFloat(position),
            ReadFloat(position + 4),
            ReadFloat(position + 8),
            ReadFloat(position + 12));
    }

    /// <summary>Reads an inline <see cref="Sphere"/> struct field.</summary>
    public Sphere GetSphere(int index)
    {
        int position = GetFieldPosition(index);
        if (position < 0)
        {
            return default;
        }

        Require(position, 16);
        return new Sphere(
            ReadFloat(position),
            ReadFloat(position + 4),
            ReadFloat(position + 8),
            ReadFloat(position + 12));
    }

    /// <summary>Reads a <c>[ubyte]</c> vector field as a memory slice.</summary>
    public ReadOnlyMemory<byte> GetByteVector(int index)
    {
        int position = GetFieldPosition(index);
        if (position < 0)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        int target = position + ReadOffset(position);
        int length = ReadLength(target);
        Require(target + 4, length);
        return new ReadOnlyMemory<byte>(_data, target + 4, length);
    }

    internal int ReadInt32(int position)
    {
        Require(position, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(position));
    }

    internal uint ReadUInt32(int position)
    {
        Require(position, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(position));
    }

    private int ReadOffset(int position)
    {
        uint value = ReadUInt32(position);
        if (value > int.MaxValue)
        {
            throw new InvalidDataException($"FlatBuffer offset 0x{value:X} is too large.");
        }

        return (int)value;
    }

    private int ReadLength(int position)
    {
        uint value = ReadUInt32(position);
        if (value > int.MaxValue)
        {
            throw new InvalidDataException($"FlatBuffer length 0x{value:X} is too large.");
        }

        return (int)value;
    }

    internal ushort ReadUInt16(int position)
    {
        Require(position, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(position));
    }

    internal byte ReadByte(int position)
    {
        Require(position, 1);
        return _data[position];
    }

    internal float ReadFloat(int position)
    {
        Require(position, 4);
        return BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(position));
    }

    internal void Require(int position, long size)
    {
        if (position < 0 || size < 0 || position > _data.Length || size > _data.Length - position)
        {
            throw new InvalidDataException(
                $"FlatBuffer range 0x{position:X} + 0x{size:X} lies outside the buffer (0x{_data.Length:X}).");
        }
    }

    private int GetFieldPosition(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        int vtable = _position - ReadInt32(_position);
        int vtableSize = ReadUInt16(vtable);
        if (vtableSize < 4 || (vtableSize & 1) != 0)
        {
            throw new InvalidDataException($"Invalid FlatBuffer vtable size {vtableSize}.");
        }

        Require(vtable, vtableSize);
        int fieldCount = (vtableSize - 4) / 2;
        if (index >= fieldCount)
        {
            return -1;
        }

        int offset = ReadUInt16(vtable + 4 + (index * 2));
        if (offset == 0)
        {
            return -1;
        }

        int objectSize = ReadUInt16(vtable + 2);
        if (offset > objectSize)
        {
            throw new InvalidDataException($"FlatBuffer field {index} offset exceeds the table size.");
        }

        return _position + offset;
    }
}

/// <summary>A FlatBuffer vector whose elements are scalars or uoffsets.</summary>
public readonly struct FlatBufferVector
{
    private readonly byte[] _data;
    private readonly int _start;
    private readonly int _count;
    private readonly int _elementSize;

    internal FlatBufferVector(byte[] data, int start, int count, int elementSize)
    {
        _data = data;
        _start = start;
        _count = count;
        _elementSize = elementSize;
    }

    public int Count => _count;

    public FlatBufferTable GetTable(int index)
    {
        int position = GetElementPosition(index);
        int target = position + BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(position));
        if (target <= 0 || target + 4 > _data.Length)
        {
            throw new InvalidDataException($"FlatBuffer table at 0x{target:X} is out of range.");
        }

        return new FlatBufferTable(_data, target);
    }

    public string GetString(int index)
    {
        int position = GetElementPosition(index);
        int target = position + BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(position));
        if (target <= 0 || target + 4 > _data.Length)
        {
            throw new InvalidDataException($"FlatBuffer string at 0x{target:X} is out of range.");
        }

        int length = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(target));
        if (length < 0 || target + 4 > _data.Length || length > _data.Length - target - 4)
        {
            throw new InvalidDataException("FlatBuffer string is out of range.");
        }

        return Encoding.UTF8.GetString(_data, target + 4, length);
    }

    private int GetElementPosition(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _start + (index * _elementSize);
    }
}
