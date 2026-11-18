# Field object placement (`.trins` / `SrtInstanceBuffer`)

Status: verified against the base terrain packs for areas 00–06 and 11. The
placement data is **vegetation instancing**, not a general scene graph: trees,
shrubs, flowers, and grass clumps are listed as SRT instances and referenced by
terrain manifests. Static map props use the resident scene tables described in
`docs/scene-layout.md`; they are not `.trins` instances.

Investigated example: `RomFS/bin/archive/field/terrain/ha_area00.gfpak`.

## Where placement lives

- Container: `RomFS/bin/archive/field/terrain/ha_areaNN.gfpak`.
- Overrides: `RomFS/bin/field/terrain/...` (for example
  `field/terrain/ha_area05/terrain_table.bin` in the v1.1.1 layer).
- Inside each terrain pack, placements are separate `.trins` entries, plus one
  terrain manifest entry that references them.
- Logical gfpak paths use `bin/` and forward slashes, e.g.:

```text
bin/field/terrain/ha_area00/terrain/area00_0_0_plants_tree02_X7Z9_d0.trins
bin/field/model/pack/room_area00_s07.trmdl
```

Path hashes are FNV-1a 64 with the Game Freak offset basis
`0xCBF29CE484222645` (see `docs/gfpak.md`).

## Terrain manifest

One FlatBuffer entry per terrain pack lists the plant model groups and their
instance files. The entry has **no resolved file name**: gfpak stores only path
hashes and no candidate name matched. It is located structurally and reliably:
its root has plant/detail group fields, every plant group model ends in
`.trmdl`, and every instance path ends in `.trins`. Entry indices vary by pack
(5, 7, 5, 7, 7, 5, 139, 81 for areas 00–06 and 11). Decompressed size ranges
from 1,664 to 35,284 bytes.

Its folder hash resolves to `bin/field/terrain/ha_areaNN/terrain/` (the folder
hash includes the trailing slash). `areaNN_0_0.trmtr` and the `.trins` files in
the same folder resolve with the usual file-path hashing. The manifest file name
remains unknown because it is not among the names recovered so far.

Observed root fields (verified in areas 00–06 and 11):

| Field | Type | Contents |
| --- | --- | --- |
| 0 | table | up to four floats; present fields are 0.5/0.5/0.5/0.7 or 0.5/0.5/0.7 |
| 1 | table | f32 ×3 then u32 ×2: `1025, 1025, height`; dimensions are 1025 or 2048 |
| 2 | table | three u32: `1024, 1024, heightmap size` (1025 or 2049) |
| 3 | u32 | plant group count |
| 4 | table[] | plant groups |
| 5 | u32 | detail group count |
| 6 | table[] | detail groups |
| 7 | table | one string: terrain material path (`area00_0_0.trmtr`) |
| 8 | string[] | detail-map paths (`area00_0_0_detailmap_N.trtbf`) |
| 9 | — | absent in all sampled packs |
| 10 | table | two floats: `[WorldMinX, WorldMinX + GridWidth]` |

Field 1's third float is the terrain height scale (185, 121.756, 200.24, 127.08,
302, 168, 36.67, 238 for areas 00–06 and 11); see `terrain.md`. Field 10's
first value is the world X origin: 0 for every sampled pack except
`ha_area00_s02` (200), whose second value is 712.5 (`200 + 512.5`). The previous
eight-float reading of field 10 was incorrect. Field 10's second value is used
as a sanity check, not as a separate Z origin; Z origin is 0 in all samples.
Fields 0, 1 fields 3/4, and field 9 remain unused.

Plant group (field 4):

| Field | Type | Contents |
| --- | --- | --- |
| 0 | string | model path, e.g. `plants_tree02.trmdl` |
| 1 | string[] | `.trins` paths for this model |

Verified group and instance-path totals:

| Pack | Plant groups | `.trins` paths | Detail groups | Detail maps |
| --- | ---: | ---: | ---: | ---: |
| `ha_area00` | 3 | 25 | 11 | 3 |
| `ha_area01` | 9 | 217 | 20 | 5 |
| `ha_area02` | 11 | 574 | 14 | 4 |
| `ha_area03` | 6 | 145 | 16 | 4 |
| `ha_area04` | 5 | 232 | 11 | 3 |
| `ha_area05` | 6 | 195 | 7 | 2 |
| `ha_area06` | 5 | 19 | 4 | 1 |
| `ha_area11` | 4 | 14 | 3 | 1 |

