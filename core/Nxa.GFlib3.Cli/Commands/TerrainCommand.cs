// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Cli.Commands;

internal static class TerrainCommand
{
    public static int Run(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing terrain subcommand");
            return 1;
        }

        return args[0] switch
        {
            "manifest" when args.Length == 2 => Manifest(args[1]),
            "heightmap" when args.Length >= 2 => Heightmap(args),
            "mesh" when args.Length >= 2 => Mesh(args),
            "manifest" or "heightmap" => MissingArguments(),
            _ => UnknownSubcommand(args[0]),
        };
    }

    private static int Manifest(string path)
    {
        using GfpakArchive archive = GfpakArchive.Open(path);
        TerrainManifest manifest = TerrainManifest.FindInArchive(archive, out GfpakEntry entry);

        int instancePaths = manifest.PlantGroups.Sum(group => group.InstancePaths.Count);

        Console.WriteLine($"entry:        {entry.Index}");
        Console.WriteLine($"size:         {entry.SizeDecompressed}");
        Console.WriteLine($"plant groups: {manifest.PlantGroupCount} ({instancePaths} .trins)");
        Console.WriteLine($"detail groups:{manifest.DetailGroupCount,3}");
        Console.WriteLine($"detail maps:  {manifest.DetailMapPaths.Count}");
        Console.WriteLine($"material:     {manifest.MaterialPath ?? "(none)"}");

        foreach (TerrainPlantGroup group in manifest.PlantGroups)
        {
            Console.WriteLine($"  {group.ModelPath} ({group.InstancePaths.Count})");
        }

        return 0;
    }

    private static int Heightmap(ReadOnlySpan<string> args)
    {
        string path = args[1];
        (int X, int Y)? sample = null;
        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--sample" && i + 2 < args.Length)
            {
                if (!int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sampleX)
                    || !int.TryParse(args[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sampleY))
                {
                    Console.Error.WriteLine("error: invalid sample coordinates");
                    return 1;
                }

                sample = (sampleX, sampleY);
                i += 2;
            }
            else
            {
                Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                return 1;
            }
        }

        using GfpakArchive archive = GfpakArchive.Open(path);
        string packName = Path.GetFileNameWithoutExtension(path);
        string folder = $"bin/field/terrain/{packName}/terrain/";
        TerrainManifest manifest = TerrainManifest.FindInArchive(archive, out _);
        TerrainMaterialSet material = TerrainMaterialSet.FindInArchive(archive, folder);
        TerrainSurface surface = TerrainSurface.Load(archive, manifest, material);
        TerrainHeightmap map = surface.Heightmap;
        (float min, float max) = map.GetRange();

        Console.WriteLine($"material:  {material.GetTexturePath(TerrainMaterialSet.HeightMapTexture) ?? "(none)"}");
        Console.WriteLine($"heightmap: {map.Width}x{map.Height}");
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"extent:    {surface.WorldExtentX:0.###} x {surface.WorldExtentZ:0.###} "
                + $"({surface.WorldUnitsPerSampleX:0.###} x {surface.WorldUnitsPerSampleZ:0.###} units/sample)"));
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"scale:     {surface.HeightScale:0.###}"));
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"range:     {min:0.####} .. {max:0.####} ({min * surface.HeightScale:0.###} .. {max * surface.HeightScale:0.###})"));
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"center:    {surface.GetHeightAtSample(map.Width / 2, map.Height / 2):0.###}"));

        if (sample is (int sampleXValue, int sampleYValue))
        {
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"sample:    ({sampleXValue}, {sampleYValue}) = {surface.GetHeightAtSample(sampleXValue, sampleYValue):0.###}"));
        }

        return 0;
    }

    private static int Mesh(ReadOnlySpan<string> args)
    {
        string path = args[1];
        int stride = TerrainMeshBuilder.DefaultStride;
        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--stride" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out stride)
                        || stride < 1)
                    {
                        Console.Error.WriteLine($"error: invalid stride '{args[i]}'");
                        return 1;
                    }

                    break;
                default:
                    Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                    return 1;
            }
        }

        using GfpakArchive archive = GfpakArchive.Open(path);
        string packName = Path.GetFileNameWithoutExtension(path);
        string folder = $"bin/field/terrain/{packName}/terrain/";
        TerrainManifest manifest = TerrainManifest.FindInArchive(archive, out _);
        TerrainMaterialSet material = TerrainMaterialSet.FindInArchive(archive, folder);
        TerrainSurface surface = TerrainSurface.Load(archive, manifest, material);
        TerrainMeshGeometry mesh = TerrainMeshBuilder.Build(surface, stride);

        Console.WriteLine($"vertices:  {mesh.Vertices.Length:N0}");
        Console.WriteLine($"indices:   {mesh.Indices.Length:N0}");
        Console.WriteLine($"bounds:    min {mesh.Bounds.Min.X:0.#}, {mesh.Bounds.Min.Y:0.#}, {mesh.Bounds.Min.Z:0.#}");
        Console.WriteLine($"           max {mesh.Bounds.Max.X:0.#}, {mesh.Bounds.Max.Y:0.#}, {mesh.Bounds.Max.Z:0.#}");
        return 0;
    }

    private static int MissingArguments()
    {
        Console.Error.WriteLine("error: missing arguments");
        return 1;
    }

    private static int UnknownSubcommand(string command)
    {
        Console.Error.WriteLine($"error: unknown terrain subcommand '{command}'");
        return 1;
    }
}
