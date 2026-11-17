# SARC (`.sarc` payloads inside `.gfpak`)

Status: verified against six SARC payloads extracted from the dump
(`map.gfpak`, `resident_hud_arc00`, `system_resident`, `battle_resident`,
`pause_menu`, `poke_dex`). All node hashes matched their resolved names.

SARC is not GFlib3-specific; it is the Nintendo archive format, with one
observed Game Freak variation in the name-offset encoding (below).

Implementation: `core/Nxa.GFlib3.Core/Formats/Sarc` (`SarcArchive`).

## Header (0x14 bytes)

| Offset | Type | Field |
| --- | --- | --- |
| 0x00 | u32 | magic `SARC` (`0x43524153`) |
| 0x04 | u16 | header size (`0x14` in the dump) |
| 0x06 | u16 | byte-order mark (`0xFEFF`) |
| 0x08 | u32 | declared file size |
| 0x0C | u32 | data offset (`0x1000` in all observed archives) |
| 0x10 | u16 | version (`0x0100`) |
| 0x12 | u16 | reserved |

The SFAT table starts at the header size.

## SFAT (node table)

| Offset | Type | Field |
| --- | --- | --- |
| +0x00 | u32 | magic `SFAT` (`0x54414653`) |
| +0x04 | u16 | table header size (`0x0C`) |
| +0x06 | u16 | node count |
| +0x08 | u32 | hash key (`0x65` in the dump) |

Nodes follow, `0x10` bytes each:

| Offset | Type | Field |
| --- | --- | --- |
| +0x00 | u32 | name hash |
| +0x04 | u32 | attributes: name offset in 4-byte units in the low 24 bits |
| +0x08 | u32 | data start, relative to the data offset |
| +0x0C | u32 | data end, relative to the data offset |

**Game Freak variation:** the name offset is
`(attributes & 0xFFFFFF) << 2` bytes from the start of the name table, not a
plain byte offset. The high attribute byte is `0x01` for every observed entry.
Nodes are sorted ascending by hash.

## SFNT (name table)

SFNT follows the nodes:

| Offset | Type | Field |
| --- | --- | --- |
| +0x00 | u32 | magic `SFNT` (`0x544E4653`) |
| +0x04 | u16 | table header size (`0x08`) |
| +0x06 | u16 | reserved |

The name table starts at `SFNT + 8`. Names are UTF-8, null-terminated, and
4-byte aligned; they include directory prefixes such as `anim/` and `blyt/`.
The table is padded up to the data offset.

## Name hash

The rolling SARC hash without the usual final key multiplication:

```text
h = 0
for each byte c in name (UTF-8):
    h = (h * 0x65 + c) mod 2^32
```

The `0x65` multiplier is the value stored in the SFAT hash-key field. The
standard SARC final `* key` step is not applied by this game.

## File data

File bytes live at `dataOffset + dataStart .. dataOffset + dataEnd`. Starts are
4-byte aligned; `dataEnd - dataStart` is the file size.

## Observed contents

SARC payloads contain UI assets: `bflan` animations, `bflyt` layouts, `bnsh`
shader archives, and similar. Other `.gfpak` entries are not SARC: the field
area packs hold Trinity model tables (`trmdl`/`trmsh`/`trmtr`/`trmbf`/`trskl`)
directly.

## Open questions

- Meaning of the high attribute byte (always `0x01` in observed entries).
- Whether directory entries exist in these archives (none observed).
- Whether the `<< 2` name offset is constant across all GFlib3 titles.
