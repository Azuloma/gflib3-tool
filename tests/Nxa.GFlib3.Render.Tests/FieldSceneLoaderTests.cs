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

    [Fact]
    public void LoadsSubPackRoomFromResidentPlacementOnce()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, "ha_area00_s07", "ha_area00_s07", null);

        Assert.Empty(scene.RoomMeshes);
        int roomParts = scene.PlacedMeshes.Count(
            mesh => mesh.Name.StartsWith("room_area00_s07", StringComparison.OrdinalIgnoreCase));
        Assert.True(roomParts > 0);
        Assert.Equal(scene.PlacedMeshes.Count, roomParts);
        Assert.All(scene.Materials, material => Assert.False(material.UseVertexColor));
    }

    [Fact]
    public void ExplicitMeshFilterWinsOverSubPackRoomMatch()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, "ha_area01_s01", "ha_area01", "ground_area01_cave01");

        Assert.Contains("ground_area01_cave01", scene.RoomModelName);
        Assert.NotEmpty(scene.RoomMeshes);
    }

    [Fact]
    public void SkipsMissingSubPackRoomWithoutFallback()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, "ha_area03_s01", "ha_area03", null);

        Assert.Empty(scene.RoomMeshes);
        Assert.Contains(
            scene.Messages,
            message => message.Contains("no 'room_area03_s01.trmsh'", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadsVertexColorFlagFromMaterialShaders()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, "ha_area01_s01", "ha_area01", null);

        Assert.Contains(scene.Materials, material => material.UseVertexColor);
    }

    [Fact]
    public void TreatsZeroMeshSceneModelsAsEmpty()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        FieldScene scene = FieldSceneLoader.Load(dump, "ha_area01", "ha_area01", null);

        Assert.Equal(585, scene.PlacedMeshes.Count);
        Assert.Contains(
            "scene: 486 area scenes, 585 placed parts, 2 empty models, 0 unresolved models (resident_release.gfpak).",
            scene.Messages);
        Assert.DoesNotContain(
            scene.Messages,
            message => message.Contains("is not a Trinity file", StringComparison.Ordinal));
    }
}
