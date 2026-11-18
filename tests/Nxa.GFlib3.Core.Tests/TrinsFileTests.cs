// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Buffers.Binary;
using System.Collections;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Core.Tests;

public sealed class TrinsFileTests
{
    [Fact]
    public void WritesSyntheticRoundTrip()
    {
        Matrix4x4RowMajor[] matrices =
        [
            At(1f, 2f, 3f),
            At(4f, 5f, 6f),
        ];
        TrinsFile file = TrinsFile.Create(
            TrinsFile.SrtInstanceBufferType,
            1,
            0,
            matrices);

        TrinsFile parsed = TrinsFile.Parse(file.ToBytes());

        Assert.Equal(TrinsFile.SrtInstanceBufferType, parsed.TypeName);
        Assert.Equal(1u, parsed.Version);
        Assert.Equal(2u, parsed.Count);
        Assert.Equal(0u, parsed.Reserved);
        Assert.Equal(matrices, parsed.Instances);
    }

    [Fact]
    public void WritesZeroInstanceRoundTrip()
    {
        TrinsFile file = TrinsFile.Create(
            TrinsFile.SrtInstanceBufferType,
            1,
            0,
            []);

        TrinsFile parsed = TrinsFile.Parse(file.ToBytes());

        Assert.Equal(0u, parsed.Count);
        Assert.Empty(parsed.Instances);
    }

    [Fact]
    public void WithInstanceIncrementsCountAndAppendsMatrix()
    {
        Matrix4x4RowMajor first = At(1f, 2f, 3f);
        Matrix4x4RowMajor second = At(4f, 5f, 6f);
        TrinsFile file = TrinsFile.Create(
            TrinsFile.SrtInstanceBufferType,
            1,
            0,
            [first]);

        TrinsFile appended = file.WithInstance(second);
        TrinsFile parsed = TrinsFile.Parse(appended.ToBytes());

        Assert.Equal(1u, file.Count);
        Assert.Equal(2u, appended.Count);
        Assert.Equal(2u, parsed.Count);
        Assert.Equal(first, parsed.Instances[0]);
        Assert.Equal(second, parsed.Instances[1]);
    }

    [Fact]
    public void WriterRejectsReservedLayout()
    {
        TrinsFile file = TrinsFile.Create(
            TrinsFile.SrtInstanceBufferType,
            1,
            1,
            []);

        Assert.Throws<NotSupportedException>(() => file.ToBytes());
    }

    [Fact]
    public void CreateRejectsExcessiveInstanceCount()
    {
        var instances = new HugeMatrixList(TrinsFile.MaxInstances + 1);

        Assert.Throws<InvalidDataException>(() => TrinsFile.Create(
            TrinsFile.SrtInstanceBufferType,
            1,
            0,
            instances));
    }

    [Fact]
    public void RejectsEmptyBuffer()
    {
        Assert.Throws<InvalidDataException>(() => TrinsFile.Parse(Array.Empty<byte>()));
    }

    [Fact]
    public void RejectsOutOfRangeRootOffset()
    {
        var data = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(data, 0x7FFFFFFF);

        Assert.Throws<InvalidDataException>(() => TrinsFile.Parse(data));
    }

    [Fact]
    public void ParsesTerrainInstancesWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "terrain", "ha_area00.gfpak");
        if (!File.Exists(path))
        {
            return;
        }

        using var archive = GfpakArchive.Open(path);
        int files = 0;
        int instances = 0;

        foreach (GfpakEntry entry in archive.Entries)
        {
            byte[] data = archive.ReadEntry(entry);
            if (data.AsSpan().IndexOf("SrtInstanceBuffer"u8) < 0)
            {
                continue;
            }

            TrinsFile trins = TrinsFile.Parse(data);
            Assert.Equal(TrinsFile.SrtInstanceBufferType, trins.TypeName);
            Assert.Equal(1u, trins.Version);
            Assert.Equal(trins.Count, (uint)trins.Instances.Count);

            foreach (var matrix in trins.Instances)
            {
                Assert.Equal(0f, matrix.Row0.Y);
                Assert.Equal(0f, matrix.Row0.W);
                Assert.Equal(0f, matrix.Row1.X);
                Assert.Equal(0f, matrix.Row1.Z);
                Assert.Equal(0f, matrix.Row1.W);
                Assert.Equal(0f, matrix.Row2.Y);
                Assert.Equal(0f, matrix.Row2.W);
                Assert.Equal(1f, matrix.Row3.W);
            }

            files++;
            instances += trins.Instances.Count;
        }

        Assert.Equal(25, files);
        Assert.Equal(60, instances);
    }

    [Fact]
    public void RebuildsTerrainInstancesByteForByteWhenDumpIsConfigured()
    {
        string? dump = Environment.GetEnvironmentVariable("NXA_GF3_DUMP");
        if (string.IsNullOrWhiteSpace(dump))
        {
            return;
        }

        string path = Path.Combine(
            dump, "RomFS", "bin", "archive", "field", "terrain", "ha_area00.gfpak");
        if (!File.Exists(path))
        {
            return;
        }

        using var archive = GfpakArchive.Open(path);
        int files = 0;
        foreach (GfpakEntry entry in archive.Entries)
        {
            byte[] data = archive.ReadEntry(entry);
            if (data.AsSpan().IndexOf("SrtInstanceBuffer"u8) < 0)
            {
                continue;
            }

            byte[] rebuilt = TrinsFile.Parse(data).ToBytes();
            Assert.True(
                rebuilt.AsSpan().SequenceEqual(data),
                $"entry {entry.Index} did not rebuild byte-for-byte.");
            files++;
        }

        Assert.Equal(25, files);
    }

    private static Matrix4x4RowMajor At(float x, float y, float z) => new(
        new Vector4f(1f, 0f, 0f, 0f),
        new Vector4f(0f, 1f, 0f, 0f),
        new Vector4f(0f, 0f, 1f, 0f),
        new Vector4f(x, y, z, 1f));

    private sealed class HugeMatrixList(int count) : IReadOnlyList<Matrix4x4RowMajor>
    {
        public int Count { get; } = count;

        public Matrix4x4RowMajor this[int index] =>
            throw new ArgumentOutOfRangeException(nameof(index));

        public IEnumerator<Matrix4x4RowMajor> GetEnumerator() =>
            throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
