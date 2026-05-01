# Timberborn Menu Coordinate Guide

This guide records the Timberborn menu targets observed during QA for `TWF-013`.

Coordinates are screenshot pixel coordinates with origin at the top-left corner. They are valid only for the documented display state.

## Capture Environment

| Field | Value |
| --- | --- |
| Date | 2026-05-01 |
| App | `com.mechanistry.timberborn` |
| Timberborn version text | `v1.0.13.0-1e60728-xsm` |
| Mode text | `EXPERIMENTAL MODE` |
| Display | `C27F390` |
| Resolution | `1920 x 1080` |
| macOS UI scaling | `UI Looks like: 1920 x 1080 @ 60.00Hz` |
| Screenshot size | `1920 x 1080` |
| Coordinate source | `screencapture` PNGs plus live click verification |

Computer Use could inspect and screenshot the Timberborn window, but direct Computer Use clicks returned `Apple event error -10005: noWindowsAvailable`. Manual click verification used `cliclick` against the same 1920 x 1080 display.

## Screenshot Evidence

| Screen | Path |
| --- | --- |
| In-game running baseline | `docs/reference/screenshots/timberborn-menu-coordinate-guide/01-ingame-running.png` |
| In-game Escape menu | `docs/reference/screenshots/timberborn-menu-coordinate-guide/02-ingame-escape-menu.png` |
| Load Game click attempt | `docs/reference/screenshots/timberborn-menu-coordinate-guide/03-load-menu-attempt.png` |
| Load Game dialog | `docs/reference/screenshots/timberborn-menu-coordinate-guide/03-load-menu.png` |
| Load Game close return state | `docs/reference/screenshots/timberborn-menu-coordinate-guide/04-after-load-menu-close.png` |
| Mods dialog | `docs/reference/screenshots/timberborn-menu-coordinate-guide/05-mods-menu.png` |
| Mods close return state | `docs/reference/screenshots/timberborn-menu-coordinate-guide/06-after-mods-close.png` |

## Coordinate Targets

### In-Game Escape Menu

Precondition: a save is loaded and the Escape menu is visible.

Open this state with:

```bash
osascript -e 'tell application id "com.mechanistry.timberborn" to activate'
```

Then press `Escape`.

| Target | X | Y | Verified | Notes |
| --- | ---: | ---: | --- | --- |
| `pause.resume` | 960 | 270 | Yes | Returns to the running save. |
| `pause.save_game` | 960 | 321 | No | Not clicked during QA to avoid changing save data. |
| `pause.load_game` | 960 | 371 | Yes | Opens the Load Game dialog. |
| `pause.key_bindings` | 960 | 423 | No | Not needed for this ticket. |
| `pause.mods` | 960 | 475 | Yes | Opens the Mods dialog. |
| `pause.settings` | 960 | 527 | No | Safe to inspect later if settings coordinates are needed. |
| `pause.feedback_site` | 960 | 579 | No | Do not click in automation; may open a browser. |
| `pause.exit_to_main_menu` | 960 | 633 | No | Boundary target only; may prompt about unsaved progress. |
| `pause.exit_to_desktop` | 960 | 684 | No | Do not click in QA automation unless explicitly assigned. |

### Load Game Dialog

Precondition: a save is loaded, the Escape menu is visible, and `pause.load_game` has been clicked.

Manual verification path:

```bash
cliclick c:960,371
```

Result: the Load Game dialog opened. QA stopped before selecting `load_dialog.load_selected_save` because loading a save changes the running game state.

| Target | X | Y | Verified | Notes |
| --- | ---: | ---: | --- | --- |
| `load_dialog.close` | 1414 | 156 | Yes | Returns to the Escape menu. |
| `load_dialog.settlement_first_row` | 601 | 255 | Visible | First settlement row shown as `Wildfire testing`. |
| `load_dialog.save_first_row` | 1043 | 271 | Visible | First save row shown as `Autosave`, `Cycle 1, day 2`. |
| `load_dialog.delete_settlement` | 744 | 849 | No | Destructive. Never click from generic QA automation. |
| `load_dialog.delete_save` | 960 | 849 | No | Destructive. Never click from generic QA automation. |
| `load_dialog.load_selected_save` | 1178 | 849 | Boundary | Would load the selected save and change runtime state. |
| `load_dialog.browse_folder` | 1325 | 891 | No | Opens local file browser. |

### Mods Dialog

Precondition: a save is loaded, the Escape menu is visible, and `pause.mods` has been clicked.

Manual verification path:

```bash
cliclick c:960,475
```

| Target | X | Y | Verified | Notes |
| --- | ---: | ---: | --- | --- |
| `mods.ok` | 960 | 879 | Yes | Returns to the Escape menu. |
| `mods.close` | 1294 | 156 | Visible | Alternative close target. |
| `mods.prometheus_row` | 877 | 630 | Visible | `Prometheus v0.2` is visible and enabled. |
| `mods.wildfire_row` | n/a | n/a | Not visible | No Wildfire mod row was visible in the captured Mods dialog. |
| `mods.browse_local_mods` | 1506 | 269 | No | Opens local folder or file browser. |
| `mods.create_new_mod` | 1506 | 322 | No | Not relevant for runtime QA. |

### Debug Menus

No Timberborn debug menu, developer console, or Wildfire-specific menu was visible in the captured in-game, Escape, Load Game, or Mods states.

Use Timberborn keybinding or mod documentation before adding debug-menu coordinates. Do not invent targets from hidden or unconfirmed UI.

### In-Game Wildfire-Relevant HUD

Precondition: a save is loaded and no modal menu is open.

| Target | X | Y | Verified | Notes |
| --- | ---: | ---: | --- | --- |
| `hud.tool_palette_start` | 278 | 1044 | Visible | Bottom tool palette begins near this point. |
| `hud.version_label` | 1638 | 1071 | Visible | Version text appears at lower right. |
| `hud.global_view` | 1298 | 20 | Visible | Top-right view control. |
| `hud.cycle_indicator` | 1592 | 20 | Visible | Shows `Cycle 1, day 2` in captured state. |

## Opening Screens And Main Menu

Opening screens and the standalone main menu were not visible in this run because Timberborn was already inside a loaded save.

Do not click `pause.exit_to_main_menu` or confirm any exit prompt from a running save unless the coordinator explicitly allows that state change. Exiting to the main menu can discard unsaved progress or change the active runtime state. Capture these targets in a later QA pass that starts Timberborn directly at the title screen or uses a disposable save state.

## Refresh Instructions

Refresh this guide whenever Timberborn version, monitor, display resolution, UI scaling, or menu layout changes.

1. Open Timberborn on the target display.
2. Record display information:

   ```bash
   system_profiler SPDisplaysDataType
   ```

3. Capture each UI state:

   ```bash
   screencapture -x docs/reference/screenshots/timberborn-menu-coordinate-guide/<NN-state>.png
   ```

4. Record coordinates in screenshot pixels, not percentages.
5. Manually verify at least `pause.load_game -> load_dialog`.
6. Stop before destructive targets such as delete buttons, save overwrite actions, exiting to desktop, or loading a save unless explicitly assigned.
7. Update this guide and the assigned ticket notes with screenshot paths, commands, pass/fail results, and boundaries.
