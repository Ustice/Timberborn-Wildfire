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

# TWF-133: Validate Large Scenario Acceptance Surface

## Goal

Give Sprint 9 one stable large-scenario acceptance surface for real-field tuning, visuals, and consequence follow-up. The surface can be the generated world-consequence scenario or a better stable real save.

## Why

`TWF-119` and `TWF-132` proved useful generated-scenario mechanics, but the generated scenario should not become a technicality that blocks real gameplay proof. If a real save is more stable and better at exercising connected fuel, water, badwater, crops, trees, structures, storage, and camera lanes, use that save as the acceptance surface and keep generated-scenario work as reusable QA tooling.

## Requirements

- Keep the generator as a Bun/TypeScript tool.
- Preserve `TWF-131` save metadata timestamp compatibility.
- Preserve `TWF-132` honest manifest behavior: unsupported planned placements must be blocked rather than injected into invalid load locations.
- Implement or select one accepted large-layout strategy:
  - validate and mutate `TerrainMap.Voxels.Array` plus support/channel state for planned coordinates, or
  - use a purpose-built known-valid template layout whose manifest explicitly maps the world-consequence lanes, or
  - use an existing stable save when it loads reliably and exercises the needed fields.
- Produce or document checkpoints for water sources, badwater sources, trees, crops or harvestable equivalents, structures, storage, connected fuel, and camera/path lanes.
- Prove or explicitly record water and badwater behavior where the selected save supports it.
- Prove or explicitly record storage inventory contents or a safe reason they remain deferred.
- If using a generated artifact, write it under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios`.
- If using an existing save, preserve its exact path, copied archive or checksum, map dimensions, checkpoint notes, and rationale for why it is a better acceptance surface.
- `50x50` saves can be used for this Sprint 9 real-field gate when they are stable and useful. Sprint 10 owns the separate `256x256` map creation and max-size local-fire proof in `TWF-156`.
- Leave `TWF-119` evidence untouched except for notes pointing to the new full-layout artifact.

## Dependencies

- `TWF-119` provides the load-survival evidence and current narrowed blocker list.
- `TWF-132` provides the valid-placement manifest contract.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start by identifying known stable saves or generating one from a stable template.
- Do not reintroduce cloned arbitrary BlockObjects that Timberborn will delete on load.
- The current survivor artifact has `generatedEntities=24`, `blockedPlacements=6`, and fallback template dimensions `128x128x23`.
- Remaining full-scope blockers from `TWF-119`: original 50 by 50 layout, crop pads, full badwater source count, water/badwater flow layout, storage inventory, and contamination test areas.
- Do not spend sprint time forcing an oversized generated save to load if a stable real save proves the real-field system and exercises the gameplay surfaces better.

## Verification

- Run `git diff --check`.
- If generator code changes, run `bun test tests/generate-wildfire-scenario-save.test.ts`.
- If using a generated artifact, run the generator and preserve the artifact path plus manifest.
- If using an existing save, preserve the exact save path, checksum or copied artifact, dimensions, checkpoint notes, and why it is preferred.
- Inspect the archive to prove declared checkpoints match the selected save before live QA.
- Live Timberborn QA must load the selected save under active `caffeinate -disu`, prove command readiness after unpause, and verify the selected checkpoints or exact remaining blockers.

## Notes

- 2026-05-03 coordinator: created as the follow-up for full generated-scenario content after `TWF-119` passed only the narrower `TWF-132` load-survival and manifest checkpoint-presence gate.
- 2026-05-04 worker pass added valid-template fallback support for harvestable crop equivalents (`Carrot`, `Potato`, `Wheat`, `Sunflower`) and Folktails paths (`Path.Folktails`) so the generator can map modern known-valid saves without cloning unsupported planned BlockObjects.
- Manifest output now records static evidence for water span, badwater span, and storage-good references. The generated `TWF-133` artifact at `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-133-full-layout-20260504T174048` uses `Home (20).timber` and reports three badwater sources, four water sources, 12 tree checkpoints, four crop checkpoints, one warehouse, one pile, one tank, and three path tiles. Storage evidence references `Carrot`, `Log`, and `Water`.
- Static archive inspection matched every manifest-declared generated checkpoint back to `world.json` in the generated `.timber` archive. The remaining blocker is honest: the known-valid template has only three `BadwaterSource` entities for the planned four, and live QA still needs to confirm flow direction.
- Live QA on 2026-05-04 did not load the artifact because `bun scripts/load-latest-save-and-unpause.ts --launch --wait=120 --lock-timeout=120` stayed on the main menu after repeated `Continue` clicks, including a manual `cliclick` injection. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-04T21-41-46-757Z`.
- Manual load evidence on 2026-05-04 showed Timberborn did open `Wildfire world consequence scenario TWF-133.timber` and Wildfire reached the adapter load path for a `256x256x23` grid (`1,507,328` cells), then the command bridge stopped responding before processing `qa-readiness`. To avoid adding Wildfire-owned pressure while full-map tiling/active-region dispatch is still unimplemented, the runtime now skips simulator initialization for live grids above `500,000` cells. A follow-up live run logged `wildfire_timberborn_runtime_initialize_skipped`, but Timberborn still became AppleEvent-unresponsive on this oversized generated save, so the load utility now preflights the newest `.timber` save and refuses to click `Continue` when the estimated `width x height x 23` cells exceed `500,000`. Use `--skip-latest-save-preflight` only for intentional manual stress runs.
- 2026-05-06 direction update: generated-scenario completeness should not block Sprint 9 if the newer real-field system works on a stable save. The user later clarified that the `256x256` proof should roll into Sprint 10 rather than block Sprint 9; `TWF-156` owns creating that max-size scenario map.
