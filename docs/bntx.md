# BNTX textures

Status: header, texture-info, format, and layout fields verified against 205
texture containers from the dump. Decoding is implemented in
`core/Nxa.GFlib3.Core/Formats/Bntx`. Data-layout claims marked *hypothesis* are
supported by observation but not confirmed by a second independent source.

Investigated packs:

- `RomFS/bin/archive/field/model/pack/ha_area00_s07.gfpak` (17 BNTX)
- `RomFS/bin/archive/field/model/pack/ha_area00_s01.gfpak` (79 BNTX)
- `RomFS/bin/archive/field/model/pack/ha_area02_s01.gfpak` (9 BNTX)
- `RomFS/bin/archive/field/terrain/ha_area00.gfpak` (36 BNTX)
- `RomFS/bin/appli/map/map_texture/*.bntx` (24 standalone files)

All surveyed containers are little-endian, target `NX  `, and store one texture
per file. Each BNTX payload is a gfpak entry (Kraken) or a standalone RomFS
file; the container format is identical in both cases.

## File header

References: EPD wiki `nn::gfx::ResTextureFile` (4.1.0), Wexos BNTX page,
`tomolib` BNTX parser. Offsets below were re-checked on every surveyed file.

| Offset | Type | Meaning | Observed |
| --- | --- | --- | --- |
| 0x00 | char[8] | magic `BNTX\0\0\0\0` | always |
| 0x08 | u32 | version bytes | `00 01 04 00` = 4.1.0 |
| 0x0C | u16 | byte-order mark | `FF FE` (little-endian) |
| 0x0E | u8 | alignment shift (log2) | 12 → 4096 bytes |
| 0x0F | u8 | target pointer size in bits | 64 |
| 0x10 | u32 | file-name offset | points into `_STRX` |
| 0x14 | u16 | runtime flag | 0 |
| 0x16 | u16 | first block offset | 0x1A0 (`_STRX`) |
| 0x18 | u32 | relocation-table offset | before file size |
| 0x1C | u32 | file size | equals payload length |
| 0x20 | char[4] | target platform | `NX  ` |
| 0x24 | u32 | texture count | 1 |
| 0x28 | u64 | texture-info pointer array | 0x198 |
| 0x30 | u64 | texture data block (`BRTD`) | 0x0FF0 |
| 0x38 | u64 | texture dictionary (`_DIC`) | 0x1D0/0x1D8 |
| 0x40 | u64 | in-place memory pool pointer | 0x58 |
| 0x48 | u64 | user memory pool pointer | 0 |
| 0x50 | s32 | base memory pool offset | 0 |
| 0x54 | u32 | reserved | 0 |

The header name is a NUL-terminated string at `0x10`, not a length-prefixed
string. In every single-texture sample it equals the texture name. The
relocation table occupies the last `0x68`–`0x70` bytes and is not needed for
reading.

The region `0x58`–`0x198` is the in-place memory-pool area plus padding; it is
zero in the dump. The texture-info pointer array (`0x198` in all samples) holds
one absolute `u64` per texture.

## Texture info (`BRTI`)

Each pointer leads to a `BRTI` block. The block header is 0x10 bytes; the
texture-info fields start at `BRTI + 0x10`.

| Offset | Type | Meaning | Observed |
| --- | --- | --- | --- |
| 0x00 | char[4] | magic `BRTI` | always |
| 0x04 | u32 | next-block offset, relative to `BRTI` | distance to `BRTD` |
| 0x08 | u32 | section size | same as 0x04 in samples |
| 0x0C | u32 | reserved | 0 |
| 0x10 | u8 | flags | 9 |
| 0x11 | u8 | storage dimension | 2 (2D) |
| 0x12 | u16 | tile mode | 0 (block-linear) |
| 0x14 | u16 | swizzle value | 0 |
| 0x16 | u16 | mip count | 1–12 |
| 0x18 | u16 | sample count | 1 |
| 0x1A | u16 | reserved | 0 |
| 0x1C | u32 | image format | see below |
| 0x20 | u32 | GPU access flag | 0x20 |
| 0x24 | u32 | width | 16–2048 |
| 0x28 | u32 | height | 16–2048 |
| 0x2C | u32 | depth | 1 |
| 0x30 | u32 | array/surface count | 1 |
| 0x34 | u32 | layout; low 3 bits = block-height log2 | 0–4 |
| 0x38 | u32 | layout flags | 0x00010007 |
| 0x3C | u8[20] | reserved | 0 |
| 0x50 | u32 | total image size (all mips, padded) | see below |
| 0x54 | u32 | data alignment | 512 |
| 0x58 | u8[4] | channel sources R,G,B,A | 0x00–0x05 |
| 0x5C | u32 | surface dimension | 1 (2D) |
| 0x60 | u64 | texture-name offset | `BRTI`-relative string |
| 0x68 | u64 | parent container offset | 0x20 |
| 0x70 | u64 | mip-pointer array offset | one `u64` per mip |
| 0x78 | u64 | user-data array offset | 0 or valid |
| 0x80 | u64 | runtime texture pointer | not used |
| 0x88 | u64 | runtime texture-view pointer | not used |
| 0x90 | u64 | descriptor slot | not used |
| 0x98 | u64 | user-data dictionary offset | 0 or valid |

