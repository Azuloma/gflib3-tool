# Editing

Stage 5 edits vegetation placement (`.trins` instances) non-destructively. The
dump and its archives are never written; edits live in the loaded `FieldScene`,
in a session undo history, and in an optional `.gfproj` project of diffs.

## Scope

Step 1 covers viewport selection and gizmo transforms for existing vegetation
instances. Step 2 adds placement of existing vegetation models through the Place
tool and duplication of the selected instance. Both append a matrix to an
existing `.trins` file through `PlantEditSession.AddInstance`; undo and redo
remain session-local.

Step 4 adds project persistence: saving and loading per-area diffs, synthetic
`.trins` identities, dirty tracking, and reload reapplication. Step 3 added the
16×16 terrain cells and synthetic in-memory files. Multi-select, collision
updates, and RomFS export belong to later steps. Transform fields in the
Inspector stay read-only; the gizmo and the two add actions are the only edits.

## Placement target

Placement writes into a `.trins` file for the hit terrain cell:

- The target model defaults to the selected instance's model or the selected
  plant model.
- With `Follow terrain cell` enabled (default), the target file is chosen from
  the cell under the terrain hit: `cellX = (worldX - WorldMinX) / cellSize`,
  `cellZ = (GridHeight - worldZ) / cellSize`, using the 16×16 grid and the
  manifest X origin. The Inspector shows the target cell and `.trins` name.
- If the cell has no `.trins` for that model, the editor synthesizes
  `<material-stem>_<model-stem>_X<x>Z<z>_d0.trins` in memory and registers it
  when the first instance is placed. Export must write the file and add it to the
  terrain manifest.
- With `Follow terrain cell` disabled, the Inspector target selector chooses an
  existing `.trins` file, as in Step 2.
- A selected instance shows its source `.trins` logical path and its local
  index inside that file.

New instances use yaw 0 and scale 1. The appended matrix is committed by
`PlantEditSession`, so the regular add-command history applies. The terrain
world X origin (200 for `ha_area00_s02`) is applied to terrain height sampling,
the terrain mesh, and placement cell lookup.

## Selection

- Left click in the Scene panel casts a world ray through the mouse pixel.
- Picking is CPU-side: the ray is transformed into each instance's model space
  and tested against the model's local AABB. The nearest hit wins.
- Empty clicks clear the selection in Select/Move/Rotate/Scale modes. In Place
  mode a click on the terrain appends an instance instead.
- Picking is AABB-accurate, not triangle- or alpha-accurate; dense canopies use
  their bounding box.
- `--select-instance <n>` selects an instance at startup for screenshot
  verification; `--tool <select|move|rotate|scale|place>` sets the startup tool
  and `--frame-selection` frames the startup selection. The startup instance is
  selected before the tool is applied, so `--tool place` can start with a target.

## Gizmo

| Tool | Key | Handles |
| --- | --- | --- |
| Select | `1` | none; only picking |
| Move | `2` | X, Y, Z axis arrows |
| Rotate | `3` | Y ring |
| Scale | `4` | shared XZ handle, independent Y handle |
| Place | `5` | none; terrain preview and click placement |

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

## Place tool

The Place tool (`5`, toolbar crosshair) requires a selected plant model or
instance. While the cursor is over the Scene panel and the ray hits the terrain,
the viewer:

1. Picks the first terrain surface point with `ScenePicking.TryPickTerrain`.
2. Resolves the 16×16 cell and the target `.trins` (existing or synthetic).
3. Builds a yaw-0, unit-scale matrix with `PlantPlacement.AtTerrainPoint`.
4. Draws the chosen model through `SceneRenderer.Preview` without adding it to
   the scene.
5. Draws the prospective world AABB in the viewport.

Left click registers a synthetic file if needed, appends the matrix through
`PlantEditSession.AddInstance`, selects the new instance, and keeps Place active
for repeated placement. Clicks that miss the terrain do nothing. The Place
button stays disabled without a vegetation selection; pressing `5` without one
appends a warning to the Console.

The preview is submitted before the next scene render, so it follows the cursor
one frame behind the ImGui overlay. The model drawn by the preview is the
placement model chosen in the Inspector.

## Duplicate

Duplicate (`Ctrl+D`, toolbar, Edit menu) copies the selected instance into its
own source `.trins`:

- The X and Z translation both increase by 2.0 world units.
- Y snaps to the loaded terrain height at the new X/Z when the position lies
  inside the heightmap; otherwise the source Y is kept.
- Yaw and scale are preserved.
- The new instance is selected, shares the source file, and receives the local
  index after any previously added instances.

Duplicate is disabled when the selection is not a vegetation instance or has no
source file. `--duplicate-instance <n>` performs the same action for instance
`n` after the first frame (after a `--reload-area` load when both are set), which
makes refresh and screenshot checks reproducible.

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

- History entries are either transform commands or add commands.
- `SetMatrix` applies a live drag frame without recording history.
- `Commit` records one transform when the drag ends; identical matrices are
  ignored, and a new commit clears the redo tail.
- `AddInstance` appends a matrix to a registered `.trins` source (existing or
  synthetic), records one add, and bumps the scene instance revision. Adds are
  append-only; undo removes the last appended instance and redo inserts it again.
- `Undo`/`Redo` restore transform before/after matrices or remove/reinsert an
  added instance.
- History is linear, capped at 512 entries. Undo/redo shortcuts are `Ctrl+Z`
  and `Ctrl+Y` (also `Ctrl+Shift+Z` for redo), disabled while a text field is
  focused or a drag is active.
- The History tab lists applied entries normally and redoable entries dimmed.
- Adding, undoing, or redoing an add rebuilds the Hierarchy instance index. When
  undoing removes the selected added instance, the selection falls back to its
  plant model and keeps the current placement target, so Place stays usable.
- Reloading an area creates a new session. Unsaved project changes are reported
  and discarded; a saved project for the same area is reopened automatically.
  Changing area or model pack closes the active project.

## Project persistence

`ProjectScene` captures only matrices that differ from `OriginalMatrix`, the
append order, and synthetic file cells; `ProjectSession` owns the active path,
the normalized JSON baseline, and dirty state. See `project.md` for the file
schema, conflict rules, and reload behavior. `Ctrl+S` saves, `Ctrl+O` opens, and
the File menu exposes save-as and close. The default save path is
`analysis/projects/<area>.gfproj`, which is gitignored.

## Limitations

- Edits are memory-only until saved to a `.gfproj`; export to a RomFS overlay is
  still Step 5 work.
- Missing cells are synthesized in memory and stored in the project. Export must
  write the new `.trins` and add its path to the terrain manifest.
- New instances are visual-only until collision generation is understood.
- In-game cell streaming behavior is still unknown.
- No transform snapping, pivot modes, or numeric Inspector input.
- Multi-select and box-select are not implemented.
- Animation, collision, and streaming data are not updated.
- A reload discards unsaved edits; there is no modal save prompt yet.
- A project cannot be merged into a scene that already has appended instances;
  reload the area first.
- The placement preview appears one frame behind the cursor because the scene
  render runs before ImGui input for the current frame.
