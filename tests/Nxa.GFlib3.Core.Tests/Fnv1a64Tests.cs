// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.Hashing;

namespace Nxa.GFlib3.Core.Tests;

public sealed class Fnv1a64Tests
{
    [Theory]
    [InlineData("", 0xCBF29CE484222325UL)]
    [InlineData("a", 0xAF63DC4C8601EC8CUL)]
    [InlineData("foobar", 0x85944171F73967E8UL)]
    public void HashesKnownVectors(string input, ulong expected)
    {
        Assert.Equal(expected, Fnv1a64.Hash(input));
    }
}
