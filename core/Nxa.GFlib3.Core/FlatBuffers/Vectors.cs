// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.FlatBuffers;

public readonly record struct Vector2f(float X, float Y);

public readonly record struct Vector3f(float X, float Y, float Z);

public readonly record struct Vector4f(float X, float Y, float Z, float W);

/// <summary>Bounding sphere: center plus radius.</summary>
public readonly record struct Sphere(float X, float Y, float Z, float Radius);

public readonly record struct BoundingBox(Vector3f Min, Vector3f Max);
