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
- 2026-05-06 coordinator static inspection: `~/Documents/Timberborn/ExperimentalSaves/Fuel/Fuel.timber` is the preferred Sprint 9 stable real-field acceptance candidate unless live QA chooses a better save. It is a `50x50` save with SHA-256 `9ad5f989f57676f2107c6e99563ba38a92ce91d3fab000eaba26485e2022ac41`, timestamp `2026-05-05 13:08:16`, game version `1.0.13.1-b769e88-xsm`, and `1766` entities. Static `world.json` inspection found four `WaterSource` entities at `x=12..15 y=6 z=3`, one `BadwaterSource` at `x=45 y=9 z=3`, connected fuel from `933` `Oak`, `15` `Birch`, and `769` `BlueberryBush` entities across the map, five storage entities (`SmallTank.Folktails` x3, `MediumWarehouse.Folktails`, `LargePile.Folktails`), 23 `Path` entities, and eight early Folktails structures (`DistrictCenter`, `Forester`, `GathererFlag`, `LumberjackFlag`, `WaterPump`). Storage contents include `Berries` in the warehouse and `Log` in the pile, with construction-site `Log` contents on the tanks, warehouse, and pile. The matching map file `~/Documents/Timberborn/Maps/Fuel.timber` is also `50x50` with SHA-256 `ecdbaa476a1299d4b5cbf1214d1e2e7814b2f7784beb9273e1062401dd049dd6`, but it has no built storage, paths, or Folktails buildings; prefer the save for Sprint 9 QA. Live readiness, command responsiveness, and final acceptance remain owned by the `TWF-144`/`TWF-148` QA run before this ticket should move out of `03-in-progress`.
- 2026-05-06 QA follow-up: the combined `TWF-144`/`TWF-148` run loaded the `Fuel` save under active QA, deployed current `main`, captured command transcripts, and proved command-responsive real-field simulation evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-144-148-live-20260506T071808Z/`. This is enough to treat `Fuel` as the Sprint 9 stable acceptance surface for follow-up gates. The run did not pass the visual gates, but the acceptance-surface blocker is now the visual path, not save stability or generated-scenario completeness.
- 2026-05-06 reviewer: accepted this ticket for closeout as a stable acceptance-surface selection ticket. The exact live-loaded autosave was `~/Documents/Timberborn/ExperimentalSaves/Fuel/2026-05-06 03h18m, Day 10-8.autosave.timber` with SHA-256 `5f5861e52e4c33857fa418fd7a9b8b8f855476ec724ac8eb296e1443f1343732`. The launch log preflighted it as `50x50x23`, reached `latest_save_startup_complete`, and command transcripts proved `runtime_loaded=true`, `loaded_game_ready=true`, `simulator_integrated=true`, imported water/badwater/storage/building/infrastructure/fuel fields, nonzero fire deltas, water-change proof, and cooldown. `TWF-144` and `TWF-148` remain blocked only on visual/renderer acceptance, not save stability or generated-scenario completeness.
