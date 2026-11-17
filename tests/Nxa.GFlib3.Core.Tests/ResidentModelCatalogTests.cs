// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Resident;

namespace Nxa.GFlib3.Core.Tests;

public sealed class ResidentModelCatalogTests
{
    private const int CatalogEntry = 81;
    private const int DoorEntry = 88;

    [Fact]
    public void RejectsEmptyBuffer()
    {
        Assert.Throws<InvalidDataException>(() => ResidentModelCatalog.Parse(Array.Empty<byte>()));
    }

    [Fact]
    public void RejectsOutOfRangeRootOffset()
    {
        var data = new byte[16];
        data[0] = 0x7F;

        Assert.Throws<InvalidDataException>(() => ResidentModelCatalog.Parse(data));
    }

    [Fact]
    public void RejectsNonCatalogEntryWhenDumpIsConfigured()
    {
        byte[]? data = ReadEntry(DoorEntry);
        if (data is null)
        {
            return;
        }

        Assert.False(ResidentModelCatalog.TryParse(data, out _));
    }

    [Fact]
    public void ReadsCatalogWhenDumpIsConfigured()
    {
        byte[]? data = ReadEntry(CatalogEntry);
        if (data is null)
        {
            return;
        }

        ResidentModelCatalog catalog = ResidentModelCatalog.Parse(data);

        Assert.Equal(658, catalog.Entries.Count);
        Assert.True(catalog.TryGet(0x96FB4DA5E97D8CAFul, out ResidentModelCatalogEntry mountain));
        Assert.Equal(
            "bin/field/model/pack/ground_mountain01_tengan.trmdl",
            mountain.PackedModelPath);
        Assert.Equal(
            "bin/field/model/bg_parts/ground/ground_mountain01_tengan/ground_mountain01_tengan.trmdl",
            mountain.ModelPath);

        Assert.True(catalog.TryGet(0x96631D44ECC80661ul, out ResidentModelCatalogEntry house));
        Assert.Equal("objects_area00_house01_a.trmdl", house.PackedModelFileName);
    }

    [Fact]
    public void FindsCatalogEntryWhenDumpIsConfigured()
    {
        using GfpakArchive? archive = OpenResidentArchive();
        if (archive is null)
        {
            return;
        }

        ResidentModelCatalog catalog = ResidentModelCatalog.FindInArchive(archive, out GfpakEntry entry);

        Assert.Equal(CatalogEntry, entry.Index);
        Assert.Equal(658, catalog.Entries.Count);
    }

    private static byte[]? ReadEntry(int index)
    {
        using GfpakArchive? archive = OpenResidentArchive();
        if (archive is null)
        {
            return null;
        }

        Assert.InRange(index, 0, archive.Entries.Count - 1);
        return archive.ReadEntry(archive.Entries[index]);
    }

    private static GfpakArchive? OpenResidentArchive()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return null;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "resident_release.gfpak");
        return File.Exists(path) ? GfpakArchive.Open(path) : null;
    }
}
