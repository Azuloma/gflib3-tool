// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;

namespace Nxa.GFlib3.Render;

/// <summary>Command-line settings for the viewer.</summary>
public sealed class AppOptions
{
    public const string DefaultArea = "ha_area00";
    public const string DefaultModel = "ha_area00_s02";

    public string? DumpRoot { get; private set; }

    public string Area { get; private set; } = DefaultArea;

    public string ModelPack { get; private set; } = DefaultModel;

    public string? MeshFilter { get; private set; }

    public string Language { get; private set; } = "en";

    public bool LoadCheck { get; private set; }

    public int SmokeFrames { get; private set; }

    public string? ScreenshotPath { get; private set; }

    public double ScreenshotDelaySeconds { get; private set; }

    public bool NoPlants { get; private set; }

    public bool NoTextures { get; private set; }

    public bool NoRoom { get; private set; }

    public bool NoTerrain { get; private set; }

    public bool NoScene { get; private set; }

    public int TerrainStride { get; private set; } = 1;

    /// <summary>Multiplier applied to the material's SplatST terrain tiling.</summary>
    public float TerrainTileScale { get; private set; } = 64f;

    public static AppOptions Parse(string[] args)
    {
        var options = new AppOptions
        {
            DumpRoot = Environment.GetEnvironmentVariable("NXA_GF3_DUMP"),
        };

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg)
            {
                case "--dump" when i + 1 < args.Length:
                    options.DumpRoot = args[++i];
                    break;
                case "--area" when i + 1 < args.Length:
                    options.Area = args[++i];
                    break;
                case "--model" when i + 1 < args.Length:
                    options.ModelPack = args[++i];
                    break;
                case "--mesh" when i + 1 < args.Length:
                    options.MeshFilter = args[++i];
                    break;
                case "--lang" when i + 1 < args.Length:
                    options.Language = args[++i];
                    break;
                case "--load-check":
                    options.LoadCheck = true;
                    break;
                case "--smoke-test":
                    options.SmokeFrames = 90;
                    break;
                case "--frames" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], out int frames) || frames < 1)
                    {
                        throw new ArgumentException("--frames requires a positive integer.");
                    }

                    options.SmokeFrames = frames;
                    break;
                case "--no-plants":
                    options.NoPlants = true;
                    break;
                case "--no-textures":
                    options.NoTextures = true;
                    break;
                case "--no-room":
                    options.NoRoom = true;
                    break;
                case "--no-terrain":
                    options.NoTerrain = true;
                    break;
                case "--no-scene":
                    options.NoScene = true;
                    break;
                case "--terrain-stride" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stride)
                        || stride < 1)
                    {
                        throw new ArgumentException("--terrain-stride requires a positive integer.");
                    }

                    options.TerrainStride = stride;
                    break;
                case "--terrain-tile-scale" when i + 1 < args.Length:
                    if (!float.TryParse(
                            args[++i],
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float tileScale)
                        || !float.IsFinite(tileScale)
                        || tileScale <= 0)
                    {
                        throw new ArgumentException("--terrain-tile-scale requires a positive number.");
                    }

                    options.TerrainTileScale = tileScale;
                    break;
                case "--screenshot" when i + 1 < args.Length:
                    options.ScreenshotPath = args[++i];
                    break;
                case "--screenshot-delay" when i + 1 < args.Length:
                    if (!double.TryParse(args[++i], NumberStyles.Float, CultureInfo.InvariantCulture, out double delay) || delay < 0)
                    {
                        throw new ArgumentException("--screenshot-delay requires a non-negative number of seconds.");
                    }

                    options.ScreenshotDelaySeconds = delay;
                    break;
                case "-h":
                case "--help":
                    throw new ShowHelpException();
                default:
                    throw new ArgumentException($"Unknown argument '{arg}'.");
            }
        }

        return options;
    }

    public string RequireDumpRoot()
    {
        if (string.IsNullOrWhiteSpace(DumpRoot))
        {
            throw new InvalidOperationException(
                "Dump root not set. Use --dump <path> or the NXA_GF3_DUMP environment variable.");
        }

        string romfs = Path.Combine(DumpRoot, "RomFS");
        if (!Directory.Exists(romfs))
        {
            throw new DirectoryNotFoundException($"Dump root '{DumpRoot}' has no RomFS directory.");
        }

        return DumpRoot;
    }
}

public sealed class ShowHelpException : Exception;









