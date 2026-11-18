// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Render.Ui;

namespace Nxa.GFlib3.Render.Tests;

public sealed class ConsoleLogTests
{
    [Theory]
    [InlineData("error: failed to load", ConsoleLevel.Error)]
    [InlineData("ERROR: failed", ConsoleLevel.Error)]
    [InlineData("warning: missing texture", ConsoleLevel.Warning)]
    [InlineData("Warning: missing", ConsoleLevel.Warning)]
    [InlineData("Loaded ha_area00", ConsoleLevel.Info)]
    [InlineData("warnings: 2", ConsoleLevel.Info)]
    public void ClassifiesMessagesByPrefix(string message, ConsoleLevel expected)
    {
        Assert.Equal(expected, ConsoleLog.Classify(message));
    }

    [Fact]
    public void FiltersByTextIgnoringCase()
    {
        Assert.True(ConsoleLog.Matches(
            ConsoleLevel.Info, "Loaded ha_area00", "AREA00", true, true, true));
        Assert.False(ConsoleLog.Matches(
            ConsoleLevel.Info, "Loaded ha_area00", "area01", true, true, true));
        Assert.True(ConsoleLog.Matches(
            ConsoleLevel.Info, "Loaded ha_area00", string.Empty, true, true, true));
    }

    [Fact]
    public void HidesDisabledLevels()
    {
        Assert.False(ConsoleLog.Matches(
            ConsoleLevel.Info, "Loaded", string.Empty, false, true, true));
        Assert.False(ConsoleLog.Matches(
            ConsoleLevel.Warning, "warning: x", string.Empty, true, false, true));
        Assert.False(ConsoleLog.Matches(
            ConsoleLevel.Error, "error: x", string.Empty, true, true, false));
        Assert.True(ConsoleLog.Matches(
            ConsoleLevel.Error, "error: x", string.Empty, false, false, true));
    }
}
