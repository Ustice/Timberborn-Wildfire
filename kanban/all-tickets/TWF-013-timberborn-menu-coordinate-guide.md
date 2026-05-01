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
