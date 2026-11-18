// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Render.Ui;

public enum EditorSelectionKind
{
    None,
    RoomMesh,
    PlacedGroup,
    PlacedMesh,
    PlantModel,
    PlantInstance,
    Terrain,
}

/// <summary>
/// Selected scene object. <see cref="Index"/> is the room/placed mesh index,
/// placed-group index, plant model index, or global plant instance index.
/// </summary>
public readonly record struct EditorSelection(EditorSelectionKind Kind, int Index = -1)
{
    public static EditorSelection None { get; } = new(EditorSelectionKind.None);

    public bool IsNone => Kind == EditorSelectionKind.None;
}
