// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

namespace Nxa.GFlib3.Core.Formats.Gfpak;

/// <summary>A file reference inside a folder hash table.</summary>
public readonly record struct GfpakFolderFile(ulong NameHash, int FileIndex);

/// <summary>A folder hash table inside a GFLXPACK header.</summary>
public sealed class GfpakFolder
{
    internal GfpakFolder(ulong nameHash, GfpakFolderFile[] files)
    {
        NameHash = nameHash;
        Files = files;
    }

    public ulong NameHash { get; }

    public IReadOnlyList<GfpakFolderFile> Files { get; }
}
