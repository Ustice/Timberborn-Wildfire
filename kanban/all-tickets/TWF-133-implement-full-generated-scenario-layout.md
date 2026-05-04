---
ticket: TWF-133
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-119
  - TWF-132
write_scope:
  - scripts/generate-wildfire-scenario-save.ts
  - tests/generate-wildfire-scenario-save.test.ts
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-133-implement-full-generated-scenario-layout.md
  - kanban/all-tickets/TWF-119-validate-generated-world-consequence-scenario-save.md
---

# TWF-133: Implement Full Generated Scenario Layout

## Goal

Turn the load-surviving `TWF-132` generated save into the intended world-consequence scenario with valid water, badwater, crop, storage, structure, tree, and camera checkpoints.

## Why

`TWF-119` now proves Timberborn can load a generated artifact under active `caffeinate -disu` and preserve manifest-declared existing-coordinate checkpoints. That is not the full scenario promised by the original generator plan. The manifest still blocks the 50 by 50 generated layout, two planned badwater source slots, crop pads, storage inventory, and water/badwater flow layout because the generator does not yet carve terrain/channels or guarantee support for arbitrary planned coordinates.

## Requirements

- Keep the generator as a Bun/TypeScript tool.
- Preserve `TWF-131` save metadata timestamp compatibility.
- Preserve `TWF-132` honest manifest behavior: unsupported planned placements must be blocked rather than injected into invalid load locations.
- Implement one accepted full-layout strategy:
  - validate and mutate `TerrainMap.Voxels.Array` plus support/channel state for planned coordinates, or
  - use a purpose-built known-valid template layout whose manifest explicitly maps the world-consequence lanes.
- Produce manifest-declared checkpoints for water sources, badwater sources, trees, crops or harvestable equivalents, structures, storage, and camera/path lanes.
- Prove or explicitly record north-to-south water and badwater flow.
- Prove or explicitly record storage inventory contents or a safe reason they remain deferred.
- Generate a new artifact under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios`.
- Leave `TWF-119` evidence untouched except for notes pointing to the new full-layout artifact.

## Dependencies

- `TWF-119` provides the load-survival evidence and current narrowed blocker list.
- `TWF-132` provides the valid-placement manifest contract.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-132-template-supported-checkpoints-20260503T154213Z`.
- Do not reintroduce cloned arbitrary BlockObjects that Timberborn will delete on load.
- The current survivor artifact has `generatedEntities=24`, `blockedPlacements=6`, and fallback template dimensions `128x128x23`.
- Remaining full-scope blockers from `TWF-119`: original 50 by 50 layout, crop pads, full badwater source count, water/badwater flow layout, storage inventory, and contamination test areas.

## Verification

- Run `git diff --check`.
- Run `bun test tests/generate-wildfire-scenario-save.test.ts`.
- Run the generator and preserve the artifact path plus manifest.
- Inspect the archive to prove manifest-declared full-layout checkpoints match the generated save before live QA.
- Live Timberborn QA must load the artifact under active `caffeinate -disu`, prove command readiness after unpause, and verify the full-layout checkpoints or exact remaining blockers.

## Notes

- 2026-05-03 coordinator: created as the follow-up for full generated-scenario content after `TWF-119` passed only the narrower `TWF-132` load-survival and manifest checkpoint-presence gate.
- 2026-05-04 worker pass added valid-template fallback support for harvestable crop equivalents (`Carrot`, `Potato`, `Wheat`, `Sunflower`) and Folktails paths (`Path.Folktails`) so the generator can map modern known-valid saves without cloning unsupported planned BlockObjects.
- Manifest output now records static evidence for water span, badwater span, and storage-good references. The generated `TWF-133` artifact at `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-133-full-layout-20260504T174048` uses `Home (20).timber` and reports three badwater sources, four water sources, 12 tree checkpoints, four crop checkpoints, one warehouse, one pile, one tank, and three path tiles. Storage evidence references `Carrot`, `Log`, and `Water`.
- Static archive inspection matched every manifest-declared generated checkpoint back to `world.json` in the generated `.timber` archive. The remaining blocker is honest: the known-valid template has only three `BadwaterSource` entities for the planned four, and live QA still needs to confirm flow direction.
- Live QA on 2026-05-04 did not load the artifact because `bun scripts/load-latest-save-and-unpause.ts --launch --wait=120 --lock-timeout=120` stayed on the main menu after repeated `Continue` clicks, including a manual `cliclick` injection. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-04T21-41-46-757Z`.
- Manual load evidence on 2026-05-04 showed Timberborn did open `Wildfire world consequence scenario TWF-133.timber` and Wildfire reached the adapter load path for a `256x256x23` grid (`1,507,328` cells), then the command bridge stopped responding before processing `qa-readiness`. To avoid adding Wildfire-owned pressure while full-map tiling/active-region dispatch is still unimplemented, the runtime now skips simulator initialization for live grids above `500,000` cells. A follow-up live run logged `wildfire_timberborn_runtime_initialize_skipped`, but Timberborn still became AppleEvent-unresponsive on this oversized generated save, so the load utility now preflights the newest `.timber` save and refuses to click `Continue` when the estimated `width x height x 23` cells exceed `500,000`. Use `--skip-latest-save-preflight` only for intentional manual stress runs.
