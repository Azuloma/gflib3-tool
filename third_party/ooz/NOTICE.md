# ooz (vendored)

Upstream: https://github.com/powzix/ooz
Revision: 05038060aa68f9187ae9923b2388ca8db40e58d1
License: GNU General Public License v3 (see `COPYING` and the file headers)

Vendored files: `kraken.cpp`, `lzna.cpp`, `bitknit.cpp`, `stdafx.cpp`,
`stdafx.h`, `targetver.h`.

Local changes: the CLI/test harness at the end of `kraken.cpp` is wrapped in
`#ifndef NXA_OOZ_NO_CLI` so the file can be compiled into a library; the decoder
code itself is unchanged. Only `Kraken_Decompress` is used, through the shim in
`core/Nxa.GFlib3.Native`.
