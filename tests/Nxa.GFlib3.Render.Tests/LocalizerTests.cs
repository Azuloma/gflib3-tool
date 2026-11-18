// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Render.Localization;

namespace Nxa.GFlib3.Render.Tests;

public sealed class LocalizerTests
{
    [Fact]
    public void EnglishAndJapaneseKeysMatch()
    {
        var localizer = new Localizer();

        Assert.Empty(localizer.MissingKeys);
    }
}
