// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Cli.Commands;

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    PrintUsage();
    return 0;
}

try
{
    return args[0] switch
    {
        "gfpak" => GfpakCommand.Run(args.AsSpan(1)),
        "sarc" => SarcCommand.Run(args.AsSpan(1)),
        "trinity" => TrinityCommand.Run(args.AsSpan(1)),
        _ => UnknownCommand(args[0]),
    };
}
catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or DllNotFoundException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static int UnknownCommand(string command)
{
    Console.Error.WriteLine($"error: unknown command '{command}'");
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        Nxa.GFlib3.Cli

        Commands:
          gfpak info <archive>                          Show header and totals
          gfpak list <archive>                          List file-table entries
          gfpak extract <archive> <output-dir> [--entry <index>]
                                                        Decompress entries to disk
          sarc info <archive>                           Show SARC header details
          sarc list <archive>                           List SARC entries
          sarc extract <archive> <output-dir> [--entry <index>]
                                                        Extract SARC entries to disk
          trinity info <file> [--type <kind>]           Inspect a Trinity model file
                                                        (trmbf/trmsh/trmdl/trmtr/trskl)

        The native decoder is loaded from NXA_GF3_NATIVE, the application
        directory, or artifacts/native. Build it with scripts/build-native.ps1.
        """);
}
