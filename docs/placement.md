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
| 10 | float[] | eight floats, e.g. `0,0,0,0,1025,185,1025,0` |

Field 1's third float and field 10's sixth value agree and are the terrain
height scale (185, 121.76, 200.24, 127.08, 302, 168, 36.67, 238 for areas
00–06 and 11); see `terrain.md`. Fields 0–2, 9, and 10 are not yet used by the
viewer.

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
bytes.

File naming:

```text
area00_0_0_plants_tree02_X7Z9_d0.trins      (areas 00–05)
terrain_1_1_plants_tree01_X8Z8_d0.trins     (areas 06 and 11)
       ^ ^             ^  ^  ^
       | |             |  |  LOD/detail marker (d0; hypothesis)
       | |             |  cell Z index
       | |             cell X index
       | terrain cell block (0_0 or 1_1 observed)
       area id (only in the areaNN form)
```

Two prefix forms are observed: `areaNN_0_0_...` for areas 00–05 and
`terrain_X_Z_...` for areas 06 and 11. Both carry the same model name and
X/Z cell indices; the prefix likely selects a terrain cell/block, but the
mapping to world/streaming coordinates is not yet resolved.

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
| **Total** | **6,358** |

Each count is one `SrtInstanceBuffer` entry per `.trins` path; the table sums the
instances inside those entries.

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
- The instance must belong to a terrain cell that the game loads. Cell mapping,
  streaming behavior, and collision (`tree_collision_info.bin` / collision
  packs) are unresolved; new instances may be visual-only until collision is
  addressed.
- Any edit requires writing the modified gfpak back into a RomFS overlay; the
  compression path is still an open question.

## Open questions

- Manifest file name: the entry is found structurally, but its gfpak path hash
  is not matched by any recovered name.
- Fields 0–2, 9, and 10: dimensions and the height-like value are consistent
  with a heightmap, but the individual field meanings are not confirmed.
- Prefix-to-cell mapping (`areaNN_0_0` vs. `terrain_X_Z`) and streaming rules.
- Whether appending an instance to any loaded cell is sufficient, or whether the
  cell/grid coordinates must match the world position.
- Collision generation/update for newly placed vegetation.
- Detail-group (field 6) semantics: are grass layers explicitly instanced?
- Coordinate handedness at render time against an external game reference; the
  row-vector convention is fixed by the upload path.
- `d0` LOD/detail semantics.
- Whether update-layer terrain packs replace whole packs or individual entries.
- Gfpak writing/compression for the RomFS overlay (Kraken encoder unresolved;
  test whether `None`/`Zlib` entries are accepted).
