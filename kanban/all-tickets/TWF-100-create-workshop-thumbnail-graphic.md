---
ticket: TWF-100
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-066
  - TWF-067
  - TWF-068
write_scope:
  - release/**
  - docs/**
  - kanban/all-tickets/TWF-100-create-workshop-thumbnail-graphic.md
---

# TWF-100: Create Workshop Thumbnail Graphic

## Goal

Create or collect the Steam Workshop thumbnail or key graphic for Wildfire.

## Why

The Workshop thumbnail is a separate visual asset from screenshots and metadata copy. It needs source tracking and should not imply features the current build does not have.

## Requirements

- Create or collect one release thumbnail/key image suitable for Steam Workshop.
- Preserve source, prompt, screenshot basis, license, and attribution details.
- Keep the graphic honest about current Wildfire behavior and visual quality.
- Store the asset and metadata in `release/` or another documented durable path.
- Coordinate with `TWF-061` for attribution needs.

## Dependencies

- `TWF-066`, `TWF-067`, and `TWF-068` provide fire, smoke, and ash visual baselines.

## Parent Reference

- Parent gate: `TWF-055`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Review the asset for current-behavior accuracy and attribution completeness.

## Notes

- Live screenshot capture belongs to `TWF-101`; copy belongs to `TWF-102`.
