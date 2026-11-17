# Documentation

Project documentation for the GFlib3 Tool. This folder is tracked in git and
holds the durable notes; `analysis/` remains gitignored scratch space.

## Index

| Document | Contents |
| --- | --- |
| [architecture.md](architecture.md) | Solution layout, project responsibilities, build and native plan |
| [gfpak.md](gfpak.md) | `GFLXPACK` container format (verified against the dump) |
| [sarc.md](sarc.md) | SARC payloads nested inside gfpak entries |
| [trinity.md](trinity.md) | Trinity FlatBuffer model formats (trmdl/trmsh/trmbf/trmtr/trskl) |

## Related files

- `WORK_PLAN.md` (repository root) — current stage, decisions, next actions.
- `OLD-WORK_PLAN.md` (repository root) — dated history.
- `analysis/` — scratch: inventory scripts, extracted data, RE notes.

When this folder grows past a handful of documents, group them into
subdirectories (for example `formats/`, `design/`) and update this index.
