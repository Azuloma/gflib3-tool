// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Editing;

/// <summary>Builds `.trins` matrices for placement and duplication.</summary>
public static class PlantPlacement
{
    /// <summary>Duplicate offset along both X and Z, in world units.</summary>
    public const float DuplicateOffset = 2f;

    public static Matrix4x4RowMajor AtTerrainPoint(Vector3 point) =>
        SceneGeometry.Compose(new SceneTransform(point, 0f, Vector3.One));

    /// <summary>
    /// Copies the rotation/scale rows, offsets X and Z, then snaps Y to the
    /// terrain when the offset position lies inside the loaded heightmap.
    /// </summary>
    public static Matrix4x4RowMajor Duplicate(Matrix4x4RowMajor source, TerrainSurface? terrain)
    {
        if (!IsFinite(source))
        {
            throw new ArgumentException(
                "Source matrix contains a non-finite value.",
                nameof(source));
        }

        float x = source.Row3.X + DuplicateOffset;
        float z = source.Row3.Z + DuplicateOffset;
        float y = source.Row3.Y;
        if (terrain is not null && terrain.TryGetWorldHeight(x, z, out float height))
        {
            y = height;
        }

        var result = new Matrix4x4RowMajor(
            source.Row0,
            source.Row1,
            source.Row2,
            new Vector4f(x, y, z, source.Row3.W));
        if (!IsFinite(result))
        {
            throw new InvalidDataException(
                "Duplicated matrix contains a non-finite value; the source translation is too large.");
        }

        return result;
    }

    public static bool IsFinite(Matrix4x4RowMajor matrix) =>
        IsFinite(matrix.Row0)
        && IsFinite(matrix.Row1)
        && IsFinite(matrix.Row2)
        && IsFinite(matrix.Row3);

    private static bool IsFinite(Vector4f row) =>
        float.IsFinite(row.X)
        && float.IsFinite(row.Y)
        && float.IsFinite(row.Z)
        && float.IsFinite(row.W);
}
