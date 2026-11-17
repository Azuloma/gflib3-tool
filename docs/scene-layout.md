# Scene layout (resident data)

Status: **schema and transform application verified** for areas 00 and 04.
`resident_release.gfpak` places background models, event objects, NPCs, doors,
and effects in world space. Model files carry local coordinates only; the
viewer must apply the transforms below. Yaw sign/handedness still needs a
non-symmetric visual check.

## Where it lives

`RomFS/bin/archive/field/resident_release.gfpak` (2,066 entries, 1,858 folders,
20.5 MB decompressed). The pack has no file names, only path hashes. Entries are
identified by content:

| Entry | Size | Contents |
| --- | ---: | --- |
| `0081` | 261,584 | Model-path catalog: 658 entries with `bg_parts` and `pack` model paths |
| `0085` | 314,120 | Character/NPC model paths and positions |
| `0088` | 1,264 | Door/transition nodes (`DOOR_AREA00_TO_AREA00_S01_1`) |
| `0091` | 28,112 | Village building parts (`CommonWall_Housewall12`) with grid coordinates |
| `0157` | 2,024 | Mt. Tengen placement: `area00_s02_ground_mountain01_tengan` |
| `1531` | 29,288 | NPC placements (39 scenes, 11 nodes for `area04_npc_ouji`) |
| `1707` | 14,904 | Temple statue and gate placements (76 scenes) |

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

`1707` places three statue types on a square around the temple with positions
`(483.18, 59.0, 570.64)`, `(511.97, 59.0, 570.64)`,
`(511.97, 59.0, 548.03)`, and `(483.18, 59.0, 548.0)`, with yaws of 90, 180,
and -90 degrees. The consistent square and ground height support world-space
positions with a yaw-only rotation.

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
not match the visible name, so treat the hash as opaque. The viewer resolves a
scene to a TRMDL by stripping `areaNN_`, `sNN_`, trailing `_NN`, and `bgparts_`,
then matching the result against mesh paths and material names. This places
`ha_area00_s02` (mountain) and `ha_area04` (buildings, stairs) but is unverified
for other packs.

## Transform application

Use `world = T * R_y(yaw) * S` on local model vertices. Areas 00 and 04 render
correctly with this order (Mt. Tengen and the area-04 building cluster). The
yaw sign and handedness are the remaining unknowns; check them with the temple
statue square or a door frame.

## Consequence

Drawing a TRMDL at the origin is a viewer bug, not a geometry bug. The local
vertices and bounds are correct. Scene placement is implemented in
`ResidentSceneFile` and exposed through `scene info`.

## Open questions

- Scene field 5 in the 6-field variant.
- Node fields 3–6 (empty vectors in every observed node).
- Scene-to-model-pack resolution and resource-name matching.
- Visibility-group semantics (`FEVE_VISIBLE_*`) and streaming cells.
- Yaw sign and coordinate handedness for the final transform.
- Scene flag (field 3) semantics; distinction between model and event scenes.
