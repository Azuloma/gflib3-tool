# Trinity model formats (`trmdl`, `trmsh`, `trmbf`, `trmtr`, `trskl`)

Status: verified against the `ha_area00_s07` and `ha_area00_s01` area packs.
The files are **Google FlatBuffers**, not custom offset tables. Schemas were
cross-checked against the public GFTool schema definitions and the actual dump.

Implementation: `core/Nxa.GFlib3.Core/Formats/Trinity`, with a small
bounds-checked FlatBuffer reader in `core/Nxa.GFlib3.Core/FlatBuffers`.

## File roles

| Extension | FlatBuffer root | Role |
| --- | --- | --- |
| `.trmdl` | `TRMDL` | Model root: mesh and material paths, optional skeleton, LODs |
| `.trmsh` | `TRMSH` | Mesh metadata: names, bounds, vertex declarations, parts, buffer path |
| `.trmbf` | `TRMBF` | Vertex/index buffer bytes referenced by a TRMSH |
| `.trmtr` | `TRMTR` | Materials: shader parameters, samplers, texture file names |
| `.trskl` | `TRSKL` | Transform-node hierarchy, joints, helper bones |

References are by file name, e.g. a `trmdl` points at `room_area00_s07.trmsh`
and `room_area00_s07.trmtr`; a `trmsh` points at `room_area00_s07.trmbf`.

## TRMDL

| Field | Type | Contents |
| --- | --- | --- |
| 0 | uint | version (0 in observed files) |
| 1 | table[] | meshes, each with a `PathName` string |
| 2 | table | skeleton, with a `PathName` string (optional) |
| 3 | string[] | material file names |
| 4 | table[] | LODs: entry indices and a type string |
| 5 | table | bounds (`MinBound`, `MaxBound` as vec3) |
| 6 | vec4 | unlabelled (observed as four floats) |
| 7 | string | `trltt` path (optional) |
| 8, 9 | uint | unlabelled |

Example (`ha_area00_s07`): one mesh `room_area00_s07.trmsh`, one material
`room_area00_s07.trmtr`, no skeleton, no LOD entries.

## TRMSH

| Field | Type | Contents |
| --- | --- | --- |
| 0 | uint | version |
| 1 | table[] | meshes |
| 2 | string | referenced `trmbf` file name |

Mesh fields:

| Field | Type | Contents |
| --- | --- | --- |
| 0 | string | mesh name |
| 1 | table | bounding box |
| 2 | uint | index format |
| 3 | table[] | vertex declarations |
| 4 | table[] | mesh parts |
| 5–8 | uint | unlabelled |
| 9 | struct | clip sphere (x, y, z, radius) |
| 10 | table[] | bone weights |
| 11 | table[] | visibility shapes |
| 12 | string | unlabelled |
| 13 | uint | unlabelled |
| 14 | table[] | morph shapes |

Vertex declaration: `vertexElements[]` plus `vertexElementSizes[]`. Each element
has a size index, usage, layer, format, and byte offset. The first size entry is
the vertex stride.

Observed mesh `mirror_low_shape`: index format 16-bit, one part with 3,018
indices, material `room_area00_s07_01`, one declaration with stride 40 bytes:

| Offset | Usage | Format | Bytes |
| --- | --- | --- | --- |
| 0 | Position | `X32Y32Z32Float` (51) | 12 |
| 12 | Normal | `W16X16Y16Z16Float` (43) | 8 |
| 20 | Tangent | `W16X16Y16Z16Float` (43) | 8 |
| 28 | TexCoord | `X32Y32Float` (48) | 8 |
| 36 | Color | `R8G8B8A8UnsignedNormalized` (20) | 4 |

The 4-component half formats (`W16X16Y16Z16Float` and
`W32X32Y32Z32Float`) store components in **X, Y, Z, W** order despite the
format name. For `mirror_low_shape` vertex 0 the measured values are
normal `(1, 0, 0)`, tangent `(0, 0, -1, 1)`, matching a right-handed frame.
Reading the first half as W yields a zero normal and a non-unit tangent.

Model texture V maps to the decoded BNTX image as `decoded row = 1 - V`
(U unchanged): V=0 addresses the image's last row and V=1 its first row. The
viewer applies this flip in the model shader; terrain UVs are generated
separately and do not use this rule.

Mesh part: index count, index offset (in indices, not bytes), unlabelled field,
material name, and vertex declaration index.

## TRMBF

| Field | Type | Contents |
| --- | --- | --- |
| 0 | uint | version |
| 1 | table[] | mesh buffers |

Each mesh buffer holds `IndexBuffer[]`, `VertexBuffer[]`, and `MorphTargets[]`;
each buffer is a byte vector. The `ha_area00_s07` pack has 39 mesh buffers
(260,964 index bytes and 1,595,176 vertex bytes), matching the 39 TRMSH meshes.
Mapping is by order — verified for all 39 meshes in `ha_area00_s07` and both
model sets in `ha_area00_s01`; still a hypothesis for packs with unequal
counts.

