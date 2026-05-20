---
ticket: TWF-163
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-082
write_scope:
  - src/Wildfire.Timberborn/**
  - docs/reference/assets/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-163-create-release-consequence-and-ash-icons.md
---

# TWF-163: Create Release Consequence And Ash Icons

## Goal

Create Timberborn-style release icons for aggregated Wildfire consequence alerts, fertile ash, tainted ash, and the fertile-ash crop/tree menu buttons.

## Why

Release feedback needs to look native. The alert icons and bottom-menu icons should be generated or edited with direct reference to the collected Timberborn asset sheets, not guessed from memory or generic fantasy icon style.

## Requirements

- Use the collected Timberborn icon sheets as imagegen style anchors:
  - `docs/reference/assets/menu-icons/composite.png`
  - `docs/reference/assets/goods-icons/composite.png`
- Use specific nearby reference icons when designing final assets, including:
  - `docs/reference/assets/goods-icons/DirtIcon.png`
  - `docs/reference/assets/menu-icons/FieldsPlantingToolGroupIcon.png`
  - `docs/reference/assets/menu-icons/ForestryPlantingToolGroupIcon.png`
  - `docs/reference/assets/menu-icons/ContaminationBarrierIcon.png`
  - `docs/reference/assets/menu-icons/ContaminationSensorIcon.png`
  - `docs/reference/assets/menu-icons/CampfireIcon.png`
  - `docs/reference/assets/menu-icons/GrillIcon.png`
  - `docs/reference/assets/menu-icons/BrazierIcon.png`
- Create or bind a fertile ash good icon that mostly preserves the current dirt/ash look, but adds a small green sprout emerging from the ash.
- Create or bind a tainted ash icon that uses the same dirt/ash base image, with the native contamination subicon language in the top-right.
- Create or bind separate fertilize-crop and fertilize-tree/bush menu button icons that read as area tools, not generic goods icons.
- Create or bind alert icons for aggregated fire consequence classes from `TWF-080`, including beaver danger, structure damage, plant/resource loss, fertile ash, and tainted ash.
- Preserve Timberborn icon conventions: square readable silhouette, simple material shading, restrained outline, no text, no photo texture, no off-style badge shape.
- Store generated source prompts, source asset references, final PNGs, and import/binding notes in repo-owned paths.
- Update localization or blueprint bindings if any icon sprite names change.
- Capture screenshots proving the icons render in the relevant alert, good, storage, and toolbar contexts.

## Dependencies

- `TWF-080` defines the consequence alert classes that need player-facing icons.
- `TWF-082` owns fertile ash collection/application and the crop/tree fertilization buttons.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Use the `imagegen` skill for bitmap generation or editing when producing new icon art.
- Do not overwrite Timberborn reference assets. Generated Wildfire assets should live under Wildfire-owned data or art paths.
- The tainted ash top-right mark should reuse the visual language of the contamination icons, not create a new hazard symbol.
- Fertile ash and tainted ash must remain distinguishable at toolbar and inventory scale.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if TypeScript asset tooling changes.
- Run `dotnet test` if C# bindings or blueprint-loading code changes.
- Live QA must capture screenshots of the final icons in the UI or document the exact binding blocker.

## Notes

- This ticket is specifically about release-quality icon assets and bindings. Gameplay behavior remains in `TWF-080`, `TWF-082`, `TWF-164`, and related consequence tickets.
- 2026-05-20 coordination note: Non-live implementation in `~/repos/wildfire-TWF-163-release-icons` passed review and static verification. Worker added final fertile ash, tainted ash, fertilize crop, and fertilize tree/bush icon assets plus candidate-only alert assets, bound fertile ash good/resource blueprints and fertilize toolbar buttons, and preserved TWF-080 alert icons as unbound candidates. Coordinator and reviewer visually inspected `docs/reference/assets/wildfire-icons/twf-163-icon-scale-contact-sheet.png`; icons read plausibly at small Timberborn UI scale. Checks passed: `git diff --check`, `dotnet test --no-restore` with 450 tests, reviewer-targeted `TimberbornReleaseIconBindingTests`, and reviewer full `dotnet test Wildfire.slnx --no-restore` with 450 tests. Live UI screenshot QA is blocked because Timberborn is not visible or command-responsive: coordinator `bun scripts/invoke-timberborn-command.ts status --wait=5` timed out waiting for `command-outbox.txt`, and visible process listing did not include Timberborn. Keep this ticket out of integration until screenshots prove the icons render in good/storage/resource and toolbar contexts, or until the binding blocker is resolved and documented with evidence.
