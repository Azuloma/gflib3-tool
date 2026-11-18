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
  Terrain sections. Fields remain read-only; Stage 5 Step 1 edits through the
  viewport gizmo.
- Frame selection: `F`, the toolbar button, and the Inspector button frame the
  selected object; with no selection they frame the scene.

The Project panel uses `Info` and `Assets` tabs. The asset browser, console
toolbar, and tab/section icons belong to Phase 2b.

## Phase 2b scope

Phase 2b adds the workspace tools on top of the read-only viewer:

- `Project > Assets` lists every discovered terrain and model pack with name,
  kind, file size, entry count, and counterpart. The kind filter covers base
  areas, sub-areas, area models, sub-area models, auxiliary packs, and other
  packs. Search matches either side of a pair.
- Header metadata is read lazily through `GfpakHeader` the first time an asset
  row is shown, then cached. Read failures stay in the row and do not create
  scene warnings.
- Double-clicking a paired row loads the area/model pair. The current area and
  model rows stay highlighted. Auxiliary and unpaired rows are not loadable.
- `Project > Info` keeps the scene statistics and terrain UV control. The quick
  Area/Model selectors live at the top of the Assets tab.
- The Console tab has a toolbar with text filtering, info/warning/error
  toggles, session clear, copy-visible, and auto-scroll. Validation remains a
  warning summary.
- Bottom tabs, Inspector component headers, and asset rows use vector icons.
  Hierarchy rows expose `Copy name` in their context menu.

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

Stage 5 Step 1 adds viewport picking and transform gizmos. Left click selects a
vegetation instance by ray/AABB; selecting empty space clears the selection.
The toolbar enables Select/Move/Rotate/Scale (`1`-`4`), drawing an ImGui overlay
gizmo over the Scene image. `Ctrl+Z`/`Ctrl+Y` drive the session history, and the
History tab lists applied and redoable edits. Details are in `docs/editing.md`.

Stage 5 Step 2 adds the Place tool and Duplicate action:

- The toolbar gains a crosshair Place button (`5`) and a duplicate button after
  the transform tools. Place is enabled only with a selected vegetation model or
  instance and a valid `.trins` target; Duplicate is enabled for a selected
  instance with a source file.
- The Edit menu carries `Duplicate` with `Ctrl+D`.
- The Inspector shows a Placement section for plant models and instances: a
  placement-model combo and a target `.trins` combo. Target labels stay as
  `.trins` file names, with the full logical path in the tooltip.
- A vegetation instance's Instances section also shows its source `.trins` and
  local index.
- In Place mode the Scene shows the terrain-hit model preview and its AABB; left
  click appends the instance and selects it.

## Project assets

`SceneCatalog` classifies packs by folder and name. Terrain and model packs pair
by the same name; a sub-area terrain pack without a same-named model falls back
to its base area model. Auxiliary model packs (`resident_bg_parts_model`) and
unclassified packs such as `animation` and `collision` remain visible in
`Assets` but are not loadable.

The asset table uses `GfpakHeader` for the entry count. The header read happens
on first display per file and the result, including a read error, is cached for
the process lifetime. The table is not virtualized; the dump has roughly one
hundred asset rows. Column widths are fixed and the name column plus header row
stay frozen, so narrow panels get a horizontal scrollbar instead of truncating
the pair column.

## Console

`ConsoleLog` classifies messages by the `error:` and `warning:` prefixes; all
other messages are info. The toolbar filter is case-insensitive and applies to
the message text after the level toggles. Session clear hides messages that
already existed at the time of the click; the next scene reload starts clear.
Copy-visible writes the currently displayed messages to the clipboard.

## Icons

Toolbar icons are drawn as vector shapes through `ImDrawList`
(`Ui/EditorIcons.cs`). The set covers select, move, rotate, scale, place,
duplicate, and frame. This avoids an icon-font dependency and keeps the glyphs
crisp at 16px.

The transform tools became active in Stage 5 Step 1 and share the toolbar's
active/disabled treatment. They are rendered with the Unity-style active
treatment so the toolbar layout stays final.

Bottom tabs, Inspector component headers, and asset kinds reuse the same vector
set. Tab and collapsing-header labels reserve four leading spaces; the icon is
drawn over the item rect after ImGui lays the label out. Docked window titles
stay text-only because they are also the stable docking identifiers. DPI
scaling is deferred; the current metrics assume a 100% display scale.

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

## Docking implementation

ImGui.NET 1.90.8.1 wraps `DockSpace` and `SetNextWindowDockID` but does not
expose the `DockBuilder` API. The native `cimgui` library shipped by the same
package does export the builder functions, so `Ui/EditorLayout.cs` calls them
through direct Cdecl imports. This is the supported native library of the
managed wrapper; there is no extra dependency. If the package is upgraded,
verify the exported symbols before changing the imports.
