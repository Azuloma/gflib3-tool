// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class FieldSceneLoaderTests
{
    [Fact]
    public void KeepsSameNamedMaterialsFromDifferentFilesDistinct()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, "ha_area05", "ha_area05", null);

        List<SceneMaterial> water = scene.Materials
            .Where(material => material.Name == "ground_area05_water01_01")
            .ToList();
        Assert.True(water.Count >= 2);
        Assert.Equal(water.Count, water.Select(material => material.Key).Distinct().Count());
        Assert.Contains(water, material => material.BaseColorTexture >= 0);
        Assert.Contains(water, material => material.BaseColorTexture < 0);
    }
}
