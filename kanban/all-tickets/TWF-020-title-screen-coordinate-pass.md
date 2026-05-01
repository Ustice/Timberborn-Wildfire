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
