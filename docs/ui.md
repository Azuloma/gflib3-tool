# Editor UI

The viewer uses Dear ImGui through Silk.NET and ImGui.NET. The editor shell
follows the Unity 2019 dark editor as the visual reference: neutral gray panels,
compact metrics, icon toolbars, tabbed docks, and a slim status bar.

## Phase 1 scope

Phase 1 establishes the visual foundation only:

- Unity-style palette and metrics.
- Programmatic default dock layout with tabbed panels.
- Icon toolbar.
- Bottom status bar.

Panel content is still the Stage 4 read-only presentation. The Project browser,
component-style Inspector, hierarchy tree, console toolbar, and selection model
belong to Phase 2.

## Layout

```text
+ Menu bar --------------------------------------------------+
| Toolbar (tools, separator, frame)                          |
| Hierarchy 20%  | Scene (tab)                58% | Inspector |
|                |                                 |   22%     |
| Project 20%    | Console (tabs) 32% height       |           |
+ Status bar 22px -------------------------------------------+
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

| Setting | Value |
| --- | --- |
| Font size | 14px (Japanese-capable system font) |
| Frame padding | 4 x 2 |
| Item spacing | 4 x 3 |
| Window padding | 6 x 6 |
| Frame rounding | 2 |
| Window / tab rounding | 0 |
| Scrollbar | 12px |
| Toolbar | 28px |
| Status bar | 22px |

## Icons

Toolbar icons are drawn as vector shapes through `ImDrawList`
(`Ui/EditorIcons.cs`). Phase 1 provides select, move, rotate, scale, and frame.
This avoids an icon-font dependency and keeps the glyphs crisp at 16px.

The transform tools remain disabled until Stage 5. They are rendered with the
Unity-style active/disabled treatment so the toolbar layout is final.

ImGui tab labels cannot host arbitrary draw-list icons. Tab icons and content
section icons are deferred to Phase 2. DPI scaling is also deferred; the current
metrics assume a 100% display scale.

## Docking implementation

ImGui.NET 1.90.8.1 wraps `DockSpace` and `SetNextWindowDockID` but does not
expose the `DockBuilder` API. The native `cimgui` library shipped by the same
package does export the builder functions, so `Ui/EditorLayout.cs` calls them
through direct Cdecl imports. This is the supported native library of the
managed wrapper; there is no extra dependency. If the package is upgraded,
verify the exported symbols before changing the imports.
