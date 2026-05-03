---
ticket: TWF-055
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-046
   - TWF-066
   - TWF-067
   - TWF-068
   - TWF-070
   - TWF-069
   - TWF-074
write_scope:
   - docs/**
   - release/**
   - kanban/all-tickets/TWF-055-create-release-graphic-and-metadata.md
---

# TWF-055: Create Release Graphic And Metadata

## Goal

Create the release graphic, screenshot set, and store metadata needed for Steam Workshop release preparation.

## Why

Players need to understand Wildfire before installing it. A release needs a thumbnail/key image, at least one real gameplay screenshot, a short description, a long description, feature bullets, compatibility notes, and known limitations.

## Requirements

- Create or collect a release thumbnail/key graphic suitable for Steam Workshop.
- Capture at least one live in-game screenshot from the coherent gameplay loop.
- Write short description, long description, feature bullets, compatibility notes, and known limitations.
- Store source metadata in a durable repo path such as `release/` or a documented docs path.
- Track image source, generation prompt, screenshot source, and license/attribution needs.
- Keep the description honest about current limitations.

## Dependencies

- `TWF-046` proves the coherent live gameplay loop that screenshots and copy should describe.
- `TWF-066` through `TWF-070` provide the tuned visual-effect and fire-behavior evidence that release media should not get ahead of.
- `TWF-074` proves beaver behavior in wildfire fields before release screenshots or copy imply more than the game does.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- QA must confirm screenshots show real current behavior and are not stale or misleading.

## Notes

- If generated art is used, preserve prompt/source details for `TWF-061`.
