// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Reflection;
using System.Text.Json;

namespace Nxa.GFlib3.Render.Localization;

/// <summary>Runtime English/Japanese labels loaded from embedded keyed JSON files.</summary>
public sealed class Localizer
{
    private const string DefaultLanguage = "en";

    private readonly Dictionary<string, Dictionary<string, string>> _languages = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _current;

    public Localizer()
    {
        _languages["en"] = LoadEmbedded("en.json");
        _languages["ja"] = LoadEmbedded("ja.json");

        Language = DefaultLanguage;
        _current = _languages[DefaultLanguage];
        MissingKeys = CompareKeys();
    }

    public string Language { get; private set; }

    public static IReadOnlyList<string> AvailableLanguages { get; } = new[] { "en", "ja" };

    public IReadOnlyList<string> MissingKeys { get; }

    public void SetLanguage(string language)
    {
        if (!_languages.TryGetValue(language, out Dictionary<string, string>? values))
        {
            throw new ArgumentException($"Unsupported language '{language}'.", nameof(language));
        }

        Language = language;
        _current = values;
    }

    public string T(string key)
    {
        if (_current.TryGetValue(key, out string? value))
        {
            return value;
        }

        if (_languages["en"].TryGetValue(key, out string? fallback))
        {
            return fallback;
        }

        return key;
    }

    private static Dictionary<string, string> LoadEmbedded(string fileName)
    {
        Assembly assembly = typeof(Localizer).Assembly;
        string? resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("Localization." + fileName, StringComparison.Ordinal));
        if (resource is null)
        {
            throw new InvalidOperationException($"Embedded localization resource '{fileName}' is missing.");
        }

        using Stream stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Embedded localization resource '{resource}' cannot be opened.");
        Dictionary<string, string>? values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        if (values is null || values.Count == 0)
        {
            throw new InvalidDataException($"Localization resource '{fileName}' is empty.");
        }

        return values;
    }

    private List<string> CompareKeys()
    {
        var missing = new List<string>();
        if (!_languages.TryGetValue("en", out Dictionary<string, string>? english)
            || !_languages.TryGetValue("ja", out Dictionary<string, string>? japanese))
        {
            return missing;
        }

        missing.AddRange(english.Keys.Where(key => !japanese.ContainsKey(key)).Select(key => "ja:" + key));
        missing.AddRange(japanese.Keys.Where(key => !english.ContainsKey(key)).Select(key => "en:" + key));
        return missing;
    }
}
