# Field viewer (`Nxa.GFlib3.Render`)

Status: Stage 4 shell. The project loads one field area, displays room geometry
with BNTX base-color textures, and draws vegetation instances from the terrain
manifest. The UI is a dockable Dear ImGui shell with English/Japanese labels.
Editing controls are visible but disabled.

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
| `--lang <en\|ja>` | UI language |
| `--load-check` | Load the area, print counts and bounds, then exit |
| `--smoke-test` | Render 90 frames, then exit |
| `--frames <n>` | Render `n` frames, then exit |
| `--screenshot <path>` | Capture the whole window as a 24-bit BMP |
| `--screenshot-delay <s>` | Wait before the screenshot, then exit |
| `--no-plants`, `--no-room`, `--no-textures` | Diagnostic rendering toggles |

## Loading

1. Open `RomFS/bin/archive/field/terrain/<area>.gfpak` and
   `RomFS/bin/archive/field/model/pack/<model>.gfpak`.
2. In the model pack, scan entries: `BNTX` magic becomes a cached texture,
   other entries are parsed as Trinity FlatBuffers and TRMDL files are grouped.
   Files are resolved by the FNV-1a path hash of
   `bin/field/model/pack/<name>`.
3. For each LOD group, keep only non-`_lodN` mesh paths. Plants list every LOD
   in one group (`entries=0,1,2`); using all three overlapped the low-detail
   billboards with LOD0.
4. Build geometry with `TrinityMeshBuilder` from the mesh's TRMBF buffer.
   A TRMSH's `BufferFilePath` resolves the exact `.trmbf` entry.
5. Resolve TRMTR materials by name, then base-color, normal, and AO maps by the
   material's texture slots. Base color is uploaded as RGBA8; normal/AO are
   loaded but not yet sampled by the shader.
6. Find the terrain manifest with `TerrainManifest.FindInArchive`, load each
   plant model from the terrain pack, and append all `.trins` matrices as
   `PlantInstance` records.

The terrain pack's logical folder is
`bin/field/terrain/<area>/terrain/`; room packs use `bin/field/model/pack/`.
Folder hashes include the trailing slash.

## Rendering

- One shader: transform, vertex color, base-color texture, and a directional
  light plus ambient. Both System.Numerics matrices and instance matrices are
  uploaded for the column-vector convention.
- Room meshes and vegetation are drawn into an off-screen RGBA8 framebuffer
  with a 24-bit depth buffer; the Scene panel displays that texture.
- Vegetation uses a model-matrix draw per instance. Instanced attributes were
  initially planned, but produced incorrectly transformed geometry in the
  current GL path; per-instance model matrices are correct and fast enough for
  the observed counts (60 in area 00, 2,204 in area 02).
- The free-fly camera is Y-up: right mouse look, WASD, Q/E, Shift sprint, F
  frame. Default framing focuses the vegetation cluster.

## UI shell

The host window contains a menu bar, a disabled toolbar, and a dockspace.
Panels: Hierarchy, Scene, Inspector, Project, Console, Validation, History, and
Status. Windows receive default positions on first use; docking is enabled and
the layout persists in `imgui.ini` next to the executable.

Labels come from embedded `Localization/en.json` and `ja.json`. The menu switch
changes language at runtime. A CJK font is loaded from the Windows font folder
(`meiryo.ttc`, `msgothic.ttc`, or `YuGothM.ttc`) for Japanese glyphs.

## Known limitations

- Terrain and vegetation world-coordinate alignment is not fully verified. In
  `ha_area00`, the `s02` terrain spans about ±2,430 units with heights up to
  1,458, while vegetation translations are around x=445, z=650, y=60. The
  cluster appears to sit on the terrain in area 00, but the placement transform
  or cell mapping for other areas is not yet confirmed. The viewer frames the
  vegetation cluster by default.
- Only one terrain/model pack pair is loaded. Selecting a room sub-pack such as
  `ha_area00_s07` shows an interior while the vegetation remains elsewhere.
- No skinning, animations, collision, terrain heightmap, or LOD switching.
- No editor operations; Stage 5 adds selection, transforms, and export.
- BNTX textures are decoded to CPU RGBA8 once; no streaming or texture cache
  eviction.
