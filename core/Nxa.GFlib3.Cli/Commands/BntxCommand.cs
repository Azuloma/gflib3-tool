// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using Nxa.GFlib3.Core.Formats.Bntx;

namespace Nxa.GFlib3.Cli.Commands;

internal static class BntxCommand
{
    public static int Run(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing bntx subcommand");
            return 1;
        }

        return args[0] switch
        {
            "info" when args.Length >= 2 => Info(args),
            "stats" when args.Length == 2 => Stats(args[1]),
            "decode" when args.Length >= 3 => Decode(args),
            "info" or "decode" or "stats" => MissingArguments(),
            _ => UnknownSubcommand(args[0]),
        };
    }

    private static int Info(ReadOnlySpan<string> args)
    {
        string path = args[1];
        if (args.Length > 2)
        {
            Console.Error.WriteLine($"error: unknown argument '{args[2]}'");
            return 1;
        }

        BntxFile file;
        try
        {
            file = BntxFile.Load(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            Console.Error.WriteLine($"error: {path}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"version:      {file.Version}");
        Console.WriteLine($"platform:     {file.Platform.TrimEnd()}");
        Console.WriteLine($"alignment:    1 << {file.AlignmentShift}");
        Console.WriteLine($"address size: {file.TargetAddressSize}");
        Console.WriteLine($"name:         {file.Name}");
        Console.WriteLine($"file size:    {file.FileSize:N0} bytes");
        Console.WriteLine($"textures:     {file.Textures.Count}");

        foreach (BntxTexture texture in file.Textures)
        {
            Console.WriteLine($"  [{texture.Index}] {texture.Name}");
            Console.WriteLine(
                $"      format={BntxFormats.GetName(texture.Format)} (0x{(uint)texture.Format:X4}) " +
                $"{texture.Width}x{texture.Height}x{texture.Depth} " +
                $"mips={texture.MipCount} tile={texture.TileMode} " +
                $"block-height-log2={texture.BlockHeightLog2}");
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"      image={texture.ImageSize:N0} B align={texture.Alignment} " +
                    $"channels=[{texture.ChannelRed},{texture.ChannelGreen},{texture.ChannelBlue},{texture.ChannelAlpha}] " +
                    $"mip0=0x{texture.MipOffsets[0]:X}"));
        }

        return 0;
    }

    private static int Stats(string path)
    {
        BntxFile file;
        try
        {
            file = BntxFile.Load(path);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            Console.Error.WriteLine($"error: {path}: {ex.Message}");
            return 1;
        }

        if (file.Textures.Count == 0)
        {
            Console.Error.WriteLine($"error: {path} has no textures.");
            return 1;
        }

        BntxTexture texture = file.Textures[0];
        Console.WriteLine(
            $"{texture.Name} {texture.Width}x{texture.Height} {BntxFormats.GetName(texture.Format)} " +
            $"array={texture.ArrayCount} mips={texture.MipCount}");

        for (int slice = 0; slice < texture.ArrayCount; slice++)
        {
            BntxImage image;
            try
            {
                image = BntxTextureDecoder.Decode(texture, 0, slice);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
            {
                Console.Error.WriteLine($"error: {path} slice {slice}: {ex.Message}");
                continue;
            }

            Console.WriteLine($" slice {slice}:");
            PrintChannelStats(image);
        }

        return 0;
    }

    private static void PrintChannelStats(BntxImage image)
    {
        string[] names = ["r", "g", "b", "a"];
        for (int channel = 0; channel < 4; channel++)
        {
            var histogram = new int[256];
            long sum = 0;
            byte min = 255;
            byte max = 0;
            for (int i = channel; i < image.Pixels.Length; i += 4)
            {
                byte value = image.Pixels[i];
                histogram[value]++;
                sum += value;
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }

            int count = image.Pixels.Length / 4;
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {names[channel]}: min={min} max={max} mean={(double)sum / count:0.##}"));

            var top = histogram
                .Select((countValue, value) => (Value: value, Count: countValue))
                .Where(item => item.Count > 0)
                .OrderByDescending(item => item.Count)
                .Take(6);
            foreach ((int value, int occurrences) in top)
            {
                Console.WriteLine(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"     {value,3}: {occurrences,9:N0} ({100.0 * occurrences / count:0.##}%)"));
            }
        }
    }

    private static int Decode(ReadOnlySpan<string> args)
    {
        string path = args[1];
        string output = args[2];
        int textureIndex = 0;
        int mip = 0;

        for (int i = 3; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--texture" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out textureIndex) ||
                        textureIndex < 0)
                    {
                        Console.Error.WriteLine($"error: invalid texture index '{args[i]}'");
                        return 1;
                    }

                    break;
                case "--mip" when i + 1 < args.Length:
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out mip) ||
                        mip < 0)
                    {
                        Console.Error.WriteLine($"error: invalid mip index '{args[i]}'");
                        return 1;
                    }

                    break;
                default:
                    Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                    return 1;
            }
        }

        BntxImage image;
        try
        {
            BntxFile file = BntxFile.Load(path);
            if (textureIndex >= file.Textures.Count)
            {
                Console.Error.WriteLine(
                    $"error: texture index {textureIndex} is out of range (count {file.Textures.Count})");
                return 1;
            }

            image = file.Textures[textureIndex].DecodeMip(mip);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"error: {path}: {ex.Message}");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)) ?? ".");
        WritePpm(output, image);
        Console.WriteLine(
            $"decoded {image.Width}x{image.Height} to {output} ({image.Pixels.Length:N0} bytes RGBA)");
        return 0;
    }

    private static void WritePpm(string path, BntxImage image)
    {
        using FileStream stream = File.Create(path);
        using var writer = new StreamWriter(stream, leaveOpen: true);
        writer.Write($"P6\n{image.Width} {image.Height}\n255\n");
        writer.Flush();

        byte[] rgb = new byte[checked(image.Width * image.Height * 3)];
        for (int i = 0; i < image.Width * image.Height; i++)
        {
            rgb[i * 3] = image.Pixels[i * 4];
            rgb[(i * 3) + 1] = image.Pixels[(i * 4) + 1];
            rgb[(i * 3) + 2] = image.Pixels[(i * 4) + 2];
        }

        stream.Write(rgb);
    }

    private static int MissingArguments()
    {
        Console.Error.WriteLine("error: missing arguments for 'bntx info|decode'");
        return 1;
    }

    private static int UnknownSubcommand(string subcommand)
    {
        Console.Error.WriteLine($"error: unknown bntx subcommand '{subcommand}'");
        return 1;
    }
}
