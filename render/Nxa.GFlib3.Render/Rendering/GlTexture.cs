// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Bntx;
using Silk.NET.OpenGL;

namespace Nxa.GFlib3.Render.Rendering;

/// <summary>RGBA8 texture uploaded from a decoded BNTX image.</summary>
internal sealed class GlTexture : IDisposable
{
    private readonly GL _gl;

    public GlTexture(GL gl, BntxImage image)
    {
        _gl = gl;
        Handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, Handle);

        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);

        unsafe
        {
            fixed (byte* pointer = image.Pixels)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba8,
                    (uint)image.Width,
                    (uint)image.Height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    pointer);
            }
        }

        _gl.GenerateMipmap(TextureTarget.Texture2D);
    }

    public uint Handle { get; }

    public void Bind(TextureUnit unit)
    {
        _gl.ActiveTexture(unit);
        _gl.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose() => _gl.DeleteTexture(Handle);
}
