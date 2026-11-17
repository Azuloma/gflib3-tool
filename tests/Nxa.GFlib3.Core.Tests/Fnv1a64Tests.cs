// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Hashing;

namespace Nxa.GFlib3.Core.Tests;

public sealed class Fnv1a64Tests
{
    [Theory]
    [InlineData("", 0xCBF29CE484222645UL)]
    [InlineData("a", 0xAF66BC4C8606CF2CUL)]
    [InlineData("foobar", 0x09D3072CDA4F5808UL)]
    [InlineData("bin/field/model/pack/room_area00_s07.trmdl", 0xD5BA487F99277D0FUL)]
    [InlineData("bin/field/terrain/ha_area00/terrain/area00_0_0_plants_tree02_X7Z9_d0.trins", 0x8C9AEC8627143999UL)]
    public void HashesKnownVectors(string input, ulong expected)
    {
        Assert.Equal(expected, Fnv1a64.Hash(input));
    }
}
