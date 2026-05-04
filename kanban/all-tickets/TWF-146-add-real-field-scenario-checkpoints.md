---
ticket: TWF-146
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-133
  - TWF-137
write_scope:
  - scripts/generate-wildfire-scenario-save.ts
  - tests/generate-wildfire-scenario-save.test.ts
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-146-add-real-field-scenario-checkpoints.md
---

# TWF-146: Add Real Field Scenario Checkpoints

## Goal

Extend the generated QA scenario manifest so importer, tuning, visual, and consequence gates can target known real map objects.

## Requirements

- Add manifest checkpoints for terrain, tree, crop, building, storage, infrastructure, water, badwater, and empty control cells.
- Include expected material class, expected resource or template ID where known, expected packed-cell band, and expected companion-field category.
- Report blocked placements when the generator cannot create or preserve a checkpoint.
- Keep generated saves loadable by Timberborn.
- Add tests that manifest checkpoint counts and categories are deterministic.

## Dependencies

- `TWF-133` provides the full generated scenario layout.
- `TWF-137` defines expected material and field categories.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Do not implement importer logic here.
- Do not clone unsupported Timberborn entities just to satisfy the manifest.
- Prefer existing valid entities when Timberborn rejects generated placement.
- The output manifest should be easy for QA scripts and humans to compare with live `status` output.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `bun test`.

## Notes

- This ticket makes the generated QA map useful as an acceptance fixture instead of just a load-survival artifact.
- 2026-05-04 worker pass added manifest `fieldCheckpoints` for terrain, empty controls, tree, crop, building, storage-origin structure, infrastructure, water, and badwater categories. Each checkpoint includes coordinates, template identity where known, expected source material class, expected resolved cell material class, packed-cell band values from `MaterialFieldSchema.v1`, and companion-field expectations.
- Dry-run against `Wildfire world consequence scenario TWF-133.timber` produced checkpoints for every required category except the already-blocked fourth badwater source placement. The manifest still reports the generator's known blockers: terrain/channel carving is not implemented, storage inventory is template-dependent, crop pads depend on crop prototypes, and only three valid badwater-source prototypes were available in that template.
