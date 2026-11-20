# Editor project format (`.gfproj`)

Status: Stage 5 Step 4. A project stores non-destructive vegetation edits for
one field area. The dump is never modified; the file records only differences
from the loaded `.trins` sources.

## Scope

- One project belongs to one area and model pack. Loading it onto a different
  area or model pack is rejected.
- Instances must be backed by a `.trins` file. Projects do not store terrain,
  room geometry, scene-table placements, or new plant models.
- Undo history is session-local and is not saved. Loading a project resets the
  edit baseline, so loaded changes cannot be undone into the dump state.
- Synthetic `.trins` files live in the project until export (Step 5) writes them
  and adds them to the terrain manifest.

## File layout

JSON, UTF-8 without BOM, LF, two-space indentation. Unknown members are ignored
when reading; missing or invalid required members are rejected. Matrices are 16
floats in row-major order.

```json
{
  "format": "GFlib3.EditorProject",
  "version": 1,
  "area": "ha_area00",
  "modelPack": "ha_area00",
  "terrain": {
    "cellPrefix": "area00_0_0",
    "logicalFolder": "bin/field/terrain/ha_area00/terrain/",
    "worldOriginX": 0.0,
    "worldExtentX": 1024.0,
    "worldExtentZ": 1024.0
  },
  "files": [
    {
      "path": "bin/field/terrain/ha_area00/terrain/area00_0_0_plants_tree02_X7Z9_d0.trins",
      "model": "plants_tree02.trmdl",
      "sourceCount": 3,
      "synthetic": false,
      "modified": [
        {
          "index": 1,
          "original": [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 10, 2, 20, 1],
          "matrix": [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 12, 2, 20, 1]
        }
      ],
      "added": [
        { "matrix": [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 14, 2, 20, 1] }
      ]
    },
    {
      "path": "bin/field/terrain/ha_area00/terrain/area00_0_0_plants_tree01_X1Z2_d0.trins",
      "model": "plants_tree01.trmdl",
      "sourceCount": 0,
      "synthetic": true,
      "cell": [1, 2],
      "added": [
        { "matrix": [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 5, 60, 5, 1] }
      ]
    }
  ]
}
```

### Root fields

| Field | Type | Contents |
| --- | --- | --- |
| `format` | string | `GFlib3.EditorProject` |
| `version` | int | `1` |
| `area` | string | terrain pack name, e.g. `ha_area00` |
| `modelPack` | string | model pack name used when the project was saved |
| `terrain` | object? | terrain identity for synthetic cell validation |
| `files` | array | edited `.trins` files, sorted by `path` |

Exactly one root object represents one area. Files with no changes are omitted.
File entries are sorted by `path`; `modified` entries are sorted by `index`;
`added` entries keep their append order.

### File fields

| Field | Type | Contents |
| --- | --- | --- |
| `path` | string | RomFS-relative logical `.trins` path with forward slashes |
| `model` | string | plant model path, e.g. `plants_tree02.trmdl` |
| `sourceCount` | int | original instance count expected in the dump file |
| `synthetic` | bool | true when the file exists only in the project |
| `cell` | int[2]? | `[x, z]` terrain cell for a synthetic file only |
| `modified` | array? | edits to existing instances |
| `added` | array? | appended instances, in append order |

A synthetic entry has `sourceCount` 0 and no `modified` list. Its additions are
written as a new `.trins` at `path` during export.

### Modified and added entries

| Field | Type | Contents |
| --- | --- | --- |
| `index` | int | local index inside the source `.trins`, 0-based |
| `original` | float[16] | source matrix at project save, for conflict detection |
| `matrix` | float[16] | current matrix |
| `added.matrix` | float[16] | current matrix of the appended instance |

For `added`, the local index is implicit: `sourceCount + position`. The writer
requires the scene's added indices to be contiguous. `index`, `original`, and
`matrix` are required; an omitted matrix is a load error, never a zero transform.
Null entries in `files`, `modified`, or `added` are rejected before sorting.

## Identity and conflict rules

A source instance is identified by `(path, index)`. The project stores the
original matrix so a changed dump is detected instead of moving the wrong
instance.

On load:

- Area and model pack must match the loaded scene; otherwise the whole project
  is rejected.
- A missing source `.trins`, missing plant model, or mismatched model index
  skips that file with a warning.
- `sourceCount` mismatch skips the file's additions; existing-instance edits are
  still checked one by one.
- A modified entry whose current source matrix differs from `original` is
  skipped with a warning.
- A synthetic entry is reconstructed through `PlantCellCatalog` from `cell` and
  `model`. A terrain identity mismatch, missing terrain catalog, or a cell that
  now maps to another path skips it.
- Additions require an empty `AddedCount` in the target file. The editor refuses
  to merge into a scene that already has appended instances.
- Null records, missing required members, non-finite matrices, duplicate paths
  or indices, invalid cells, and instance counts above `TrinsFile.MaxInstances`
  are rejected while reading.
- Warnings are written to the Console as `warning: project '<path>': ...` and
  appear in the Validation tab.

## Dirty state and save behavior

The active project keeps a normalized JSON snapshot as its baseline. Edits mark
the project dirty; commit, undo, redo, and drag-cancel re-capture the scene and
compare it with the baseline, so undoing back to the saved state clears the
dirty flag. The viewer shows `*` in the window title while changes are unsaved.

- Saving writes only differences, recreates the parent directory, and replaces
  the target through a same-directory temporary file.
- Opening a project is allowed only on a freshly loaded area with no active
  project or unsaved edits. Reload the area to discard edits or to switch
  projects.
- Reloading the same area reopens the saved project automatically and warns when
  unsaved changes were discarded. Changing area or model pack closes the active
  project with a warning.
- Closing a project keeps its edits in the scene but marks them unsaved relative
  to the dump.

## UI and CLI

The File menu exposes open, save, save-as, and close. The Project panel shows the
path, state, saved file/modified/added/synthetic counts, and a path field with
Save/Load buttons. There is no native file dialog.

- `--project <path>` opens a project at startup and supplies `--area`/`--model`
  defaults when they are not given explicitly.
- `--save-project <path>` saves the current project after startup actions
  (frame 3, or frame 4 when `--reload-area` is used).
- Default save path is `analysis/projects/<area>.gfproj` (gitignored).

## Limitations

- One area per project; no multi-area envelope.
- Only vegetation `.trins` instances are persisted.
- Project load cannot merge with existing additions; reload first.
- Export to a RomFS overlay, synthetic file materialization, and manifest path
  insertion remain Stage 5 Step 5 and later work.
- Collision and in-game streaming behavior for new instances remain unknown.
