---
ticket: TWF-025
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-013
  - TWF-020
  - TWF-024
write_scope:
  - docs/timberborn-bottom-menu-guide.md
  - docs/reference/screenshots/timberborn-bottom-menu-guide/**
---

# TWF-025: Create Bottom Menu Screenshot Guide

## Goal

Create a Timberborn bottom-menu screenshot guide that identifies each bottom menu item, shows the important submenu states, and combines the useful screenshot details into one clear reference artifact.

## Why

Future QA automation and Timberborn UI work need a reliable visual map of the bottom toolbar and its submenus. The guide should reduce repeated live UI inspection and make coordinate-based automation easier to review.

## Requirements

- Capture or reuse clean Timberborn screenshots at the documented 1920 x 1080 coordinate-guide resolution.
- Cover each bottom menu item that appears in the loaded-save HUD.
- For each bottom menu item, capture the relevant submenu or expanded state when one exists.
- Include the developer menu opened with `Alt-Shift-Z`, plus its important submenus or panels that affect QA/debug workflows.
- Combine the important bits of the screenshots into one composite reference image.
- Keep the composite readable enough that labels, menu icons, and submenu regions can be inspected without opening every source screenshot.
- Document the source screenshots, capture state, and any menu items that do not expose a submenu.
- Document the exact key sequence used for the developer menu and any state prerequisites.
- Do not rely on contaminated screenshots that show Codex or another desktop app.
- Do not change gameplay code, automation scripts, or unrelated documentation.

## Dependencies

- `TWF-013` owns the coordinate-guide format.
- `TWF-020` owns title-screen and menu-coordinate coverage.
- `TWF-024` must replace the known contaminated in-game running screenshot before this guide is accepted.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Confirm all new or updated screenshot assets have expected dimensions or documented crop/composite dimensions.
- Inspect the composite image to ensure every bottom menu item and important submenu region is visible and correctly labeled.
- Verify the guide links render to existing image paths.

## Notes

- Prefer a single composite image plus concise source-image notes over a long page of individual screenshots.
- Jason specifically requested that the guide include dev menus opened with `Alt-Shift-Z`.
- If live capture is blocked, update this ticket with the exact missing state, capture error, or smallest user action needed to unblock it.

## Worker Notes

- Created worktree `~/repos/wildfire-TWF-025` on branch `codex/TWF-025-bottom-menu-guide`.
- Captured live Timberborn screenshots from a loaded save at `1920 x 1080`; all source screenshots in `docs/reference/screenshots/timberborn-bottom-menu-guide/` are clean Timberborn captures with no Codex or desktop app contamination.
- Added `docs/timberborn-bottom-menu-guide.md` for bottom toolbar coverage.
- Removed the separate debug-panels guide after scope correction; TWF-027 owns the deeper debug-panel documentation.
- Built `composite-bottom-menu-reference.png` at `3724 x 6240`.
- Built `composite-dev-debug-panels.png` at `1862 x 5664`.

## Coverage Checklist

| Item                                | Source                                  | Composite                             | Status                                                       |
| ----------------------------------- | --------------------------------------- | ------------------------------------- | ------------------------------------------------------------ |
| Loaded-save HUD baseline            | `01-loaded-save-hud.png`                | `composite-bottom-menu-reference.png` | Captured                                                     |
| Root selector                       | `01-selector.png`                       | `composite-bottom-menu-reference.png` | Captured                                                     |
| Paths/structures submenu            | `02-paths-structures.png`               | `composite-bottom-menu-reference.png` | Captured                                                     |
| Plants/food submenu                 | `03-food-farming.png`                   | `composite-bottom-menu-reference.png` | Captured                                                     |
| Cutting/nature submenu              | `04-nature-wood.png`                    | `composite-bottom-menu-reference.png` | Captured                                                     |
| Terrain/landscaping submenu         | `05-landscaping.png`                    | `composite-bottom-menu-reference.png` | Captured                                                     |
| Housing/priorities item             | `06-housing.png`                        | `composite-bottom-menu-reference.png` | Captured; no build strip appeared                            |
| Beaver item                         | `07-beavers.png`                        | `composite-bottom-menu-reference.png` | Captured; no build strip appeared                            |
| Labor/builder item                  | `08-labor-builder.png`                  | `composite-bottom-menu-reference.png` | Captured; no build strip appeared                            |
| Water brush panel                   | `09-water.png`                          | `composite-bottom-menu-reference.png` | Captured                                                     |
| Route/path submenu                  | `10-path-routing.png`                   | `composite-bottom-menu-reference.png` | Captured                                                     |
| Platform/water structure submenu    | `11-water-platforms.png`                | `composite-bottom-menu-reference.png` | Captured                                                     |
| Storage submenu                     | `12-storage.png`                        | `composite-bottom-menu-reference.png` | Captured                                                     |
| Decoration submenu                  | `13-decoration.png`                     | `composite-bottom-menu-reference.png` | Captured                                                     |
| Knowledge/books submenu             | `14-books-knowledge.png`                | `composite-bottom-menu-reference.png` | Captured                                                     |
| Fluid/water infrastructure submenu  | `15-fluid-water.png`                    | `composite-bottom-menu-reference.png` | Captured                                                     |
| Food/carrots submenu                | `16-food-carrot.png`                    | `composite-bottom-menu-reference.png` | Captured                                                     |
| Wood/logs submenu                   | `17-wood-logs.png`                      | `composite-bottom-menu-reference.png` | Captured                                                     |
| Wellbeing/star submenu              | `18-wellbeing-star.png`                 | `composite-bottom-menu-reference.png` | Captured                                                     |
| Industry/gears submenu              | `19-industry-gears.png`                 | `composite-bottom-menu-reference.png` | Captured                                                     |
| Science submenu                     | `20-science.png`                        | `composite-bottom-menu-reference.png` | Captured                                                     |
| Lighting submenu                    | `21-lighting.png`                       | `composite-bottom-menu-reference.png` | Captured                                                     |
| Wellbeing/masks submenu             | `22-wellbeing-masks.png`                | `composite-bottom-menu-reference.png` | Captured                                                     |
| District/services submenu           | `23-district-services.png`              | `composite-bottom-menu-reference.png` | Captured                                                     |
| Leisure/chair single-item expansion | `24-leisure-chair.png`                  | `composite-bottom-menu-reference.png` | Captured                                                     |
| More submenu                        | `25-more.png`                           | `composite-bottom-menu-reference.png` | Captured                                                     |
| Irrigation/water submenu            | `26-irrigation-water.png`               | `composite-bottom-menu-reference.png` | Captured                                                     |
| Goods/cube submenu                  | `27-goods-cube.png`                     | `composite-bottom-menu-reference.png` | Captured                                                     |
| Foliage/plants submenu              | `28-foliage.png`                        | `composite-bottom-menu-reference.png` | Captured                                                     |
| Version/mod button                  | `29-version-mod.png`                    | `composite-bottom-menu-reference.png` | Captured; opens pause/version panel instead of build submenu |
| Developer mode toggle `Shift-Alt-Z` | `31-developer-menu-alt-shift-z.png`     | Both composites                       | Captured                                                     |
| Debug panel toggle `Shift-Alt-X`    | `33-debug-panel-alt-shift-x.png`        | Both composites                       | Captured                                                     |
| Object selected with debug panels   | `34-debug-panel-object-selected.png`    | `composite-dev-debug-panels.png`      | Captured                                                     |
| Expanded debug mode panel           | `35-debug-mode-panel-expanded.png`      | `composite-dev-debug-panels.png`      | Captured                                                     |
| Expanded object debugger            | `36-object-debugger-panel-expanded.png` | `composite-dev-debug-panels.png`      | Captured                                                     |

## Intentional Omissions

- Did not click destructive or save-mutating controls.
- Did not place any bottom-menu build item.
- Did not expand every debug diagnostic subsection. The expanded debug panel labels all sections, and only `Performance` was open in the live capture; expanding every section would produce volatile per-frame diagnostics rather than stable panel coverage.
- Did not open object debugger component rows.
- Did not move status symlinks.

## Evidence

- All source captures checked with `sips -g pixelWidth -g pixelHeight`.
- Bottom composite visually inspected with `view_image`.
- Developer/debug composite visually inspected with `view_image`.
- External confirmation used: Timberborn wiki notes `Alt-Shift-Z` for developer mode and `Alt-Shift-X` for debug mode.
