# Documentation

Project documentation for the GFlib3 Tool. This folder is tracked in git and
holds the durable notes; `analysis/` remains gitignored scratch space.

## Index

| Document | Contents |
| --- | --- |
| [architecture.md](architecture.md) | Solution layout, project responsibilities, build and native plan |
| [code-quality.md](code-quality.md) | Ownership, errors, constants, comments, and change-hygiene rules |
| [gfpak.md](gfpak.md) | `GFLXPACK` container format (verified against the dump) |
| [sarc.md](sarc.md) | SARC payloads nested inside gfpak entries |
| [trinity.md](trinity.md) | Trinity FlatBuffer model formats (trmdl/trmsh/trmbf/trmtr/trskl) |
| [placement.md](placement.md) | Field object placement: terrain manifests, `.trins`, SRT instance buffers |
| [terrain.md](terrain.md) | Terrain material, heightmap scale and mapping, splat layers, grid mesh |
| [bntx.md](bntx.md) | BNTX texture containers, formats, block-linear layout, decoding |
| [renderer.md](renderer.md) | Field viewer: terrain, room, scene placement, vegetation, UI shell, limitations |
| [ui.md](ui.md) | Editor shell: Unity-style layout, palette, metrics, icons, docking |
| [scene-layout.md](scene-layout.md) | Resident scene schema and background model placement (verified on the dump) |

## Related files

- `.agents/WORK_PLAN.md` — current stage, decisions, next actions.
- `.agents/OLD-WORK_PLAN.md` — dated history.
- `analysis/` — scratch: inventory scripts, extracted data, RE notes.

When this folder grows past a handful of documents, group them into
subdirectories (for example `formats/`, `design/`) and update this index.
