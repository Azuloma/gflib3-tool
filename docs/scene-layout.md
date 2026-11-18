# Scene layout (resident data)

Status: **schema and transform application verified** for areas 00 and 04.
`resident_release.gfpak` places background models, event objects, NPCs, doors,
and effects in world space. Model files carry local coordinates only; the
viewer must apply the transforms below. The renderer's row-vector convention
and sampled resident yaw are verified (see Transform application); handedness
against an external game reference remains open.

## Where it lives

`RomFS/bin/archive/field/resident_release.gfpak` (2,066 entries, 1,858 folders,
20.5 MB decompressed). The pack has no file names, only path hashes. Entries are
identified by content:

| Entry | Size | Contents |
| --- | ---: | --- |
| `0081` | 261,584 | Model-path catalog: 658 entries mapping hashes to `bg_parts` and `pack` TRMDL paths |
| `0085` | 314,120 | Character/NPC model paths and positions |
| `0088` | 1,264 | Door/transition nodes (`DOOR_AREA00_TO_AREA00_S01_1`) |
| `0091` | 28,112 | Village building parts (`CommonWall_Housewall12`) with grid coordinates; schema not implemented |
| `0095` | 241 scenes | Jubilife Village object placements; almost all scenes are unnamed |
| `0157` | 2,024 | Mt. Tengen placement: `area00_s02_ground_mountain01_tengan` |
| `1521` | 438 scenes | Area-04 buildings, stairs, tents, and object placements; mostly unnamed |
| `1531` | 29,288 | NPC placements (39 scenes, 11 nodes for `area04_npc_ouji`) |
| `1707` | 14,904 | Temple statue and gate placements (76 scenes) |

## Model catalog (entry 0081, verified)

Root field 0 is a vector of 658 entry tables. Each table has 11 fields:

| Field | Type | Meaning |
| --- | --- | --- |
| 0 | u64 | Model hash; matches the scene `ModelHash` field |
| 1 | string | TRMDL path under `bin/field/model/bg_parts/...` |
| 2 | string | Collision (`.trcol`) path for field 1 |
| 3–5 | strings | State/animation paths; empty for most entries |
| 6–7 | u64 | Zero in every observed entry |
| 8 | string | TRMDL path under `bin/field/model/pack/...` |
| 9–10 | strings | State and collision paths for field 8 |

Some tables have empty path fields. Verified hashes:

| Hash | Packed TRMDL |
| --- | --- |
| `0x96FB4DA5E97D8CAF` | `ground_mountain01_tengan.trmdl` |
| `0xCBAC0E355C979BA9` | `objects_area04_building01.trmdl` |
| `0xEF1AB1CE62726202` | `objects_area04_stairs02.trmdl` |
| `0x59C3F953E386A916` | `objects_camp01.trmdl` |
| `0x96631D44ECC80661` | `objects_area00_house01_a.trmdl` |

The hash is the authoritative scene-to-model mapping. Scene names are not
required: entries `0095` and `1521` place hundreds of objects with the name
`none`.

## Placement schema (verified)

A placement entry is a FlatBuffer whose root holds one scene vector.

Root:

| Field | Type | Meaning |
| --- | --- | --- |
| 0 | `[Scene]` | Scenes, 1–10,000 |

Scene table (5–12 fields, vtable size 14–28):

| Field | Type | Meaning |
| --- | --- | --- |
| 0 | string | Scene name, for example `area00_s02_ground_mountain01_tengan` |
| 1 | u64 | Scene hash (opaque) |
| 2 | u64 | Model hash (opaque; see below) |
| 3 | bool | Flag; present on background-model scenes, absent on event scenes |
| 4 | `[Node]` | Placement nodes |
| 5–11 | ? | Present only in larger variants (up to 12 fields); ignored for now |

Node table (10 fields, vtable size 24):

| Field | Type | Meaning |
| --- | --- | --- |
| 0 | u64 | Opaque hash |
| 1 | u64 | Opaque hash |
| 2 | string | Target name: visibility group (`FEVE_*`), event (`FSYS_*`, `WPHA_*`), or `sub_NNN` |
| 3–6 | vector | Empty in observed nodes; semantics unresolved |
| 7 | table | Position `{ float x, y, z }` |
| 8 | table | Y rotation; field 1 is degrees, absent means 0 |
| 9 | table | Scale `{ float x, y, z }` |

Structural detection over the whole pack found **156 placement entries** out of
2,066 (scene field count 5–12, node field count 10 with fields 7–9). Door tables
(`0088`) and poem/search-item tables (`1383`, 27-field scenes) do not match and
are rejected. Entry `1521` mixes 5- and 12-field scenes in one table.

## Verified samples

`0157`, scene 1 (Mt. Tengen):

```text
name    area00_s02_ground_mountain01_tengan
target  FEVE_VISIBLE_AREA00_S02_D110_BG
pos     1817.5, -7.0, -3664.5
yaw     180.0 degrees
scale   0.7, 0.7, 0.7
```

