// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Formats.Bntx;

/// <summary>Tegra X1 block-linear (GOB) addressing used by BNTX tile mode 0.</summary>
internal static class TegraBlockLinear
{
    public const int GobSize = 512;

    public static int AlignUp(int value, int alignment)
    {
        return (value + alignment - 1) & ~(alignment - 1);
    }

    public static int GetPitch(int blocksWide, int bytesPerBlock, ushort tileMode)
    {
        int alignment = tileMode == 0 ? 64 : 32;
        return AlignUp(checked(blocksWide * bytesPerBlock), alignment);
    }

    public static long GetBlockOffset(
        int blockX,
        int blockY,
        int blocksWide,
        int bytesPerBlock,
        int blockHeightLog2)
    {
        int blockHeight = 1 << blockHeightLog2;
        int gobsWide = (checked(blocksWide * bytesPerBlock) + 63) / 64;
        int bytesX = checked(blockX * bytesPerBlock);

        long gob = (long)(blockY / (8 * blockHeight)) * GobSize * blockHeight * gobsWide;
        gob += (long)(bytesX / 64) * GobSize * blockHeight;
        gob += (long)((blockY % (8 * blockHeight)) / 8) * GobSize;

        return gob
            + ((bytesX % 64) / 32) * 256
            + ((blockY % 8) / 2) * 64
            + ((bytesX % 32) / 16) * 32
            + (blockY % 2) * 16
            + (bytesX % 16);
    }
}
