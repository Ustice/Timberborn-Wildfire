# Timberborn Bottom Menu Guide

This guide records the loaded-save bottom toolbar observed for `TWF-025`.

Coordinates are screenshot pixel coordinates with origin at the top-left corner. They are valid for the documented 1920 x 1080 capture state only.

## Capture Environment

| Field | Value |
| --- | --- |
| Date | 2026-05-01 |
| App | `com.mechanistry.timberborn` |
| Timberborn version text | `v1.0.13.0-1e60728-xsm` |
| Mode text | `EXPERIMENTAL MODE` |
| Resolution | `1920 x 1080` |
| Screenshot size | `1920 x 1080` for all source captures |
| Capture method | `cliclick` menu clicks plus `screencapture -x` PNGs |
| Composite | `docs/reference/screenshots/timberborn-bottom-menu-guide/composite-bottom-menu-reference.png` |

The live state was a loaded save with the bottom development toolbar visible. No screenshot in this guide shows Codex or another desktop application.

## Composite Reference

![Composite bottom menu reference](reference/screenshots/timberborn-bottom-menu-guide/composite-bottom-menu-reference.png)

The composite uses labeled crops from the 1920 x 1080 source screenshots so root icons, submenu strips, and developer/debug toggle evidence can be reviewed in one image.

## Source Screenshots

