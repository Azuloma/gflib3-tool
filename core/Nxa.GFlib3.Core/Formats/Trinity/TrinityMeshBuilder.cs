// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

/// <summary>Decoded mesh geometry: shared vertices, indices, and material parts.</summary>
public sealed record TrinityMeshGeometry(
    string Name,
    TrinityVertex[] Vertices,
    uint[] Indices,
    IReadOnlyList<TrinitySubMesh> Parts,
    BoundingBox Bounds);

public readonly record struct TrinityVertex(
    Vector3f Position,
    Vector3f Normal,
    Vector4f Tangent,
    Vector2f TexCoord,
    Vector4f Color);

public sealed record TrinitySubMesh(
    string? MaterialName,
    int IndexStart,
    int IndexCount,
    BoundingBox Bounds);

/// <summary>Builds renderable geometry from TRMBF buffers and TRMSH declarations.</summary>
public static class TrinityMeshBuilder
{
    private const int MaxStride = 1024;
    private const int MaxVertexCount = 16 * 1024 * 1024;
    private const int MaxIndexCount = 64 * 1024 * 1024;

    public static TrinityMeshGeometry Build(TrmbfMeshBuffer buffer, TrmshMesh mesh)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentNullException.ThrowIfNull(mesh);

        string name = mesh.Name ?? "(unnamed)";
        if (buffer.VertexBuffers.Count != 1 || buffer.IndexBuffers.Count != 1)
        {
            throw new NotSupportedException(
                $"Mesh '{name}' has {buffer.VertexBuffers.Count} vertex and {buffer.IndexBuffers.Count} " +
                "index buffers; only one of each is supported.");
        }

        if (mesh.VertexDeclarations.Count != 1)
        {
            throw new NotSupportedException(
                $"Mesh '{name}' has {mesh.VertexDeclarations.Count} vertex declarations; only one is supported.");
        }

        TrVertexDeclaration declaration = mesh.VertexDeclarations[0];
        int stride = checked((int)declaration.Stride);
        if (stride <= 0 || stride > MaxStride)
        {
            throw new InvalidDataException($"Mesh '{name}' has an invalid vertex stride {stride}.");
        }

        ReadOnlyMemory<byte> vertexData = buffer.VertexBuffers[0];
        if (vertexData.Length % stride != 0)
        {
            throw new InvalidDataException(
                $"Mesh '{name}' vertex buffer 0x{vertexData.Length:X} is not a multiple of stride {stride}.");
        }

        int vertexCount = vertexData.Length / stride;
        if (vertexCount > MaxVertexCount)
        {
            throw new InvalidDataException($"Mesh '{name}' vertex count {vertexCount} exceeds the limit.");
        }

        uint[] indices = ReadIndices(buffer.IndexBuffers[0].Span, mesh.IndexFormat, name);
        TrinityVertex[] vertices = ReadVertices(vertexData.Span, vertexCount, stride, declaration, name);

        var parts = new List<TrinitySubMesh>(mesh.MeshParts.Count);
        Vector3f min = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3f max = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        if (mesh.MeshParts.Count == 0)
        {
            AccumulateBounds(indices, 0, indices.Length, vertices, ref min, ref max, name);
            parts.Add(new TrinitySubMesh(null, 0, indices.Length, new BoundingBox(min, max)));
        }

