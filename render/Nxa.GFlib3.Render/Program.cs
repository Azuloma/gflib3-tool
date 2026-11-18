// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Render;
using Nxa.GFlib3.Render.Scene;

AppOptions options;
try
{
    options = AppOptions.Parse(args);
}
catch (ShowHelpException)
{
    PrintHelp();
    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    PrintHelp();
    return 1;
}

string dumpRoot;
try
{
    dumpRoot = options.RequireDumpRoot();
}
catch (Exception ex) when (ex is InvalidOperationException or DirectoryNotFoundException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

SceneCatalog catalog = SceneCatalog.Discover(dumpRoot);

FieldScene scene;
try
{
    Console.WriteLine($"Loading {options.Area} with model pack {options.ModelPack}...");
    scene = FieldSceneLoader.Load(dumpRoot, options.Area, options.ModelPack, options.MeshFilter, options.TerrainStride, options.TerrainTileScale);
}
catch (Exception ex) when (ex is IOException
    or InvalidDataException
    or NotSupportedException
    or DllNotFoundException
    or UnauthorizedAccessException
    or ArgumentException)
{
    Console.Error.WriteLine($"error: failed to load area: {ex.Message}");
    return 1;
}

if (catalog.DiscoveryWarning is not null)
{
    scene.Messages.Add($"warning: {catalog.DiscoveryWarning}");
}

foreach (string message in scene.Messages)
{
    Console.WriteLine(message);
}

if (options.LoadCheck)
{
    var bounds = scene.ComputeBounds();
    var roomMin = new Nxa.GFlib3.Core.FlatBuffers.Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
    var roomMax = new Nxa.GFlib3.Core.FlatBuffers.Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    foreach (var mesh in scene.RoomMeshes)
    {
        roomMin = new Nxa.GFlib3.Core.FlatBuffers.Vector3f(
            Math.Min(roomMin.X, mesh.Geometry.Bounds.Min.X),
            Math.Min(roomMin.Y, mesh.Geometry.Bounds.Min.Y),
            Math.Min(roomMin.Z, mesh.Geometry.Bounds.Min.Z));
        roomMax = new Nxa.GFlib3.Core.FlatBuffers.Vector3f(
            Math.Max(roomMax.X, mesh.Geometry.Bounds.Max.X),
            Math.Max(roomMax.Y, mesh.Geometry.Bounds.Max.Y),
            Math.Max(roomMax.Z, mesh.Geometry.Bounds.Max.Z));
    }

    Console.WriteLine($"room parts:  {scene.RoomMeshes.Count}");
    Console.WriteLine($"placed parts:{scene.PlacedMeshes.Count}");
    Console.WriteLine($"terrain:     {(scene.Terrain is null ? "none" : $"{scene.Terrain.Vertices.Length:N0} vertices")}");
    Console.WriteLine($"plant models:{scene.Plants.Count}");
    Console.WriteLine($"instances:   {scene.Instances.Count}");
    Console.WriteLine($"textures:    {scene.Textures.Count}");
    Console.WriteLine($"vertices:    {scene.VertexCount:N0}");
    Console.WriteLine($"indices:     {scene.IndexCount:N0}");
    if (scene.RoomMeshes.Count > 0)
    {
        Console.WriteLine($"room min:    {roomMin.X:F2}, {roomMin.Y:F2}, {roomMin.Z:F2}");
        Console.WriteLine($"room max:    {roomMax.X:F2}, {roomMax.Y:F2}, {roomMax.Z:F2}");
    }

    Console.WriteLine($"min:         {bounds.Min.X:F2}, {bounds.Min.Y:F2}, {bounds.Min.Z:F2}");
    Console.WriteLine($"max:         {bounds.Max.X:F2}, {bounds.Max.Y:F2}, {bounds.Max.Z:F2}");

    for (int i = 0; i < scene.Plants.Count; i++)
    {
        var plant = scene.Plants[i];
        int instances = scene.Instances.Count(instance => instance.ModelIndex == i);
        Console.WriteLine($"plant {plant.Name}: {plant.Meshes.Count} parts, {instances} instances");
    }

    return 0;
}

try
{
    using var app = new RenderApp(options, scene, catalog);
    app.Run();
}
catch (Exception ex) when (ex is IOException
    or InvalidDataException
    or NotSupportedException
    or DllNotFoundException
    or InvalidOperationException
    or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"error: viewer failed: {ex.Message}");
    return 1;
}

return 0;

static void PrintHelp()
{
    Console.WriteLine("""
        Nxa.GFlib3.Render

        Usage: Nxa.GFlib3.Render [options]

          --dump <path>       Dump root (default: NXA_GF3_DUMP)
          --area <name>       Terrain pack, e.g. ha_area00
          --model <name>      Model pack inside bin/field/model/pack
          --mesh <text>       Load the first model whose mesh/material name contains text
          --lang <en|ja>      UI language
          --load-check        Load the area and print counts, then exit
          --smoke-test        Render 90 frames, then exit
          --frames <n>        Render n frames, then exit
          --screenshot <path> Save the window as a 24-bit BMP
          --screenshot-delay <s> Wait this many seconds before the screenshot
          --reload-area <name> Diagnostic: switch to this area after the first frame
          --reload-model <name> Model pack for --reload-area (default: matching pack)
          --select-instance <n> Diagnostic: select this vegetation instance at startup
          --tool <name>       Diagnostic: select, move, rotate, scale, or place at startup
          --frame-selection   Diagnostic: frame the startup selection
          --duplicate-instance <n> Diagnostic: duplicate this instance after the first frame
          --no-plants         Hide vegetation
          --no-room           Hide room geometry
          --no-terrain        Hide the heightmap terrain
          --no-scene          Hide resident scene-table placements
          --terrain-stride <n> Sample every nth terrain row/column (default 1)
          --terrain-tile-scale <n> Multiply SplatST terrain tiling (default 128)
          --no-textures       Render without base-color textures
          -h, --help          Show this help

        Controls:
          RMB look, WASD move, Q/E down/up, Shift sprint, F frame
          LMB select vegetation, 1-4 select/move/rotate/scale
          5 place vegetation, Ctrl+D duplicate
          Ctrl+Z undo, Ctrl+Y redo, Esc cancel drag
        """);
}

