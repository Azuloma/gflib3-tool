# Field viewer (`Nxa.GFlib3.Render`)

Status: Stage 4b. The project loads one field area, renders the heightmap
terrain, one room model with BNTX base-color textures, scene-table placements
from `resident_release`, and vegetation instances from the terrain manifest. The
UI is a dockable Dear ImGui shell with English/Japanese labels. Editing controls
are visible but disabled.

## Project

`render/Nxa.GFlib3.Render` targets `net9.0` and references:

- `Silk.NET.Windowing`, `Silk.NET.Input`, `Silk.NET.OpenGL` 2.23.0.
- `Silk.NET.OpenGL.Extensions.ImGui` 2.23.0, which brings ImGui.NET 1.90.8.1.
- `Nxa.GFlib3.Core` for every format reader; the viewer does not parse formats.

Run:

```powershell
dotnet run --project render/Nxa.GFlib3.Render -- --dump <dump-root>
```

The dump root can also be set with `NXA_GF3_DUMP`. The native Kraken decoder is
resolved from `artifacts/native` by walking up from the executable, so build it
first with `pwsh scripts/build-native.ps1`.

Options:

| Option | Effect |
| --- | --- |
| `--area <name>` | Terrain pack name, default `ha_area00` |
| `--model <name>` | Model pack inside `archive/field/model/pack`, default `ha_area00_s02` |
| `--mesh <text>` | Load the first TRMDL whose mesh/material name contains `text` |
| `--lang <en\|ja>` | UI language |
| `--load-check` | Load the area, print counts and bounds, then exit |
| `--smoke-test` | Render 90 frames, then exit |
| `--frames <n>` | Render `n` frames, then exit |
| `--screenshot <path>` | Capture the whole window as a 24-bit BMP |
| `--screenshot-delay <s>` | Wait before the screenshot, then exit |
| `--terrain-stride <n>` | Sample every nth heightmap row/column (default 1) |
| `--terrain-tile-scale <n>` | Multiply `SplatST` terrain tiling (default 64) |
| `--no-plants`, `--no-room`, `--no-terrain`, `--no-scene`, `--no-textures` | Diagnostic rendering toggles |

## Loading

1. Open `RomFS/bin/archive/field/terrain/<area>.gfpak` and
   `RomFS/bin/archive/field/model/pack/<model>.gfpak`.
2. In the model pack, scan entries: `BNTX` magic becomes a cached texture,
   other entries are parsed as Trinity FlatBuffers and TRMDL files are grouped.
   Files are resolved by the FNV-1a path hash of
   `bin/field/model/pack/<name>`.
3. Load **one** TRMDL: the first model whose mesh/material name contains
   `--mesh`; otherwise the first model whose mesh path contains `room_`.
   A model pack can contain many unrelated models (area `s02` has a mountain,
   five event objects, a room, and an irori); merging them at the origin produced
   overlapping geometry and made textures appear wrong.
4. For each LOD group, keep only non-`_lodN` mesh paths. Plants list every LOD
   in one group (`entries=0,1,2`); using all three overlapped the low-detail
   billboards with LOD0.
5. Build geometry with `TrinityMeshBuilder` from the mesh's TRMBF buffer.
   A TRMSH's `BufferFilePath` resolves the exact `.trmbf` entry.
6. Resolve TRMTR materials by name, then base-color, normal, and AO maps by the
   material's texture slots. Base color is uploaded as RGBA8; normal/AO are
   loaded but not yet sampled by the shader.
7. Fragments whose base-color alpha is below 0.5 are discarded, which cuts out
   leaf cards and other masked surfaces.
8. Find the terrain manifest with `TerrainManifest.FindInArchive`, load each
   plant model from the terrain pack, and append all `.trins` matrices as
   `PlantInstance` records.
9. Build the terrain grid from the `TerrainMaterial` heightmap with
   `TerrainSurface`/`TerrainMeshBuilder` (`--terrain-stride` for previews).
   Decode `SplatAlpha` slices 0/1 as the four-plus-three layer weights and
   `SplatColor0..6` as albedo; tiling is `SplatST0..6 × 64` by default
   (`--terrain-tile-scale`, also a live Project-panel slider).
10. Scan `resident_release.gfpak` with `ResidentSceneFile.FindInArchive`, keep
    scenes whose name starts with `areaNN_` or `ha_areaNN_`, resolve the
    resource name against TRMDL mesh/material names in the selected model pack,
    and instantiate each node's SRT (`T * R_y(yaw) * S`). Resource matching
    trims trailing `_NN` instance numbers and the `bgparts_` prefix. NPC, event,
    effect, and visibility scenes are skipped.

The terrain pack's logical folder is
`bin/field/terrain/<area>/terrain/`; room packs use `bin/field/model/pack/`.
Folder hashes include the trailing slash.

## Rendering

- One shader: transform, vertex color, base-color texture, and a directional
  light plus ambient. Both System.Numerics matrices and instance matrices are
  uploaded for the column-vector convention.
- Terrain uses a second shader that samples the two `SplatAlpha` control slices,
  normalizes the seven layer weights, and blends `SplatColor0..6` with
  `SplatST × terrainTileScale` tiling (default 64). Scene-table placements share
  one GPU buffer per geometry and draw with their SRT matrix.
- Room meshes, placed meshes, terrain, and vegetation are drawn into an
  off-screen RGBA8 framebuffer with a 24-bit depth buffer; the Scene panel
  displays that texture.
- Vegetation uses a model-matrix draw per instance. Instanced attributes were
  initially planned, but produced incorrectly transformed geometry in the
  current GL path; per-instance model matrices are correct and fast enough for
  the observed counts (60 in area 00, 2,204 in area 02).
- The free-fly camera is Y-up: right mouse look, WASD, Q/E, Shift sprint, F
  frame. Default framing focuses the terrain bounds; F frames the room or
  vegetation selection.

## UI shell

The host window contains a menu bar, a disabled toolbar, and a dockspace.
Panels: Hierarchy, Scene, Inspector, Project, Console, Validation, History, and
Status. Windows receive default positions on first use; docking is enabled and
the layout persists in `imgui.ini` next to the executable.

Labels come from embedded `Localization/en.json` and `ja.json`. The menu switch
changes language at runtime. A CJK font is loaded from the Windows font folder
(`meiryo.ttc`, `msgothic.ttc`, or `YuGothM.ttc`) for Japanese glyphs.

## Known limitations

- Only the selected model pack is searched for scene models. Areas whose models
  live in another pack (for example an interior sub-pack) leave those scenes
  unplaced; there is no runtime area/model selector yet.
- Scene-name to model matching is heuristic: it strips `areaNN_`, `sNN_`,
  trailing `_NN`, and `bgparts_` prefixes. Background structures in
  `ha_area00_s02` and `ha_area04` resolve; other packs are unverified.
- Terrain albedo is splatted; normal maps, the hole map, detail grass,
  collisions, and event/NPC scenes are not rendered.
- The 16 MB buffers (entries 6–8) are sparse masks; their role is unresolved.
- Only one room TRMDL is loaded. Selecting a room sub-pack such as
  `ha_area00_s07` shows that interior at the origin.
- No skinning, animations, collision, or LOD switching.
- No editor operations; Stage 5 adds selection, transforms, and export.
- BNTX textures are decoded to CPU RGBA8 once; no streaming or texture cache
  eviction.