| State | Path | Dimensions |
| --- | --- | --- |
| Loaded-save HUD baseline | `docs/reference/screenshots/timberborn-bottom-menu-guide/01-loaded-save-hud.png` | `1920 x 1080` |
| Root selector | `docs/reference/screenshots/timberborn-bottom-menu-guide/01-selector.png` | `1920 x 1080` |
| Paths/structures submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/02-paths-structures.png` | `1920 x 1080` |
| Plants/food submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/03-food-farming.png` | `1920 x 1080` |
| Cutting/nature submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/04-nature-wood.png` | `1920 x 1080` |
| Terrain/landscaping submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/05-landscaping.png` | `1920 x 1080` |
| Housing or priorities item | `docs/reference/screenshots/timberborn-bottom-menu-guide/06-housing.png` | `1920 x 1080` |
| Beaver-related item | `docs/reference/screenshots/timberborn-bottom-menu-guide/07-beavers.png` | `1920 x 1080` |
| Labor/builder item | `docs/reference/screenshots/timberborn-bottom-menu-guide/08-labor-builder.png` | `1920 x 1080` |
| Water tool brush controls | `docs/reference/screenshots/timberborn-bottom-menu-guide/09-water.png` | `1920 x 1080` |
| Paths/route submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/10-path-routing.png` | `1920 x 1080` |
| Platforms/water structures submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/11-water-platforms.png` | `1920 x 1080` |
| Storage submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/12-storage.png` | `1920 x 1080` |
| Decoration submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/13-decoration.png` | `1920 x 1080` |
| Knowledge/books submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/14-books-knowledge.png` | `1920 x 1080` |
| Fluid/water infrastructure submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/15-fluid-water.png` | `1920 x 1080` |
| Food/carrots submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/16-food-carrot.png` | `1920 x 1080` |
| Wood/logs submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/17-wood-logs.png` | `1920 x 1080` |
| Wellbeing/star submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/18-wellbeing-star.png` | `1920 x 1080` |
| Industry/gears submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/19-industry-gears.png` | `1920 x 1080` |
| Science submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/20-science.png` | `1920 x 1080` |
| Lighting submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/21-lighting.png` | `1920 x 1080` |
| Wellbeing/masks submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/22-wellbeing-masks.png` | `1920 x 1080` |
| District/services submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/23-district-services.png` | `1920 x 1080` |
| Leisure/chair item | `docs/reference/screenshots/timberborn-bottom-menu-guide/24-leisure-chair.png` | `1920 x 1080` |
| More submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/25-more.png` | `1920 x 1080` |
| Irrigation/water submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/26-irrigation-water.png` | `1920 x 1080` |
| Goods/cube submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/27-goods-cube.png` | `1920 x 1080` |
| Foliage/plants submenu | `docs/reference/screenshots/timberborn-bottom-menu-guide/28-foliage.png` | `1920 x 1080` |
| Version/mod button result | `docs/reference/screenshots/timberborn-bottom-menu-guide/29-version-mod.png` | `1920 x 1080` |
| Developer mode toggle | `docs/reference/screenshots/timberborn-bottom-menu-guide/31-developer-menu-alt-shift-z.png` | `1920 x 1080` |
| Debug panel toggle | `docs/reference/screenshots/timberborn-bottom-menu-guide/33-debug-panel-alt-shift-x.png` | `1920 x 1080` |

## Bottom Toolbar Coverage

| Order | X | Source | Expanded State | Notes |
| ---: | ---: | --- | --- | --- |
| 1 | 60 | `01-selector.png` | No submenu | Root selection cursor. |
| 2 | 126 | `02-paths-structures.png` | Yes | Shows path and structure construction icons. |
| 3 | 192 | `03-food-farming.png` | Yes | Shows the plants/food tooltip state. |
| 4 | 258 | `04-nature-wood.png` | Yes | Shows cutting-area controls and tooltip. |
| 5 | 324 | `05-landscaping.png` | Yes | Shows terrain up/down controls. |
| 6 | 390 | `06-housing.png` | No visible submenu | The click produced the assign-priorities tooltip instead of a build strip in this live state. |
| 7 | 456 | `07-beavers.png` | No visible submenu | No submenu appeared in the captured state. |
| 8 | 522 | `08-labor-builder.png` | No visible submenu | No submenu appeared in the captured state. |
| 9 | 588 | `09-water.png` | Brush panel | Shows brush shape and brush size controls. |
| 10 | 654 | `10-path-routing.png` | Yes | Shows a long build strip. |
| 11 | 720 | `11-water-platforms.png` | Yes | Shows a long build strip. |
| 12 | 786 | `12-storage.png` | Yes | Shows storage-related icons. |
| 13 | 852 | `13-decoration.png` | Yes | Shows decoration icons. |
| 14 | 918 | `14-books-knowledge.png` | Yes | Shows knowledge/book-related icons. |
| 15 | 984 | `15-fluid-water.png` | Yes | Shows water/fluid infrastructure icons. |
| 16 | 1050 | `16-food-carrot.png` | Yes | Shows food-production icons. |
| 17 | 1116 | `17-wood-logs.png` | Yes | Shows wood/log production icons. |
| 18 | 1182 | `18-wellbeing-star.png` | Yes | Shows wellbeing service icons. |
| 19 | 1248 | `19-industry-gears.png` | Yes | Shows industry/gear icons. |
| 20 | 1314 | `20-science.png` | Yes | Shows science/building icons. |
| 21 | 1380 | `21-lighting.png` | Yes | Shows lighting or utility icons. |
| 22 | 1446 | `22-wellbeing-masks.png` | Yes | Shows a long wellbeing/social strip. |
| 23 | 1512 | `23-district-services.png` | Yes | Shows district/service icons. |
| 24 | 1578 | `24-leisure-chair.png` | Single icon | Shows one expanded item. |
| 25 | 1644 | `25-more.png` | Yes | Shows more-category icons. |
| 26 | 1710 | `26-irrigation-water.png` | Yes | Shows irrigation/water-related icons. |
| 27 | 1776 | `27-goods-cube.png` | Yes | Shows goods/material icons. |
| 28 | 1842 | `28-foliage.png` | Yes | Shows foliage/plant icons. |
| 29 | 1890 | `29-version-mod.png` | Opens pause/version panel | This bottom-right version/mod button opened the pause panel with version text rather than a build submenu. |

## Developer And Debug Toggles

`Shift-Alt-Z` toggled developer mode in the live capture. The visible evidence is the developer/FPS overlay and additional developer affordance behavior, captured in `31-developer-menu-alt-shift-z.png`. The [Timberborn wiki developer mode page](https://timberborn.wiki.gg/wiki/Developer_Mode) also identifies `Alt-Shift-Z` as the developer mode shortcut.

`Shift-Alt-X` opened the debug panels. The collapsed debug state is captured in `33-debug-panel-alt-shift-x.png`. Expanded debug/object debugger states were captured as screenshot evidence for TWF-025, but the deeper panel guide belongs to TWF-027.

## Omitted Or Boundary Items

- No destructive buttons were clicked.
- No build item was placed.
- The version/mod button was clicked only far enough to identify that it opens the pause/version panel.
- `30-alt-shift-z-attempt-pause-menu.png` and `32-developer-version-panel.png` are retained as diagnostic captures from the shortcut investigation but are not part of the main coverage composite.

## Refresh Instructions

Refresh this guide whenever Timberborn version, display resolution, UI scaling, loaded faction, mod set, or bottom toolbar layout changes.

1. Open Timberborn on the target display.
2. Load the target save at 1920 x 1080.
3. For each visible bottom toolbar item, click the item once and capture:

   ```bash
   screencapture -x docs/reference/screenshots/timberborn-bottom-menu-guide/<NN-name>.png
   ```

4. Capture `Shift-Alt-Z` developer mode and `Shift-Alt-X` debug mode separately.
5. Regenerate the composite after any source screenshot changes.
6. Update the assigned ticket with coverage, dimensions, visual inspection, and blockers.