The per-pack instance counts above sum the `.trins` placements; the totals in
the later table count the matrices inside those files.

Detail group (field 6):

| Field | Type | Contents |
| --- | --- | --- |
| 0 | string | model path, e.g. `plants_area00_grass01_alb.trmdl` |
| 1–18 | float/u32 | scale ranges, wind, color, and other scatter parameters |

The sampled groups have 17–18 trailing parameters; index 8 and 9 are small u32s
(4/38), the rest are floats. **Hypothesis:** grass/detail layers are scattered
by the terrain material/shader using these parameters rather than by explicit
instances; still unverified.

Areas 06 and 11 use the `terrain_1_1` material and cell prefix instead of
`areaNN_0_0`, matching the `terrain_X_Z_*` `.trins` naming.


## `.trins` / TRINS

`.trins` files are FlatBuffers with this root:

| Field | Type | Contents |
| --- | --- | --- |
| 0 | string | type name, `SrtInstanceBuffer` in all observed files |
| 1 | u32 | version, `1` |
| 2 | u32 | instance count |
| 3 | u32 | reserved, `0` |
| 4 | table | buffer table |
| 4.0 | u8[] | raw instance data |

Data length is `count * 64` bytes. Each instance is 16 little-endian floats,
stored as a row-major 4×4 matrix. Across all 6,358 instances in the sampled
areas:

- elements 3, 7, 11 are `0`; element 15 is `1`;
- elements 12, 13, 14 are the translation;
- the 3×3 block is a Y-axis rotation with independent XZ and Y scale:
  `[ sxz*cos, 0, -sxz*sin ]`, `[ 0, sy, 0 ]`, `[ sxz*sin, 0, sxz*cos ]`.

Example (first area-00 tree instance):

```text
-0.38041  0  -0.97888  0
 0        1.06524  0    0
 0.97888  0  -0.38041  0
 445.1664 60.78404 650.67 1
```

The row-vector reading (`v' = v * M`) follows from the upload path: the stored
rows go to the `uModel` uniform untransposed, and since `Matrix4x4` is row-major
while GLSL reads the uniform as column-major, the shader's
`model * vec4(position, 1)` is equivalent to `v * M`. Translation was
spot-checked on the area-00 tree at `(445.1664, 60.78404, 650.67)`, whose Y
matches the heightmap sample and whose trunk base contacts the terrain in
close-up; this does not independently establish yaw.

## Writing TRINS

Observed `.trins` files share one fixed FlatBuffer layout. Rebuilding that
layout from parsed values reproduced all 275 sampled entries in `ha_area00`,
`ha_area01`, `ha_area06`, and `ha_area11` byte-for-byte:

| Offset | Contents |
| --- | --- |
| `0x00` | root table offset, `0x14` |
| `0x04` | two bytes of padding |
| `0x06` | root vtable: size 14, object 22, field offsets `4,8,12,0,16` |
| `0x14` | root table: soffset 14, field0 string offset, version, count, field4 buffer offset |
| `0x2A` | buffer vtable: size 6, object 8, field0 offset 4 |
| `0x30` | buffer table: soffset 6, field0 vector offset 4 |
| `0x38` | payload length, `count * 64` |
| `0x3C` | instance matrices |
| `0x3C + count * 64` | string length 17, `SrtInstanceBuffer`, zero padding to 4 bytes |

The writer appends one 64-byte matrix, increments `count`, and preserves the
version and type name. `reserved` is absent in every sampled file and must be
zero; the writer rejects other layouts. The root string offset is
`0x3C + count * 64`, and the file length is the string end rounded up to four
bytes. The viewer's Place tool and Duplicate action use this append path; see
`docs/editing.md` for the UI behavior.

## Terrain cells and file naming (verified)

```text
<material-stem>_<model-stem>_X<cellX>Z<cellZ>_d0.trins
```

- `material-stem` is `manifest.MaterialPath` without `.trmtr`:
  `area00_0_0`, `terrain_1_1`, or `terrain_area01_demo`.
- `model-stem` is the plant group model path without `.trmdl`.
- Only `d0` is observed; every (model, cell) pair has exactly one file.
- The terrain uses a fixed 16×16 cell grid:

