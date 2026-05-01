---
ticket: TWF-013
agent_level: Medium
role: qa
requires_qa: true
doc_only: true
dependencies: []
write_scope:
   - docs/timberborn-menu-coordinate-guide.md
   - docs/reference/**
---

# TWF-013: Document Timberborn Menu Coordinates

## Goal

Create a guide showing Timberborn's relevant menus and submenus with screen coordinates for QA automation.

## Why

Click automation is only as reliable as its map of the UI. A coordinate guide makes scripts easier to understand, easier to update after UI changes, and safer for workers to extend.

## Requirements

- Create a document under `docs/` for Timberborn menu and submenu coordinates.
- Include the resolution and display scaling used for every coordinate set.
- Cover opening screens, main menu, load menu, save selection, mod or debug menus, and any Wildfire-relevant in-game menus.
- Include screenshots or screenshot paths when available.
- Record coordinates as named targets, not just prose.
- Include notes about UI states that must be present before clicking.
- Include instructions for refreshing coordinates when the game UI changes.

## Dependencies

- Access to Timberborn UI on a known resolution.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Manually verify at least the load-menu coordinate path on the documented resolution.

## Notes

- Do not assume coordinates are portable across resolutions. Make the guide explicit about that limitation.
- Keep this ticket away from `docs/TEST_PLAN.md` unless the coordinator explicitly widens scope.
- Blocked during coordination on 2026-05-01 because Timberborn was not running. Steam was running, but the ticket needs actual Timberborn UI access at a known resolution.
- Smallest unblock: open Timberborn at the intended QA resolution and confirm the UI is available for coordinate capture.

## QA Notes

- Created `docs/timberborn-menu-coordinate-guide.md`.
- Captured Timberborn running as `com.mechanistry.timberborn` on display `C27F390`, resolution `1920 x 1080`, macOS `UI Looks like: 1920 x 1080 @ 60.00Hz`.
- Used Computer Use for visible UI inspection and `screencapture` for evidence. Computer Use click attempts returned `Apple event error -10005: noWindowsAvailable`, so manual coordinate verification used `cliclick` on the same display.
- Manually verified the load-menu coordinate path: from the in-game Escape menu, `pause.load_game` at `(960, 371)` opened the Load Game dialog.
- Stopped before selecting `load_dialog.load_selected_save` because loading a save changes the running game state.
- Captured the Mods dialog and verified `Prometheus v0.2` was visible and enabled.
- Did not capture standalone opening screens or the main menu because Timberborn was already inside a running save. Exiting to the main menu could discard unsaved progress or change the active runtime state, so the guide documents that boundary and refresh instructions.

## QA Evidence

- `docs/reference/screenshots/timberborn-menu-coordinate-guide/01-ingame-running.png`
- `docs/reference/screenshots/timberborn-menu-coordinate-guide/02-ingame-escape-menu.png`
- `docs/reference/screenshots/timberborn-menu-coordinate-guide/03-load-menu-attempt.png`
- `docs/reference/screenshots/timberborn-menu-coordinate-guide/03-load-menu.png`
- `docs/reference/screenshots/timberborn-menu-coordinate-guide/04-after-load-menu-close.png`
- `docs/reference/screenshots/timberborn-menu-coordinate-guide/05-mods-menu.png`
- `docs/reference/screenshots/timberborn-menu-coordinate-guide/06-after-mods-close.png`

## QA Result

- Document created: pass.
- Resolution/display scaling included: pass.
- Load-menu coordinate path manually verified: pass.
- Save-selection targets documented without destructive clicks: pass.
- Mod/Wildfire-relevant menu captured: pass.
- Opening screens/main menu captured: blocked by running-save boundary; documented as a refresh item.
