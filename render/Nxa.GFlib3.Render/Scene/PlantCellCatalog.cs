// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Text.RegularExpressions;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Render.Scene;

/// <summary>Resolves `.trins` files by plant model and 16 x 16 terrain cell.</summary>
public sealed class PlantCellCatalog
{
    private static readonly Regex CellPattern = new(
        @"_X(?<x>\d+)Z(?<z>\d+)_d\d+\.trins$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly FieldScene _scene;
    private readonly Dictionary<(int Model, int X, int Z), PlantInstanceFile> _files = [];
    private readonly Dictionary<(int Model, int X, int Z), PlantInstanceFile> _pending = [];

    public PlantCellCatalog(FieldScene scene, TerrainCellGrid grid)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));

        foreach (PlantInstanceFile file in scene.InstanceFiles)
        {
            if (!TryParseCell(file.FileName, out TerrainCell cell))
            {
                scene.Messages.Add(
                    $"warning: .trins file '{file.LogicalPath}' has no cell suffix; "
                    + "it is unavailable through cell selection.");
                continue;
            }

            var key = (file.ModelIndex, cell.X, cell.Z);
            if (!_files.TryAdd(key, file))
            {
                scene.Messages.Add(
                    $"warning: duplicate .trins cell ({cell.X}, {cell.Z}) for model "
                    + $"{file.ModelIndex}: '{_files[key].LogicalPath}' and '{file.LogicalPath}'.");
            }
        }
    }

    public TerrainCellGrid Grid { get; }

    public bool CanCreateSynthetic(int modelIndex) =>
        (uint)modelIndex < (uint)_scene.Plants.Count
        && !string.IsNullOrWhiteSpace(_scene.TerrainCellPrefix)
        && !string.IsNullOrWhiteSpace(_scene.TerrainLogicalFolder);

    public bool TryGetFile(int modelIndex, TerrainCell cell, out PlantInstanceFile file) =>
        _files.TryGetValue((modelIndex, cell.X, cell.Z), out file!);

    public bool IsRegistered(PlantInstanceFile file) => _scene.InstanceFiles.Contains(file);

    /// <summary>
    /// Returns an existing file or a detached synthetic file for a missing cell.
    /// Synthetic files are registered only when an instance is committed.
    /// </summary>
    public PlantInstanceFile? GetOrCreatePending(int modelIndex, TerrainCell cell)
    {
        ValidateCell(cell);
        if (TryGetFile(modelIndex, cell, out PlantInstanceFile existing))
        {
            return existing;
        }

        if (!CanCreateSynthetic(modelIndex))
        {
            return null;
        }

        var key = (modelIndex, cell.X, cell.Z);
        if (_pending.TryGetValue(key, out PlantInstanceFile? pending))
        {
            return pending;
        }

        string modelStem = Path.GetFileNameWithoutExtension(_scene.Plants[modelIndex].Name);
        if (string.IsNullOrWhiteSpace(modelStem))
        {
            return null;
        }

        string folder = _scene.TerrainLogicalFolder.EndsWith('/')
            ? _scene.TerrainLogicalFolder
            : _scene.TerrainLogicalFolder + "/";
        string fileName = $"{_scene.TerrainCellPrefix}_{modelStem}_X{cell.X}Z{cell.Z}_d0.trins";
        var file = new PlantInstanceFile
        {
            LogicalPath = folder + fileName,
            ModelIndex = modelIndex,
            OriginalCount = 0,
            IsSynthetic = true,
        };
        _pending[key] = file;
        return file;
    }

    /// <summary>Registers a synthetic file after its first instance is committed.</summary>
    public void Register(PlantInstanceFile file, TerrainCell cell)
    {
        ArgumentNullException.ThrowIfNull(file);
        ValidateCell(cell);

        var key = (file.ModelIndex, cell.X, cell.Z);
        if (_files.TryGetValue(key, out PlantInstanceFile? existing)
            && !ReferenceEquals(existing, file))
        {
            throw new InvalidOperationException(
                $"Cell ({cell.X}, {cell.Z}) for model {file.ModelIndex} is already mapped to "
                + $"'{existing.LogicalPath}'.");
        }

        if (!_scene.InstanceFiles.Contains(file))
        {
            _scene.InstanceFiles.Add(file);
        }

        _files[key] = file;
        _pending.Remove(key);
    }

    public static bool TryParseCell(string fileName, out TerrainCell cell)
    {
        cell = default;
        Match match = CellPattern.Match(fileName);
        if (!match.Success
            || !int.TryParse(match.Groups["x"].Value, out int x)
            || !int.TryParse(match.Groups["z"].Value, out int z)
            || x >= TerrainCellGrid.CellCount
            || z >= TerrainCellGrid.CellCount)
        {
            return false;
        }

        cell = new TerrainCell(x, z);
        return true;
    }

    private static void ValidateCell(TerrainCell cell)
    {
        if ((uint)cell.X >= TerrainCellGrid.CellCount
            || (uint)cell.Z >= TerrainCellGrid.CellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cell),
                cell,
                $"Terrain cell must be inside 0..{TerrainCellGrid.CellCount - 1} on both axes.");
        }
    }
}
