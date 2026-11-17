// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Text;

namespace Nxa.GFlib3.Core.Hashing;

/// <summary>FNV-1a 64-bit hashing as used by GFLXPACK path tables.</summary>
public static class Fnv1a64
{
    public const ulong OffsetBasis = 0xCBF29CE484222325;
    public const ulong Prime = 0x100000001B3;

    public static ulong Hash(ReadOnlySpan<byte> data)
    {
        ulong hash = OffsetBasis;
        foreach (byte value in data)
        {
            hash ^= value;
            hash *= Prime;
        }

        return hash;
    }

    public static ulong Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Hash(Encoding.UTF8.GetBytes(value));
    }
}
