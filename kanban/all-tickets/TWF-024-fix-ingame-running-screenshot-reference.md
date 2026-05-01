---
ticket: TWF-024
agent_level: Low
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-013
write_scope:
   - docs/timberborn-menu-coordinate-guide.md
   - docs/reference/screenshots/timberborn-menu-coordinate-guide/**
---

# TWF-024: Fix In-Game Running Screenshot Reference

## Goal

Replace the incorrect `01-ingame-running.png` screenshot with a clean Timberborn in-game running baseline, then update the coordinate guide entry to match the corrected evidence.

## Why

The current screenshot was captured while Jason was interacting with the computer and shows Codex instead of Timberborn. The coordinate guide should not use contaminated screenshots as evidence for UI automation.

## Requirements

- Verify the current `docs/reference/screenshots/timberborn-menu-coordinate-guide/01-ingame-running.png` is incorrect before replacing it.
- Capture a clean Timberborn loaded-save running state at the documented 1920 x 1080 coordinate-guide resolution.
- Replace `01-ingame-running.png` with the clean screenshot, or if capture is blocked, move the ticket to blocked with exact capture error and smallest unblock.
- Update `docs/timberborn-menu-coordinate-guide.md` so the screenshot table and any related notes describe the corrected state.
- Do not change gameplay code or unrelated screenshots.
- Do not guess coordinates from an unverified screenshot.

## Dependencies

- `TWF-013` owns the coordinate-guide format and original in-game screenshot set.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Confirm the replacement image dimensions are 1920 x 1080.
- Inspect the replacement image to ensure Timberborn, not Codex or another desktop app, is visible.
- If possible, verify the guide entry against the visible Timberborn state.

## Notes

- User report on 2026-05-01: `01-ingame-running.png` is incorrect and was likely captured while the user was interacting with the computer.
