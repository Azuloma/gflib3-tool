# Scene layout (resident data)

Status: **hypothesis**. `resident_release.gfpak` contains scene/visibility tables
that place background models, event objects, NPCs, doors, and effects in world
space. Mesh files carry local coordinates only; the viewer must apply these
transforms.

## Where it lives

`RomFS/bin/archive/field/resident_release.gfpak` (2,066 entries, 1,858 folders,
20.5 MB decompressed). The pack has no file names, only path hashes. Relevant
entries found by content:

| Entry | Size | Contents |
| --- | ---: | --- |
| `0081` | 261,584 | Model-path catalog: 658 entries with `bg_parts` and `pack` model paths |
| `0157` | 2,024 | Visibility group `FEVE_VISIBLE_AREA00_S02_D110_BG`, includes `area00_s02_ground_mountain01_tengan` |
| `0085` | 314,120 | Character/NPC model paths and positions |
| `0088` | 1,264 | Door/transition nodes (`DOOR_AREA00_TO_AREA00_S01_1`) |
| `0091` | 28,112 | Village building parts (`CommonWall_Housewall12`) with positions |

## Mountain observation

`ha_area00_s02.gfpak` contains `ground_mountain01_tengan.trmdl`. Its decoded
vertex bounds and TRMDL bounds agree:

```text
min  -2429.042, 0.000, -2429.047
max   2429.044, 1458.282, 2446.646
field06 (bounding sphere?) 0.00122, 729.1412, 8.799683, radius 2544.8062
```

Those are **local model coordinates**, not world coordinates. The model is
supposed to be moved by the scene table. In `0157`, near the name
`area00_s02_ground_mountain01_tengan`, the bytes contain:

- `180.0` (likely Y rotation in degrees)
- `1817.5`, `-7.0`, `-3664.5` (likely translation)
- `3428.33`, `3268.14`, `3364.09` (likely bounds/extents)
- zero-filled space (likely scale or reserved fields)

The translation is far from the village coordinates (roughly x 100–800,
z 60–700), which agrees with Mt. Tengen being a distant background landmark.
The exact field order, the rotation convention, and whether scale is stored
elsewhere are **not yet verified**.

## Consequence

Drawing the TRMDL at the origin is a viewer bug, not a geometry bug. The local
vertices and bounds are correct. `resident_release` scene parsing is required to
place mountains, buildings, event objects, doors, and NPCs; this is tracked as
Stage 4b in `.agents/WORK_PLAN.md`.

## Open questions

- Exact schema of `resident_release` entries 0081/0157/0085/0088/0091.
- Whether the transform is SRT (180° Y, translation, unit scale) or a full 4×4.
- Visibility-group semantics (`FEVE_VISIBLE_*`) and streaming cells.
- Relationship between scene entries and the `sNN` model-pack names.
