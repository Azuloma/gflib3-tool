// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Gfpak;

namespace Nxa.GFlib3.Core.Formats.Resident;

/// <summary>
/// Model-path catalog from <c>resident_release.gfpak</c>. Each entry maps a
/// scene model hash to a TRMDL path under <c>bin/field/model</c>.
/// </summary>
public sealed class ResidentModelCatalog
{
    /// <summary>Smallest catalog entry observed in the dump (261,584 bytes).</summary>
    public const int MinArchiveBytes = 64 * 1024;

    /// <summary>Upper bound for catalog candidates; larger entries are placement data.</summary>
    public const int MaxArchiveBytes = 4 * 1024 * 1024;

    private const int EntryFieldCount = 11;
    private const int MaxEntries = 20_000;

    private readonly Dictionary<ulong, ResidentModelCatalogEntry> _byHash;

    private ResidentModelCatalog(
        ResidentModelCatalogEntry[] entries,
        Dictionary<ulong, ResidentModelCatalogEntry> byHash)
    {
        Entries = entries;
        _byHash = byHash;
    }

    public IReadOnlyList<ResidentModelCatalogEntry> Entries { get; }

    public bool TryGet(ulong hash, out ResidentModelCatalogEntry entry)
        => _byHash.TryGetValue(hash, out entry!);

    public static ResidentModelCatalog Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        FlatBufferTable root = FlatBufferTable.GetRoot(data);
        if (!root.TryGetVector(0, 4, out FlatBufferVector vector))
        {
            throw new InvalidDataException("Model catalog has no entry vector.");
        }

        if (vector.Count == 0 || vector.Count > MaxEntries)
        {
            throw new InvalidDataException($"Model catalog count {vector.Count} is outside 1..{MaxEntries}.");
        }

        var entries = new ResidentModelCatalogEntry[vector.Count];
        var byHash = new Dictionary<ulong, ResidentModelCatalogEntry>(vector.Count);
        bool hasUsablePath = false;
        for (int i = 0; i < entries.Length; i++)
        {
            try
            {
                entries[i] = ParseEntry(vector.GetTable(i));
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Model catalog entry {i}: {ex.Message}", ex);
            }

            if (entries[i].ModelPath.Length > 0 || entries[i].PackedModelPath.Length > 0)
            {
                hasUsablePath = true;
                byHash.TryAdd(entries[i].Hash, entries[i]);
            }
        }

        if (!hasUsablePath)
        {
            throw new InvalidDataException("Model catalog has no usable model paths.");
        }

        return new ResidentModelCatalog(entries, byHash);
    }

    public static bool TryParse(byte[] data, out ResidentModelCatalog? catalog)
    {
        try
        {
            catalog = Parse(data);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or OverflowException)
        {
            catalog = null;
            return false;
        }
    }

    /// <summary>Finds the model catalog entry in a resident archive by shape.</summary>
    public static ResidentModelCatalog FindInArchive(GfpakArchive archive, out GfpakEntry entry)
    {
        ArgumentNullException.ThrowIfNull(archive);

        foreach (GfpakEntry candidate in archive.Entries)
        {
            if (candidate.SizeDecompressed < MinArchiveBytes
                || candidate.SizeDecompressed > MaxArchiveBytes)
            {
                continue;
            }

            if (TryParse(archive.ReadEntry(candidate), out ResidentModelCatalog? catalog))
            {
                entry = candidate;
                return catalog!;
            }
        }

        throw new InvalidDataException("Resident archive has no model catalog entry.");
    }

    private static ResidentModelCatalogEntry ParseEntry(FlatBufferTable table)
    {
        if (table.FieldCount != EntryFieldCount)
        {
            throw new InvalidDataException(
                $"Catalog entry has {table.FieldCount} fields; expected {EntryFieldCount}.");
        }

        if (!table.HasField(0))
        {
            throw new InvalidDataException("Catalog entry has no hash field.");
        }

        ulong hash = table.GetUInt64(0);
        string modelPath = table.GetString(1) ?? string.Empty;
        string packedPath = table.GetString(8) ?? string.Empty;
        ValidateTrmdlPath(modelPath, "model");
        ValidateTrmdlPath(packedPath, "packed model");

        return new ResidentModelCatalogEntry(
            hash,
            modelPath,
            packedPath,
            table.GetString(2),
            table.GetString(10));
    }

    private static void ValidateTrmdlPath(string path, string role)
    {
        if (path.Length > 0
            && (!path.StartsWith("bin/", StringComparison.Ordinal)
                || !path.EndsWith(".trmdl", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException($"Catalog {role} path '{path}' is not a bin/ TRMDL path.");
        }
    }
}

public sealed class ResidentModelCatalogEntry
{
    internal ResidentModelCatalogEntry(
        ulong hash,
        string modelPath,
        string packedModelPath,
        string? collisionPath,
        string? packedCollisionPath)
    {
        Hash = hash;
        ModelPath = modelPath;
        PackedModelPath = packedModelPath;
        CollisionPath = string.IsNullOrEmpty(collisionPath) ? null : collisionPath;
        PackedCollisionPath = string.IsNullOrEmpty(packedCollisionPath) ? null : packedCollisionPath;
    }

    public ulong Hash { get; }

    /// <summary>Path inside the shared background-part pack, for example <c>bin/field/model/bg_parts/...</c>.</summary>
    public string ModelPath { get; }

    /// <summary>Path inside an area model pack, for example <c>bin/field/model/pack/...</c>.</summary>
    public string PackedModelPath { get; }

    public string? CollisionPath { get; }

    public string? PackedCollisionPath { get; }

    /// <summary>File name used to resolve <see cref="PackedModelPath"/> inside a pack folder.</summary>
    public string PackedModelFileName => Path.GetFileName(PackedModelPath);

    public string ModelFileName => Path.GetFileName(ModelPath);
}
