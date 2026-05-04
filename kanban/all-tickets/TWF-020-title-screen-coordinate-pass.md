---
ticket: TWF-020
agent_level: Medium
role: qa
requires_qa: true
doc_only: true
dependencies:
  - TWF-013
write_scope:
  - docs/timberborn-menu-coordinate-guide.md
  - docs/reference/**
---

# TWF-020: Capture Timberborn Title Screen Coordinates

## Goal

Extend the Timberborn coordinate guide with opening-screen and standalone main-menu targets from a safe disposable state.

## Why

`TWF-013` captured the in-game Escape menu, load dialog, Mods dialog, and HUD targets, but it did not exit the running save to capture title-screen coordinates. Future startup automation needs those targets, and they should be captured without risking an active save.

## Requirements

- Start Timberborn from a safe title-screen or disposable-save state.
- Capture opening screens, main menu, load entry point, and any confirm dialogs encountered before loading a save.
- Record the same resolution, display, scaling, version, and screenshot-path metadata as `TWF-013`.
- Add named coordinate targets to `docs/timberborn-menu-coordinate-guide.md`.
- Mark destructive or state-changing targets as boundaries.
- Manually verify the path from title screen to the Load Game dialog without deleting, overwriting, or loading saves unless explicitly assigned.

## Dependencies

- `TWF-013` in-game coordinate guide.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Manually verify the title-screen to Load Game dialog coordinate path on the documented resolution.

## Notes

- This ticket exists because `TWF-013` correctly avoided exiting a running save during evidence capture.

## QA Notes - 2026-05-01

- Role: QA.
- Fixture/save state: Timberborn process was already running. `~/Library/Logs/Mechanistry/Timberborn/Player.log` shows `Loading saved game Wildfire testing - 2026-05-01 06h45m, Day 1-1.autosave at 2026-05-01 06:51:01Z`, followed by an autosave at `2026-05-01 07:01:01Z`.
- Launch command: none. Existing app process was observed with `pgrep -fl Timberborn`.
- Display metadata: `C27F390`, `1920 x 1080`, `UI Looks like: 1920 x 1080 @ 60.00Hz`; Sidecar display also present but not used for the coordinate guide.
- Version evidence: `Player.log` reports `Starting game version 1.0.13.0-1e60728-xsm`.
- Commands attempted:

  ```bash
  osascript -e 'tell application id "com.mechanistry.timberborn" to activate'
  screencapture -x docs/reference/screenshots/timberborn-menu-coordinate-guide/07-title-pass-current-state.png
  screencapture -x -D 1 docs/reference/screenshots/timberborn-menu-coordinate-guide/07-title-pass-current-state.png
  screencapture -x -D 2 /tmp/wildfire-D2.png
  system_profiler SPDisplaysDataType
  ```

- Screenshot result: blocked. `screencapture` returned `could not create image from display` for the default display, `-D 1`, and `-D 2`; no new title-screen screenshot was created.
- Manual verification result: blocked. QA did not click `pause.exit_to_main_menu` because the app was already in a loaded save and exiting could discard unsaved progress or change active runtime state. QA also could not verify the title-screen-to-Load-Game path without screenshots or a confirmed title-screen state.
- Guide update: added a `TWF-020 Capture Attempt` note to `docs/timberborn-menu-coordinate-guide.md` with current environment evidence and the explicit no-coordinate boundary.
- Pass/fail result:

  | Requirement                                              | Result       | Evidence                                                                                              |
  | -------------------------------------------------------- | ------------ | ----------------------------------------------------------------------------------------------------- |
  | Start from safe title-screen or disposable-save state    | Blocked      | Current process was in a loaded save; no explicit disposable-state approval for exiting to main menu. |
  | Capture opening screens and standalone main-menu targets | Blocked      | `screencapture` could not create an image from display.                                               |
  | Record resolution/display/scaling/version                | Partial pass | Metadata recorded from `system_profiler` and `Player.log`.                                            |
  | Add named coordinate targets                             | Not done     | No verified title-screen screenshots or clicks; no guessed coordinates added.                         |
  | Mark destructive/state-changing targets as boundaries    | Pass         | Existing guide boundary remains; TWF-020 note reinforces no exit/load/delete/overwrite actions.       |
  | Verify title-screen to Load Game dialog                  | Blocked      | No safe verified title-screen state and no screenshot capture.                                        |

- Smallest unblock action: provide or approve a known disposable title-screen path, then run the pass from an active desktop session where `screencapture` works. If the currently loaded `Wildfire testing` save is disposable, explicitly approve exiting it to the main menu before QA clicks that boundary.

## QA Notes - 2026-05-01 Follow-Up

- Captured the startup Mods dialog after the Wildfire deploy fix. `Wildfire v0.1.0.0` was visible and enabled.
- Verified `startup_mods.ok` to continue past startup screens.
- Verified post-startup auto-load into the `Wildfire testing` save.
- Used the loaded save's Escape menu to reach `pause.exit_to_main_menu`, captured the exit confirmation, clicked the confirmation `Exit` button, and reached the standalone main menu.
- Verified `main.load_game` opened the standalone Load Game dialog.
- Added startup, exit-confirmation, main-menu, and main-menu Load Game coordinates to `docs/timberborn-menu-coordinate-guide.md`.
- Screenshot evidence:
  - `docs/reference/screenshots/timberborn-menu-coordinate-guide/07-startup-mods-wildfire.png`
  - `docs/reference/screenshots/timberborn-menu-coordinate-guide/08-post-startup-loaded-save.png`
  - `docs/reference/screenshots/timberborn-menu-coordinate-guide/09-exit-to-main-confirm.png`
  - `docs/reference/screenshots/timberborn-menu-coordinate-guide/10-main-menu.png`
  - `docs/reference/screenshots/timberborn-menu-coordinate-guide/11-main-load-menu.png`
- Result: title/startup coordinate pass is complete for the documented 1920 x 1080 display state.
