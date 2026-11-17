// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Text;

namespace Nxa.GFlib3.Core.Formats.Sarc;

/// <summary>
/// Hash used by SARC name tables. GFlib3 uses the SARC rolling hash
/// (<c>h = h * 0x65 + c</c>) without the final key multiplication.
/// </summary>
public static class SarcHash
{
    public const uint DefaultMultiplier = 0x65;

    public static uint Compute(ReadOnlySpan<byte> name, uint multiplier = DefaultMultiplier)
    {
        uint hash = 0;
        foreach (byte value in name)
        {
            hash = unchecked((hash * multiplier) + value);
        }

        return hash;
    }

    public static uint Compute(string name, uint multiplier = DefaultMultiplier)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Compute(Encoding.UTF8.GetBytes(name), multiplier);
    }
}
