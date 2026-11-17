// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using Nxa.GFlib3.Core.Compression;
using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Cli.Commands;

internal static class GfpakCommand
{
    public static int Run(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing gfpak subcommand");
            return 1;
        }

        return args[0] switch
        {
            "info" when args.Length == 2 => Info(args[1]),
            "list" when args.Length == 2 => List(args[1]),
            "extract" when args.Length >= 3 => Extract(args),
            "info" or "list" or "extract" => MissingArguments(args[0]),
            _ => UnknownSubcommand(args[0]),
        };
    }

    private static int Info(string path)
    {
        using var archive = GfpakArchive.Open(path);

        Console.WriteLine($"archive:      {path}");
        Console.WriteLine($"version:      0x{archive.Version:X}");
        Console.WriteLine($"relocated:    {archive.IsRelocated}");
        Console.WriteLine($"files:        {archive.Entries.Count}");
        Console.WriteLine($"folders:      {archive.Folders.Count}");

        var codes = archive.Entries
            .GroupBy(entry => entry.Compression)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}={group.Count()}");
        Console.WriteLine($"compression:  {string.Join(", ", codes)}");

        long compressed = archive.Entries.Sum(entry => (long)entry.SizeCompressed);
        long decompressed = archive.Entries.Sum(entry => (long)entry.SizeDecompressed);
        Console.WriteLine($"compressed:   {compressed:N0} bytes");
        Console.WriteLine($"decompressed: {decompressed:N0} bytes");
        return 0;
    }

    private static int List(string path)
    {
        using var archive = GfpakArchive.Open(path);

        Console.WriteLine("IDX  TYPE            LEVEL  COMPRESSED  DECOMPRESSED  OFFSET      PATHHASH");
        foreach (GfpakEntry entry in archive.Entries)
        {
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{entry.Index,3}  {ShortName(entry.Compression),-14}  {entry.Level,5}  " +
                $"{entry.SizeCompressed,10:N0}  {entry.SizeDecompressed,12:N0}  " +
                $"0x{entry.OffsetPacked:X8}  0x{archive.PathHashes[entry.Index]:X16}"));
        }

        return 0;
    }

    private static int Extract(ReadOnlySpan<string> args)
    {
        string archivePath = args[1];
        string outputDirectory = args[2];
        int? onlyIndex = null;

        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--entry" when i + 1 < args.Length:
                    onlyIndex = int.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
                default:
                    Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                    return 1;
            }
        }

        using var archive = GfpakArchive.Open(archivePath);
        if (onlyIndex is int index && (uint)index >= (uint)archive.Entries.Count)
        {
            Console.Error.WriteLine($"error: entry {index} is out of range");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);

        int written = 0;
        int failed = 0;
        foreach (GfpakEntry entry in archive.Entries)
        {
            if (onlyIndex is int selected && entry.Index != selected)
            {
                continue;
            }

            string fileName = string.Create(
                CultureInfo.InvariantCulture,
                $"{entry.Index:D4}_{archive.PathHashes[entry.Index]:X16}.bin");
            string outputPath = Path.Combine(outputDirectory, fileName);

            try
            {
                byte[] data = archive.ReadEntry(entry);
                File.WriteAllBytes(outputPath, data);
                written++;
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or DllNotFoundException)
            {
                Console.Error.WriteLine($"error: entry {entry.Index}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"extracted {written} entr{(written == 1 ? "y" : "ies")} to {outputDirectory}");
        if (failed > 0)
        {
            Console.Error.WriteLine($"failed: {failed} entr{(failed == 1 ? "y" : "ies")}");
            return 1;
        }

        return 0;
    }

    private static string ShortName(CompressionType type) => type switch
    {
        CompressionType.None => "None",
        CompressionType.Zlib => "Zlib",
        CompressionType.Lz4 => "LZ4",
        CompressionType.OodleKraken => "OodleKraken",
        CompressionType.OodleLeviathan => "OodleLeviathan",
        CompressionType.OodleMermaid => "OodleMermaid",
        CompressionType.OodleSelkie => "OodleSelkie",
        CompressionType.OodleHydra => "OodleHydra",
        _ => $"Unknown({(byte)type})",
    };

    private static int MissingArguments(string subcommand)
    {
        Console.Error.WriteLine($"error: missing arguments for 'gfpak {subcommand}'");
        return 1;
    }

    private static int UnknownSubcommand(string subcommand)
    {
        Console.Error.WriteLine($"error: unknown gfpak subcommand '{subcommand}'");
        return 1;
    }
}
