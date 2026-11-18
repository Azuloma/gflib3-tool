<!-- SPDX-License-Identifier: GPL-3.0-or-later -->

<!-- Copyright 2026 the NXAevora Projects -->

# GFlib3 Tool

Private reverse-engineering and viewing tools for **GFlib3**, Game Freak's
in-house engine, built against a decrypted dump of *Pokemon Legends: Arceus*
(base game plus update v1.1.1).

The long-term goal is a map editor for field areas. The current state is a
read-only field viewer, a set of format libraries, and a CLI for inspection and
extraction.

> This is a private project. Do not publish, upload, or share ROM contents,
> extracted assets, keys, or tool output.

## Status

| Area | State |
| --- | --- |
| Container and compression (`gfpak`, Oodle Kraken) | Done |
| SARC payloads | Done |
| Trinity model formats | Done |
| BNTX textures | Done |
| Terrain, placements, vegetation | Done for areas 00-06 and terrain sub-packs |
| Field viewer and ImGui shell | Done (read-only) |
| Editing, transform, and RomFS export | Planned |

Verified field areas: `ha_area00`-`ha_area06`; 55 terrain sub-packs have been
load-checked. Some interiors with layered materials remain partially rendered;
see [Known limitations](#known-limitations).

## Features

- **`gfpak`**: read GFLXPACK archives, list entries, decompress Oodle Kraken
  payloads in-process through a native shim over the vendored `ooz` decoder,
  and extract entries.
- **SARC**: parse and extract SARC archives nested inside gfpak payloads.
- **Trinity**: parse `trmdl`, `trmsh`, `trmbf`, `trmtr`, and `trskl`
  FlatBuffers; build renderable meshes from vertex and index buffers.
- **BNTX**: parse texture containers and decode BC7, BC5, BC4, BC3, R16/R32
  float, and R10G10B10A2 formats, including array slices.
- **Terrain**: decode heightmaps, manifest metadata, terrain materials, splat
  weights, and grid meshes.
- **Field composition**: load resident scene placements through the model
  catalog and render heightmap terrain, static objects, rooms, and `.trins`
  vegetation together.
- **Viewer**: Silk.NET + ImGui.NET desktop shell with a free-fly camera,
  runtime area/model switching, English/Japanese UI, and screenshot output.

## Requirements

- Windows 10/11 x64.
- A .NET SDK that can build `net9.0` (the solution targets .NET 9).
- Visual Studio 2026 with the C++ toolchain and CMake for the native decoder
  (the build script uses the `Visual Studio 18 2026` CMake generator).
- A decrypted *Pokemon Legends: Arceus* dump containing `RomFS/`, `ExeFS/`, and
  `control.nacp`. The tools use `RomFS/`; `control.nacp` is ignored. No keys
  are required.

Set the dump root once per shell:

```powershell
$env:NXA_GF3_DUMP = "C:\path\to\PLA-1.1.1"
```

Every managed tool reads paths from the supplied archive arguments. The viewer
also accepts an explicit `--dump <path>` instead of relying on the environment
variable.

## Build

Build the native Kraken decoder first, then the managed solution:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-native.ps1
dotnet build GFlib3.slnx
```

The native script builds `Nxa.GFlib3.Native.dll` from
`core/Nxa.GFlib3.Native` and `third_party/ooz`, then copies it to
`artifacts/native/`. `OodleKraken` resolves the DLL from
`NXA_GF3_NATIVE`, the application directory, or an `artifacts/native/`
directory found by walking up from the application directory.

## Test

```powershell
dotnet test GFlib3.slnx
```

Dump-gated tests skip their data checks when `NXA_GF3_DUMP` is unset. Run them
against the dump as well:

```powershell
$env:NXA_GF3_DUMP = "C:\path\to\PLA-1.1.1"
dotnet test GFlib3.slnx
```

The suite currently contains 78 tests (72 Core, 6 Render).

## Quick start: field viewer

Open an area with its matching model pack:

```powershell
dotnet run --project render\Nxa.GFlib3.Render -- --area ha_area02 --model ha_area02
```

`--area` names a terrain pack under
`RomFS/bin/archive/field/terrain/`; `--model` names a model pack under
`RomFS/bin/archive/field/model/pack/`. For a sub-pack, use the matching name
pair (for example `--area ha_area00_s07 --model ha_area00_s07`). The room is
rendered from its resident placement; an origin preview is used only when no
room placement exists.

Useful options:

```text
--load-check                 Print counts and bounds, then exit
--mesh <text>                Preview the first model matching a mesh/material name
--lang <en|ja>               UI language
--screenshot <path.bmp>      Save the window after --screenshot-delay seconds
--frames <n>                 Render n frames, then exit
--no-terrain --no-scene      Diagnostic toggles
--no-plants --no-room --no-textures
--terrain-stride <n>         Sample every nth terrain row/column
--terrain-tile-scale <n>     Multiply SplatST terrain tiling (default 128)
--reload-area <name>         Switch area after the first frame (diagnostic)
--reload-model <name>        Model pack for --reload-area
```

Examples:

```powershell
# Load-check an area without opening the window
dotnet run --project render\Nxa.GFlib3.Render -- --area ha_area00 --model ha_area00_s02 --load-check

# Capture a screenshot
dotnet run --project render\Nxa.GFlib3.Render -- --area ha_area00 --model ha_area00 --screenshot analysis\screenshots\area00.bmp --screenshot-delay 8
```

Controls: right mouse button to look, WASD to move, Q/E down/up, Shift to
sprint, F to frame the scene. The full option list is in
[docs/renderer.md](docs/renderer.md).

## CLI

```powershell
dotnet run --project core\Nxa.GFlib3.Cli -- <command> [arguments]
```

| Command | Purpose |
| --- | --- |
| `gfpak info <archive>` | Show the GFLXPACK header and totals |
| `gfpak list <archive>` | List entries by index, compression, sizes, and path hash |
| `gfpak extract <archive> <dir> [--entry N]` | Decompress entries to disk |
| `sarc info/list/extract` | Inspect or extract a SARC payload |
| `trinity info <file> [--type <kind>]` | Inspect a Trinity model file |
| `bntx info/stats/decode` | Inspect, analyze, or decode a BNTX texture |
| `terrain manifest/heightmap/mesh` | Inspect terrain metadata and build a grid |
| `scene info <file|gfpak> [--entry N] [--all]` | Inspect resident scene tables |

Examples:

```powershell
dotnet run --project core\Nxa.GFlib3.Cli -- gfpak info "$env:NXA_GF3_DUMP\RomFS\bin\archive\appli\map.gfpak"
dotnet run --project core\Nxa.GFlib3.Cli -- gfpak extract "$env:NXA_GF3_DUMP\RomFS\bin\archive\appli\map.gfpak" analysis\map --entry 0
dotnet run --project core\Nxa.GFlib3.Cli -- terrain heightmap "$env:NXA_GF3_DUMP\RomFS\bin\archive\field\terrain\ha_area02.gfpak"
dotnet run --project core\Nxa.GFlib3.Cli -- scene info "$env:NXA_GF3_DUMP\RomFS\bin\archive\field\resident_release.gfpak" --entry 157
```

## Repository layout

| Path | Contents |
| --- | --- |
| `core/Nxa.GFlib3.Core` | Format readers, containers, shared models, native P/Invoke |
| `core/Nxa.GFlib3.Cli` | Command-line inspection and extraction |
| `core/Nxa.GFlib3.Native` | C ABI shim over the vendored `ooz` Kraken decoder |
| `render/Nxa.GFlib3.Render` | Field viewer/editor shell, scene loading, GPU rendering |
| `tests/Nxa.GFlib3.Core.Tests` | Unit and fixture tests |
| `tests/Nxa.GFlib3.Render.Tests` | Dump-gated scene-loading regression tests |
| `docs/` | Durable architecture and format documentation |
| `scripts/` | Native build and maintenance scripts |
| `third_party/ooz` | Vendored Oodle Kraken decoder (GPLv3) |
| `analysis/` | Gitignored scratch data, probes, screenshots, and notes |
| `artifacts/` | Gitignored build output, including the native DLL |
| `.agents/` | Gitignored local session state; not project source |

Parsing logic belongs in `core/`; the CLI and renderer consume those libraries
rather than duplicating format code.

## Data handling

- The repository must not contain ROM contents, extracted assets, keys, or tool
  output. Keep derived data in `analysis/` or another gitignored directory.
- Treat all target data as untrusted input. Parsers validate sizes, offsets, and
  counts; guest code is never executed.
- Prefer the dump's `RomFS/` tree directly. Update files, when present, should
  resolve patch-over-base; the current tools read the supplied tree as-is.
- Format details are recorded as hypotheses with evidence until verified
  against the dump. Durable explanations live in `docs/`; raw probes stay in
  `analysis/`.

## Documentation

The documentation index is [docs/README.md](docs/README.md). Start with:

- [docs/architecture.md](docs/architecture.md) - solution layout and build plan
- [docs/gfpak.md](docs/gfpak.md), [docs/sarc.md](docs/sarc.md) - containers
- [docs/trinity.md](docs/trinity.md) - model formats and mesh building
- [docs/bntx.md](docs/bntx.md) - texture formats and decoding
- [docs/terrain.md](docs/terrain.md) - heightmaps, world mapping, splat layers
- [docs/placement.md](docs/placement.md) - terrain manifests and `.trins`
- [docs/scene-layout.md](docs/scene-layout.md) - resident scene schema
- [docs/renderer.md](docs/renderer.md) - viewer behavior and options
- [docs/ui.md](docs/ui.md) - editor shell layout and theme
- [docs/code-quality.md](docs/code-quality.md) - normative coding rules

## Known limitations

- The viewer is read-only; selection, transforms, and RomFS export are planned.
- Multi-layer TRMTR materials (`NumMaterialLayer > 1`) are not blended yet:
  only `BaseColorMap` layer 0 is sampled, so some cave/lava interiors appear
  flat or white.
- Terrain normal maps, the hole map, detail grass, and the sparse entries 6-8
  masks remain unresolved. Areas without splat weights fall back to flat green.
- There is no sky or fog. Distant backdrop geometry can appear detached, and
  cave/hole caps render dark.
- Untextured water uses a flat blue tint.
- A standalone sub-pack view can leave cross-pack room references unresolved;
  base-area views exclude room catalog entries rather than resolving them.
- Entry `0091` and its building-layout schema are not parsed.
- One room TRMDL is loaded at a time. There is no skinning, animation,
  collision, or LOD switching.
- BNTX tile mode 1, ASTC/BC6H, and some multi-texture/mip edge cases are not
  supported.

## License

The project source is licensed under **GPL-3.0-or-later**; source files carry
the matching SPDX identifier and the NXAevora copyright line. The vendored
`third_party/ooz` decoder is GPLv3; see
[third_party/ooz/COPYING](third_party/ooz/COPYING). Managed dependencies include
BCnEncoder.Net (MIT/Unlicense), Silk.NET, and ImGui.NET under their respective
licenses.
