# Editor UI

The viewer uses Dear ImGui through Silk.NET and ImGui.NET. The editor shell
follows the Unity 2019 dark editor as the visual reference: neutral gray panels,
compact metrics, icon toolbars, tabbed docks, and a slim status bar.

## Phase 2a scope

Phase 1 established the palette, metrics, dock layout, toolbar, and status bar.
Phase 2a adds the read-only selection and inspection layer:

- Typed selection (`RoomMesh`, `PlacedGroup`, `PlacedMesh`, `PlantModel`,
  `PlantInstance`, `Terrain`), cleared when a scene reloads.
- Hierarchy tree grouped by object name, with a text filter and expandable
  placed-object and plant-instance lists.
- Component Inspector with Transform, Geometry, Material, Instances, and
  Terrain sections. Fields remain read-only until Stage 5.
- Frame selection: `F`, the toolbar button, and the Inspector button frame the
  selected object; with no selection they frame the scene.

The Project panel still uses the Phase 1 Area/Model selectors. The asset
browser, console toolbar, and tab/section icons belong to Phase 2b.

## Layout

```text
+ Menu bar --------------------------------------------------+
| Toolbar (tools, separator, frame)                          |
| Hierarchy 20%  | Scene (tab)                58% | Inspector |
|                |                                 |   22%     |
| Project 20%    | Console (tabs) 32% height       |           |
+ Status bar 27.5px -----------------------------------------+
```

- Left column: Hierarchy 45% height, Project 55%.
- Center column: Scene 68% height, Console 32%.
- Inspector spans the full workspace height.
- The status bar is drawn on the foreground draw list below the dock area.

Initial split ratios are built with `DockBuilder`. Window positions and sizes
are not hard-coded per panel. ImGui persists the resulting layout to:

`%LOCALAPPDATA%\Nxa\GFlib3Tool\editor-layout.ini`

`View > Layout > Reset default layout` rebuilds the default splits.

## Panel identities

Docked window names use stable English titles plus an explicit ID suffix:

| Window | Name |
| --- | --- |
| Hierarchy | `Hierarchy###Hierarchy` |
| Project | `Project###Project` |
| Scene | `Scene###Scene` |
| Inspector | `Inspector###Inspector` |
| Console | `Console###Console` |

The visible part stays English, matching Unity's panel tabs. Localized strings
remain in menus, tooltips, and panel content. Stable names are required because
docking and saved layouts key on the full window name; a language switch must not
move or lose a panel.

## Selection and hierarchy

`EditorSelection` stores the object kind and index; record equality drives the
Hierarchy highlight. Placed meshes are grouped by `SceneMesh.Name`; groups show
their member count and expand to individual placements. Plant models show their
instance count and expand to global instance indices. Lists render directly;
the largest observed model has 358 instances.

The filter box matches room meshes, placed-group names, plant model names, and
the terrain label, case-insensitively. Individual instance rows are not filtered
by position text.

Transform values come from `SceneGeometry`: resident placements decompose
through `Matrix4x4.Decompose`, `.trins` rows use `atan2(Row2.X, Row0.X)` with
the independent XZ/Y scale. The Inspector shows the decomposed position, yaw in
degrees, and scale; world bounds transform the eight local corners.

## Palette

Approximate Unity 2019 dark-skin values. The reference is a screenshot, not a
color-managed sample, so these are targets rather than exact matches.

| Role | Hex |
| --- | --- |
| Window background | `#383838` |
| Panel header / menu / toolbar | `#3C3C3C` |
| Status bar | `#2D2D2D` |
| Control background | `#2C2C2C` |
| Control hovered / active | `#3E3E3E` / `#4A4A4A` |
| Border / separator | `#232323` / `#262626` |
| Text | `#D2D2D2` |
| Muted text | `#9A9A9A` |
| Disabled text | `#6A6A6A` |
| Selection | `#2C5D87` |
| Accent / focus | `#4A90D9` |
| Warning / error | `#D9A441` / `#D95C5C` |

Rules:

- Accent blue is reserved for selection, focus, check marks, and drag targets.
- Corners are square except small 2px rounding on controls.
- `TextDisabled` is used only for genuinely disabled content; labels use the
  muted color instead.
- Separators are 1px lines, not gaps between floating windows.

## Metrics

All metrics use `EditorTheme.UiScale = 1.25`; the font is loaded at 18px and
`ScaleAllSizes` applies the same factor to padding, spacing, rounding, and
scrollbars.

| Setting | Value |
| --- | --- |
| Font size | 18px (Japanese-capable system font) |
| Frame padding | 5 x 2.5 |
| Item spacing | 5 x 3.75 |
| Window padding | 7.5 x 7.5 |
| Frame rounding | 2.5 |
| Window / tab rounding | 0 |
| Scrollbar | 15px |
| Toolbar | 35px |
| Status bar | 27.5px |

The Scene panel no longer overlays a control hint; `Help > Controls` opens the
navigation list as a modal dialog.

## Icons

Toolbar icons are drawn as vector shapes through `ImDrawList`
(`Ui/EditorIcons.cs`). Phase 1 provides select, move, rotate, scale, and frame.
This avoids an icon-font dependency and keeps the glyphs crisp at 16px.

The transform tools remain disabled until Stage 5. They are rendered with the
Unity-style active/disabled treatment so the toolbar layout is final.

ImGui tab labels cannot host arbitrary draw-list icons. Bottom tab icons and
inspector section icons belong to Phase 2b; the planned approach reserves
leading label space and draws the icon over the tab item rect. DPI scaling is
deferred; the current metrics assume a 100% display scale.

## Docking implementation

ImGui.NET 1.90.8.1 wraps `DockSpace` and `SetNextWindowDockID` but does not
expose the `DockBuilder` API. The native `cimgui` library shipped by the same
package does export the builder functions, so `Ui/EditorLayout.cs` calls them
through direct Cdecl imports. This is the supported native library of the
managed wrapper; there is no extra dependency. If the package is upgraded,
verify the exported symbols before changing the imports.
