# Terrain (heightmap and material)

Status: heightmap format, height scale, world mapping, and the manifest world X
origin verified against areas 00-06, 11, and `ha_area00_s02`. Splat-layer
weights and procedural detail remain unresolved.

## Where it lives

`RomFS/bin/archive/field/terrain/ha_areaNN.gfpak`. Entries are unnamed; tools
resolve them by FNV-1a 64 path hashes and by structural material detection.

The terrain material is a FlatBuffer `.trmtr` (3,192 E,556 bytes) whose material
is named `TerrainMaterial`. It references 17 textures:

| Name | Role | Format |
| --- | --- | --- |
| `HeightMap` | base height field | R16/R32 FLOAT, 1025² or 2049² |
| `HeightNormalMap` | terrain normal | R10G10B10A2 UNORM, 1025² |
| `SplatAlpha` | 2-slice weight control | BC7 UNORM, 1024² × 2 slices |
| `SplatColor0..6` | layer albedo | BC7 SRGB, 256² with 9 mips |
| `SplatNormal0..6` | layer normal | BC5 UNORM, 256² |

`TerrainMaterialSet.FindInArchive` locates the material and resolves each
referenced file by hashing `bin/field/terrain/ha_areaNN/terrain/<file>`.

## Height field

- Stored as BNTX tile mode 0, one mip, `R16_FLOAT` (0x0A05) or `R32_FLOAT`
  (0x1405).
- Samples are normalized to 0..1. World height is `sample * heightScale`, where
  the scale comes from terrain manifest field 1, third float. `TerrainSurface`
  caches the scaled range as `MinHeight`/`MaxHeight` for bounds and ray tests.

Verified scales:

| Pack | Size | Scale | Observed world max |
| --- | --- | ---: | ---: |
| `ha_area00` | 1025² | 185 | 158.0 |
| `ha_area01` | 1025² (R32) | 121.756 | 121.6 |
| `ha_area02` | 2049² | 200.24 | 200.1 |
| `ha_area03` | 2049² | 127.079 | 126.9 |
| `ha_area04` | 1025² | 302 | 302 |
| `ha_area05` | 2049² | 168 | 168 |
| `ha_area06` | 2049² | 36.67 | 36.6 |
| `ha_area11` | 2049² | 238 | 0 (flat) |
| `ha_area00_s02` | 513² | 121.756 | n/a (sub-pack) |

`ha_area11` decodes to all zeros; its terrain is effectively flat or unused.

## World mapping (verified)

- Areas 00-06 use a 1025×1025 manifest grid over a 1024×1024 world extent.
  World units per heightmap sample are `(grid - 1) / (heightmap size - 1)`:
  - 1025² heightmap: 1 unit/sample, extent 1024×1024.
  - 2049² heightmap: 0.5 units/sample, same 1024×1024 extent.
- `ha_area11` uses a 2048×2048 manifest grid and a 2047×2047 extent with a
  2049² heightmap, so its spacing is 2047/2048 = 0.99951171875 units/sample.
  Its vegetation cells are still 128 world units across.
- `ha_area00_s02` uses a 512.5×512.5 grid, 513² heightmap, and 511.5×511.5
  extent at 0.999 units/sample.
- Manifest field 10 is a two-float table `[WorldMinX, WorldMinX + GridWidth]`.
  All sampled packs use `WorldMinX = 0` except `ha_area00_s02` (`200`). World Z
  origin is 0 in every sample.
- Column index maps to world X = `WorldMinX + column × units/sample`, row index
  to world Z = `row × units/sample`.
- `Y = normalizedHeight * scale`, no vertical offset. `TerrainSurface` exposes
  `WorldOriginX` and `WorldMaxX`; height sampling, the terrain mesh, and terrain
  ray bounds apply the origin.
- Vegetation cell mapping and the 16×16 grid are documented in `placement.md`.

Evidence:

