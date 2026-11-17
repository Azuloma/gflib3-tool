# OLD_WORK_PLAN

Archived, dated work history for the GFlib3 Tool project. Newest entries at the top.

## 2026-11-17 — Session 1 (continued): Stages 0–3

### Stage 0 — Foundation (`60a13df`)

- `GFlib3.slnx` with `core/Nxa.GFlib3.Core`, `core/Nxa.GFlib3.Cli`,
  `tests/Nxa.GFlib3.Core.Tests`; all target `net9.0`, build clean.
- `Directory.Build.props`, `.editorconfig`, expanded `.gitignore`
  (`bin/`, `obj/`, `analysis/`, `.agents/`, `AGENTS.md`); `artifacts/` was added
  with the native shim in Stage 1.
- Format investigation started: `.gfpak` header, pointer block, hash tables, and
  file table; payload proven to be Oodle Kraken via an ooz-based decoder.
- Dump inventory: 678 archives, 102,452 entries, all compression type 3,
  1.69 GB compressed / 7.33 GB decompressed.
- Follow-up docs commit `f8c6fcc`: `docs/README.md`, `docs/architecture.md`,
  `docs/gfpak.md`.

### Stage 1 — Containers (`572ea82`)

- Vendored `third_party/ooz` (GPLv3, provenance noted); CLI harness disabled with
  `NXA_OOZ_NO_CLI`.
- `core/Nxa.GFlib3.Native` builds `Nxa.GFlib3.Native.dll` through
  `scripts/build-native.ps1`; one C entry point exposes Kraken decode.
- `GfpakArchive`: header, pointers, path hashes, folder tables, file table, with
  bounds checks; supports `None`, `Zlib`, `OodleKraken`.
- CLI: `gfpak info`, `gfpak list`, `gfpak extract [--entry N]`.
- 9 tests passed. `map.gfpak` entry 0 extracted as 665,212 bytes starting `SARC`.

### Stage 2 — SARC (`5142a07`)

- `SarcArchive`: SARC header, SFAT nodes, SFNT names, data ranges, bounds checks.
- Game Freak variant: name offset `(attributes & 0xFFFFFF) << 2`; hash
  `h = h * 0x65 + c` with no final key multiply.
- `SarcPath` rejects rooted, drive-qualified, control-character, and `..` paths.
- CLI: `sarc info`, `sarc list`, `sarc extract [--entry N]`.
- 23 tests passed. Six SARC payloads verified (map and appli packs); all 68 map
  names/hashes matched.
- Confirmed field area packs are not SARC; entries are Trinity files.
- Docs: `docs/sarc.md`.

### Stage 3 — Trinity (`7e2a8b5`)

- Confirmed the model files are Google FlatBuffers; added a bounds-checked reader
  in `core/Nxa.GFlib3.Core/FlatBuffers`.
- Parsers: `trmbf`, `trmsh`, `trmdl`, `trmtr`, `trskl`; shape-based detection in
  `TrinityFile`.
- CLI: `trinity info <file> [--type <kind>]`.
- 26 tests passed. `ha_area00_s07`: 39 buffers ↔ 39 meshes, 16-bit indices,
  40-byte stride (position/normal/tangent/uv/color), 3 materials, 43 nodes.
- `ha_area00_s01`: 88 entries = 79 BNTX + 9 Trinity (two model sets), 0 failures.
- Docs: `docs/trinity.md`.

### Skills and handoff

- Created project-local skills under `.agents/skills/`:
  `gflib3-stage-verify`, `gflib3-format-re`, `gflib3-docs-sync`.
- Added `opencode.json` (`skills.paths: [".agents/skills"]`); untracked at handoff.
- Temporary extraction directories under `%TEMP%` were removed; native build
  output under `artifacts/` remains (gitignored, rebuildable).

### Verification snapshot

- Build: 0 warnings / 0 errors.
- Tests: 26, both with and without `NXA_GF3_DUMP`.
- Git: stages 0–3 committed; working tree clean except untracked `opencode.json`.

## 2026-11-17 — Session 1: project bootstrap

- Confirmed project scope: private reverse-engineering tooling for GFlib3, using a
  *Pokémon Legends: Arceus* base + v1.1.1 dump.
- Established `.agents/` as agent/skill-only space.
- Added Section 11 (project context) and a workspace-context pointer to `AGENTS.md`.
- Created `WORK_PLAN.md` and this file.
- Confirmed repository is an empty scaffold: `core/`, `render/`, `third_party/`,
  `analysis/` contain no files.
- Open: dump location/form, first deliverable, implementation language.

## 2026-11-17 — Initial commit

- `.gitattributes`, `README.md`, `.gitignore` (`AGENTS.md`, `.agents/`, `analysis/`).
