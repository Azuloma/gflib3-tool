// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright 2026 the NXAevora Projects

using System.Globalization;
using System.Numerics;
using ImGuiNET;
using Nxa.GFlib3.Core.Formats.Gfpak;
using Nxa.GFlib3.Render.Scene;

namespace Nxa.GFlib3.Render.Ui;

public sealed partial class EditorShell
{
    private const int AssetColumnCount = 5;
    private const double Megabyte = 1024d * 1024d;
    private const double Kilobyte = 1024d;

    private readonly record struct AssetHeaderInfo(GfpakHeader? Header, string? Error);

    private void DrawProject()
    {
        if (!ImGui.Begin(EditorLayout.ProjectWindow))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##projectTabs"))
        {
            if (EditorIcons.BeginTabItem(
                    EditorIcon.PanelInfo,
                    _text.T("project.tab.info"),
                    "projectInfo"))
            {
                DrawProjectInfo();
                ImGui.EndTabItem();
            }

            if (EditorIcons.BeginTabItem(
                    EditorIcon.PanelAssets,
                    _text.T("project.tab.assets"),
                    "projectAssets"))
            {
                DrawProjectAssets();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawProjectInfo()
    {
        DrawKeyValue(_text.T("project.area"), _scene.AreaName);
        DrawKeyValue(_text.T("project.model"), _scene.RoomModelName);
        DrawKeyValue(_text.T("project.dump"), _scene.DumpRoot);
        ImGui.Separator();
        DrawKeyValue(_text.T("project.meshes"), _scene.RoomMeshes.Count.ToString(CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.vertices"), _scene.VertexCount.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.indices"), _scene.IndexCount.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.instances"), _scene.Instances.Count.ToString("N0", CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.textures"), _scene.Textures.Count.ToString(CultureInfo.InvariantCulture));
        DrawKeyValue(_text.T("project.warnings"), WarningCount().ToString(CultureInfo.InvariantCulture));

        if (_scene.Terrain is not null)
        {
            ImGui.Separator();
            float tiling = _scene.TerrainTileScale;
            if (ImGui.SliderFloat(_text.T("project.terrainUv"), ref tiling, 0.25f, 256f, "x%.2f"))
            {
                _scene.TerrainTileScale = tiling;
            }
        }
    }

    private void DrawProjectAssets()
    {
        DrawPackSelectors();
        ImGui.Separator();

        float available = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(available * 0.62f);
        ImGui.InputTextWithHint(
            "##assetFilter",
            _text.T("project.assets.filter"),
            ref _assetFilter,
            128);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(-1f);
        DrawAssetKindFilter();

        List<AssetPack> assets = _catalog.Assets.Where(MatchesAsset).ToList();
        if (assets.Count == 0)
        {
            ImGui.TextDisabled(_text.T("project.assets.empty"));
            return;
        }

        EnsureAssetHeaders(assets);
        DrawAssetTable(assets);
    }

    private void DrawAssetKindFilter()
    {
        string[] labels =
        [
            _text.T("project.assets.kind.all"),
            _text.T("project.assets.kind.baseArea"),
            _text.T("project.assets.kind.subArea"),
            _text.T("project.assets.kind.areaModel"),
            _text.T("project.assets.kind.subAreaModel"),
            _text.T("project.assets.kind.auxiliary"),
            _text.T("project.assets.kind.other"),
        ];

        int selected = Math.Clamp(_assetKindFilter, 0, labels.Length - 1);
        if (ImGui.Combo("##assetKindFilter", ref selected, labels, labels.Length))
        {
            _assetKindFilter = selected;
        }
    }

    private void DrawAssetTable(List<AssetPack> assets)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.ScrollX
            | ImGuiTableFlags.ScrollY
            | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingFixedFit;
        var size = new Vector2(0f, ImGui.GetContentRegionAvail().Y);
        if (!ImGui.BeginTable("##assetTable", AssetColumnCount, flags, size))
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(1, 1);
        ImGui.TableSetupColumn(
            _text.T("project.assets.name"),
            ImGuiTableColumnFlags.WidthFixed,
            130f * EditorTheme.UiScale);
        ImGui.TableSetupColumn(
            _text.T("project.assets.kind"),
            ImGuiTableColumnFlags.WidthFixed,
            92f * EditorTheme.UiScale);
        ImGui.TableSetupColumn(
            _text.T("project.assets.size"),
            ImGuiTableColumnFlags.WidthFixed,
            64f * EditorTheme.UiScale);
        ImGui.TableSetupColumn(
            _text.T("project.assets.entries"),
            ImGuiTableColumnFlags.WidthFixed,
            44f * EditorTheme.UiScale);
        ImGui.TableSetupColumn(
            _text.T("project.assets.pair"),
            ImGuiTableColumnFlags.WidthFixed,
            110f * EditorTheme.UiScale);
        ImGui.TableHeadersRow();

        foreach (AssetPack asset in assets)
        {
            DrawAssetRow(asset);
        }

        ImGui.EndTable();
    }

    private void DrawAssetRow(AssetPack asset)
    {
        ImGui.PushID(asset.FilePath);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();

        bool current = IsCurrentAsset(asset);
        bool pairLoaded = IsPairLoaded(asset);
        if (ImGui.Selectable(
                asset.Name,
                current,
                ImGuiSelectableFlags.AllowDoubleClick))
        {
            // Selection is loading-based; a single click only highlights the row.
        }

        if (ImGui.IsItemHovered())
        {
            if (pairLoaded)
            {
                ImGui.SetTooltip(_text.T("project.assets.current"));
            }
            else if (asset.IsLoadable)
            {
                (string area, string model) = LoadedPair(asset);
                ImGui.SetTooltip($"{_text.T("project.assets.loadHint")}\n{area} / {model}");
            }

            if (asset.IsLoadable
                && !pairLoaded
                && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                RequestAssetLoad(asset);
            }
        }

        DrawCopyMenu(asset.Name);

        ImGui.TableNextColumn();
        EditorIcons.Inline(AssetIcon(asset.Kind));
        ImGui.SameLine(0f, 4f * EditorTheme.UiScale);
        ImGui.TextUnformatted(AssetKindLabel(asset.Kind));

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(FormatSize(asset.SizeBytes));

        ImGui.TableNextColumn();
        DrawAssetEntryCount(asset);

        ImGui.TableNextColumn();
        if (asset.PairName is null)
        {
            ImGui.TextDisabled(_text.T("project.assets.noPair"));
        }
        else
        {
            ImGui.TextUnformatted(asset.PairName);
        }

        ImGui.PopID();
    }

    private void DrawAssetEntryCount(AssetPack asset)
    {
        if (!_assetHeaders.TryGetValue(asset.FilePath, out AssetHeaderInfo info))
        {
            ImGui.TextDisabled("-");
            return;
        }

        if (info.Header is GfpakHeader header)
        {
            ImGui.TextUnformatted(header.FileCount.ToString("N0", CultureInfo.InvariantCulture));
            return;
        }

        ImGui.TextColored(EditorTheme.Error, "!");
        if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(info.Error))
        {
            ImGui.SetTooltip(info.Error);
        }
    }

