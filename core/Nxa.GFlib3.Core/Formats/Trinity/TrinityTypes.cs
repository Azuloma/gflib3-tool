// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

public enum TrinityKind
{
    Unknown = 0,
    Trmbf,
    Trmsh,
    Trmdl,
    Trmtr,
    Trskl,
}

public enum TrIndexFormat : uint
{
    Index8 = 0,
    Index16 = 1,
    Index32 = 2,
    Index64 = 3,
}

public enum TrVertexUsage : uint
{
    None = 0,
    Position = 1,
    Normal = 2,
    Tangent = 3,
    Binormal = 4,
    Color = 5,
    TexCoord = 6,
    BlendIndex = 7,
    BlendWeights = 8,
    User = 9,
    UserId = 10,
}

public enum TrVertexFormat : uint
{
    None = 0,
    R8G8B8A8UnsignedNormalized = 20,
    W8X8Y8Z8Unsigned = 22,
    X32Unsigned = 36,
    X32Signed = 37,
    W16X16Y16Z16UnsignedNormalized = 39,
    W16X16Y16Z16Float = 43,
    X32Y32Float = 48,
    X32Y32Z32Float = 51,
    W32X32Y32Z32Unsigned = 52,
    W32X32Y32Z32Float = 54,
}

public enum TrNodeType : uint
{
    Normal = 0,
    Chained = 1,
    Leaf = 2,
}

public enum UVWrapMode : uint
{
    Clamp = 0,
    Wrap = 1,
    MirrorClamp = 2,
    MirrorClampToEdge = 3,
    MirrorClampToBorder = 4,
    ClampToBorder = 5,
    Mirror = 6,
    ClampToEdge = 7,
}

public readonly record struct Srt(Vector3f Scale, Vector3f Rotate, Vector3f Translate);

public readonly record struct Matrix4x3(
    Vector3f AxisX,
    Vector3f AxisY,
    Vector3f AxisZ,
    Vector3f AxisW);
