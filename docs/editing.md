# Editing

Stage 5 edits vegetation placement (`.trins` instances) non-destructively. The
dump and its archives are never written; edits live in the loaded `FieldScene`
and in an undo history for the session.

## Step 1 scope

- Viewport selection of existing vegetation instances.
- Gizmo translate, rotate, and scale for the selected instance.
- Undo/redo within the session, cleared on area reload.
- Selection remains a single instance. Multi-select, adding/duplicating,
  terrain snapping, collision, save, and export belong to later steps.

The Inspector stays read-only; the gizmo is the only transform input.

## Selection

- Left click in the Scene panel casts a world ray through the mouse pixel.
- Picking is CPU-side: the ray is transformed into each instance's model space
  and tested against the model's local AABB. The nearest hit wins.
- Empty clicks clear the selection.
- Picking is AABB-accurate, not triangle- or alpha-accurate; dense canopies use
  their bounding box.
- `--select-instance <n>` selects an instance at startup for screenshot
  verification; `--tool <select|move|rotate|scale>` sets the startup tool and
  `--frame-selection` frames the startup selection.

## Gizmo

| Tool | Key | Handles |
| --- | --- | --- |
| Select | `1` | none; only picking |
| Move | `2` | X, Y, Z axis arrows |
| Rotate | `3` | Y ring |
| Scale | `4` | shared XZ handle, independent Y handle |

The gizmo is an ImGui overlay projected with the camera view-projection; it
adds no GL pipeline. The selected instance also shows its world AABB wireframe.

- Move projects the mouse ray onto the axis line and translates along that
  world axis.
- Rotate intersects the mouse ray with the world XZ plane through the pivot and
  rotates yaw around Y. Rotation is yaw-only because every observed `.trins`
  matrix uses a Y rotation.
- Scale uses the ratio of the current axis-line distance to the distance at drag
  start, so dragging outward enlarges the instance. The XZ handle changes the
  shared XZ scale; the Y handle changes the independent Y scale. Both keep the
  verified `.trins` 3×3 layout.
- Deltas below the named move/rotation/scale thresholds keep the raw start
  matrix, so press/release without motion records no edit.
- `Esc` during a drag restores the pre-drag matrix and records nothing.
- Toolbar buttons mirror the keys. Switching tools cancels an active drag.

## Transform convention

`SceneGeometry.Decompose` reads `.trins` rows as `atan2(Row2.X, Row0.X)` with
XZ scale from the row length and Y scale from `Row1.Y`. `SceneGeometry.Compose`
writes the same layout:

```text
row0 = (sxz*cos, 0, -sxz*sin, 0)
row1 = (0, sy, 0, 0)
row2 = (sxz*sin, 0, sxz*cos, 0)
row3 = (x, y, z, 1)
```

Translation stays in row 3. Upload remains row-vector (`v * M`), unchanged from
Stage 4.

## Undo and history

`PlantEditSession` owns the edit state:

- `SetMatrix` applies a live drag frame without recording history.
- `Commit` records one change when the drag ends; identical matrices are
  ignored, and a new commit clears the redo tail.
- `Undo`/`Redo` restore the stored before/after matrices.
- History is linear, capped at 512 entries. Undo/redo shortcuts are `Ctrl+Z`
  and `Ctrl+Y` (also `Ctrl+Shift+Z` for redo), disabled while a text field is
  focused or a drag is active.
- The History tab lists applied entries normally and redoable entries dimmed.
- Reloading an area creates a new session and reports how many instances still
  differ from their original matrices. Undone edits do not count as dirty.

## Limitations

- Edits are memory-only until the project format and RomFS export exist.
- No transform snapping, pivot modes, or numeric Inspector input.
- Multi-select and box-select are not implemented.
- Animation, collision, and streaming data are not updated.
- A reload always discards the session; there is no save prompt yet.