### Mesh building

`TrinityMeshBuilder` produces `TrinityVertex[]`, a flat `uint[]` index list, and
per-part ranges (`TrinitySubMesh`) with material names and bounds. It currently
accepts one index buffer, one vertex buffer, and one vertex declaration per
mesh; multi-part meshes are supported. Observed packs satisfy this:

- `ha_area00_s07`: 39 meshes, 16-bit indices, one part each, strides 36 or 40.
- `ha_area00_s01`: 3 meshes across two model sets; one room mesh has 32-bit
  indices, 432,655 vertices, 924,645 indices, and 14 material parts.
- `ha_area11`: 1 mesh, 2 parts.

TRMSH bounds match the bounds computed from decoded vertices for many meshes
(for example `mirror_low_shape`) but differ for some (for example
`illust01_low_dup_shape`, `flowervase_low_shape`, `pin_low_shape`,
`kushi_low_shape`) even though all TRSKL node transforms are identity. The
runtime vertex positions are authoritative; the TRMSH bounds may describe
source-mesh space or include unused geometry.

Supported vertex formats: `X32Y32Z32Float`, `W32X32Y32Z32Float`,
`W16X16Y16Z16Float`, `X32Y32Float`, `R8G8B8A8UnsignedNormalized`,
`W8X8Y8Z8Unsigned`, `W16X16Y16Z16UnsignedNormalized`. Index formats 8, 16, and
32-bit are supported; 64-bit is rejected.


## TRMTR

| Field | Type | Contents |
| --- | --- | --- |
| 0 | uint | version |
| 1 | table[] | materials |

Material fields: name, shaders, textures, samplers, float parameters, light and
vec4 parameters, int parameters, several unlabelled strings, byte/int extras,
and an alpha type. Texture entries carry a semantic name, file name, and slot.

Observed map material:

| Semantic | File | Slot |
| --- | --- | --- |
| BaseColorMap | `room_area00_s07_01_alb.bntx` | 0 |
| NormalMap | `room_area00_s07_01_nrm.bntx` | 1 |
| MetallicMap | `room_area00_s07_01_mtl.bntx` | 2 |
| RoughnessMap | `room_area00_s07_01_rgn.bntx` | 3 |
| EmissionColorMap | `room_area00_s07_emv.bntx` | 4 |
| AOMap | `room_area00_s07_01_ao.bntx` | 5 |

Texture files are `.bntx` payloads; resolving them inside area packs is Stage 4.

## TRSKL

| Field | Type | Contents |
| --- | --- | --- |
| 0 | uint | version |
| 1 | table[] | transform nodes |
| 2 | table[] | joint infos |
| 3 | table[] | helper bones |
| 4 | uint | skinning palette offset |
| 5 | bool | interior map flag |

Transform node: name, SRT transform (scale/rotate/translate), scale and rotate
pivots, parent node index, joint info index, parent name, node type, priority
pass, ignore-parent-rotation.

Observed map skeleton: 43 transform nodes, no joint infos, no helper bones. The
root is `room_area00_s07_model`; `material_a` is its child, and mesh nodes follow.
Static map props therefore use a node hierarchy without skinning joints.

## Enum values

- Index format: 0 = 8-bit, 1 = 16-bit, 2 = 32-bit, 3 = 64-bit.
- Vertex usage: 1 position, 2 normal, 3 tangent, 4 binormal, 5 color,
  6 texcoord, 7 blend index, 8 blend weights, 9/10 user data.
- Vertex format: 20 `R8G8B8A8_UNORM`, 22 `W8X8Y8Z8_UINT`, 36 `X32_UINT`,
  37 `X32_SINT`, 39 `W16X16Y16Z16_UNORM`, 43 `W16X16Y16Z16_FLOAT`,
  48 `X32Y32_FLOAT`, 51 `X32Y32Z32_FLOAT`, 52 `W32X32Y32Z32_UINT`,
  54 `W32X32Y32Z32_FLOAT`.
- Node type: 0 normal, 1 chained, 2 leaf.

## Verification

- `ha_area00_s07.gfpak`: entries 0–4 are TRMBF, TRMDL, TRMSH, TRMTR, TRSKL.
  Parsed counts: 39 buffers, 39 meshes, 3 materials, 43 nodes.
- `ha_area00_s01.gfpak`: 88 entries, 79 BNTX, 9 Trinity (two model sets).
  All Trinity entries parse and detect correctly.

## Open questions

- How an area places model instances in the world (scene/placement files).
- Whether TRMSH mesh index always maps 1:1 to the TRMBF buffer index.
- Morph-shape data layout and LOD entries (both empty in the samples).
- Meaning of the unlabelled TRMDL/TRMSH fields.

## References

- GFTool FlatBuffer schemas: `Trinity.Core.Flatbuffers.TR.Model`.
- rhh-docs FlatBuffer notes for Legends Arceus.
- Pokémon-Switch-Model-Importer-Blender `trmshbf_exporter.py` (field naming).