        foreach (TrMeshPart part in mesh.MeshParts)
        {
            long start = part.IndexOffset;
            long count = part.IndexCount;
            if (start < 0 || count < 0 || start + count > indices.Length)
            {
                throw new InvalidDataException(
                    $"Mesh '{name}' part range {start}+{count} exceeds the {indices.Length} indices.");
            }

            int indexStart = (int)start;
            int indexCount = (int)count;
            Vector3f partMin = new(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3f partMax = new(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            AccumulateBounds(indices, indexStart, indexCount, vertices, ref partMin, ref partMax, name);
            AccumulateBounds(indices, indexStart, indexCount, vertices, ref min, ref max, name);

            parts.Add(new TrinitySubMesh(
                part.MaterialName,
                indexStart,
                indexCount,
                new BoundingBox(partMin, partMax)));
        }

        if (min.X > max.X)
        {
            min = max = default;
        }

        return new TrinityMeshGeometry(name, vertices, indices, parts, new BoundingBox(min, max));
    }

    private static TrinityVertex[] ReadVertices(
        ReadOnlySpan<byte> data,
        int vertexCount,
        int stride,
        TrVertexDeclaration declaration,
        string name)
    {
        TrVertexElement? position = Find(declaration, TrVertexUsage.Position);
        if (position is null)
        {
            throw new InvalidDataException($"Mesh '{name}' has no position element.");
        }

        TrVertexElement? normal = Find(declaration, TrVertexUsage.Normal);
        TrVertexElement? tangent = Find(declaration, TrVertexUsage.Tangent);
        TrVertexElement? texCoord = Find(declaration, TrVertexUsage.TexCoord);
        TrVertexElement? color = Find(declaration, TrVertexUsage.Color);

        var vertices = new TrinityVertex[vertexCount];
        for (int i = 0; i < vertices.Length; i++)
        {
            ReadOnlySpan<byte> vertex = data.Slice(i * stride, stride);
            vertices[i] = new TrinityVertex(
                ReadVector3(vertex, position, stride, name),
                normal is null ? new Vector3f(0, 0, 1) : ReadVector3(vertex, normal, stride, name),
                tangent is null ? new Vector4f(1, 0, 0, 1) : ReadVector4(vertex, tangent, stride, name),
                texCoord is null ? default : ReadVector2(vertex, texCoord, stride, name),
                color is null ? new Vector4f(1, 1, 1, 1) : ReadColor(vertex, color, stride, name));
        }

        return vertices;
    }

    private static void AccumulateBounds(
        uint[] indices,
        int start,
        int count,
        TrinityVertex[] vertices,
        ref Vector3f min,
        ref Vector3f max,
        string name)
    {
        for (int i = start; i < start + count; i++)
        {
            uint index = indices[i];
            if (index >= vertices.Length)
            {
                throw new InvalidDataException(
                    $"Mesh '{name}' index {index} at position {i} exceeds the {vertices.Length} vertices.");
            }

            Vector3f position = vertices[(int)index].Position;
            min = new Vector3f(
                Math.Min(min.X, position.X),
                Math.Min(min.Y, position.Y),
                Math.Min(min.Z, position.Z));
            max = new Vector3f(
                Math.Max(max.X, position.X),
                Math.Max(max.Y, position.Y),
                Math.Max(max.Z, position.Z));
        }
    }

    private static uint[] ReadIndices(ReadOnlySpan<byte> data, TrIndexFormat format, string name)
    {
        int size = format switch
        {
            TrIndexFormat.Index8 => 1,
            TrIndexFormat.Index16 => 2,
            TrIndexFormat.Index32 => 4,
            _ => throw new NotSupportedException($"Mesh '{name}' index format {format} is not supported."),
        };

        if (data.Length % size != 0)
        {
            throw new InvalidDataException(
                $"Mesh '{name}' index buffer 0x{data.Length:X} is not a multiple of {size}.");
        }

        int count = data.Length / size;
        if (count > MaxIndexCount)
        {
            throw new InvalidDataException($"Mesh '{name}' index count {count} exceeds the limit.");
        }

        var indices = new uint[count];
        for (int i = 0; i < count; i++)
        {
            indices[i] = size switch
            {
                1 => data[i],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(data[(i * 2)..]),
                _ => BinaryPrimitives.ReadUInt32LittleEndian(data[(i * 4)..]),
            };
        }

        return indices;
    }

    private static TrVertexElement? Find(TrVertexDeclaration declaration, TrVertexUsage usage)
    {
        foreach (TrVertexElement element in declaration.Elements)
        {
            if (element.Usage == usage && element.Layer == 0)
            {
                return element;
            }
        }

        return null;
    }

    private static Vector2f ReadVector2(ReadOnlySpan<byte> vertex, TrVertexElement element, int stride, string name)
    {
        int size = CheckElement(element, stride, name);
        ReadOnlySpan<byte> data = vertex[(int)element.Offset..];
        return element.Format switch
        {
            TrVertexFormat.X32Y32Float when size == 8 => new Vector2f(
                BinaryPrimitives.ReadSingleLittleEndian(data),
                BinaryPrimitives.ReadSingleLittleEndian(data[4..])),
            TrVertexFormat.W16X16Y16Z16Float when size == 8 => new Vector2f(
                (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data)),
                (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data[2..]))),
            _ => throw new NotSupportedException(
                $"Mesh '{name}' texcoord format {element.Format} is not supported."),
        };
    }

    private static Vector3f ReadVector3(ReadOnlySpan<byte> vertex, TrVertexElement element, int stride, string name)
    {
        int size = CheckElement(element, stride, name);
        ReadOnlySpan<byte> data = vertex[(int)element.Offset..];
        return element.Format switch
        {
            TrVertexFormat.X32Y32Z32Float when size == 12 => new Vector3f(
                BinaryPrimitives.ReadSingleLittleEndian(data),
                BinaryPrimitives.ReadSingleLittleEndian(data[4..]),
                BinaryPrimitives.ReadSingleLittleEndian(data[8..])),
            TrVertexFormat.W32X32Y32Z32Float when size == 16 => new Vector3f(
                BinaryPrimitives.ReadSingleLittleEndian(data),
                BinaryPrimitives.ReadSingleLittleEndian(data[4..]),
                BinaryPrimitives.ReadSingleLittleEndian(data[8..])),
            TrVertexFormat.W16X16Y16Z16Float when size == 8 => new Vector3f(
                (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data)),
                (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data[2..])),
                (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data[4..]))),
            _ => throw new NotSupportedException(
                $"Mesh '{name}' vector format {element.Format} is not supported."),
        };
    }

    private static Vector4f ReadVector4(ReadOnlySpan<byte> vertex, TrVertexElement element, int stride, string name)
    {
        int size = CheckElement(element, stride, name);
        ReadOnlySpan<byte> data = vertex[(int)element.Offset..];
        return element.Format switch
        {
            TrVertexFormat.W16X16Y16Z16Float when size == 8 => ReadHalf4(data),
            TrVertexFormat.W32X32Y32Z32Float when size == 16 => ReadFloat4(data),
            _ => throw new NotSupportedException(
                $"Mesh '{name}' vector format {element.Format} is not supported."),
        };
    }

    private static Vector4f ReadColor(ReadOnlySpan<byte> vertex, TrVertexElement element, int stride, string name)
    {
        int size = CheckElement(element, stride, name);
        ReadOnlySpan<byte> data = vertex[(int)element.Offset..];
        return element.Format switch
        {
            TrVertexFormat.R8G8B8A8UnsignedNormalized when size == 4 => new Vector4f(
                data[0] / 255f,
                data[1] / 255f,
                data[2] / 255f,
                data[3] / 255f),
            TrVertexFormat.W8X8Y8Z8Unsigned when size == 4 => new Vector4f(
                data[0] / 255f,
                data[1] / 255f,
                data[2] / 255f,
                data[3] / 255f),
            TrVertexFormat.W16X16Y16Z16UnsignedNormalized when size == 8 => new Vector4f(
                BinaryPrimitives.ReadUInt16LittleEndian(data) / 65535f,
                BinaryPrimitives.ReadUInt16LittleEndian(data[2..]) / 65535f,
                BinaryPrimitives.ReadUInt16LittleEndian(data[4..]) / 65535f,
                BinaryPrimitives.ReadUInt16LittleEndian(data[6..]) / 65535f),
            TrVertexFormat.W16X16Y16Z16Float when size == 8 => ReadHalf4(data),
            TrVertexFormat.W32X32Y32Z32Float when size == 16 => ReadFloat4(data),
            _ => throw new NotSupportedException(
                $"Mesh '{name}' color format {element.Format} is not supported."),
        };
    }

    private static int CheckElement(TrVertexElement element, int stride, string name)
    {
        int size = GetSize(element.Format);
        if (size <= 0 || element.Offset + size > stride)
        {
            throw new InvalidDataException(
                $"Mesh '{name}' element {element.Usage} at {element.Offset} does not fit stride {stride}.");
        }

        return size;
    }

    private static int GetSize(TrVertexFormat format) => format switch
    {
        TrVertexFormat.R8G8B8A8UnsignedNormalized => 4,
        TrVertexFormat.W8X8Y8Z8Unsigned => 4,
        TrVertexFormat.X32Unsigned => 4,
        TrVertexFormat.X32Signed => 4,
        TrVertexFormat.W16X16Y16Z16UnsignedNormalized => 8,
        TrVertexFormat.W16X16Y16Z16Float => 8,
        TrVertexFormat.X32Y32Float => 8,
        TrVertexFormat.X32Y32Z32Float => 12,
        TrVertexFormat.W32X32Y32Z32Unsigned => 16,
        TrVertexFormat.W32X32Y32Z32Float => 16,
        _ => 0,
    };

    private static Vector4f ReadHalf4(ReadOnlySpan<byte> data) => new(
        (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data)),
        (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data[2..])),
        (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data[4..])),
        (float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(data[6..])));

    private static Vector4f ReadFloat4(ReadOnlySpan<byte> data) => new(
        BinaryPrimitives.ReadSingleLittleEndian(data),
        BinaryPrimitives.ReadSingleLittleEndian(data[4..]),
        BinaryPrimitives.ReadSingleLittleEndian(data[8..]),
        BinaryPrimitives.ReadSingleLittleEndian(data[12..]));
}
