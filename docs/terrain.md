# Terrain (heightmap and material)

Status: heightmap format, height scale, and world mapping verified against areas
00, 01, 04, and 05. Splat-layer weights and procedural detail remain unresolved.

## Where it lives

`RomFS/bin/archive/field/terrain/ha_areaNN.gfpak`. Entries are unnamed; tools
resolve them by FNV-1a 64 path hashes and by structural material detection.

The terrain material is a FlatBuffer `.trmtr` (3,192–3,556 bytes) whose material
is named `TerrainMaterial`. It references 17 textures:

| Name | Role | Format |
| --- | --- | --- |
| `HeightMap` | base height field | R16/R32 FLOAT, 1025² or 2049² |
| `HeightNormalMap` | terrain normal | R10G10B10A2 UNORM, 1025² |
| `SplatAlpha` | layer weight mask | BC7 UNORM, 1024² |
| `SplatColor0..6` | layer albedo | BC7 SRGB, 256² with 9 mips |
| `SplatNormal0..6` | layer normal | BC5 UNORM, 256² |

`TerrainMaterialSet.FindInArchive` locates the material and resolves each
referenced file by hashing `bin/field/terrain/ha_areaNN/terrain/<file>`.

## Height field

- Stored as BNTX tile mode 0, one mip, `R16_FLOAT` (0x0A05) or `R32_FLOAT`
  (0x1405).
- Samples are normalized to 0..1. World height is `sample * heightScale`, where
  the scale comes from terrain manifest field 1, third float.

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

`ha_area11` decodes to all zeros; its terrain is effectively flat or unused.

## World mapping (verified)

- Column index maps to world X, row index to world Z, at one world unit per
  sample, with the grid origin at `(0, 0)`.
- `Y = normalizedHeight * scale`, no vertical offset.

Evidence:

```text
terrain area00 sample (445, 650) = 60.793
first tree instance y at (445.1664, 650.67) = 60.784

terrain area04 sample (184, 472) = 154.843
area04 NPC node y at (184.913, 472.91) = 154.804
```

The area-05 temple statues sit at `y=59` while the surrounding terrain sample is
`109.594`; the temple is an interior/underground space, so this is not evidence
against the mapping.

Heightmaps of 2049² cover multiple 1025² cells (for example 2×2 cells sharing
edges). The mapping stays one unit per sample.

## Splat layers

`SplatAlpha` is a 1024² BC7 texture, but the material declares seven layers,
so four BC7 channels cannot hold all weights. Entries 2, 3, 6, 7, and 8 in
`ha_area00` (3.4 MB, 12.6 MB, and three 16 MB payloads) are not textures:

- entry 3 is a terrain descriptor with `1025 × 185 × 1025`, seven named layers,
  and quad/vertex counts;
- entries 6–8 are 16 MB flat buffers with a small metadata trailer
  (`2048 × 2048 × 4` bytes per texel).

These are likely splat weight or height caches. Their exact layout is
unresolved.

## Mesh building

`TerrainMeshBuilder.Build(surface, stride)` produces a regular grid:

- position `(x, height, y)`, up-facing triangle winding;
- normals from central differences of scaled heights;
- UV `(x / (width - 1), y / (height - 1))` in file row order;
- `stride` samples every Nth row/column for previews.

## CLI

```text
terrain manifest <gfpak>                 plant/detail groups and material
terrain heightmap <gfpak> [--sample X Y] dimensions, scale, range, sample
terrain mesh <gfpak> [--stride N]        vertex/index counts and bounds
```

## Open questions

- Splat weight layout: how seven layers map to `SplatAlpha` and entries 6–8.
- Terrain descriptor entry 3: layer tables, quad counts, and remaining fields.
- World offset and streaming for multi-cell and `terrain_X_Z_*` packs.
- Splat texture orientation (V direction) and channel order at render time.
- Detail groups: procedural grass scatter rules and the 17–18 trailing
  parameters.
- Entry 2 (`TAG0` / `SDKV` container): role still unidentified.