Texture names are length-prefixed `u16` strings (`0x60` points at the length).
Mip pointers (`0x70`) are absolute file offsets; each mip is aligned to the
value at `0x54` (512 in every sample).

## Image formats

The format ID packs a channel layout and a data type:
`format = (channel << 8) | type`.

| ID | Format | Bytes/block | Seen |
| --- | --- | --- | --- |
| 0x0201 | R8 UNORM | 1 | no |
| 0x0701 | R5G6B5 UNORM | 2 | no |
| 0x0901 | R8G8 UNORM | 2 | no |
| 0x0A05 | R16 FLOAT | 2 | yes |
| 0x0B01 | R8G8B8A8 UNORM | 4 | no |
| 0x0B06 | R8G8B8A8 SRGB | 4 | no |
| 0x0C01 | B8G8R8A8 UNORM | 4 | no |
| 0x0E01 | R10G10B10A2 UNORM | 4 | yes |
| 0x1A01/0x1A06 | BC1 UNORM/SRGB | 8 | no |
| 0x1B01/0x1B06 | BC2 UNORM/SRGB | 16 | no |
| 0x1C01/0x1C06 | BC3 UNORM/SRGB | 16 | yes (UNORM) |
| 0x1D01/0x1D02 | BC4 UNORM/SNORM | 8 | yes (UNORM) |
| 0x1E01/0x1E02 | BC5 UNORM/SNORM | 16 | yes (UNORM) |
| 0x1F01/0x1F02 | BC6H UF16/SF16 | 16 | no |
| 0x2001/0x2006 | BC7 UNORM/SRGB | 16 | yes |
| 0x2D01–0x3A06 | ASTC 4x4 … 12x12 | 16 | no |

Data type codes: `01` UNORM, `02` SNORM, `05` FLOAT, `06` SRGB. Channel layout
codes: `02` R8, `07` R5G6B5, `09` R8G8, `0A` R16, `0B` R8G8B8A8, `0C`
B8G8R8A8, `0E` R10G10B10A2, `1A` BC1 … `20` BC7, `2D`+ ASTC.

Observed counts (205 textures): BC7 SRGB 56, BC4 UNORM 68, BC5 UNORM 44, BC7
UNORM 10, BC3 UNORM 24, R16 FLOAT 2, R10G10B10A2 UNORM 1.

## Mip data layout

- The `BRTD` block at `0x30` carries the magic `BRTD`, a zero word, the data
  size (including the 0x10-byte `BRTD` header), and a zero word.
- Image data begins at the first mip pointer; in every sample this is
  `0x1000`, immediately after `BRTD` at `0x0FF0`.
- `image_size` (`0x50`) is the sum of all mip surfaces. Each mip starts at its
  pointer and is padded to the texture alignment (512); the next pointer is
  authoritative.
- For block-compressed mips the observed sizes are the exact block bytes for
  that mip rounded up to 512 (for example BC7 1024²: 1,048,576 + 262,144 +
  65,536 + … = 1,400,320).

### Tile mode 0: block-linear (verified)

All 205 surveyed textures use tile mode 0. Decoding the 1025² terrain
heightmap and normal map as pitch-linear produces row discontinuities; decoding
with the Tegra X1 block-linear address function produces a smooth height field
and normal map. Tile mode 0 is therefore **block-linear**, not pitch-linear.
(Older BNTX-Extractor code names the field the other way; its field offsets do
not match the 4.1.0 layout.)

