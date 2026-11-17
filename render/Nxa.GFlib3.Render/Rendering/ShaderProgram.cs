// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Silk.NET.OpenGL;

namespace Nxa.GFlib3.Render.Rendering;

/// <summary>Compiled OpenGL shader program with cached uniform lookups.</summary>
internal sealed class ShaderProgram : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniforms = new(StringComparer.Ordinal);

    public ShaderProgram(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        uint vertex = Compile(ShaderType.VertexShader, vertexSource);
        uint fragment = Compile(ShaderType.FragmentShader, fragmentSource);

        Handle = _gl.CreateProgram();
        _gl.AttachShader(Handle, vertex);
        _gl.AttachShader(Handle, fragment);
        _gl.LinkProgram(Handle);
        _gl.GetProgram(Handle, ProgramPropertyARB.LinkStatus, out int linked);
        _gl.DetachShader(Handle, vertex);
        _gl.DetachShader(Handle, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);

        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(Handle);
            _gl.DeleteProgram(Handle);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }
    }

    public uint Handle { get; }

    public void Use() => _gl.UseProgram(Handle);

    public void SetMatrix(string name, Matrix4x4 value)
    {
        int location = Location(name);
        if (location < 0)
        {
            return;
        }

        unsafe
        {
            // System.Numerics stores row-major; OpenGL expects column-major.
            _gl.UniformMatrix4(location, 1, false, &value.M11);
        }
    }

    public void SetInt(string name, int value) => _gl.Uniform1(Location(name), value);

    public void SetFloat(string name, float value) => _gl.Uniform1(Location(name), value);

    public void SetVector3(string name, Vector3 value) => _gl.Uniform3(Location(name), value);

    public void Dispose()
    {
        if (Handle != 0)
        {
            _gl.DeleteProgram(Handle);
        }
    }

    private int Location(string name)
    {
        if (!_uniforms.TryGetValue(name, out int location))
        {
            location = _gl.GetUniformLocation(Handle, name);
            _uniforms[name] = location;
        }

        return location;
    }

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
        if (status == 0)
        {
            string log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new InvalidOperationException($"{type} compilation failed: {log}");
        }

        return shader;
    }
}

