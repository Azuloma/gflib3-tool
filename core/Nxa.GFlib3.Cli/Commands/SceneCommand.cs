// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Resident;

namespace Nxa.GFlib3.Cli.Commands;

internal static class SceneCommand
{
    public static int Run(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing scene subcommand");
            return 1;
        }

        return args[0] switch
        {
            "info" when args.Length >= 2 => Info(args),
            "info" => MissingArguments(),
            _ => UnknownSubcommand(args[0]),
        };
    }

    private static int Info(ReadOnlySpan<string> args)
    {
        string path = args[1];
        int? onlyEntry = null;
        bool all = false;

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--entry" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        Console.Error.WriteLine($"error: invalid entry index '{args[i]}'");
                        return 1;
                    }

                    onlyEntry = parsed;
                    break;
                case "--all":
                    all = true;
                    break;
                default:
                    Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                    return 1;
            }
        }

        if (onlyEntry is null && !path.EndsWith(".gfpak", StringComparison.OrdinalIgnoreCase))
        {
            PrintFile(ResidentSceneFile.Load(path), all);
            return 0;
        }

        using GfpakArchive archive = GfpakArchive.Open(path);
        if (onlyEntry is int index)
        {
            if ((uint)index >= (uint)archive.Entries.Count)
            {
                Console.Error.WriteLine($"error: entry {index} is out of range");
                return 1;
            }

            GfpakEntry entry = archive.Entries[index];
            byte[] data = archive.ReadEntry(entry);
            ResidentSceneFile file = ResidentSceneFile.Parse(data);
            Console.WriteLine($"entry {entry.Index}  scenes={file.Scenes.Count}");
            PrintFile(file, all: true);
            return 0;
        }

        IReadOnlyList<ResidentSceneArchiveEntry> found = ResidentSceneFile.FindInArchive(archive);
        foreach (ResidentSceneArchiveEntry item in found)
        {
            int named = item.File.Scenes.Count(scene => scene.Name != "none");
            if (named == 0)
            {
                continue;
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"entry {item.Entry.Index:D4}  size={item.Entry.SizeDecompressed,7:N0}  " +
                    $"scenes={item.File.Scenes.Count,3}  named={named,3}"));
            PrintFile(item.File, all);
        }

        Console.WriteLine($"scanned {archive.Entries.Count} entries; {found.Count} placement tables");
        return 0;
    }

    private static void PrintFile(ResidentSceneFile file, bool all)
    {
        foreach (ResidentScene scene in file.Scenes)
        {
            if (!all && scene.Name == "none")
            {
                continue;
            }

            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {scene.Name}  flag={(scene.Flag is bool flag ? (flag ? "1" : "0") : "(none)")}  " +
                    $"hash={scene.ModelHash:X16}  nodes={scene.Nodes.Count}"));

            foreach (ResidentSceneNode node in scene.Nodes)
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"    {node.TargetName}  pos={Format(node.Position)}  " +
                        $"yaw={node.YawDegrees:0.###}  scale={Format(node.Scale)}"));
            }
        }
    }

    private static string Format(Vector3f value) =>
        string.Create(CultureInfo.InvariantCulture, $"({value.X:0.###}, {value.Y:0.###}, {value.Z:0.###})");

    private static int MissingArguments()
    {
        Console.Error.WriteLine("error: missing arguments for 'scene info'");
        return 1;
    }

    private static int UnknownSubcommand(string subcommand)
    {
        Console.Error.WriteLine($"error: unknown scene subcommand '{subcommand}'");
        return 1;
    }
}
