// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Formats.Sarc;

/// <summary>Normalizes SARC entry names into safe relative paths.</summary>
public static class SarcPath
{
    /// <summary>
    /// Converts an entry name to a relative path below an extraction root.
    /// Rejects rooted paths, drive qualifiers, and <c>.</c>/<c>..</c> segments.
    /// </summary>
    /// <exception cref="InvalidDataException">The name is not a safe relative path.</exception>
    public static string GetSafeRelativePath(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Length == 0 || name[0] is '/' or '\\' || name.Contains(':'))
        {
            throw new InvalidDataException($"Unsafe SARC entry name '{name}'.");
        }

        string normalized = name.Replace('\\', '/');
        foreach (char c in normalized)
        {
            if (char.IsControl(c))
            {
                throw new InvalidDataException($"Unsafe SARC entry name '{name}'.");
            }
        }

        string[] segments = normalized.Split('/');
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                throw new InvalidDataException($"Unsafe SARC entry name '{name}'.");
            }
        }

        return Path.Combine(segments);
    }
}