```text
terrain area00 sample (445, 650) = 60.793
first tree instance y at (445.1664, 650.67) = 60.784

terrain area04 sample (184, 472) = 154.843
area04 NPC node y at (184.913, 472.91) = 154.804

area02 keystone world (640.058, 121.931, 138.576)
  -> sample (1280, 277) = 121.925
area02 tent world (641.457, 45.491, 731.721)
  -> sample (1283, 1463) = 45.492
area02 cave world (746.033, 92, 298.152)
  -> sample (1492, 596) = 92.004
```

The area-05 temple statues sit at `y=59` while the surrounding terrain sample is
`109.594`; the temple is an interior/underground space, so this is not evidence
against the mapping.

The 2049² heightmaps are higher-resolution samples of the same 1024×1024 extent,
not 2×2 cells at one unit per sample.

Area06's `terrain_1_1` pack is a single cell: its heightmap covers the full
1024×1024 extent but is zero outside the playable patch, so the viewer draws a
flat surrounding plane. The area06 NPCs at world (502, 516) align with sample
(1004, 1033) = 10.475. Cell naming is resolved in `placement.md`; in-game
streaming and hole masking remain unresolved.

## Splat layers

`SplatAlpha` is a **2-slice array texture** (BC7 UNORM, 1024² per slice), matching
the shader value `ControlTextureNum = Two`. Each slice holds four 8-bit weights:

| Slice | R | G | B | A |
| --- | --- | --- | --- | --- |
| 0 | SplatColor0 (cliff) | SplatColor1 (grass01) | SplatColor2 (grass02) | SplatColor3 (sand01) |
| 1 | SplatColor4 (sand03) | SplatColor5 (gravel02) | SplatColor6 (gravel01) | unused |

Weights are normalized after sampling and blended across the seven albedo
textures. Per-layer UV tiling is `SplatST0..6.X x128`: the X components are
4.0, 2.5, 2.5, 3.0, 3.0, 2.0, 2.5 for area 00, so the effective repeats over the
map are 320–512. The x128 factor is the viewer default
(`--terrain-tile-scale`, live Project-panel slider); without it the textures
appear far too large.

Observed channel statistics for area 00 (`bntx stats`): slice 0 has
mean R/G/B/A = 126/101/16/0.2, slice 1 has 6.2/2.6/2.4/0. That matches a
rock/grass/sand mix with sparse gravel layers.

Entries 2, 3, 6, 7, and 8 in `ha_area00` (3.4 MB, 12.6 MB, and three 16 MB
payloads) are not albedo textures:

- entry 3 is a terrain descriptor with `1025 ÁE185 ÁE1025`, seven named layers,
  and quad/vertex counts;
- entries 6 E are 16 MB flat buffers (`2048 ÁE2048 ÁE4` bytes per texel) that are
  mostly zero with sparse 255 values. They are masks, not splat weights; their
  role (holes, water, collision?) is unresolved.

## Mesh building

`TerrainMeshBuilder.Build(surface, stride)` produces a regular grid:

- position `(WorldOriginX + x × unitsPerSampleX, height, y × unitsPerSampleZ)`,
  up-facing triangle winding;
- normals from central differences of scaled heights over world distance;
- UV `(x / (width - 1), y / (height - 1))` in file row order;
- `stride` samples every Nth row/column for previews.

## CLI

```text
terrain manifest <gfpak>                 plant/detail groups and material
terrain heightmap <gfpak> [--sample X Y] extent, dimensions, scale, range, sample
terrain mesh <gfpak> [--stride N]        vertex/index counts and bounds
```

`--sample` takes heightmap sample indices; use `extent` and `units/sample` to map
world coordinates to samples.

## Open questions

- Splat normal maps and the hole map (`EnableHoleMap`) are not sampled yet.
- Formal `SplatST` units (x128 is the current default) and V orientation.
- sRGB handling for terrain albedo uploads.
- Terrain descriptor entry 3: layer tables, quad counts, and remaining fields.
- In-game cell streaming: no per-cell flags found; the manifest lists all
  `.trins`. World Z origin is 0 in every sample.
- Detail groups: procedural grass scatter rules and the 17�E8 trailing
  parameters.
- Entry 2 (`TAG0` / `SDKV` container): role still unidentified.
