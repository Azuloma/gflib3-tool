// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Numerics;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;
using Nxa.GFlib3.Render.Editing;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Tests;

public sealed class PlantEditSessionTests
{
    [Fact]
    public void CommitAppliesAndSupportsUndoRedo()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor moved = At(1f, 2f, 3f);

        session.SetMatrix(0, moved);
        session.Commit(0, start, moved);

        Assert.Equal(moved, scene.Instances[0].Matrix);
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);

        Assert.True(session.Undo());
        Assert.Equal(start, scene.Instances[0].Matrix);
        Assert.True(session.CanRedo);
        Assert.Equal(0, session.HistoryPosition);

        Assert.True(session.Redo());
        Assert.Equal(moved, scene.Instances[0].Matrix);
        Assert.Equal(1, session.HistoryPosition);
    }

    [Fact]
    public void NoOpCommitDoesNotRecordHistory()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);

        session.Commit(0, start, start);

        Assert.False(session.CanUndo);
        Assert.Equal(0, session.EditCount);
    }

    [Fact]
    public void NewCommitAfterUndoDropsRedoTail()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor first = At(1f, 0f, 0f);
        Matrix4x4RowMajor second = At(2f, 0f, 0f);

        session.SetMatrix(0, first);
        session.Commit(0, start, first);
        session.Undo();

        session.SetMatrix(0, second);
        session.Commit(0, start, second);

        Assert.False(session.CanRedo);
        Assert.Equal(1, session.EditCount);
        Assert.Equal(second, scene.Instances[0].Matrix);
    }

    [Fact]
    public void HistoryIsCapped()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor previous = start;

        for (int i = 0; i < PlantEditSession.MaxHistoryEntries + 40; i++)
        {
            Matrix4x4RowMajor next = At(i + 1f, 0f, 0f);
            session.SetMatrix(0, next);
            session.Commit(0, previous, next);
            previous = next;
        }

        Assert.Equal(PlantEditSession.MaxHistoryEntries, session.EditCount);
        Assert.Equal(PlantEditSession.MaxHistoryEntries, session.HistoryPosition);
        Assert.True(session.Undo());
    }

    [Fact]
    public void DirtyCountTracksOriginalMatrix()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor moved = At(4f, 5f, 6f);

        Assert.Equal(0, session.DirtyCount);

        session.SetMatrix(0, moved);
        Assert.Equal(1, session.DirtyCount);

        session.Commit(0, start, moved);
        Assert.Equal(1, session.DirtyCount);

        session.Undo();
        Assert.Equal(0, session.DirtyCount);

        session.Redo();
        Assert.Equal(1, session.DirtyCount);
    }

    [Fact]
    public void NoOpCommitPreservesRedoTail()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor moved = At(1f, 0f, 0f);

        session.SetMatrix(0, moved);
        session.Commit(0, start, moved);
        session.Undo();
        Assert.True(session.CanRedo);

        session.Commit(0, start, start);

        Assert.True(session.CanRedo);
        Assert.Equal(1, session.EditCount);
    }

    [Fact]
    public void UndoWalksCappedHistoryToOldestRetainedBeforeState()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor previous = start;

        for (int i = 0; i < PlantEditSession.MaxHistoryEntries + 40; i++)
        {
            Matrix4x4RowMajor next = At(i + 1f, 0f, 0f);
            session.SetMatrix(0, next);
            session.Commit(0, previous, next);
            previous = next;
        }

        for (int i = 0; i < PlantEditSession.MaxHistoryEntries; i++)
        {
            Assert.True(session.Undo());
        }

        Assert.False(session.CanUndo);
        Assert.Equal(40f, scene.Instances[0].Matrix.Row3.X, 3);

        // The 40 edits dropped by the cap cannot be undone, so the instance
        // still differs from its session-start matrix.
        Assert.Equal(1, session.DirtyCount);
    }

    [Fact]
    public void RejectsOutOfRangeInstance()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);

        Assert.Throws<ArgumentOutOfRangeException>(() => session.SetMatrix(9, start));
    }

    [Fact]
    public void RejectsNonFiniteMatrix()
    {
        FieldScene scene = CreateScene(out Matrix4x4RowMajor start);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor invalid = NonFinite();

        Assert.Throws<ArgumentException>(() => session.SetMatrix(0, invalid));
    }

    [Fact]
    public void AddInstanceAppendsAndSupportsUndoRedo()
    {
        FieldScene scene = CreateSceneWithFile(out PlantInstanceFile file);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor added = At(10f, 20f, 30f);
        int revision = scene.InstanceRevision;

        int index = session.AddInstance(file, added);

        Assert.Equal(1, index);
        Assert.Equal(2, scene.Instances.Count);
        Assert.Equal(1, file.AddedCount);
        Assert.Equal(1, session.DirtyCount);
        Assert.Equal(revision + 1, scene.InstanceRevision);
        Assert.IsType<PlantAddCommand>(session.History[0]);
        Assert.Same(file, scene.Instances[index].SourceFile);
        Assert.Equal(1, scene.Instances[index].SourceIndex);
        Assert.Equal(added, scene.Instances[index].Matrix);
        Assert.True(session.CanUndo);
        Assert.False(session.CanRedo);

        Assert.True(session.Undo());
        Assert.Single(scene.Instances);
        Assert.Equal(0, file.AddedCount);
        Assert.Equal(0, session.DirtyCount);
        Assert.Equal(revision + 2, scene.InstanceRevision);
        Assert.True(session.CanRedo);

        Assert.True(session.Redo());
        Assert.Equal(2, scene.Instances.Count);
        Assert.Same(file, scene.Instances[1].SourceFile);
        Assert.Equal(1, scene.Instances[1].SourceIndex);
        Assert.Equal(1, file.AddedCount);
        Assert.Equal(1, session.DirtyCount);
        Assert.Equal(revision + 3, scene.InstanceRevision);
    }

    [Fact]
    public void AddedInstanceTransformDoesNotDoubleCountDirty()
    {
        FieldScene scene = CreateSceneWithFile(out PlantInstanceFile file);
        var session = new PlantEditSession(scene);
        Matrix4x4RowMajor before = At(5f, 0f, 0f);
        Matrix4x4RowMajor after = At(6f, 0f, 0f);
        int index = session.AddInstance(file, before);

        session.SetMatrix(index, after);
        session.Commit(index, before, after);

        Assert.Equal(2, session.EditCount);
        Assert.Equal(1, session.DirtyCount);
        Assert.True(session.Undo());
        Assert.Equal(before, scene.Instances[index].Matrix);
        Assert.Equal(1, session.DirtyCount);
        Assert.True(session.Undo());
        Assert.Single(scene.Instances);
        Assert.Equal(0, session.DirtyCount);
        Assert.True(session.Redo());
        Assert.Equal(1, session.DirtyCount);
        Assert.True(session.Redo());
        Assert.Equal(after, scene.Instances[index].Matrix);
        Assert.Equal(1, session.DirtyCount);
    }

    [Fact]
    public void AddInstanceRejectsForeignFileAndNonFiniteMatrix()
    {
        FieldScene scene = CreateSceneWithFile(out PlantInstanceFile file);
        var session = new PlantEditSession(scene);
        var foreign = new PlantInstanceFile
        {
            LogicalPath = "bin/field/terrain/foreign/terrain/foreign.trins",
            ModelIndex = 0,
            OriginalCount = 0,
        };

        Assert.Throws<ArgumentException>(() => session.AddInstance(foreign, At(0f, 0f, 0f)));
        Assert.Throws<ArgumentException>(() => session.AddInstance(file, NonFinite()));
        Assert.Single(scene.Instances);
        Assert.Equal(0, file.AddedCount);
        Assert.Equal(0, session.DirtyCount);
    }

    [Fact]
    public void AddAfterUndoDropsRedoTail()
    {
        FieldScene scene = CreateSceneWithFile(out PlantInstanceFile file);
        var session = new PlantEditSession(scene);
        session.AddInstance(file, At(1f, 0f, 0f));
        session.Undo();
        Assert.True(session.CanRedo);

        int index = session.AddInstance(file, At(2f, 0f, 0f));

        Assert.Equal(1, index);
        Assert.False(session.CanRedo);
        Assert.Equal(2, scene.Instances.Count);
        Assert.Equal(1, file.AddedCount);
        Assert.Equal(2f, scene.Instances[index].Matrix.Row3.X);
        Assert.Equal(1, session.DirtyCount);
    }

    [Fact]
    public void AddInstanceRejectsSourceAtLimit()
    {
        FieldScene scene = CreateSceneWithFile(out _);
        var fullFile = new PlantInstanceFile
        {
            LogicalPath = "bin/field/terrain/full/terrain/full.trins",
            ModelIndex = 0,
            OriginalCount = TrinsFile.MaxInstances,
        };
        scene.InstanceFiles.Add(fullFile);
        var session = new PlantEditSession(scene);

        Assert.Throws<InvalidDataException>(() => session.AddInstance(fullFile, At(0f, 0f, 0f)));
    }

    private static FieldScene CreateSceneWithFile(out PlantInstanceFile file)
    {
        var scene = new FieldScene
        {
            AreaName = "test",
            ModelPackName = "test",
            DumpRoot = ".",
        };
        scene.Plants.Add(new PlantModel { Name = "plant" });
        file = new PlantInstanceFile
        {
            LogicalPath = "bin/field/terrain/test/terrain/test.trins",
            ModelIndex = 0,
            OriginalCount = 1,
        };
        scene.InstanceFiles.Add(file);
        scene.Instances.Add(new PlantInstance(0, At(0f, 0f, 0f))
        {
            SourceFile = file,
            SourceIndex = 0,
        });
        return scene;
    }

    private static Matrix4x4RowMajor NonFinite() => new(
        new Vector4f(float.NaN, 0f, 0f, 0f),
        new Vector4f(0f, 1f, 0f, 0f),
        new Vector4f(0f, 0f, 1f, 0f),
        new Vector4f(0f, 0f, 0f, 1f));

    private static FieldScene CreateScene(out Matrix4x4RowMajor start)
    {
        var scene = new FieldScene
        {
            AreaName = "test",
            ModelPackName = "test",
            DumpRoot = ".",
        };
        scene.Plants.Add(new PlantModel { Name = "plant" });
        start = At(0f, 0f, 0f);
        scene.Instances.Add(new PlantInstance(0, start));
        return scene;
    }

    private static Matrix4x4RowMajor At(float x, float y, float z) =>
        SceneGeometry.Compose(new SceneTransform(new Vector3(x, y, z), 0f, Vector3.One));
}
