// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

#include <cstddef>
#include <cstdint>

#include "stdafx.h"

// Defined in third_party/ooz/kraken.cpp. Writes up to 64 bytes past dst_len,
// so callers must provide that much padding after the output buffer.
int Kraken_Decompress(const byte* src, size_t src_len, byte* dst, size_t dst_len);

extern "C" __declspec(dllexport) int nxa_oodle_decompress(
    const std::uint8_t* src,
    std::size_t src_len,
    std::uint8_t* dst,
    std::size_t dst_len)
{
    if (src == nullptr || dst == nullptr)
        return -1;
    if (src_len > 0x7FFFFFFF || dst_len > 0x7FFFFFFF)
        return -1;

    return Kraken_Decompress(
        reinterpret_cast<const byte*>(src),
        src_len,
        reinterpret_cast<byte*>(dst),
        dst_len);
}
