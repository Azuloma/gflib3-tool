// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

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
            "manifest" => MissingArguments(),
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
