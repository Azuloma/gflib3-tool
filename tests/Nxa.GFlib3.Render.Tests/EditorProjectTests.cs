// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using System.Text.Json.Nodes;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class EditorProjectTests
{
    private const string SourcePath =
        "bin/field/terrain/test/terrain/area00_0_0_plants_tree01_X1Z2_d0.trins";

    private const string SyntheticPath =
        "bin/field/terrain/test/terrain/area00_0_0_plants_tree01_X3Z4_d0.trins";

    private const string ModelName = "plants_tree01.trmdl";

    [Fact]
    public void JsonRoundTripsModificationsAndSyntheticFiles()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "round-trip.gfproj");
        EditorProject project = CreateProject();

        EditorProject.Write(project, path);

        string json = File.ReadAllText(path);
        Assert.DoesNotContain('\r', json);
        Assert.Contains("\"format\": \"GFlib3.EditorProject\"", json);
        Assert.Contains("\"synthetic\": true", json);

        EditorProject read = EditorProject.Read(path);
        Assert.Equal("test", read.Area);
        Assert.Equal("test-model", read.ModelPack);
        Assert.Equal(2, read.FileCount);
        Assert.Equal(1, read.ModifiedCount);
        Assert.Equal(2, read.AddedCount);
        Assert.Equal(1, read.SyntheticFileCount);

        EditorProjectFile existing = read.Files!.Single(file => file.Path == SourcePath);
        Assert.False(existing.Synthetic);
        Assert.Equal(2, existing.SourceCount);
        Assert.Equal(At(10f, 0f, 0f), existing.Modified![0].Matrix);
        Assert.Equal(At(2f, 0f, 2f), existing.Added![0].Matrix);

        EditorProjectFile synthetic = read.Files!.Single(file => file.Path == SyntheticPath);
        Assert.True(synthetic.Synthetic);
        Assert.Equal(3, synthetic.Cell![0]);
        Assert.Equal(4, synthetic.Cell[1]);
        Assert.Equal(0, synthetic.SourceCount);
    }

    [Fact]
    public void RejectsUnsupportedFormatVersionAndMatrixLength()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "invalid.gfproj");
        EditorProject project = CreateProject();
        project.Version = EditorProject.CurrentVersion + 1;
        File.WriteAllText(path, System.Text.Encoding.UTF8.GetString(EditorProject.Serialize(project)));
        Assert.Throws<InvalidDataException>(() => EditorProject.Read(path));

        project = CreateProject();
        project.Format = "Other";
        File.WriteAllText(path, System.Text.Encoding.UTF8.GetString(EditorProject.Serialize(project)));
        Assert.Throws<InvalidDataException>(() => EditorProject.Read(path));

        string validMatrixJson = System.Text.Encoding.UTF8.GetString(EditorProject.Serialize(CreateProject()));
        string invalidMatrixJson = validMatrixJson.Replace(
            "\"matrix\": [",
            "\"matrix\": [1, ",
            StringComparison.Ordinal);
        File.WriteAllText(path, invalidMatrixJson);
        Assert.Throws<InvalidDataException>(() => EditorProject.Read(path));
    }

    [Fact]
    public void CaptureStoresOnlyDifferencesAndKeepsAppendOrder()
    {
        FieldScene scene = CreateScene(out PlantInstanceFile source, out PlantCellCatalog cells);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor original = scene.Instances[0].Matrix;
        session.SetMatrix(0, At(10f, 0f, 0f));
        session.Commit(0, original, At(10f, 0f, 0f));
        session.AddInstance(source, At(2f, 0f, 2f));

        PlantInstanceFile synthetic = cells.GetOrCreatePending(0, new TerrainCell(3, 4))!;
        cells.Register(synthetic, new TerrainCell(3, 4));
        session.AddInstance(synthetic, At(5f, 0f, 5f));

        EditorProject project = ProjectScene.Capture(scene);

        Assert.Equal(2, project.FileCount);
        EditorProjectFile existing = project.Files!.Single(file => file.Path == SourcePath);
        EditorProjectModification modification = Assert.Single(existing.Modified!);
        Assert.Equal(0, modification.Index);
        Assert.Equal(original, modification.Original);
        Assert.Equal(At(10f, 0f, 0f), modification.Matrix);
        Assert.Single(existing.Added!);

        EditorProjectFile addedFile = project.Files!.Single(file => file.Path == SyntheticPath);
        Assert.True(addedFile.Synthetic);
        Assert.Equal(3, addedFile.Cell![0]);
        Assert.Equal(4, addedFile.Cell[1]);
        Assert.Single(addedFile.Added!);
    }

    [Fact]
    public void ApplyRestoresExistingEditsAndSyntheticFiles()
    {
        FieldScene first = CreateScene(out PlantInstanceFile source, out PlantCellCatalog firstCells);
        var firstSession = new PlantEditSession(first);
        Matrix4x4RowMajor original = first.Instances[0].Matrix;
        firstSession.SetMatrix(0, At(10f, 0f, 0f));
        firstSession.Commit(0, original, At(10f, 0f, 0f));
        firstSession.AddInstance(source, At(2f, 0f, 2f));
        PlantInstanceFile synthetic = firstCells.GetOrCreatePending(0, new TerrainCell(3, 4))!;
        firstCells.Register(synthetic, new TerrainCell(3, 4));
        firstSession.AddInstance(synthetic, At(5f, 0f, 5f));
        EditorProject project = ProjectScene.Capture(first);

        FieldScene second = CreateScene(out PlantInstanceFile secondSource, out PlantCellCatalog secondCells);
        ProjectApplyResult result = ProjectScene.Apply(project, second, secondCells);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.FilesApplied);
        Assert.Equal(1, result.ModifiedApplied);
        Assert.Equal(2, result.AddedApplied);
        Assert.Equal(1, result.SyntheticFiles);
        Assert.Equal(At(10f, 0f, 0f), second.Instances[0].Matrix);
        Assert.Equal(1, secondSource.AddedCount);
        PlantInstanceFile restoredSynthetic = second.InstanceFiles.Single(file => file.IsSynthetic);
        Assert.Equal(SyntheticPath, restoredSynthetic.LogicalPath);
        Assert.Equal(1, restoredSynthetic.AddedCount);
        Assert.Equal(
            At(5f, 0f, 5f),
            second.Instances.Single(instance => ReferenceEquals(instance.SourceFile, restoredSynthetic)).Matrix);
    }

    [Fact]
    public void ApplySkipsChangedSourceMatrixAndMissingFiles()
    {
        EditorProject project = CreateProject();
        project.Files![0].Modified![0].Original = At(99f, 0f, 0f);
        project.Files.Add(new EditorProjectFile
        {
            Path = "bin/field/terrain/test/terrain/missing.trins",
            Model = ModelName,
            SourceCount = 1,
            Synthetic = false,
            Added = [new EditorProjectAddition { Matrix = At(1f, 0f, 1f) }],
        });

        FieldScene scene = CreateScene(out _, out PlantCellCatalog cells);
        ProjectApplyResult result = ProjectScene.Apply(project, scene, cells);

        Assert.Equal(0, result.ModifiedApplied);
        Assert.Equal(2, result.AddedApplied);
        Assert.Contains(result.Warnings, warning => warning.Contains("different source matrix"));
        Assert.Contains(result.Warnings, warning => warning.Contains("is not present"));
    }

    [Fact]
    public void ApplySkipsAdditionsWhenSourceCountDiffers()
    {
        EditorProject project = CreateProject();
        var scene = new FieldScene
        {
            AreaName = "test",
            ModelPackName = "test-model",
            DumpRoot = ".",
            TerrainCellPrefix = "area00_0_0",
            TerrainLogicalFolder = "bin/field/terrain/test/terrain/",
        };
        scene.Plants.Add(new PlantModel { Name = ModelName });
        var source = new PlantInstanceFile
        {
            LogicalPath = SourcePath,
            ModelIndex = 0,
            OriginalCount = 3,
        };
        scene.InstanceFiles.Add(source);
        for (int i = 0; i < 3; i++)
        {
            scene.Instances.Add(new PlantInstance(0, At(i, 0f, 0f))
            {
                SourceFile = source,
                SourceIndex = i,
            });
        }

        var cells = new PlantCellCatalog(scene, new TerrainCellGrid(0f, 1024f, 1024f));
        ProjectApplyResult result = ProjectScene.Apply(project, scene, cells);

        Assert.Equal(1, result.AddedApplied);
        Assert.Contains(result.Warnings, warning => warning.Contains("source count"));
    }

    [Fact]
    public void ApplyRejectsAreaMismatch()
    {
        EditorProject project = CreateProject();
        project.Area = "other";
        FieldScene scene = CreateScene(out _, out PlantCellCatalog cells);

        Assert.Throws<InvalidDataException>(() => ProjectScene.Apply(project, scene, cells));
    }

    [Fact]
    public void SessionTracksDirtyStateAndRoundTripsThroughDisk()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "session.gfproj");
        FieldScene scene = CreateScene(out _, out PlantCellCatalog cells);
        var project = new ProjectSession(scene);
        var session = new PlantEditSession(scene);

        Assert.False(project.IsDirty);
        Matrix4x4RowMajor first = At(10f, 0f, 0f);
        session.SetMatrix(0, first);
        session.Commit(0, scene.Instances[0].OriginalMatrix, first);
        project.MarkEdited();
        project.RefreshDirty(scene);
        Assert.True(project.IsDirty);
        Assert.Equal(1, project.CountUnsavedChanges(scene));

        project.Save(scene, path);
        Assert.False(project.IsDirty);
        Assert.Equal(0, project.CountUnsavedChanges(scene));

        Matrix4x4RowMajor second = At(20f, 0f, 0f);
        session.SetMatrix(0, second);
        session.Commit(0, first, second);
        project.MarkEdited();
        project.RefreshDirty(scene);
        Assert.Equal(1, project.CountUnsavedChanges(scene));
        session.Undo();
        project.RefreshDirty(scene);
        Assert.False(project.IsDirty);

        FieldScene reloaded = CreateScene(out _, out PlantCellCatalog reloadedCells);
        var reopened = new ProjectSession(reloaded);
        ProjectApplyResult result = reopened.Open(reloaded, reloadedCells, path);

        Assert.Equal(1, result.ModifiedApplied);
        Assert.False(reopened.IsDirty);
        Assert.Equal(At(10f, 0f, 0f), reloaded.Instances[0].Matrix);
    }

    [Fact]
    public void UndoAfterSaveMarksDirtyAndRedoClears()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "undo-after-save.gfproj");
        FieldScene scene = CreateScene(out _, out _);
        var project = new ProjectSession(scene);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor start = scene.Instances[0].OriginalMatrix;
        Matrix4x4RowMajor moved = At(10f, 0f, 0f);

        session.SetMatrix(0, moved);
        session.Commit(0, start, moved);
        project.Save(scene, path);
        Assert.False(project.IsDirty);

        session.Undo();
        project.RefreshDirty(scene);
        Assert.True(project.IsDirty);
        Assert.Equal(1, project.CountUnsavedChanges(scene));

        session.Redo();
        project.RefreshDirty(scene);
        Assert.False(project.IsDirty);
    }

    [Fact]
    public void RedoAwayFromSavedBaselineMarksDirty()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "redo-after-save.gfproj");
        FieldScene scene = CreateScene(out _, out _);
        var project = new ProjectSession(scene);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor start = scene.Instances[0].OriginalMatrix;
        Matrix4x4RowMajor moved = At(10f, 0f, 0f);

        project.Save(scene, path);
        Assert.False(project.IsDirty);

        session.SetMatrix(0, moved);
        session.Commit(0, start, moved);
        project.MarkEdited();
        project.RefreshDirty(scene);
        Assert.True(project.IsDirty);

        session.Undo();
        project.RefreshDirty(scene);
        Assert.False(project.IsDirty);

        session.Redo();
        project.RefreshDirty(scene);
        Assert.True(project.IsDirty);
        Assert.Equal(1, project.CountUnsavedChanges(scene));
    }

    [Fact]
    public void RejectsMissingRequiredMembers()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "missing.gfproj");

        AssertMutatedProjectFails(path, root => root.Remove("format"));
        AssertMutatedProjectFails(path, root => root.Remove("files"));
        AssertMutatedProjectFails(path, root => root["files"] = null);
        AssertMutatedProjectFails(
            path,
            root => root["files"]!.AsArray()[0]!.AsObject().Remove("sourceCount"));
        AssertMutatedProjectFails(
            path,
            root => root["files"]!.AsArray()[0]!.AsObject()["modified"]!.AsArray()[0]!
                .AsObject().Remove("index"));
        AssertMutatedProjectFails(
            path,
            root => root["files"]!.AsArray()[0]!.AsObject()["modified"]!.AsArray()[0]!
                .AsObject().Remove("matrix"));
        AssertMutatedProjectFails(
            path,
            root => root["files"]!.AsArray()[0]!.AsObject()["added"]!.AsArray()[0]!
                .AsObject().Remove("matrix"));
    }

    [Fact]
    public void RejectsNullRecords()
    {
        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "nulls.gfproj");

        AssertMutatedProjectFails(path, root => root["files"]!.AsArray()[0] = null);
        AssertMutatedProjectFails(
            path,
            root => root["files"]!.AsArray()[0]!.AsObject()["modified"]!.AsArray()[0] = null);
        AssertMutatedProjectFails(
            path,
            root => root["files"]!.AsArray()[0]!.AsObject()["added"]!.AsArray()[0] = null);
    }

    [Fact]
    public void ReloadReappliesProjectWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        using var temp = new TempDirectory();
        string path = Path.Combine(temp.Path, "dump-area00.gfproj");
        FieldScene first = FieldSceneLoader.Load(dump, "ha_area00", "ha_area00", null);
        var firstCatalog = new PlantCellCatalog(first, TerrainCellGrid.FromSurface(
            Assert.IsType<TerrainSurface>(first.TerrainSurface)));
        var firstSession = new PlantEditSession(first);
        PlantInstance original = first.Instances[0];
        Matrix4x4RowMajor moved = At(
            original.Matrix.Row3.X + 2f,
            original.Matrix.Row3.Y,
            original.Matrix.Row3.Z + 2f);
        firstSession.SetMatrix(0, moved);
        firstSession.Commit(0, original.Matrix, moved);
        firstSession.AddInstance(original.SourceFile!, moved);

        TerrainCell syntheticCell = FindFreeCell(firstCatalog);
        PlantInstanceFile synthetic = firstCatalog.GetOrCreatePending(0, syntheticCell)
            ?? throw new InvalidOperationException("area00 cannot synthesize cell files.");
        firstCatalog.Register(synthetic, syntheticCell);
        firstSession.AddInstance(synthetic, moved);

        var firstProject = new ProjectSession(first);
        firstProject.Save(first, path);

        FieldScene second = FieldSceneLoader.Load(dump, "ha_area00", "ha_area00", null);
        var secondCatalog = new PlantCellCatalog(second, TerrainCellGrid.FromSurface(
            Assert.IsType<TerrainSurface>(second.TerrainSurface)));
        var secondProject = new ProjectSession(second);
        ProjectApplyResult result = secondProject.Open(second, secondCatalog, path);

        Assert.True(result.ModifiedApplied >= 1);
        Assert.True(result.AddedApplied >= 2);
        Assert.True(result.SyntheticFiles >= 1);
        Assert.False(secondProject.IsDirty);
        Assert.Equal(first.Instances.Count, second.Instances.Count);
        Assert.Equal(moved, second.Instances[0].Matrix);
        Assert.Equal(
            moved,
            second.Instances.Single(instance =>
                ReferenceEquals(instance.SourceFile, synthetic)
                || instance.SourceFile?.LogicalPath == synthetic.LogicalPath).Matrix);
    }

    private static void AssertMutatedProjectFails(
        string path,
        Action<JsonObject> mutate)
    {
        EditorProject.Write(CreateProject(), path);
        JsonObject root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        mutate(root);
        File.WriteAllText(path, root.ToJsonString());
        Assert.Throws<InvalidDataException>(() => EditorProject.Read(path));
    }

    private static TerrainCell FindFreeCell(PlantCellCatalog catalog)
    {
        for (int z = 0; z < TerrainCellGrid.CellCount; z++)
        {
            for (int x = 0; x < TerrainCellGrid.CellCount; x++)
            {
                var cell = new TerrainCell(x, z);
                if (!catalog.TryGetFile(0, cell, out _)
                    && catalog.GetOrCreatePending(0, cell) is not null)
                {
                    return cell;
                }
            }
        }

        throw new InvalidOperationException("No free terrain cell for plant model 0.");
    }

    private static EditorProject CreateProject()
    {
        return new EditorProject
        {
            Format = EditorProject.FormatName,
            Version = EditorProject.CurrentVersion,
            Area = "test",
            ModelPack = "test-model",
            Files =
            [
                new EditorProjectFile
                {
                    Path = SourcePath,
                    Model = ModelName,
                    SourceCount = 2,
                    Synthetic = false,
                    Modified =
                    [
                        new EditorProjectModification
                        {
                            Index = 0,
                            Original = At(0f, 0f, 0f),
                            Matrix = At(10f, 0f, 0f),
                        },
                    ],
                    Added =
                    [
                        new EditorProjectAddition { Matrix = At(2f, 0f, 2f) },
                    ],
                },
                new EditorProjectFile
                {
                    Path = SyntheticPath,
                    Model = ModelName,
                    SourceCount = 0,
                    Synthetic = true,
                    Cell = [3, 4],
                    Added =
                    [
                        new EditorProjectAddition { Matrix = At(5f, 0f, 5f) },
                    ],
                },
            ],
        };
    }

    private static FieldScene CreateScene(
        out PlantInstanceFile source,
        out PlantCellCatalog cells)
    {
        var scene = new FieldScene
        {
            AreaName = "test",
            ModelPackName = "test-model",
            DumpRoot = ".",
            TerrainCellPrefix = "area00_0_0",
            TerrainLogicalFolder = "bin/field/terrain/test/terrain/",
        };
        scene.Plants.Add(new PlantModel { Name = ModelName });
        source = new PlantInstanceFile
        {
            LogicalPath = SourcePath,
            ModelIndex = 0,
            OriginalCount = 2,
        };
        scene.InstanceFiles.Add(source);
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 0f))
        {
            SourceFile = source,
            SourceIndex = 0,
        });
        scene.Instances.Add(new PlantInstance(0, At(1f, 0f, 0f))
        {
            SourceFile = source,
            SourceIndex = 1,
        });
        cells = new PlantCellCatalog(scene, new TerrainCellGrid(0f, 1024f, 1024f));
        return scene;
    }

    private static Matrix4x4RowMajor At(float x, float y, float z) =>
        SceneGeometry.Compose(new SceneTransform(new Vector3(x, y, z), 0f, Vector3.One));

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "nxa-gf3-project-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
