// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Compression;

/// <summary>Compression codec identifiers used by GFlib3 containers.</summary>
public enum CompressionType : byte
{
    None = 0,
    Zlib = 1,
    Lz4 = 2,
    OodleKraken = 3,
    OodleLeviathan = 4,
    OodleMermaid = 5,
    OodleSelkie = 6,
    OodleHydra = 7,
}