The mountain's local bounds are ±2429.0 in X/Z and 1458.3 in Y, so the scale is
not unity. Applying `T * R_y(180°) * S` places the centre near
`(1817.5, 503.2, -3660.1)` with extents around ±1700.

`1707` is the area-05 temple interior and contains two distinct statue groups:

- Aisle statues (`objects_area05_ktemple_statue01..03`) at `y=50`, in two
  columns at `x=488.2` and `x=512.3`, with `z` near 519.9, 527.8, and 543.8,
  and yaws of `+90` (west column) and `-90` (east column). Close-up renders
  show both columns facing the central walkway.
- Upper puzzle statues (`ground_area05_ktemple_statue01..03`) at `y=59`,
  scattered around the platform. Parsed samples include
  `(483.18, 59.0, 570.64)` yaw 90, `(511.97, 59.0, 570.64)` yaw 180,
  `(511.97, 59.0, 548.03)` yaw -90, and `(472.0, 59.0, 558.1)` yaw 180.

The same entry places two Unown gate panels with world width along the wall
plane: `gimmick_area05_ktemple_gate01` at `(508.0, 59.1, 515.5)` yaw 0 spans
world X, and the panel at `(499.7, 59.1, 547.9)` yaw 90 spans world Z; both sit
flush in wall openings.

`1531`, scene 0 (`area04_npc_ouji`): 11 nodes, first at
`(184.913, 154.804, 472.91)`, yaw `-58.3`, scale 1.

`1521` places area-04 buildings and stairs. `area04_bgparts_building01_a`
targets `FEVE_VISIBLE_AREA04_BUILDING01_BROKEN` at `(213.9, 298.1, 93.7)` with
zero yaw; the terrain sample at `(213, 93)` is `293.686`, so the building sits
on a small foundation. `area04_objects_area04_stairs02_01` uses
`FMAP_AREA04_STAIR` at `(213.92, 304.85, 26.24)`, yaw `1.126`.

`0088` uses a different schema: area pack name, door name, and `x`/`z` grid
coordinates as strings (`"40"`, `"100"`); it is not a placement entry.

## Model naming and hashes

Scene names combine an area or pack token with a resource name. In `0157`, the
model hash equals FNV-1a 64 of the suffix `ground_mountain01_tengan`. That is
not universal: statue and bonfire scenes share one hash per model family and do
not match the visible name, so treat the hash as opaque.

The viewer resolves `ModelHash` through entry `0081` and loads the packed TRMDL.
This covers unnamed scenes and shared models reused across area packs. For
scenes outside the catalog (NPC/event/effect data), the viewer skips the scene;
a name-based fallback is no longer used for static objects.

The viewer assigns ownership per **placement table**, because a table can mix
areas and because a model's path token names its origin, not its placement:

1. The table area is the highest-scoring scene-name `areaNN` / `areaNN_sNN`
   token; when names are absent, the catalog path token is used.
2. A sub-area token with an interior `room_*` model stays a sub-area. A
   sub-area token without a room is an outdoor feature pack and folds into its
   base area (`area00_s02` → `area00`).
3. A base area includes tables whose area is exactly that base area. For a mixed
   sub-area table that contains a room, only named scenes whose token matches the
   base area are included (for example the outdoor mountain in a room's table).
4. A sub-area load includes only tables whose area is exactly that sub-area.
5. Tables with no area token fall back per scene: a matching scene-name token,
   then node-Y/heightmap proximity (12 units) for generic models.

Height-matched nodes are placed individually only when they pass the terrain
check. Vista shells (`*_vista*`) are skipped because the viewer already draws
the heightmap terrain. Catalog paths remain resource identities; they do not
decide placement area.

## Transform application

Use `world = T * R_y(yaw) * S` on local model vertices; as row-vector math this
is `v' = v * S * R_y * T`. Mt. Tengen, the area-04 temple and its staircase,
and the area-05 temple interior render correctly with this order.

The yaw sign is confirmed by the area-05 temple interior. The aisle statues
(`objects_area05_ktemple_statue*` at `y=50`) stand at `x=488.2` and `x=512.3`
with yaws `+90` and `-90`; both columns face the central walkway, and the two
Unown gate panels sit flush in their wall openings. A flipped sign would rotate
every statue away from the aisle. The `.trins` vegetation matrices use the same
row-major convention, but vegetation yaw was not independently verified because
the sampled trees offered no reliable reference orientation.

## Consequence

Drawing a TRMDL at the origin is a viewer bug, not a geometry bug. The local
vertices and bounds are correct. Scene placement is implemented in
`ResidentSceneFile` and exposed through `scene info`.

## Open questions

- Scene field 5 in the 6-field variant.
- Node fields 3–6 (empty vectors in every observed node).
- Entry `0091` building-layout schema and whether it adds objects not covered by `0095`.
- Visibility-group semantics (`FEVE_VISIBLE_*`) and streaming cells.
- Coordinate handedness against an external game reference; the renderer
  convention and sampled resident yaw are verified.
- Scene flag (field 3) semantics; distinction between model and event scenes.
