// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Core.Formats.Trinity;
using Silk.NET.OpenGL;

namespace Nxa.GFlib3.Render.Rendering;

/// <summary>Vertex/index buffers and VAO for one mesh part.</summary>
internal sealed class GlMesh : IDisposable
{
    public const int VertexFloats = 12;

    private readonly GL _gl;
    private readonly uint _vao;
    private readonly uint _vbo;
    private readonly uint _ebo;

    public GlMesh(GL gl, TrinityMeshGeometry geometry)
        : this(gl, Interleave(geometry.Vertices), geometry.Indices)
    {
    }

    public GlMesh(GL gl, TerrainMeshGeometry geometry)
        : this(gl, Interleave(geometry.Vertices), geometry.Indices)
    {
    }

    private GlMesh(GL gl, float[] vertices, uint[] indices)
    {
        _gl = gl;
        IndexCount = indices.Length;

        _vao = _gl.GenVertexArray();
        _gl.BindVertexArray(_vao);

        _vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);

        unsafe
        {
            fixed (float* pointer = vertices)
            {
                _gl.BufferData(
                    BufferTargetARB.ArrayBuffer,
                    (nuint)(vertices.Length * sizeof(float)),
                    pointer,
                    BufferUsageARB.StaticDraw);
            }
        }

        uint stride = VertexFloats * sizeof(float);
        unsafe
        {
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)(3 * sizeof(float)));
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, stride, (void*)(6 * sizeof(float)));
            _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, stride, (void*)(8 * sizeof(float)));
        }

        _gl.EnableVertexAttribArray(0);
        _gl.EnableVertexAttribArray(1);
        _gl.EnableVertexAttribArray(2);
        _gl.EnableVertexAttribArray(3);

        _ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        unsafe
        {
            fixed (uint* pointer = indices)
            {
                _gl.BufferData(
                    BufferTargetARB.ElementArrayBuffer,
                    (nuint)(indices.Length * sizeof(uint)),
                    pointer,
                    BufferUsageARB.StaticDraw);
            }
        }

        _gl.BindVertexArray(0);
    }

    public int IndexCount { get; }

    public void Draw(int start, int count)
    {
        _gl.BindVertexArray(_vao);
        unsafe
        {
            _gl.DrawElements(
                PrimitiveType.Triangles,
                (uint)count,
                DrawElementsType.UnsignedInt,
                (void*)(start * sizeof(uint)));
        }
    }

    public void Dispose()
    {
        _gl.DeleteVertexArray(_vao);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteBuffer(_ebo);
    }

    private static float[] Interleave(TerrainVertex[] vertices)
    {
        var data = new float[vertices.Length * VertexFloats];
        for (int i = 0; i < vertices.Length; i++)
        {
            TerrainVertex vertex = vertices[i];
            int offset = i * VertexFloats;
            data[offset] = vertex.Position.X;
            data[offset + 1] = vertex.Position.Y;
            data[offset + 2] = vertex.Position.Z;
            data[offset + 3] = vertex.Normal.X;
            data[offset + 4] = vertex.Normal.Y;
            data[offset + 5] = vertex.Normal.Z;
            data[offset + 6] = vertex.TexCoord.X;
            data[offset + 7] = vertex.TexCoord.Y;
            data[offset + 8] = 1f;
            data[offset + 9] = 1f;
            data[offset + 10] = 1f;
            data[offset + 11] = 1f;
        }

        return data;
    }

    private static float[] Interleave(TrinityVertex[] vertices)
    {
        var data = new float[vertices.Length * VertexFloats];
        for (int i = 0; i < vertices.Length; i++)
        {
            TrinityVertex vertex = vertices[i];
            int offset = i * VertexFloats;
            data[offset] = vertex.Position.X;
            data[offset + 1] = vertex.Position.Y;
            data[offset + 2] = vertex.Position.Z;
            data[offset + 3] = vertex.Normal.X;
            data[offset + 4] = vertex.Normal.Y;
            data[offset + 5] = vertex.Normal.Z;
            data[offset + 6] = vertex.TexCoord.X;
            data[offset + 7] = vertex.TexCoord.Y;
            data[offset + 8] = vertex.Color.X;
            data[offset + 9] = vertex.Color.Y;
            data[offset + 10] = vertex.Color.Z;
            data[offset + 11] = vertex.Color.W;
        }

        return data;
    }
}
