// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using Nxa.GFlib3.Core.FlatBuffers;

namespace Nxa.GFlib3.Core.Formats.Trinity;

internal static class TrinityParse
{
    public static T[] TableVector<T>(FlatBufferTable table, int field, Func<FlatBufferTable, T> parse)
    {
        if (!table.TryGetVector(field, 4, out FlatBufferVector vector))
        {
            return Array.Empty<T>();
        }

        var result = new T[vector.Count];
        for (int i = 0; i < result.Length; i++)
        {
            try
            {
                result[i] = parse(vector.GetTable(i));
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Field {field}, entry {i}: {ex.Message}", ex);
            }
        }

        return result;
    }

    public static string[] StringVector(FlatBufferTable table, int field)
    {
        if (!table.TryGetVector(field, 4, out FlatBufferVector vector))
        {
            return Array.Empty<string>();
        }

        var result = new string[vector.Count];
        for (int i = 0; i < result.Length; i++)
        {
            try
            {
                result[i] = vector.GetString(i);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"Field {field}, entry {i}: {ex.Message}", ex);
            }
        }

        return result;
    }

    public static BoundingBox? Bounds(FlatBufferTable table, int field)
    {
        FlatBufferTable? bounds = table.GetTable(field);
        if (bounds is null)
        {
            return null;
        }

        FlatBufferTable value = bounds.Value;
        return new BoundingBox(value.GetVector3f(0), value.GetVector3f(1));
    }

    public static Srt Srt(FlatBufferTable table)
    {
        return new Srt(table.GetVector3f(0), table.GetVector3f(1), table.GetVector3f(2));
    }
}
