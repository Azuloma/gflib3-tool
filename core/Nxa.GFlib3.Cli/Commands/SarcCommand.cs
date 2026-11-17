// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using Nxa.GFlib3.Core.Formats.Sarc;

namespace Nxa.GFlib3.Cli.Commands;

internal static class SarcCommand
{
    public static int Run(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing sarc subcommand");
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
        using var archive = SarcArchive.Open(path);

        Console.WriteLine($"archive:      {path}");
        Console.WriteLine($"version:      0x{archive.Version:X}");
        Console.WriteLine($"header size:  0x{archive.HeaderSizeValue:X}");
        Console.WriteLine($"data offset:  0x{archive.DataOffset:X}");
        Console.WriteLine($"file size:    {archive.DeclaredFileSize:N0} bytes");
        Console.WriteLine($"hash key:     0x{archive.HashKey:X}");
        Console.WriteLine($"files:        {archive.Entries.Count}");

        long total = archive.Entries.Sum(entry => (long)entry.Size);
        Console.WriteLine($"payload:      {total:N0} bytes");
        return 0;
    }

    private static int List(string path)
    {
        using var archive = SarcArchive.Open(path);

        Console.WriteLine("IDX       SIZE  HASH      NAME");
        foreach (SarcEntry entry in archive.Entries)
        {
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{entry.Index,3}  {entry.Size,9:N0}  0x{entry.Hash:X8}  {entry.Name}"));
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
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                    {
                        Console.Error.WriteLine($"error: invalid entry index '{args[i]}'");
                        return 1;
                    }

                    onlyIndex = parsed;
                    break;
                default:
                    Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                    return 1;
            }
        }

        using var archive = SarcArchive.Open(archivePath);
        if (onlyIndex is int index && (uint)index >= (uint)archive.Entries.Count)
        {
            Console.Error.WriteLine($"error: entry {index} is out of range");
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        string outputRoot = Path.GetFullPath(outputDirectory);

        int written = 0;
        int failed = 0;
        foreach (SarcEntry entry in archive.Entries)
        {
            if (onlyIndex is int selected && entry.Index != selected)
            {
                continue;
            }

            try
            {
                string relative = SarcPath.GetSafeRelativePath(entry.Name);
                string outputPath = Path.GetFullPath(Path.Combine(outputRoot, relative));
                if (!outputPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Unsafe SARC entry name '{entry.Name}'.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                File.WriteAllBytes(outputPath, archive.ReadEntry(entry));
                written++;
            }
            catch (Exception ex) when (ex is InvalidDataException
                or NotSupportedException
                or IOException
                or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"error: entry {entry.Index} ({entry.Name}): {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"extracted {written} file{(written == 1 ? string.Empty : "s")} to {outputDirectory}");
        if (failed > 0)
        {
            Console.Error.WriteLine($"failed: {failed} file{(failed == 1 ? string.Empty : "s")}");
            return 1;
        }

        return 0;
    }

    private static int MissingArguments(string subcommand)
    {
        Console.Error.WriteLine($"error: missing arguments for 'sarc {subcommand}'");
        return 1;
    }

    private static int UnknownSubcommand(string subcommand)
    {
        Console.Error.WriteLine($"error: unknown sarc subcommand '{subcommand}'");
        return 1;
    }
}
