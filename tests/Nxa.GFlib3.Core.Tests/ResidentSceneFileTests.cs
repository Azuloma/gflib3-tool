// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Resident;

namespace Nxa.GFlib3.Core.Tests;

public sealed class ResidentSceneFileTests
{
    private const int MountainEntry = 157;
    private const int TempleEntry = 1707;
    private const int NpcEntry = 1531;
    private const int BuildingEntry = 1521;
    private const int DoorEntry = 88;
    private const int CatalogEntry = 81;
    private const int PoemEntry = 1383;

    [Fact]
    public void RejectsEmptyBuffer()
    {
        Assert.Throws<InvalidDataException>(() => ResidentSceneFile.Parse(Array.Empty<byte>()));
    }

    [Fact]
    public void RejectsOutOfRangeRootOffset()
    {
        var data = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(data, 0x7FFFFFFF);

        Assert.Throws<InvalidDataException>(() => ResidentSceneFile.Parse(data));
    }

    [Fact]
    public void RejectsDoorTable()
    {
        byte[]? data = ReadEntry(DoorEntry);
        if (data is null)
        {
            return;
        }

        Assert.False(ResidentSceneFile.TryParse(data, out _));
    }

    [Fact]
    public void RejectsModelCatalog()
    {
        byte[]? data = ReadEntry(CatalogEntry);
        if (data is null)
        {
            return;
        }

        Assert.False(ResidentSceneFile.TryParse(data, out _));
    }

    [Fact]
    public void RejectsPoemTable()
    {
        byte[]? data = ReadEntry(PoemEntry);
        if (data is null)
        {
            return;
        }

        Assert.False(ResidentSceneFile.TryParse(data, out _));
    }

    [Fact]
    public void ReadsMountainPlacementWhenDumpIsConfigured()
    {
        byte[]? data = ReadEntry(MountainEntry);
        if (data is null)
        {
            return;
        }

        ResidentSceneFile file = ResidentSceneFile.Parse(data);

        Assert.Equal(9, file.Scenes.Count);
        ResidentScene mountain = file.Scenes[1];
        Assert.Equal("area00_s02_ground_mountain01_tengan", mountain.Name);
        Assert.Equal(0x96FB4DA5E97D8CAFul, mountain.ModelHash);
        Assert.True(mountain.Flag);

        ResidentSceneNode node = Assert.Single(mountain.Nodes);
        Assert.Equal("FEVE_VISIBLE_AREA00_S02_D110_BG", node.TargetName);
        AssertVector(new Vector3f(1817.5f, -7.0f, -3664.5f), node.Position);
        Assert.Equal(180.0f, node.YawDegrees, 3);
        AssertVector(new Vector3f(0.7f, 0.7f, 0.7f), node.Scale);
    }

    [Fact]
    public void ReadsTempleStatuesWhenDumpIsConfigured()
    {
        byte[]? data = ReadEntry(TempleEntry);
        if (data is null)
        {
            return;
        }

        ResidentSceneFile file = ResidentSceneFile.Parse(data);
        Assert.Equal(76, file.Scenes.Count);

        ResidentScene statue = file.Scenes.First(scene => scene.Name == "01_statue_rock_01");
        ResidentSceneNode node = Assert.Single(statue.Nodes);
        AssertVector(new Vector3f(483.18f, 59.0f, 570.64f), node.Position);
        Assert.Equal(90.0f, node.YawDegrees, 3);
        AssertVector(new Vector3f(1.0f, 1.0f, 1.0f), node.Scale);
    }

    [Fact]
    public void ReadsBuildingPlacementWhenDumpIsConfigured()
    {
        byte[]? data = ReadEntry(BuildingEntry);
        if (data is null)
        {
            return;
        }

        ResidentSceneFile file = ResidentSceneFile.Parse(data);
        Assert.Equal(438, file.Scenes.Count);

        ResidentScene stairs = file.Scenes.First(
            scene => scene.Name == "area04_objects_area04_stairs02_01");
        ResidentSceneNode node = Assert.Single(stairs.Nodes);
        AssertVector(new Vector3f(213.92f, 304.85f, 26.24f), node.Position);
        Assert.Equal(1.126f, node.YawDegrees, 3);
    }

    [Fact]
    public void ReadsNpcPlacementWhenDumpIsConfigured()
    {
        byte[]? data = ReadEntry(NpcEntry);
        if (data is null)
        {
            return;
        }

        ResidentSceneFile file = ResidentSceneFile.Parse(data);
        Assert.Equal(39, file.Scenes.Count);

        ResidentScene npc = file.Scenes[0];
        Assert.Equal("area04_npc_ouji", npc.Name);
        Assert.Equal(11, npc.Nodes.Count);

        ResidentSceneNode node = npc.Nodes[0];
        Assert.Equal("WPHA_STORY", node.TargetName);
        AssertVector(new Vector3f(184.913f, 154.804f, 472.91f), node.Position);
        Assert.Equal(-58.3f, node.YawDegrees, 3);
    }

    private static byte[]? ReadEntry(int index)
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return null;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "resident_release.gfpak");
        if (!File.Exists(path))
        {
            return null;
        }

        using var archive = GfpakArchive.Open(path);
        Assert.InRange(index, 0, archive.Entries.Count - 1);
        return archive.ReadEntry(archive.Entries[index]);
    }

    private static void AssertVector(Vector3f expected, Vector3f actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Z, actual.Z, 3);
    }
}
