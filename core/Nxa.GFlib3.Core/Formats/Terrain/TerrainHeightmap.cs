// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Bntx;

namespace Nxa.GFlib3.Core.Formats.Terrain;

/// <summary>
/// Height field decoded from the terrain material <c>HeightMap</c> BNTX entry.
/// Values are row-major in file order; world orientation is applied by the
/// consumer until the mapping is verified.
/// </summary>
public sealed class TerrainHeightmap
{
    public const int MinDimension = 2;
    public const int MaxDimension = 4096;

    private readonly float[] _heights;

    private TerrainHeightmap(int width, int height, float[] heights)
    {
        Width = width;
        Height = height;
        _heights = heights;
    }

    public int Width { get; }

    public int Height { get; }

    public ReadOnlyMemory<float> Heights => _heights;

    public static TerrainHeightmap Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            return FromFile(BntxFile.Load(path));
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"Failed to parse terrain heightmap '{path}': {ex.Message}", ex);
        }
    }

    public static TerrainHeightmap FromFile(BntxFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Textures.Count != 1)
        {
            throw new InvalidDataException(
                $"Terrain heightmap BNTX '{file.Name}' has {file.Textures.Count} textures; expected one.");
        }

        return FromTexture(file.Textures[0]);
    }

    public static TerrainHeightmap FromTexture(BntxTexture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);

        if (texture.Format is not (BntxFormat.R16Float or BntxFormat.R32Float))
        {
            throw new NotSupportedException(
                $"Terrain heightmap '{texture.Name}' uses {BntxFormats.GetName(texture.Format)}; " +
                "expected R16_FLOAT or R32_FLOAT.");
        }

        int width = checked((int)texture.Width);
        int height = checked((int)texture.Height);
        if (width < MinDimension || width > MaxDimension || height < MinDimension || height > MaxDimension)
        {
            throw new InvalidDataException(
                $"Terrain heightmap '{texture.Name}' is {width}x{height}; " +
                $"expected {MinDimension}..{MaxDimension} per axis.");
        }

        float[] heights = BntxTextureDecoder.DecodeScalarFloat(texture, 0);
        return new TerrainHeightmap(width, height, heights);
    }

    public float GetHeight(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        return _heights[(y * Width) + x];
    }

    /// <summary>Minimum and maximum height across all samples.</summary>
    public (float Min, float Max) GetRange()
    {
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        foreach (float value in _heights)
        {
            if (float.IsNaN(value))
            {
                continue;
            }

            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        if (float.IsPositiveInfinity(min))
        {
            throw new InvalidDataException("Terrain heightmap contains no finite samples.");
        }

        return (min, max);
    }
}
