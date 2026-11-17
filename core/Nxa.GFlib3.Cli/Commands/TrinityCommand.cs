// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Trinity;

namespace Nxa.GFlib3.Cli.Commands;

internal static class TrinityCommand
{
    public static int Run(ReadOnlySpan<string> args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("error: missing trinity subcommand");
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
        TrinityKind? forced = null;

        for (int i = 2; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--type" when i + 1 < args.Length:
                    forced = ParseKind(args[++i]);
                    if (forced is null)
                    {
                        Console.Error.WriteLine($"error: unknown type '{args[i]}'");
                        return 1;
                    }

                    break;
                default:
                    Console.Error.WriteLine($"error: unknown argument '{args[i]}'");
                    return 1;
            }
        }

        byte[] data = File.ReadAllBytes(path);
        TrinityFile file;
        try
        {
            file = forced is null ? TrinityFile.Parse(data) : TrinityFile.Parse(data, forced.Value);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            Console.Error.WriteLine($"error: {path}: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"kind:     {file.Kind}");
        switch (file.Kind)
        {
            case TrinityKind.Trmbf:
                PrintTrmbf(file.Trmbf!);
                break;
            case TrinityKind.Trmsh:
                PrintTrmsh(file.Trmsh!);
                break;
            case TrinityKind.Trmdl:
                PrintTrmdl(file.Trmdl!);
                break;
            case TrinityKind.Trmtr:
                PrintTrmtr(file.Trmtr!);
                break;
            case TrinityKind.Trskl:
                PrintTrskl(file.Trskl!);
                break;
        }

        return 0;
    }

    private static void PrintTrmbf(TrmbfFile file)
    {
        Console.WriteLine($"version:  {file.Version}");
        Console.WriteLine($"buffers:  {file.Buffers.Count}");

        long indexBytes = 0;
        long vertexBytes = 0;
        foreach (TrmbfMeshBuffer buffer in file.Buffers)
        {
            indexBytes += buffer.IndexBuffers.Sum(static b => (long)b.Length);
            vertexBytes += buffer.VertexBuffers.Sum(static b => (long)b.Length);
        }

        Console.WriteLine($"index:    {indexBytes:N0} bytes");
        Console.WriteLine($"vertex:   {vertexBytes:N0} bytes");

        int shown = Math.Min(file.Buffers.Count, 5);
        for (int i = 0; i < shown; i++)
        {
            TrmbfMeshBuffer buffer = file.Buffers[i];
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  [{i}] index={buffer.IndexBuffers.Count} " +
                $"({buffer.IndexBuffers.Sum(static b => (long)b.Length):N0} B) " +
                $"vertex={buffer.VertexBuffers.Count} " +
                $"({buffer.VertexBuffers.Sum(static b => (long)b.Length):N0} B) " +
                $"morphs={buffer.MorphTargets.Count}"));
        }
    }

    private static void PrintTrmsh(TrmshFile file)
    {
        Console.WriteLine($"version:  {file.Version}");
        Console.WriteLine($"buffer:   {file.BufferFilePath ?? "(none)"}");
        Console.WriteLine($"meshes:   {file.Meshes.Count}");

        int shown = Math.Min(file.Meshes.Count, 10);
        for (int i = 0; i < shown; i++)
        {
            TrmshMesh mesh = file.Meshes[i];
            Console.WriteLine($"  [{i}] {mesh.Name ?? "(unnamed)"}");
            uint stride = mesh.VertexDeclarations.Count > 0 ? mesh.VertexDeclarations[0].Stride : 0;
            Console.WriteLine(
                $"      index={mesh.IndexFormat} parts={mesh.MeshParts.Count} " +
                $"decls={mesh.VertexDeclarations.Count} stride={stride}");

            foreach (TrMeshPart part in mesh.MeshParts.Take(4))
            {
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"      part: {part.IndexCount:N0} indices @ {part.IndexOffset:N0}, " +
                    $"material={part.MaterialName ?? "(none)"}, decl={part.VertexDeclarationIndex}"));
            }
        }
    }

    private static void PrintTrmdl(TrmdlFile file)
    {
        Console.WriteLine($"version:  {file.Version}");
        Console.WriteLine($"meshes:   {file.MeshPaths.Count}");
        foreach (string path in file.MeshPaths)
        {
            Console.WriteLine($"  {path}");
        }

        Console.WriteLine($"materials: {file.Materials.Count}");
        foreach (string material in file.Materials)
        {
            Console.WriteLine($"  {material}");
        }

        Console.WriteLine($"skeleton: {file.SkeletonPath ?? "(none)"}");
        if (file.Bounds is { } bounds)
        {
            Console.WriteLine($"bounds:   min {Format(bounds.Min)} max {Format(bounds.Max)}");
        }

        Console.WriteLine(
            $"field06:  {file.Field06.X}, {file.Field06.Y}, {file.Field06.Z}, {file.Field06.W}");
        Console.WriteLine($"trltt:    {file.TrlttPath ?? "(none)"}");
        Console.WriteLine($"lods:     {file.Lods.Count}");
        for (int i = 0; i < file.Lods.Count; i++)
        {
            Console.WriteLine(
                $"  [{i}] type={file.Lods[i].Type ?? "(none)"} entries="
                + string.Join(',', file.Lods[i].EntryIndices));
        }
    }

    private static void PrintTrmtr(TrmtrFile file)
    {
        Console.WriteLine($"materials: {file.Materials.Count}");
        for (int i = 0; i < file.Materials.Count; i++)
        {
            TrmtrMaterial material = file.Materials[i];
            Console.WriteLine($"  [{i}] {material.Name ?? "(unnamed)"}");
            Console.WriteLine($"      shaders={material.Shaders.Count} textures={material.Textures.Count}");

            foreach (TrmtrTexture texture in material.Textures.Take(8))
            {
                Console.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"      {texture.Name ?? "(unnamed)"} -> {texture.File ?? "(none)"} (slot {texture.Slot})"));
            }
        }
    }

    private static void PrintTrskl(TrsklFile file)
    {
        Console.WriteLine($"version:  {file.Version}");
        Console.WriteLine(
            $"nodes:    {file.TransformNodes.Count} (joints {file.JointInfos.Count}, " +
            $"helpers {file.HelperBones.Count})");

        int shown = Math.Min(file.TransformNodes.Count, 10);
        for (int i = 0; i < shown; i++)
        {
            TrTransformNode node = file.TransformNodes[i];
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  [{i}] {node.Name ?? "(unnamed)"} parent={node.ParentNodeIndex} " +
                $"joint={node.JointInfoIndex} type={node.NodeType}"));
        }
    }

    private static string Format(Vector3f value) => string.Create(
        CultureInfo.InvariantCulture,
        $"{value.X:F3}, {value.Y:F3}, {value.Z:F3}");

    private static TrinityKind? ParseKind(string value) => value.ToLowerInvariant() switch
    {
        "trmbf" => TrinityKind.Trmbf,
        "trmsh" => TrinityKind.Trmsh,
        "trmdl" => TrinityKind.Trmdl,
        "trmtr" => TrinityKind.Trmtr,
        "trskl" => TrinityKind.Trskl,
        _ => null,
    };

    private static int MissingArguments()
    {
        Console.Error.WriteLine("error: missing arguments for 'trinity info'");
        return 1;
    }

    private static int UnknownSubcommand(string subcommand)
    {
        Console.Error.WriteLine($"error: unknown trinity subcommand '{subcommand}'");
        return 1;
    }
}


