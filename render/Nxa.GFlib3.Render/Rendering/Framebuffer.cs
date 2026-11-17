// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Silk.NET.OpenGL;

namespace Nxa.GFlib3.Render.Rendering;

/// <summary>Off-screen color/depth target displayed by the Scene panel.</summary>
internal sealed class SceneFramebuffer : IDisposable
{
    private readonly GL _gl;
    private uint _fbo;
    private uint _color;
    private uint _depth;

    public SceneFramebuffer(GL gl)
    {
        _gl = gl;
    }

    public uint ColorTexture => _color;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Resize(int width, int height)
    {
        width = Math.Max(width, 1);
        height = Math.Max(height, 1);
        if (width == Width && height == Height)
        {
            return;
        }

        Release();

        Width = width;
        Height = height;

        _color = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, _color);
        unsafe
        {
            _gl.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba8,
                (uint)width,
                (uint)height,
                0,
                PixelFormat.Rgba,
                PixelType.UnsignedByte,
                (void*)0);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);

        _depth = _gl.GenRenderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depth);
        _gl.RenderbufferStorage(
            RenderbufferTarget.Renderbuffer,
            InternalFormat.DepthComponent24,
            (uint)width,
            (uint)height);

        _fbo = _gl.GenFramebuffer();
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.FramebufferTexture2D(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.ColorAttachment0,
            TextureTarget.Texture2D,
            _color,
            0);
        _gl.FramebufferRenderbuffer(
            FramebufferTarget.Framebuffer,
            FramebufferAttachment.DepthAttachment,
            RenderbufferTarget.Renderbuffer,
            _depth);

        var status = (FramebufferStatus)_gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferStatus.Complete)
        {
            throw new InvalidOperationException($"Framebuffer is incomplete: {status}.");
        }

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Bind()
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo);
        _gl.Viewport(0, 0, (uint)Width, (uint)Height);
    }

    public void Dispose()
    {
        Release();
    }

    private void Release()
    {
        if (_fbo != 0)
        {
            _gl.DeleteFramebuffer(_fbo);
            _fbo = 0;
        }

        if (_color != 0)
        {
            _gl.DeleteTexture(_color);
            _color = 0;
        }

        if (_depth != 0)
        {
            _gl.DeleteRenderbuffer(_depth);
            _depth = 0;
        }
    }
}

