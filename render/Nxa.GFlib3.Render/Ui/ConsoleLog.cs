// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Render.Ui;

/// <summary>Severity of a console message, derived from its prefix.</summary>
public enum ConsoleLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>Prefix classification and filtering for scene console messages.</summary>
public static class ConsoleLog
{
    private const string WarningPrefix = "warning:";
    private const string ErrorPrefix = "error:";

    public static ConsoleLevel Classify(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.StartsWith(ErrorPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return ConsoleLevel.Error;
        }

        return message.StartsWith(WarningPrefix, StringComparison.OrdinalIgnoreCase)
            ? ConsoleLevel.Warning
            : ConsoleLevel.Info;
    }

    public static bool Matches(
        ConsoleLevel level,
        string message,
        string filter,
        bool showInfo,
        bool showWarnings,
        bool showErrors)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(filter);

        bool enabled = level switch
        {
            ConsoleLevel.Info => showInfo,
            ConsoleLevel.Warning => showWarnings,
            ConsoleLevel.Error => showErrors,
            _ => false,
        };
        if (!enabled)
        {
            return false;
        }

        return filter.Length == 0
            || message.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }
}
