// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Trinity;

namespace Nxa.GFlib3.Core.Tests;

public sealed class TrinityMeshBuilderTests
{
    [Fact]
    public void BuildsRoomMeshesWhenDumpIsConfigured()
    {
        string? path = DumpPack("ha_area00_s07.gfpak");
        if (path is null)
        {
            return;
        }

        using var pack = GfpakArchive.Open(path);
        TrmbfFile trmbf = TrinityFile.Parse(pack.ReadEntry(pack.Entries[0])).Trmbf!;
        TrmshFile trmsh = TrinityFile.Parse(pack.ReadEntry(pack.Entries[2])).Trmsh!;

        Assert.Equal(39, trmbf.Buffers.Count);
        Assert.Equal(39, trmsh.Meshes.Count);

        TrinityMeshGeometry mirror = TrinityMeshBuilder.Build(trmbf.Buffers[0], trmsh.Meshes[0]);
        Assert.Equal("mirror_low_shape", mirror.Name);
        Assert.Equal(1043, mirror.Vertices.Length);
        Assert.Equal(3018, mirror.Indices.Length);
        Assert.Single(mirror.Parts);
        Assert.Equal("room_area00_s07_01", mirror.Parts[0].MaterialName);
        AssertBounds(trmsh.Meshes[0].Bounds, mirror.Bounds, 1e-4f);

        TrinityVertex vertex = mirror.Vertices[0];
        AssertVector(vertex.Normal, 1, 0, 0, 1e-3f);
        AssertVector(vertex.Tangent, 0, 0, -1, 1e-3f);
        Assert.InRange(vertex.Tangent.W, 0.999f, 1.001f);
        AssertVector(vertex.Color, 1, 100 / 255f, 0, 1e-3f);
        Assert.InRange(vertex.Color.W, 0.999f, 1.001f);

        for (int i = 0; i < trmsh.Meshes.Count; i++)
        {
            TrinityMeshGeometry geometry = TrinityMeshBuilder.Build(trmbf.Buffers[i], trmsh.Meshes[i]);
            Assert.Equal(trmsh.Meshes[i].MeshParts.Count, geometry.Parts.Count);
            Assert.All(geometry.Indices, index => Assert.True(index < geometry.Vertices.Length));
        }
    }

    [Fact]
    public void BuildsMultiPartIndex32MeshWhenDumpIsConfigured()
    {
        string? path = DumpPack("ha_area00_s01.gfpak");
        if (path is null)
        {
            return;
        }

        using var pack = GfpakArchive.Open(path);
        TrinityFile[] files = pack.Entries
            .Select(entry => TryParse(pack.ReadEntry(entry)))
            .Where(file => file is not null)
            .Cast<TrinityFile>()
            .ToArray();

        TrmshFile trmsh = files.First(file => file.Kind == TrinityKind.Trmsh && file.Trmsh!.Meshes.Count == 2).Trmsh!;
        TrmbfFile trmbf = files.First(file => file.Kind == TrinityKind.Trmbf && file.Trmbf!.Buffers.Count == 2).Trmbf!;
        TrmshMesh room = trmsh.Meshes[1];
        Assert.Equal(TrIndexFormat.Index32, room.IndexFormat);
        Assert.Equal(14, room.MeshParts.Count);

        TrinityMeshGeometry geometry = TrinityMeshBuilder.Build(trmbf.Buffers[1], room);
        Assert.Equal(432655, geometry.Vertices.Length);
        Assert.Equal(924645, geometry.Indices.Length);
        Assert.Equal(14, geometry.Parts.Count);
        AssertBounds(room.Bounds, geometry.Bounds, 1e-4f);

        long expectedStart = 0;
        foreach (TrinitySubMesh part in geometry.Parts)
        {
            Assert.Equal(expectedStart, part.IndexStart);
            Assert.NotNull(part.MaterialName);
            expectedStart += part.IndexCount;
        }

        Assert.Equal(geometry.Indices.Length, expectedStart);
    }

    private static TrinityFile? TryParse(byte[] data)
    {
        try
        {
            return TrinityFile.Parse(data);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static void AssertBounds(BoundingBox? expected, BoundingBox actual, float tolerance)
    {
        Assert.NotNull(expected);
        Assert.InRange(Math.Abs(expected.Value.Min.X - actual.Min.X), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Value.Min.Y - actual.Min.Y), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Value.Min.Z - actual.Min.Z), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Value.Max.X - actual.Max.X), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Value.Max.Y - actual.Max.Y), 0, tolerance);
        Assert.InRange(Math.Abs(expected.Value.Max.Z - actual.Max.Z), 0, tolerance);
    }

    private static void AssertVector(Vector3f actual, float x, float y, float z, float tolerance)
    {
        Assert.InRange(Math.Abs(actual.X - x), 0, tolerance);
        Assert.InRange(Math.Abs(actual.Y - y), 0, tolerance);
        Assert.InRange(Math.Abs(actual.Z - z), 0, tolerance);
    }

    private static void AssertVector(Vector4f actual, float x, float y, float z, float tolerance)
    {
        Assert.InRange(Math.Abs(actual.X - x), 0, tolerance);
        Assert.InRange(Math.Abs(actual.Y - y), 0, tolerance);
        Assert.InRange(Math.Abs(actual.Z - z), 0, tolerance);
    }

    private static string? DumpPack(string name)
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return null;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "model", "pack", name);
        return File.Exists(path) ? path : null;
    }
}
