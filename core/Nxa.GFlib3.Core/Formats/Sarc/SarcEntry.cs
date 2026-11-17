// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Formats.Sarc;

/// <summary>One SFAT node with its resolved name.</summary>
public sealed class SarcEntry
{
    internal SarcEntry(
        int index,
        string name,
        uint hash,
        uint attributes,
        uint dataStart,
        uint dataEnd,
        long absoluteOffset)
    {
        Index = index;
        Name = name;
        Hash = hash;
        Attributes = attributes;
        DataStart = dataStart;
        DataEnd = dataEnd;
        AbsoluteOffset = absoluteOffset;
    }

    public int Index { get; }

    /// <summary>Name as stored in the SFNT table (may contain '/' directories).</summary>
    public string Name { get; }

    public uint Hash { get; }

    public uint Attributes { get; }

    /// <summary>Offset relative to the SARC data region.</summary>
    public uint DataStart { get; }

    /// <summary>End offset relative to the SARC data region.</summary>
    public uint DataEnd { get; }

    /// <summary>Absolute offset of the file data in the archive.</summary>
    public long AbsoluteOffset { get; }

    public int Size => checked((int)(DataEnd - DataStart));
}
