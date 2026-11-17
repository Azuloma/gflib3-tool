# Field object placement (`.trins` / `SrtInstanceBuffer`)

Status: verified against the base terrain packs for areas 00–06 and 11. The
placement data is **vegetation instancing**, not a general scene graph: trees,
shrubs, flowers, and grass clumps are listed as SRT instances and referenced by
terrain manifests. Static map props are baked into room models.

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
instance files. The manifest's own path has not been resolved yet; in
`ha_area00.gfpak` it is entry 5.

Observed root fields:

| Field | Type | Contents |
| --- | --- | --- |
| 0 | table | four floats (0.5, 0.5, 0.5, 0.7 in area 00) |
| 1 | table | five ints (1025, 1025, 185, 8, 2048 in area 00) |
| 2 | table | three ints (1024, 1024, 1025 in area 00) |
| 3 | u32 | plant group count |
| 4 | table[] | plant groups |
| 5 | u32 | detail group count |
| 6 | table[] | detail groups |
| 7 | table | terrain material path (`area00_0_0.trmtr`) |
| 8 | string[] | detail-map paths (`area00_0_0_detailmap_N.trtbf`) |
| 9, 10 | — | unlabelled |

Plant group (field 4):

| Field | Type | Contents |
| --- | --- | --- |
| 0 | string | model path, e.g. `plants_tree02.trmdl` |
| 1 | string[] | `.trins` paths for this model |

Example (`ha_area00`): 3 plant groups and 25 `.trins` files.
`plants_tree02.trmdl` → 6 `.trins`; `plants_tree01.trmdl` → 11;
`plants_area02_tree01.trmdl` → 8.

Detail group (field 6) tables carry a model path and many float parameters
(scale ranges, wind, color, etc.). No `.trins` vector was observed in these
groups. **Hypothesis:** grass/detail layers are placed or scattered by the
terrain material/shader rather than by explicit instances; not yet verified.

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

The exact multiplication convention (row-vector vs. column-vector, handedness)
is still a hypothesis until the renderer matches the game.

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
  packs contain no `.trins` or `SrtInstanceBuffer` strings.
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

- Prefix-to-cell mapping (`areaNN_0_0` vs. `terrain_X_Z`) and streaming rules.
- Whether appending an instance to any loaded cell is sufficient, or whether the
  cell/grid coordinates must match the world position.
- Collision generation/update for newly placed vegetation.
- Exact manifest path/name and the meaning of fields 0–2, 9, 10.
- Detail-group (field 6) semantics: are grass layers explicitly instanced?
- Matrix multiplication convention and coordinate handedness at render time.
- `d0` LOD/detail semantics.
- Whether update-layer terrain packs replace whole packs or individual entries.
- Gfpak writing/compression for the RomFS overlay (Kraken encoder unresolved;
  test whether `None`/`Zlib` entries are accepted).