    private void EnsureAssetHeaders(List<AssetPack> assets)
    {
        foreach (AssetPack asset in assets)
        {
            if (_assetHeaders.ContainsKey(asset.FilePath))
            {
                continue;
            }

            _assetHeaders[asset.FilePath] = ReadAssetHeader(asset);
        }
    }

    private static AssetHeaderInfo ReadAssetHeader(AssetPack asset)
    {
        try
        {
            return new AssetHeaderInfo(GfpakHeader.Open(asset.FilePath), Error: null);
        }
        catch (Exception ex) when (ex is IOException
            or InvalidDataException
            or NotSupportedException
            or UnauthorizedAccessException)
        {
            return new AssetHeaderInfo(null, ex.Message);
        }
    }

    private bool MatchesAsset(AssetPack asset)
    {
        if (_assetKindFilter > 0)
        {
            var kind = (AssetKind)(_assetKindFilter - 1);
            if (asset.Kind != kind)
            {
                return false;
            }
        }

        string filter = _assetFilter.Trim();
        return filter.Length == 0
            || asset.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (asset.PairName is not null
                && asset.PairName.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsCurrentAsset(AssetPack asset)
    {
        return asset.Kind switch
        {
            AssetKind.BaseArea or AssetKind.SubArea =>
                string.Equals(asset.Name, _scene.AreaName, StringComparison.OrdinalIgnoreCase),
            AssetKind.AreaModel or AssetKind.SubAreaModel =>
                string.Equals(asset.Name, _scene.ModelPackName, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private bool IsPairLoaded(AssetPack asset)
    {
        if (!asset.IsLoadable)
        {
            return false;
        }

        (string area, string model) = LoadedPair(asset);
        return string.Equals(area, _scene.AreaName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(model, _scene.ModelPackName, StringComparison.OrdinalIgnoreCase);
    }

    private void RequestAssetLoad(AssetPack asset)
    {
        if (!asset.IsLoadable)
        {
            return;
        }

        (string area, string model) = LoadedPair(asset);
        RequestLoad(area, model);
    }

    private static (string Area, string Model) LoadedPair(AssetPack asset)
    {
        return asset.Kind is AssetKind.BaseArea or AssetKind.SubArea
            ? (asset.Name, asset.PairName!)
            : (asset.PairName!, asset.Name);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= (long)Megabyte)
        {
            return $"{bytes / Megabyte:0.0} MB";
        }

        return bytes >= (long)Kilobyte
            ? $"{bytes / Kilobyte:0.0} KB"
            : $"{bytes} B";
    }

    private static EditorIcon AssetIcon(AssetKind kind) => kind switch
    {
        AssetKind.BaseArea or AssetKind.SubArea => EditorIcon.AssetTerrain,
        AssetKind.AreaModel or AssetKind.SubAreaModel => EditorIcon.AssetModel,
        AssetKind.AuxiliaryModel => EditorIcon.AssetAuxiliary,
        _ => EditorIcon.AssetOther,
    };

    private string AssetKindLabel(AssetKind kind) => kind switch
    {
        AssetKind.BaseArea => _text.T("project.assets.kind.baseArea"),
        AssetKind.SubArea => _text.T("project.assets.kind.subArea"),
        AssetKind.AreaModel => _text.T("project.assets.kind.areaModel"),
        AssetKind.SubAreaModel => _text.T("project.assets.kind.subAreaModel"),
        AssetKind.AuxiliaryModel => _text.T("project.assets.kind.auxiliary"),
        _ => _text.T("project.assets.kind.other"),
    };

    private void DrawPackSelectors()
    {
        bool hasAreas = _catalog.Areas.Count > 0;
        bool hasModels = _catalog.ModelPacks.Count > 0;

        ImGui.TextDisabled(_text.T("project.area"));
        ImGui.BeginDisabled(!hasAreas);
        ImGui.SetNextItemWidth(-1f);
        if (hasAreas && ImGui.BeginCombo("##areaPack", _catalog.Areas[_areaIndex]))
        {
            for (int i = 0; i < _catalog.Areas.Count; i++)
            {
                string area = _catalog.Areas[i];
                bool selected = i == _areaIndex;
                if (ImGui.Selectable(area, selected) && i != _areaIndex)
                {
                    _areaIndex = i;
                    SelectMatchingModel(area);
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.EndDisabled();

        ImGui.TextDisabled(_text.T("project.modelPack"));
        ImGui.BeginDisabled(!hasModels);
        ImGui.SetNextItemWidth(-1f);
        if (hasModels && ImGui.BeginCombo("##modelPack", _catalog.ModelPacks[_modelIndex]))
        {
            for (int i = 0; i < _catalog.ModelPacks.Count; i++)
            {
                bool selected = i == _modelIndex;
                if (ImGui.Selectable(_catalog.ModelPacks[i], selected))
                {
                    _modelIndex = i;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.EndDisabled();

        if (!hasAreas || !hasModels)
        {
            ImGui.TextDisabled(_text.T("project.noPacks"));
            return;
        }

        if (ImGui.Button(_text.T("project.load"), new Vector2(-1f, 0f)))
        {
            RequestLoad(_catalog.Areas[_areaIndex], _catalog.ModelPacks[_modelIndex]);
        }
    }

    private void SelectMatchingModel(string area)
    {
        int match = _catalog.IndexOfModel(area);
        if (match >= 0)
        {
            _modelIndex = match;
        }
    }

    private void SyncPackSelection()
    {
        _areaIndex = Math.Max(_catalog.IndexOfArea(_scene.AreaName), 0);
        _modelIndex = Math.Max(_catalog.IndexOfModel(_scene.ModelPackName), 0);
    }
}