```text
cellSizeX = GridWidth / 16 = (WorldExtentX + 1) / 16
cellSizeZ = GridHeight / 16 = (WorldExtentZ + 1) / 16

cellX = floor((worldX - WorldMinX) / cellSizeX)
cellZ = floor((GridHeight - worldZ) / cellSizeZ)   // clamp to 0..15
```

`WorldMinX` is manifest field 10's first float. Cell Z increases toward
decreasing world Z. The mapping matched all 1,526 manifest-referenced files and
all 6,550 instances (areas 00-06, 11, `ha_area00_s02`, `ha_area05_s02`).

`ha_area00_s02` is the only sampled pack with a nonzero X origin (200); the
viewer applies `WorldMinX` to the terrain mesh, height sampling, and terrain ray
bounds so vegetation and terrain stay aligned.

Place with `Follow terrain cell` enabled resolves the target from the hit cell.
When that cell has no `.trins`, the editor synthesizes
`<material-stem>_<model-stem>_X<x>Z<z>_d0.trins` in memory and registers it when
the first instance is committed. Materializing the file and adding its path to
the terrain manifest belong to the export step.

## Counts observed in the dump

| Terrain pack | `.trins` files / instances |
| --- | ---: |
| `ha_area00` | 60 |
| `ha_area01` | 1,430 |
| `ha_area02` | 2,204 |
| `ha_area03` | 789 |
| `ha_area04` | 901 |
| `ha_area05` | 828 |
| `ha_area06` | 121 |
| `ha_area11` | 25 |
| **Base total** | **6,358** |

The sub-packs with terrain add `ha_area00_s02` (91 files / 167 instances) and
`ha_area05_s02` (14 files / 25 instances), for 1,526 files and 6,550 instances
overall. Each count is one `SrtInstanceBuffer` entry per `.trins` path; the table
sums the instances inside those entries.

## What is *not* placement data

- `archive/field/model/pack/ha_areaNN*.gfpak` room models contain TRMDL/TRMSH/
  TRMBF/TRMTR/TRSKL and BNTX only.
- `resident_release.gfpak`, `resident_bg_parts_model.gfpak`, and the area room
  packs contain no `.trins` or `SrtInstanceBuffer` strings. `resident_release`
  instead carries scene placement tables and the model catalog.
- In `ha_area00_s07`, all 43 TRSKL node transforms are identity
  (scale 1, rotation 0, translation 0). Props are baked into room vertices, not
  instanced.

Consequence for the editor: the current scope (existing-object placement and
transform) can cover the vegetation instances. Moving baked static structures
would require editing model geometry and is out of scope for now.

## Editing implications

The format supports adding instances, not just moving existing ones:

- **Append to an existing `.trins`**: increment `count` and append one 64-byte
  matrix. The terrain manifest already lists that file, so no manifest change is
  needed. This is the simplest path for “place another tree/shrub/flower”.
- **Create a new `.trins`**: write a new entry and add its path to the matching
  plant group in the terrain manifest. This also requires updating the gfpak
  path-hash/file tables.
- Only **existing vegetation models** can be placed; authoring new plant models
  is out of scope.
- The cell naming and world mapping are verified; new instances can be assigned
  to the cell under the terrain hit. In-game streaming behavior and collision
  (`tree_collision_info.bin` / collision packs) are still unresolved; new
  instances may be visual-only until collision is addressed.
- Place synthesizes a missing cell file in memory. Export must write that file
  and add its path to the matching plant group; the gfpak/manifest writer is the
  remaining export work.
- Any edit requires writing the modified gfpak back into a RomFS overlay; the
  compression path is still an open question.

## Open questions

- Manifest file name: the entry is found structurally, but its gfpak path hash
  is not matched by any recovered name.
- Fields 0, 1 fields 3/4, 2, and 9: individual meanings outside the verified
  dimensions and height scale remain unresolved.
- In-game streaming rules: no per-cell flags were found; the manifest lists all
  `.trins`.
- Collision generation/update for newly placed vegetation.
- Whether a synthesized cell file needs any manifest metadata beyond its path.
- Detail-group (field 6) semantics: are grass layers explicitly instanced?
- Coordinate handedness at render time against an external game reference; the
  row-vector convention is fixed by the upload path.
- `d0` LOD/detail semantics.
- Whether update-layer terrain packs replace whole packs or individual entries.
- Gfpak writing/compression for the RomFS overlay (Kraken encoder unresolved;
  test whether `None`/`Zlib` entries are accepted).
