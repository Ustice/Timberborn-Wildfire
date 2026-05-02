---
ticket: TWF-015
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-013
   - TWF-014
   - TWF-020
write_scope:
   - scripts/**
   - docs/**
---

# TWF-015: Add Load Latest Save Utility

## Goal

Use the QA utility skill to create a script that clicks past Timberborn opening screens and loads the latest save.

## Why

Live validation loses time when every run starts with manual menu navigation. A reliable load-latest-save utility reduces setup friction and makes repeated QA runs more consistent.

## Requirements

- Use the QA utility skill from `TWF-014`.
- Write the utility in TypeScript and run it with Bun.
- Use documented coordinates from `TWF-013`.
- Click past opening screens only when the expected UI state is visible or otherwise confirmed.
- Open the load menu.
- Select and load the latest save according to Timberborn's visible ordering or documented save metadata.
- Fail loudly with a clear message when the game is not open, the resolution does not match, or the expected menu is missing.
- Document the command and prerequisites.
- Avoid destructive actions such as deleting saves or overwriting saves.

## Dependencies

- `TWF-013` Timberborn menu coordinate guide.
- `TWF-014` QA utility skill.
- `TWF-020` title-screen coordinate pass.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run any dry-run or help mode added to the script with `bun`.
- Validate against Timberborn on the documented resolution.

## Notes

- Prefer a `--dry-run` mode that prints intended clicks before moving the mouse.
- If detecting the latest save requires OCR or filesystem access, document that choice before implementing it.
- Deferred during coordination on 2026-05-01 because `TWF-030` supersedes this older utility shape with the fuller launch/load/unpause workflow and overlapping `scripts/**` write scope.
