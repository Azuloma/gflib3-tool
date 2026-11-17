// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Trinity;

namespace Nxa.GFlib3.Core.Tests;

public sealed class TrinityFileTests
{
    private static readonly string[] ExpectedMeshPaths = ["room_area00_s07.trmsh"];
    private static readonly string[] ExpectedMaterials = ["room_area00_s07.trmtr"];

    [Fact]
    public void RejectsEmptyBuffer()
    {
        Assert.Throws<InvalidDataException>(() => TrinityFile.Parse(Array.Empty<byte>()));
    }

    [Fact]
    public void RejectsOutOfRangeRootOffset()
    {
        var data = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(data, 0x7FFFFFFF);

        Assert.Throws<InvalidDataException>(() => TrinityFile.Parse(data));
    }

    [Fact]
    public void ParsesAreaPackWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string path = Path.Combine(
            dump,
            "RomFS", "bin", "archive", "field", "model", "pack", "ha_area00_s07.gfpak");
        if (!File.Exists(path))
        {
            return;
        }

        using var pack = GfpakArchive.Open(path);

        TrinityFile trmbf = TrinityFile.Parse(pack.ReadEntry(pack.Entries[0]));
        Assert.Equal(TrinityKind.Trmbf, trmbf.Kind);
        Assert.Equal(39, trmbf.Trmbf!.Buffers.Count);
        Assert.Equal(6036, trmbf.Trmbf.Buffers[0].IndexBuffers[0].Length);
        Assert.Equal(41720, trmbf.Trmbf.Buffers[0].VertexBuffers[0].Length);

        TrinityFile trmdl = TrinityFile.Parse(pack.ReadEntry(pack.Entries[1]));
        Assert.Equal(TrinityKind.Trmdl, trmdl.Kind);
        Assert.Equal(ExpectedMeshPaths, trmdl.Trmdl!.MeshPaths);
        Assert.Equal(ExpectedMaterials, trmdl.Trmdl.Materials);
        Assert.Null(trmdl.Trmdl.SkeletonPath);

        TrinityFile trmsh = TrinityFile.Parse(pack.ReadEntry(pack.Entries[2]));
        Assert.Equal(TrinityKind.Trmsh, trmsh.Kind);
        Assert.Equal("room_area00_s07.trmbf", trmsh.Trmsh!.BufferFilePath);
        Assert.Equal(39, trmsh.Trmsh.Meshes.Count);

        TrmshMesh mesh = trmsh.Trmsh.Meshes[0];
        Assert.Equal("mirror_low_shape", mesh.Name);
        Assert.Equal(TrIndexFormat.Index16, mesh.IndexFormat);
        Assert.Single(mesh.MeshParts);
        Assert.Equal(3018u, mesh.MeshParts[0].IndexCount);
        Assert.Equal("room_area00_s07_01", mesh.MeshParts[0].MaterialName);
        Assert.Single(mesh.VertexDeclarations);
        Assert.Equal(40u, mesh.VertexDeclarations[0].Stride);
        Assert.Equal(5, mesh.VertexDeclarations[0].Elements.Count);
        Assert.Equal(TrVertexUsage.Position, mesh.VertexDeclarations[0].Elements[0].Usage);
        Assert.Equal(TrVertexFormat.X32Y32Z32Float, mesh.VertexDeclarations[0].Elements[0].Format);

        TrinityFile trmtr = TrinityFile.Parse(pack.ReadEntry(pack.Entries[3]));
        Assert.Equal(TrinityKind.Trmtr, trmtr.Kind);
        Assert.Equal(3, trmtr.Trmtr!.Materials.Count);
        Assert.Equal("room_area00_s07_01", trmtr.Trmtr.Materials[0].Name);
        Assert.Equal(6, trmtr.Trmtr.Materials[0].Textures.Count);
        Assert.Equal("room_area00_s07_01_alb.bntx", trmtr.Trmtr.Materials[0].Textures[0].File);

        TrinityFile trskl = TrinityFile.Parse(pack.ReadEntry(pack.Entries[4]));
        Assert.Equal(TrinityKind.Trskl, trskl.Kind);
        Assert.Equal(43, trskl.Trskl!.TransformNodes.Count);
        Assert.Equal("room_area00_s07_model", trskl.Trskl.TransformNodes[0].Name);
        Assert.Equal(-1, trskl.Trskl.TransformNodes[0].ParentNodeIndex);
        Assert.Equal(0, trskl.Trskl.TransformNodes[1].ParentNodeIndex);
    }
}
