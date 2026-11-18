// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Render.Editing;

namespace Nxa.GFlib3.Render.Tests;

public sealed class AppOptionsTests
{
    [Fact]
    public void ParsesPlaceToolAndDuplicateDiagnostic()
    {
        AppOptions options = AppOptions.Parse(
            ["--select-instance", "3", "--tool", "place", "--duplicate-instance", "4"]);

        Assert.Equal(3, options.SelectInstance);
        Assert.Equal(GizmoMode.Place, options.StartTool);
        Assert.Equal(4, options.DuplicateInstance);
    }

    [Fact]
    public void RejectsNegativeDuplicateInstance()
    {
        Assert.Throws<ArgumentException>(
            () => AppOptions.Parse(["--duplicate-instance", "-1"]));
    }
}
