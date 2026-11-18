// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using System.Numerics;
using ImGuiNET;
using Nxa.GFlib3.Core.FlatBuffers;
using Nxa.GFlib3.Render.Editing;

namespace Nxa.GFlib3.Render.Ui;

public sealed partial class EditorShell
{
    private const int HistoryDisplayLimit = 100;

    private void DrawHistory()
    {
        int applied = _editSession.HistoryPosition;
        int redoable = _editSession.History.Count - applied;
        ImGui.TextDisabled(
            $"{_text.T("history.undoDepth")}: {applied}   {_text.T("history.redoDepth")}: {redoable}");
        ImGui.Separator();

        if (_editSession.History.Count == 0)
        {
            ImGui.TextDisabled(_text.T("history.empty"));
            return;
        }

        ImGui.BeginChild("##historyList", new Vector2(0f, 0f));
        int first = Math.Max(0, _editSession.History.Count - HistoryDisplayLimit);
        for (int i = first; i < _editSession.History.Count; i++)
        {
            PlantTransformChange change = _editSession.History[i];
            string line = HistoryLine(change, i);
            if (i < applied)
            {
                ImGui.BulletText(line);
            }
            else
            {
                ImGui.TextDisabled($"    {line}");
            }
        }

        ImGui.EndChild();
    }

    private static string HistoryLine(PlantTransformChange change, int index)
    {
        string before = FormatPosition(change.Before);
        string after = FormatPosition(change.After);
        return $"#{index + 1}  instance {change.InstanceIndex}: {before} -> {after}";
    }

    private static string FormatPosition(Matrix4x4RowMajor matrix) =>
        $"{matrix.Row3.X.ToString("0.##", CultureInfo.InvariantCulture)}, "
        + $"{matrix.Row3.Y.ToString("0.##", CultureInfo.InvariantCulture)}, "
        + $"{matrix.Row3.Z.ToString("0.##", CultureInfo.InvariantCulture)}";
}