Definitions, using blocks (`bw × bh`, `bpp` bytes per block):

```text
blocksWide = ceil(width / bw)
blocksHigh = ceil(height / bh)
pitch      = alignUp(blocksWide * bpp, 64)
paddedRows = alignUp(blocksHigh, 8 << (layout & 7))
surface    = alignUp(pitch * paddedRows, alignment)
```

Tegra X1 address of block `(x, y)` inside that surface:

```text
gobsWide = ceil(pitch / 64)
gob = (y / (8 * blockHeight)) * 512 * blockHeight * gobsWide
    + (x * bpp / 64) * 512 * blockHeight
    + (y % (8 * blockHeight) / 8) * 512
byte = gob
     + ((x * bpp % 64) / 32) * 256
     + ((y % 8) / 2) * 64
     + ((x * bpp % 32) / 16) * 32
     + (y % 2) * 16
     + (x * bpp % 16)
```

`blockHeight = 1 << (layout & 7)`. For the 1025×1025 R16 FLOAT heightmap:
blocks 1025×1025, pitch `alignUp(2050, 64) = 2112`, padded rows
`alignUp(1025, 128) = 1152`, surface `2112 × 1152 = 2,433,024` — exactly the
observed `image_size`.

### Tile mode 1: pitch-linear (hypothesis, no sample)

No surveyed texture uses tile mode 1. The reference description is
`pitch = alignUp(blocksWide * bpp, 32)` with rows stored consecutively and no
GOB reordering. This remains unverified.

### Mip surfaces smaller than one block row

Observed `image_size` values match tight mip sizes for mips shorter than
`8 << blockHeightLog2` (for example BC7 64² mips inside a 1024² texture are
4,096 bytes, not padded to 8,192). *Hypothesis*: the effective block height is
clamped to the mip height, or later mips are stored without GOB row padding.
Only mip 0 decoding depends on this today.

## Observed texture set

Naming and channel conventions in field packs:

| Suffix | Format | Channels | Role |
| --- | --- | --- | --- |
| `_alb` | BC7 SRGB | R,G,B,A | albedo |
| `_emv` | BC7 SRGB | R,G,B,A | emissive |
| `_nrm` | BC5 UNORM | R,G,B,A (RG stored) | normal |
| `_ao`, `_mtl`, `_rgn` | BC4 UNORM | R,R,R,One | masks |
| `windmask` | BC7 UNORM | R,G,B,A | vegetation wind |
| `heightmap` | R16 FLOAT | R,R,R,One | terrain height |
| `normalmap` | R10G10B10A2 UNORM | R,G,B,A | terrain normal |
| `map_lmap_pic_*` | BC3 UNORM | R,G,B,A | world-map images |

All textures are 2D, single-surface, tile mode 0. Width/height are powers of
two except the 1025² terrain maps. Mip counts range from 1 to 12. `layout`
(block-height log2) is 0–4 and correlates with texture size (1024² → 4, 256²
→ 3, 64² → 1).

## Decoding

`BntxTextureDecoder` produces tightly packed RGBA8 for mip 0:

1. Compute the padded surface size for the mip from the format block size and
   `layout & 7`.
2. For tile mode 0, undo the block-linear layout into row-major blocks with
   pitch `alignUp(blocksWide * bpp, 64)`; for tile mode 1, treat the source as
   pitch-linear.
3. Copy block rows into a tightly packed buffer (strip pitch padding).
4. Decode BC1–BC7 with BCnEncoder.Net (MIT/Unlicense); decode uncompressed
   formats in Core.
5. Apply the channel sources only when they differ from the identity mapping.

sRGB is preserved as a format flag; the renderer selects an sRGB internal
format. BC5 normal maps store X/Y and reconstruct Z in the shader.

## Open questions

- Tile mode 1 layout (no sample in the dump).
- Exact mip padding rule below one block row (clamping vs. tight mips).
- Header-name semantics for multi-texture containers (none observed).
- ASTC, BC6H, cubemaps, arrays (not present in the surveyed field/map packs).
- `_STRX`/`_DIC` internal layout (not required to read textures).
- Whether channel sources other than identity or R/R/R/One occur.
