# GFLXPACK (`.gfpak`) — format notes

Status: header, hash tables, and file table verified against the dump. Payload
decompression verified. Name hashing and nested formats still under investigation.

Primary reference: pkNX `GFPack.cs` (structure cross-checked against 678 archives).
All findings below were also confirmed directly on the dump. Promoted from the
initial scratch notes in `analysis/`.

## Header (0x18 bytes)

| Offset | Type | Field |
| --- | --- | --- |
| 0x00 | u64 | magic `GFLXPACK` (`0x4B434150584C4647`) |
| 0x08 | u32 | version (`0x1000` in the dump) |
| 0x0C | u32 | isRelocated (0 or 1) |
| 0x10 | i32 | file count |
| 0x14 | i32 | folder count |

Immediately after the header:

| Offset | Type | Field |
| --- | --- | --- |
| 0x18 | i64 | pointer to file table |
| 0x20 | i64 | pointer to absolute path-hash table |
| 0x28 | i64[folderCount] | pointers to per-folder hash tables |

Example (`archive/appli/map.gfpak`): version `0x1000`, 35 files, 1 folder,
file table `0x388`, path hashes `0x30`, folder hash `0x148`.

## Path-hash table (`ptrHashPaths`)

`fileCount` entries of `u64`, each the FNV-1a 64-bit hash of a file's full path.

## Folder hash tables (`ptrHashFolders[i]`)

```text
u64  folder-name FNV1a-64
i32  fileCount
u32  0xCC (padding)
fileCount * {
    u64  file-name FNV1a-64
    i32  index into the file table
    u32  0xCC (padding)
}
```

FNV-1a 64-bit uses offset basis `0xCBF29CE484222325` and prime
`0x100000001B3`. Character case/normalization is not yet determined.

## File table (`ptrFileTable`)

Entries are `0x18` bytes:

| Offset | Type | Field |
| --- | --- | --- |
| 0x00 | u16 | level (9 in all observed entries) |
| 0x02 | u8 | compression type |
| 0x03 | u8 | padding |
| 0x04 | u32 | size decompressed |
| 0x08 | u32 | size compressed |
| 0x0C | u32 | `0xCC` (padding) |
| 0x10 | u32 | offset packed |
| 0x14 | u32 | unused |

Packed payloads are placed at `offsetPacked` and padded to `0x10` alignment.

## Compression

Compression type values: 0 none, 1 zlib, 2 LZ4, 3 Oodle Kraken, 4–7 other Oodle
codecs.

Dump inventory (all 678 archives, 102,452 entries): **type 3 only**.
Compressed payload totals 1,694,240,080 bytes; decompressed totals
7,331,069,407 bytes.

Decompression was verified with an ooz-derived Kraken decoder (powzix/ooz,
GPLv3): `map.gfpak` entry 0 (`67,830 → 665,212` bytes) decodes to a payload
starting with the magic `SARC`. Entries are therefore nested SARC archives, and
some entries are Trinity-style tables (e.g. `24` bytes beginning `0C 00 00 00`).

## Largest area packs

| File | Size | Entries |
| --- | ---: | ---: |
| `archive/field/model/pack/ha_area04.gfpak` | 97,534,896 | 860 |
| `archive/field/model/pack/ha_area00.gfpak` | 69,650,000 | 1075 |
| `archive/field/model/pack/ha_area05.gfpak` | 69,475,696 | 782 |
| `archive/field/model/pack/ha_area01.gfpak` | 63,965,696 | 1055 |
| `archive/field/model/pack/ha_area03.gfpak` | 55,616,752 | 524 |
| `archive/field/model/pack/ha_area02.gfpak` | 53,769,488 | 721 |

## Open questions

- How are full-path hashes derived (root prefix, separators, case)?
- What does `level` mean, and can it vary?
- SARC layout of entries, and how names inside SARC relate to gfpak hashes.
- Trinity table formats (`trmdl`, `trmsh`, `trmtr`, `trmbf`, `trskl`).
