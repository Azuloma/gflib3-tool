// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Bntx;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Core.Formats.Trinity;
using Nxa.GFlib3.Core.Hashing;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>A gfpak archive with logical-folder name resolution and parse caches.</summary>
internal sealed class GfpakPack : IDisposable
{
    private readonly GfpakArchive _archive;
    private readonly Dictionary<int, TrinityFile?> _trinity = [];
    private readonly Dictionary<int, BntxFile?> _bntx = [];

    public GfpakPack(string path, string logicalFolder)
    {
        ArchivePath = path;
        LogicalFolder = normaliseFolder(logicalFolder);
        _archive = GfpakArchive.Open(path);
    }

    public string ArchivePath { get; }

    public string LogicalFolder { get; }

    public IReadOnlyList<GfpakEntry> Entries => _archive.Entries;

    public GfpakArchive Archive => _archive;

    public GfpakEntry? Find(string fileName)
    {
        ulong hash = Fnv1a64.Hash(LogicalFolder + fileName);
        return _archive.TryGetEntryByPathHash(hash, out GfpakEntry? entry) ? entry : null;
    }

    public byte[] Read(GfpakEntry entry) => _archive.ReadEntry(entry);

    public TerrainManifest FindManifest() => TerrainManifest.FindInArchive(_archive, out _);

    public byte[]? TryRead(string fileName)
    {
        GfpakEntry? entry = Find(fileName);
        return entry is null ? null : Read(entry);
    }

    public TrinityFile? ParseTrinity(GfpakEntry entry)
    {
        if (_trinity.TryGetValue(entry.Index, out TrinityFile? cached))
        {
            return cached;
        }

        return ParseTrinity(entry, Read(entry));
    }

    public TrinityFile? ParseTrinity(GfpakEntry entry, byte[] data)
    {
        if (_trinity.TryGetValue(entry.Index, out TrinityFile? cached))
        {
            return cached;
        }

        TrinityFile? result;
        try
        {
            result = TrinityFile.Parse(data);
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
        {
            result = null;
        }

        _trinity[entry.Index] = result;
        return result;
    }

    public BntxFile? ParseBntx(GfpakEntry entry)
    {
        if (_bntx.TryGetValue(entry.Index, out BntxFile? cached))
        {
            return cached;
        }

        BntxFile? result = null;
        try
        {
            result = BntxFile.Parse(Read(entry));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or OverflowException)
        {
            result = null;
        }

        _bntx[entry.Index] = result;
        return result;
    }

    public void Dispose() => _archive.Dispose();

    private static string normaliseFolder(string folder)
    {
        string value = folder.Replace('\\', '/');
        return value.EndsWith('/') ? value : value + "/";
    }
}
