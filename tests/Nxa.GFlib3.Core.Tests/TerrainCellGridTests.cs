// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Core.Tests;

public sealed class TerrainCellGridTests
{
    [Fact]
    public void MapsBaseAreaCellsAndFlipsZ()
    {
        var grid = new TerrainCellGrid(0f, 1024f, 1024f);

        Assert.Equal(new TerrainCell(6, 5), grid.CellAt(445.166f, 650.67f));
        Assert.Equal(new TerrainCell(0, 0), grid.CellAt(0f, 1024f));
        Assert.Equal(new TerrainCell(15, 15), grid.CellAt(1024f, 0f));
        Assert.Equal(new TerrainCell(15, 0), grid.CellAt(1024f, 1024f));
    }

    [Fact]
    public void MapsArea11Cells()
    {
        var grid = new TerrainCellGrid(0f, 2047f, 2047f);

        Assert.Equal(new TerrainCell(5, 5), grid.CellAt(755.96f, 1338.28f));
        Assert.Equal(new TerrainCell(15, 0), grid.CellAt(1920f, 2047f));
        Assert.Equal(new TerrainCell(15, 15), grid.CellAt(2047f, 0f));
    }

    [Fact]
    public void AppliesSubPackXOrigin()
    {
        var grid = new TerrainCellGrid(200f, 511.5f, 511.5f);

        Assert.Equal(new TerrainCell(2, 2), grid.CellAt(290.25f, 434.898f));
        Assert.Equal(new TerrainCell(15, 15), grid.CellAt(711.5f, 0f));
        Assert.False(grid.TryGetCell(199f, 100f, out _));
        Assert.True(grid.TryGetCell(200f, 100f, out _));
    }

    [Fact]
    public void RejectsOutOfRangeWorldPositions()
    {
        var grid = new TerrainCellGrid(0f, 1024f, 1024f);

        Assert.False(grid.TryGetCell(-0.125f, 0f, out _));
        Assert.False(grid.TryGetCell(0f, -0.125f, out _));
        Assert.False(grid.TryGetCell(1024.125f, 0f, out _));
        Assert.False(grid.TryGetCell(0f, 1024.125f, out _));
        Assert.False(grid.TryGetCell(float.NaN, 0f, out _));
    }
}
