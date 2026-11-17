// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Bntx;
using Nxa.GFlib3.Core.Formats.Trinity;
using Nxa.GFlib3.Render.Camera;
using Nxa.GFlib3.Render.Scene;
using Silk.NET.OpenGL;

namespace Nxa.GFlib3.Render.Rendering;

/// <summary>Draws room geometry and instanced vegetation into the Scene framebuffer.</summary>
internal sealed class SceneRenderer : IDisposable
{
    private const string VertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aTexCoord;
        layout(location = 3) in vec4 aColor;
        layout(location = 4) in vec4 aInstance0;
        layout(location = 5) in vec4 aInstance1;
        layout(location = 6) in vec4 aInstance2;
        layout(location = 7) in vec4 aInstance3;

        uniform mat4 uViewProjection;
        uniform mat4 uModel;
        uniform int uInstanced;

        out vec3 vNormal;
        out vec2 vTexCoord;
        out vec4 vColor;

        void main()
        {
            mat4 model = uModel;
            if (uInstanced != 0)
            {
                model = mat4(aInstance0, aInstance1, aInstance2, aInstance3);
            }

            vec4 world = model * vec4(aPosition, 1.0);
            vNormal = mat3(model) * aNormal;
            vTexCoord = aTexCoord;
            vColor = aColor;
            gl_Position = uViewProjection * world;
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        in vec3 vNormal;
        in vec2 vTexCoord;
        in vec4 vColor;

        uniform sampler2D uBaseColor;
        uniform int uHasBaseColor;
        uniform int uAlphaTest;
        uniform vec3 uLightDirection;
        uniform vec3 uTint;

        out vec4 FragColor;

        void main()
        {
            vec3 albedo = uTint * vColor.rgb;
            if (uHasBaseColor != 0)
            {
                vec4 sampled = texture(uBaseColor, vTexCoord);
                if (uAlphaTest != 0 && sampled.a < 0.5)
                {
                    discard;
                }

                albedo *= sampled.rgb;
            }

            vec3 normal = normalize(vNormal);
            if (!gl_FrontFacing)
            {
                normal = -normal;
            }

            float diffuse = max(dot(normal, normalize(uLightDirection)), 0.0);
            float light = 0.55 + 0.75 * diffuse;
            FragColor = vec4(albedo * light, 1.0);
        }
        """;

    private const string TerrainVertexShader = """
        #version 330 core
        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec3 aNormal;
        layout(location = 2) in vec2 aTexCoord;

        uniform mat4 uViewProjection;

        out vec3 vNormal;
        out vec2 vTexCoord;

        void main()
        {
            vNormal = aNormal;
            vTexCoord = aTexCoord;
            gl_Position = uViewProjection * vec4(aPosition, 1.0);
        }
        """;

    private const string TerrainFragmentShader = """
        #version 330 core
        in vec3 vNormal;
        in vec2 vTexCoord;

        uniform sampler2D uAlbedo0;
        uniform sampler2D uAlbedo1;
        uniform sampler2D uAlbedo2;
        uniform sampler2D uAlbedo3;
        uniform sampler2D uAlbedo4;
        uniform sampler2D uAlbedo5;
        uniform sampler2D uAlbedo6;
        uniform sampler2D uControl0;
        uniform sampler2D uControl1;
        uniform float uLayerScales[7];
        uniform vec3 uLightDirection;

        out vec4 FragColor;

        void main()
        {
            vec4 c0 = texture(uControl0, vTexCoord);
            vec4 c1 = texture(uControl1, vTexCoord);
            float w0 = c0.r;
            float w1 = c0.g;
            float w2 = c0.b;
            float w3 = c0.a;
            float w4 = c1.r;
            float w5 = c1.g;
            float w6 = c1.b;
            float total = w0 + w1 + w2 + w3 + w4 + w5 + w6;

            vec3 albedo;
            if (total < 0.001)
            {
                albedo = vec3(0.35, 0.40, 0.30);
            }
            else
            {
                albedo = (
                    w0 * texture(uAlbedo0, vTexCoord * uLayerScales[0]).rgb
                  + w1 * texture(uAlbedo1, vTexCoord * uLayerScales[1]).rgb
                  + w2 * texture(uAlbedo2, vTexCoord * uLayerScales[2]).rgb
                  + w3 * texture(uAlbedo3, vTexCoord * uLayerScales[3]).rgb
                  + w4 * texture(uAlbedo4, vTexCoord * uLayerScales[4]).rgb
                  + w5 * texture(uAlbedo5, vTexCoord * uLayerScales[5]).rgb
                  + w6 * texture(uAlbedo6, vTexCoord * uLayerScales[6]).rgb
                ) / total;
            }

            vec3 normal = normalize(vNormal);
            if (!gl_FrontFacing)
            {
                normal = -normal;
            }

            float diffuse = max(dot(normal, normalize(uLightDirection)), 0.0);
            FragColor = vec4(albedo * (0.55 + 0.75 * diffuse), 1.0);
        }
        """;

    private readonly GL _gl;
    private readonly ShaderProgram _shader;
    private readonly ShaderProgram _terrainShader;
    private readonly List<GlTexture> _terrainAlbedo = [];
    private readonly GlTexture? _terrainControl0;
    private readonly GlTexture? _terrainControl1;
    private readonly float[] _terrainScales = new float[7];
    private readonly List<GlTexture> _textures = [];
    private readonly List<GlMesh> _roomMeshes = [];
    private readonly List<PlacedRender> _placed = [];
    private readonly List<GlMesh> _placedBuffers = [];
    private readonly List<PlantRender> _plants = [];
    private readonly GlMesh? _terrain;
    private readonly GlTexture _white;
    private readonly bool _showPlants;
    private readonly bool _showTextures;
    private readonly bool _showRoom;
    private readonly bool _showTerrain;
    private readonly bool _showScene;
    private bool _disposed;

    public SceneRenderer(
        GL gl,
        FieldScene scene,
        bool showPlants,
        bool showTextures,
        bool showRoom,
        bool showTerrain,
        bool showScene)
    {
        _gl = gl;
        _shader = new ShaderProgram(gl, VertexShader, FragmentShader);
        _terrainShader = new ShaderProgram(gl, TerrainVertexShader, TerrainFragmentShader);
        _white = new GlTexture(gl, new BntxImage(1, 1, [255, 255, 255, 255]));
        _showPlants = showPlants;
        _showTextures = showTextures;
        _showRoom = showRoom;
        _showTerrain = showTerrain;
        _showScene = showScene;

        foreach (SceneTexture texture in scene.Textures)
        {
            _textures.Add(new GlTexture(gl, texture.Image));
        }

        foreach (SceneMesh mesh in scene.RoomMeshes)
        {
            _roomMeshes.Add(new GlMesh(gl, mesh.Geometry));
        }

        if (scene.Terrain is not null)
        {
            _terrain = new GlMesh(gl, scene.Terrain);
        }

        if (scene.HasTerrainSplat)
        {
            foreach (SceneTexture layer in scene.TerrainLayers)
            {
                _terrainAlbedo.Add(new GlTexture(gl, layer.Image));
            }

            _terrainControl0 = new GlTexture(gl, scene.TerrainControl0!.Image);
            _terrainControl1 = new GlTexture(gl, scene.TerrainControl1!.Image);
        }

        var placedBuffers = new Dictionary<TrinityMeshGeometry, GlMesh>(ReferenceEqualityComparer.Instance);
        foreach (SceneMesh mesh in scene.PlacedMeshes)
        {
            if (!placedBuffers.TryGetValue(mesh.Geometry, out GlMesh? buffer))
            {
                buffer = new GlMesh(gl, mesh.Geometry);
                placedBuffers.Add(mesh.Geometry, buffer);
                _placedBuffers.Add(buffer);
            }

            _placed.Add(new PlacedRender(mesh, buffer));
        }

        for (int model = 0; model < scene.Plants.Count; model++)
        {
            List<Matrix4x4RowMajor> instances = scene.Instances
                .Where(instance => instance.ModelIndex == model)
                .Select(instance => instance.Matrix)
                .ToList();
            if (instances.Count == 0 || scene.Plants[model].Meshes.Count == 0)
            {
                continue;
            }

            var render = new PlantRender();
            foreach (SceneMesh mesh in scene.Plants[model].Meshes)
            {
                render.Meshes.Add(new GlMesh(gl, mesh.Geometry));
            }

            render.SceneMeshes = scene.Plants[model].Meshes;
            render.Instances = instances.Select(ToMatrix).ToList();
            _plants.Add(render);
        }

        BoundingBox bounds = scene.ComputeBounds();
        Vector3 center = ToVector(bounds.Min) + (ToVector(bounds.Max) - ToVector(bounds.Min)) * 0.5f;
        float radius = MathF.Max((ToVector(bounds.Max) - center).Length(), 1f);
        FarPlane = MathF.Max(500f, radius * 8f);
        Center = center;
        Radius = radius;
    }

    public float FarPlane { get; }

    public Vector3 Center { get; }

    public float Radius { get; }

    public int RenderTriangleCount { get; private set; }

    public int RenderInstanceCount { get; private set; }

    public void Render(FieldScene scene, FreeFlyCamera camera, int width, int height)
    {
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.Disable(EnableCap.CullFace);
        _gl.ClearColor(0.07f, 0.08f, 0.10f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        Matrix4x4 viewProjection = camera.View * FreeFlyCamera.Projection((float)width / height, FarPlane);
        Vector3 lightDirection = Vector3.Normalize(new Vector3(-0.4f, 0.9f, 0.35f));
        _shader.Use();
        _shader.SetMatrix("uViewProjection", viewProjection);
        _shader.SetVector3("uLightDirection", lightDirection);
        _shader.SetInt("uBaseColor", 0);
        _shader.SetMatrix("uModel", Matrix4x4.Identity);

        int triangles = 0;
        int instances = 0;

        if (_showRoom)
        {
            _shader.SetInt("uInstanced", 0);
            for (int i = 0; i < scene.RoomMeshes.Count && i < _roomMeshes.Count; i++)
            {
                SceneMesh mesh = scene.RoomMeshes[i];
                BindMaterial(scene, mesh.MaterialIndex);
                _roomMeshes[i].Draw(mesh.PartIndexStart, mesh.PartIndexCount);
                triangles += mesh.PartIndexCount / 3;
            }
        }

        if (_showScene)
        {
            _shader.SetInt("uInstanced", 0);
            foreach (PlacedRender placed in _placed)
            {
                _shader.SetMatrix("uModel", placed.Mesh.Placement ?? Matrix4x4.Identity);
                BindMaterial(scene, placed.Mesh.MaterialIndex);
                placed.Buffer.Draw(placed.Mesh.PartIndexStart, placed.Mesh.PartIndexCount);
                triangles += placed.Mesh.PartIndexCount / 3;
            }
        }

        if (_showPlants)
        {
            _shader.SetInt("uInstanced", 0);
            foreach (PlantRender plant in _plants)
            {
                foreach (Matrix4x4 transform in plant.Instances)
                {
                    _shader.SetMatrix("uModel", transform);
                    for (int i = 0; i < plant.SceneMeshes.Count && i < plant.Meshes.Count; i++)
                    {
                        SceneMesh mesh = plant.SceneMeshes[i];
                        BindMaterial(scene, mesh.MaterialIndex);
                        plant.Meshes[i].Draw(mesh.PartIndexStart, mesh.PartIndexCount);
                        triangles += mesh.PartIndexCount / 3;
                    }
                }

                instances += plant.Instances.Count;
            }
        }

        if (_showTerrain && _terrain is not null)
        {
            if (scene.HasTerrainSplat
                && _terrainAlbedo.Count == 7
                && _terrainControl0 is not null
                && _terrainControl1 is not null)
            {
                _terrainShader.Use();
                _terrainShader.SetMatrix("uViewProjection", viewProjection);
                _terrainShader.SetVector3("uLightDirection", lightDirection);

                for (int i = 0; i < _terrainAlbedo.Count; i++)
                {
                    _terrainAlbedo[i].Bind((TextureUnit)((int)TextureUnit.Texture0 + i));
                    _terrainShader.SetInt($"uAlbedo{i}", i);
                }

                _terrainControl0.Bind(TextureUnit.Texture7);
                _terrainShader.SetInt("uControl0", 7);
                _terrainControl1.Bind(TextureUnit.Texture8);
                _terrainShader.SetInt("uControl1", 8);

                for (int i = 0; i < _terrainScales.Length; i++)
                {
                    _terrainScales[i] = scene.TerrainLayerScales[i] * scene.TerrainTileScale;
                }

                _terrainShader.SetFloatArray("uLayerScales[0]", _terrainScales);

                _terrain.Draw(0, _terrain.IndexCount);
            }
            else
            {
                _shader.Use();
                _shader.SetMatrix("uViewProjection", viewProjection);
                _shader.SetVector3("uLightDirection", lightDirection);
                _shader.SetInt("uInstanced", 0);
                _shader.SetMatrix("uModel", Matrix4x4.Identity);
                _white.Bind(TextureUnit.Texture0);
                _shader.SetInt("uHasBaseColor", 0);
                _shader.SetInt("uAlphaTest", 0);
                _shader.SetVector3("uTint", new Vector3(0.42f, 0.53f, 0.34f));
                _terrain.Draw(0, _terrain.IndexCount);
            }

            triangles += _terrain.IndexCount / 3;
        }

        _shader.SetMatrix("uModel", Matrix4x4.Identity);

        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
        RenderTriangleCount = triangles;
        RenderInstanceCount = instances;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (GlMesh mesh in _roomMeshes)
        {
            mesh.Dispose();
        }

        _terrain?.Dispose();

        foreach (GlTexture texture in _terrainAlbedo)
        {
            texture.Dispose();
        }

        _terrainControl0?.Dispose();
        _terrainControl1?.Dispose();
        _terrainShader.Dispose();

        foreach (GlMesh mesh in _placedBuffers)
        {
            mesh.Dispose();
        }

        foreach (PlantRender plant in _plants)
        {
            foreach (GlMesh mesh in plant.Meshes)
            {
                mesh.Dispose();
            }
        }

        foreach (GlTexture texture in _textures)
        {
            texture.Dispose();
        }

        _white.Dispose();
        _shader.Dispose();
    }

    private void BindMaterial(FieldScene scene, int materialIndex)
    {
        SceneMaterial? material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        int texture = _showTextures ? (material?.BaseColorTexture ?? -1) : -1;
        if (texture >= 0 && texture < _textures.Count)
        {
            _textures[texture].Bind(TextureUnit.Texture0);
            _shader.SetInt("uHasBaseColor", 1);
            _shader.SetInt("uAlphaTest", 1);
        }
        else
        {
            _white.Bind(TextureUnit.Texture0);
            _shader.SetInt("uHasBaseColor", 0);
            _shader.SetInt("uAlphaTest", 0);
        }

        _shader.SetVector3("uTint", material?.Tint ?? Vector3.One);
    }

    private static Vector3 ToVector(Vector3f value) => new(value.X, value.Y, value.Z);

    private static Matrix4x4 ToMatrix(Matrix4x4RowMajor value) => new(
        value.Row0.X,
        value.Row0.Y,
        value.Row0.Z,
        value.Row0.W,
        value.Row1.X,
        value.Row1.Y,
        value.Row1.Z,
        value.Row1.W,
        value.Row2.X,
        value.Row2.Y,
        value.Row2.Z,
        value.Row2.W,
        value.Row3.X,
        value.Row3.Y,
        value.Row3.Z,
        value.Row3.W);

    private sealed record PlacedRender(SceneMesh Mesh, GlMesh Buffer);

    private sealed class PlantRender
    {
        public List<GlMesh> Meshes { get; } = [];

        public List<SceneMesh> SceneMeshes { get; set; } = [];

        public List<Matrix4x4> Instances { get; set; } = [];
    }
}










