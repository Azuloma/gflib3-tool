// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

/// <summary>A parsed Trinity model file, detected from its FlatBuffer shape.</summary>
public sealed class TrinityFile
{
    private readonly byte[] _data;

    private TrinityFile(byte[] data, TrinityKind kind)
    {
        _data = data;
        Kind = kind;
    }

    public TrinityKind Kind { get; }

    /// <summary>Raw parsed bytes; read-only so callers cannot invalidate parsed views.</summary>
    public ReadOnlyMemory<byte> Data => _data;

    public TrmbfFile? Trmbf { get; private init; }

    public TrmshFile? Trmsh { get; private init; }

    public TrmdlFile? Trmdl { get; private init; }

    public TrmtrFile? Trmtr { get; private init; }

    public TrsklFile? Trskl { get; private init; }

    public static TrinityFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllBytes(path));
    }

    public static TrinityFile Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        FlatBufferTable root = FlatBufferTable.GetRoot(data);
        return Parse(data, root, Detect(root));
    }

    /// <summary>Parses using an explicit kind when detection is ambiguous.</summary>
    public static TrinityFile Parse(byte[] data, TrinityKind kind)
    {
        ArgumentNullException.ThrowIfNull(data);
        FlatBufferTable root = FlatBufferTable.GetRoot(data);
        return Parse(data, root, kind);
    }

    private static TrinityFile Parse(byte[] data, FlatBufferTable root, TrinityKind kind)
    {
        return kind switch
        {
            TrinityKind.Trmbf => new TrinityFile(data, kind) { Trmbf = TrmbfFile.Parse(root) },
            TrinityKind.Trmsh => new TrinityFile(data, kind) { Trmsh = TrmshFile.Parse(root) },
            TrinityKind.Trmdl => new TrinityFile(data, kind) { Trmdl = TrmdlFile.Parse(root) },
            TrinityKind.Trmtr => new TrinityFile(data, kind) { Trmtr = TrmtrFile.Parse(root) },
            TrinityKind.Trskl => new TrinityFile(data, kind) { Trskl = TrsklFile.Parse(root) },
            _ => throw new InvalidDataException("Unrecognized Trinity FlatBuffer layout."),
        };
    }

    public static TrinityKind Detect(FlatBufferTable root)
    {
        if (TryEndsWith(root, 2, ".trmbf"))
        {
            return TrinityKind.Trmsh;
        }

        if (!root.TryGetVector(1, 4, out FlatBufferVector entries) || entries.Count == 0)
        {
            return TrinityKind.Unknown;
        }

        FlatBufferTable first = entries.GetTable(0);

        // TRMBF model buffers start with the index-buffer table vector.
        if (TryVector(first, 0))
        {
            return TrinityKind.Trmbf;
        }

        string? name = TryString(first, 0);

        // TRMDL mesh entries point at a .trmsh.
        if (name?.EndsWith(".trmsh", StringComparison.OrdinalIgnoreCase) == true)
        {
            return TrinityKind.Trmdl;
        }

        // TRSKL roots carry joint/helper vectors; TRMTR roots only carry materials.
        if (root.HasField(2) || root.HasField(3))
        {
            return TrinityKind.Trskl;
        }

        if (TryVector(first, 1))
        {
            return TrinityKind.Trmtr;
        }

        return TrinityKind.Unknown;
    }

    private static bool TryEndsWith(FlatBufferTable table, int field, string suffix)
    {
        try
        {
            return table.GetString(field)?.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static string? TryString(FlatBufferTable table, int field)
    {
        try
        {
            return table.GetString(field);
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static bool TryVector(FlatBufferTable table, int field)
    {
        try
        {
            return table.TryGetVector(field, 4, out _);
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
}
