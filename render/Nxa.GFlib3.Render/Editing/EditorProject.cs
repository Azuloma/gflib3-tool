// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Text.Json;
using System.Text.Json.Serialization;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Core.Formats.Terrain;

namespace Nxa.GFlib3.Render.Editing;

/// <summary>Non-destructive per-area vegetation edits stored outside the dump.</summary>
public sealed class EditorProject
{
    public const string FormatName = "GFlib3.EditorProject";
    public const int CurrentVersion = 1;
    public const string Extension = ".gfproj";

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public required string Format { get; set; }

    public required int Version { get; set; }

    public required string Area { get; set; }

    public required string ModelPack { get; set; }

    public EditorProjectTerrain? Terrain { get; set; }

    public required List<EditorProjectFile>? Files { get; set; }

    [JsonIgnore]
    public int FileCount => Files?.Count ?? 0;

    [JsonIgnore]
    public int ModifiedCount => Files?.Sum(file => file.Modified?.Count ?? 0) ?? 0;

    [JsonIgnore]
    public int AddedCount => Files?.Sum(file => file.Added?.Count ?? 0) ?? 0;

    [JsonIgnore]
    public int SyntheticFileCount => Files?.Count(file => file.Synthetic) ?? 0;

    public static EditorProject Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);

        EditorProject? project;
        try
        {
            using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            project = JsonSerializer.Deserialize<EditorProject>(stream, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Project '{fullPath}' is not valid JSON: {ex.Message}",
                ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Failed to read project '{fullPath}': {ex.Message}", ex);
        }

        if (project is null)
        {
            throw new InvalidDataException($"Project '{fullPath}' has no JSON object.");
        }

        EnsureRecords(project, fullPath);
        Normalize(project);
        Validate(project, fullPath);
        return project;
    }

    public static void Write(EditorProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        EnsureRecords(project, fullPath);
        Normalize(project);
        Validate(project, fullPath);

        byte[] bytes = Serialize(project);
        string tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(tempPath, bytes);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(tempPath);
            throw new IOException($"Failed to write project '{fullPath}': {ex.Message}", ex);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    public static byte[] Serialize(EditorProject project)
    {
        ArgumentNullException.ThrowIfNull(project);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(project, Options);
        return RemoveCarriageReturns(bytes);
    }

    private static byte[] RemoveCarriageReturns(byte[] source)
    {
        int carriageReturns = source.AsSpan().Count((byte)0x0D);
        if (carriageReturns == 0)
        {
            return source;
        }

        var output = new byte[source.Length - carriageReturns];
        int offset = 0;
        foreach (byte value in source)
        {
            if (value != 0x0D)
            {
                output[offset++] = value;
            }
        }

        return output;
    }

    public static void ValidateAgainstScene(EditorProject project, string area, string modelPack)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!string.Equals(project.Area, area, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Project area '{project.Area}' does not match the loaded area '{area}'.");
        }

        if (!string.Equals(project.ModelPack, modelPack, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Project model pack '{project.ModelPack}' does not match the loaded model pack "
                + $"'{modelPack}'.");
        }
    }

    private static void EnsureRecords(EditorProject project, string source)
    {
        InvalidDataException Invalid(string message) => new($"Project '{source}' is invalid: {message}");

        if (project.Files is null)
        {
            throw Invalid("files is missing.");
        }

        for (int i = 0; i < project.Files.Count; i++)
        {
            EditorProjectFile? file = project.Files[i];
            if (file is null)
            {
                throw Invalid($"files[{i}] is null.");
            }

            if (file.Modified is { } modified)
            {
                for (int j = 0; j < modified.Count; j++)
                {
                    if (modified[j] is null)
                    {
                        throw Invalid($"files[{i}].modified[{j}] is null.");
                    }
                }
            }

            if (file.Added is { } added)
            {
                for (int j = 0; j < added.Count; j++)
                {
                    if (added[j] is null)
                    {
                        throw Invalid($"files[{i}].added[{j}] is null.");
                    }
                }
            }
        }
    }

    internal static void Normalize(EditorProject project)
    {
        project.Files ??= [];
        project.Files.Sort((left, right) => string.CompareOrdinal(left.Path, right.Path));
        foreach (EditorProjectFile file in project.Files)
        {
            if (file.Modified is { } modified)
            {
                modified.Sort((left, right) => left.Index.CompareTo(right.Index));
                if (modified.Count == 0)
                {
                    file.Modified = null;
                }
            }

            if (file.Added is { Count: 0 })
            {
                file.Added = null;
            }
        }
    }

    private static void Validate(EditorProject project, string source)
    {
        InvalidDataException Invalid(string message) => new($"Project '{source}' is invalid: {message}");

        if (!string.Equals(project.Format, FormatName, StringComparison.Ordinal))
        {
            throw Invalid($"format '{project.Format}' is not '{FormatName}'.");
        }

        if (project.Version != CurrentVersion)
        {
            throw Invalid($"version {project.Version} is not {CurrentVersion}.");
        }

        if (string.IsNullOrWhiteSpace(project.Area))
        {
            throw Invalid("area is empty.");
        }

        if (string.IsNullOrWhiteSpace(project.ModelPack))
        {
            throw Invalid("model pack is empty.");
        }

        if (project.Terrain is { } terrain)
        {
            ValidateTerrain(terrain, Invalid);
        }

        project.Files ??= [];
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (EditorProjectFile file in project.Files)
        {
            ValidateFile(file, paths, Invalid);
        }
    }

    private static void ValidateTerrain(
        EditorProjectTerrain terrain,
        Func<string, InvalidDataException> invalid)
    {
        if (!IsFinite(terrain.WorldOriginX)
            || !IsFinite(terrain.WorldExtentX)
            || !IsFinite(terrain.WorldExtentZ))
        {
            throw invalid("terrain identity contains a non-finite value.");
        }

        if (terrain.WorldExtentX <= 0f || terrain.WorldExtentZ <= 0f)
        {
            throw invalid("terrain extents must be positive.");
        }
    }

    private static void ValidateFile(
        EditorProjectFile file,
        HashSet<string> paths,
        Func<string, InvalidDataException> invalid)
    {
        ValidateLogicalPath(file.Path, "file path", invalid);
        if (!paths.Add(file.Path))
        {
            throw invalid($"file path '{file.Path}' is listed more than once.");
        }

        ValidateLogicalPath(file.Model, "model path", invalid);
        if (file.SourceCount < 0 || file.SourceCount > TrinsFile.MaxInstances)
        {
            throw invalid($"'{file.Path}': source count {file.SourceCount} is out of range.");
        }

        if (file.Synthetic)
        {
            if (file.SourceCount != 0)
            {
                throw invalid($"'{file.Path}': a synthetic file needs source count 0.");
            }

            if (file.Cell is not { Length: 2 } cell
                || cell[0] < 0
                || cell[0] >= TerrainCellGrid.CellCount
                || cell[1] < 0
                || cell[1] >= TerrainCellGrid.CellCount)
            {
                throw invalid($"'{file.Path}': a synthetic file needs a cell inside 0..15.");
            }

            if (file.Modified is { Count: > 0 })
            {
                throw invalid($"'{file.Path}': a synthetic file cannot have modified instances.");
            }
        }
        else if (file.Cell is not null)
        {
            throw invalid($"'{file.Path}': cell is only valid for synthetic files.");
        }

        if ((file.Modified?.Count ?? 0) == 0 && (file.Added?.Count ?? 0) == 0)
        {
            throw invalid($"'{file.Path}' has no changes.");
        }

        var indices = new HashSet<int>();
        if (file.Modified is { } modified)
        {
            foreach (EditorProjectModification change in modified)
            {
                if (change.Index < 0 || change.Index >= file.SourceCount)
                {
                    throw invalid(
                        $"'{file.Path}': modified index {change.Index} is outside 0..{file.SourceCount - 1}.");
                }

                if (!indices.Add(change.Index))
                {
                    throw invalid($"'{file.Path}': modified index {change.Index} is listed twice.");
                }

                ValidateMatrix(change.Original, file.Path, "original", invalid);
                ValidateMatrix(change.Matrix, file.Path, "matrix", invalid);
            }
        }

        if (file.Added is { } added)
        {
            long limit = (long)file.SourceCount + added.Count;
            if (limit > TrinsFile.MaxInstances)
            {
                throw invalid(
                    $"'{file.Path}': {limit} instances exceed the limit of {TrinsFile.MaxInstances}.");
            }

            foreach (EditorProjectAddition addition in added)
            {
                ValidateMatrix(addition.Matrix, file.Path, "added matrix", invalid);
            }
        }
    }

    private static void ValidateMatrix(
        Matrix4x4RowMajor matrix,
        string file,
        string label,
        Func<string, InvalidDataException> invalid)
    {
        if (!IsFinite(matrix.Row0)
            || !IsFinite(matrix.Row1)
            || !IsFinite(matrix.Row2)
            || !IsFinite(matrix.Row3))
        {
            throw invalid($"'{file}': {label} contains a non-finite value.");
        }
    }

    private static void ValidateLogicalPath(
        string path,
        string label,
        Func<string, InvalidDataException> invalid)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw invalid($"{label} is empty.");
        }

        if (path.Contains('\\', StringComparison.Ordinal) || Path.IsPathRooted(path))
        {
            throw invalid($"{label} '{path}' must be RomFS-relative and use forward slashes.");
        }

        foreach (string segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                throw invalid($"{label} '{path}' has an invalid path segment.");
            }
        }
    }

    private static bool IsFinite(Vector4f value) =>
        float.IsFinite(value.X)
        && float.IsFinite(value.Y)
        && float.IsFinite(value.Z)
        && float.IsFinite(value.W);

    private static bool IsFinite(float value) => float.IsFinite(value);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        options.Converters.Add(new Matrix4x4RowMajorJsonConverter());
        return options;
    }

    private sealed class Matrix4x4RowMajorJsonConverter : JsonConverter<Matrix4x4RowMajor>
    {
        public override Matrix4x4RowMajor Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException("A matrix must be an array of 16 floats.");
            }

            Span<float> values = stackalloc float[16];
            int count = 0;
            while (true)
            {
                if (!reader.Read())
                {
                    throw new JsonException("A matrix array ended unexpectedly.");
                }

                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.Number || count >= values.Length)
                {
                    throw new JsonException("A matrix must contain exactly 16 floats.");
                }

                values[count++] = reader.GetSingle();
            }

            if (count != values.Length)
            {
                throw new JsonException($"A matrix needs 16 floats, found {count}.");
            }

            return new Matrix4x4RowMajor(
                new Vector4f(values[0], values[1], values[2], values[3]),
                new Vector4f(values[4], values[5], values[6], values[7]),
                new Vector4f(values[8], values[9], values[10], values[11]),
                new Vector4f(values[12], values[13], values[14], values[15]));
        }

        public override void Write(
            Utf8JsonWriter writer,
            Matrix4x4RowMajor value,
            JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            WriteRow(writer, value.Row0);
            WriteRow(writer, value.Row1);
            WriteRow(writer, value.Row2);
            WriteRow(writer, value.Row3);
            writer.WriteEndArray();
        }

        private static void WriteRow(Utf8JsonWriter writer, Vector4f row)
        {
            writer.WriteNumberValue(row.X);
            writer.WriteNumberValue(row.Y);
            writer.WriteNumberValue(row.Z);
            writer.WriteNumberValue(row.W);
        }
    }
}

public sealed class EditorProjectTerrain
{
    public required string CellPrefix { get; set; }

    public required string LogicalFolder { get; set; }

    public required float WorldOriginX { get; set; }

    public required float WorldExtentX { get; set; }

    public required float WorldExtentZ { get; set; }
}

public sealed class EditorProjectFile
{
    public required string Path { get; set; }

    public required string Model { get; set; }

    public required int SourceCount { get; set; }

    public required bool Synthetic { get; set; }

    public int[]? Cell { get; set; }

    public List<EditorProjectModification>? Modified { get; set; }

    public List<EditorProjectAddition>? Added { get; set; }
}

public sealed class EditorProjectModification
{
    public required int Index { get; set; }

    public required Matrix4x4RowMajor Original { get; set; }

    public required Matrix4x4RowMajor Matrix { get; set; }
}

public sealed class EditorProjectAddition
{
    public required Matrix4x4RowMajor Matrix { get; set; }
}
