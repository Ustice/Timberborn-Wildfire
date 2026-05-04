---
ticket: TWF-132
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-118
  - TWF-131
write_scope:
  - scripts/generate-wildfire-scenario-save.ts
  - tests/generate-wildfire-scenario-save.test.ts
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-132-fix-generated-scenario-placement-validity.md
  - kanban/all-tickets/TWF-119-validate-generated-world-consequence-scenario-save.md
---

# TWF-132: Fix Generated Scenario Placement Validity

## Goal

Make the world-consequence scenario generator place validation checkpoints in Timberborn-valid locations so generated objects survive load validation.

## Why

`TWF-119` proved the archive now loads under `caffeinate -disu`, reaches a command-responsive save, and preserves the fixed `save_metadata.json` shape from `TWF-131`. The remaining blocker is content validity: Timberborn's Loading issues dialog deletes generated water sources, badwater sources, trees, paths, storage, and structure pads as invalid locations before QA can inspect the scenario.

## Requirements

- Use the `TWF-119` evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T152225Z` as the failure source.
- Keep the generator as a Bun/TypeScript tool.
- Keep output confined to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios`.
- Determine why cloned generated entities are invalid after load: coordinates, terrain support, block occupancy, component state, template assumptions, or missing terrain/channel mutation.
- Prefer a deterministic archive-level fix with tests before live Timberborn QA.
- Update the manifest so blocked or fallback placements are explicit and future QA can tell which checkpoints are expected to survive.
- Generate a new fixed artifact and manifest under a new `TWF-132` evidence folder.
- Preserve the accepted `TWF-131` metadata timestamp shape.
- Do not mutate Timberborn fire rules or move host-specific logic into `Wildfire.Core`.

## Dependencies

- `TWF-118` owns the original scenario generator.
- `TWF-131` fixed the save metadata timestamp shape so Timberborn can read the archive.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- The failing `TWF-119` exact-save evidence loaded `Wildfire world consequence scenario TWF-119.timber`, then Timberborn deleted `Badwater Source (4)`, `Water Source (4)`, `Birch (4)`, `Oak (4)`, `Pine (4)`, `Path (3)`, `Small Tank`, `Large Pile`, and `Medium Warehouse` as invalid locations.
- The first `Continue` pass in that evidence loaded an autosave and should not be used for placement judgment.
- The exact generated archive statically matched all generated manifest entities before Timberborn load validation, so the blocker is not missing ZIP writes.
- Crop pads and storage inventory already had manifest blockers; fix or explicitly defer those separately from the invalid-location deletions.
- A passing worker result should leave `TWF-119` ready for a QA-only rerun with the new artifact path.

## Verification

- Run `git diff --check`.
- Run `bun test tests/generate-wildfire-scenario-save.test.ts`.
- Run the generator against the same copied template lineage used by `TWF-131` and preserve the new artifact path.
- If feasible within the worker pass, perform an archive inspection proving the generated survivor coordinates and manifest agree before handing back to QA.
- Live Timberborn loading and visual checkpoint acceptance still belong to `TWF-119` after this fix is ready.

## Notes

- 2026-05-03 coordinator: created after `TWF-119` live QA under active `caffeinate -disu` proved launch/readiness is healthy but generated checkpoints are deleted by Timberborn loading issues.
- 2026-05-03 worker diagnosis: the failing `TWF-119` exact save statically contained every manifest-generated clone, but `Player.log` reported Timberborn deleting the loaded BlockObjects at the planned coordinates as not backward compatible. The generator was cloning template BlockObjects into arbitrary planned coordinates while leaving `Singletons.TerrainMap.Voxels.Array` unchanged, so there was no deterministic archive-level proof that those coordinates had valid terrain/channel/support.
- 2026-05-03 first worker fix was rejected in review because it blocked every otherwise valid placement and produced no survivor checkpoints.
- 2026-05-03 resumed worker fix: changed `scripts/generate-wildfire-scenario-save.ts` to avoid cloning planned checkpoints into unvalidated coordinates, but to emit survivor-expected checkpoints by relocating each requested category/template to existing template-supported BlockObject coordinates when available. Shortages remain honest manifest blockers.
- 2026-05-03 worker evidence: generated artifact `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-132-template-supported-checkpoints-20260503T154213Z`. Manifest inspection: generator version `TWF-132.0`, `generatedEntities=24`, `blockedPlacements=6`, `schemaBlockers=9`, no `save_metadata` blockers. Generated checkpoint counts: badwater source `2`, water source `4`, tree `12`, warehouse `1`, pile `1`, tank `1`, path `3`. Blockers: two extra planned `BadwaterSource` placements because the template only has two existing supported badwater sources, and four `Carrot` placements because the template has no carrot BlockObject prototypes.
- 2026-05-03 archive inspection: generated `world.json` entity count remains `2246`, matching the template, because the manifest now points QA at existing template-supported coordinates rather than injecting clones. Every manifest generated checkpoint matched an existing archive entity by template and coordinate (`missing=0`). `save_metadata.json.Timestamp` is `05/03/2026 11:42:13`.
- 2026-05-03 worker verification passed after review fix: `bun test tests/generate-wildfire-scenario-save.test.ts`, generator run against `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-131-worker/template-copy.timber`, manifest/archive inspection with `jq`/`unzip`, and `git diff --check`.
- 2026-05-03 review failed: the safety guard is useful but does not satisfy this ticket as worded. `terrainPlacementBlocker` always returns a non-empty string, so `mutateWorldEntities` blocks every otherwise valid planned placement and can never generate survivor checkpoints. Return this ticket to worker implementation with the review requirement to keep the honest blocker behavior but add an actual valid placement strategy: mutate/validate terrain and support, or derive known-valid checkpoint locations from the template. Add a success-path test proving nonzero survivor-expected checkpoints can be emitted when support is valid.
- 2026-05-03 second review passed: no blocking findings. The generator now derives checkpoint coordinates from existing template `Template` plus `BlockObject.Coordinates` entries and consumes those coordinates per requested template with shortage blockers, addressing the earlier zero-checkpoint failure. Review noted that this unblocks a narrower `TWF-119` rerun for load survival and manifest checkpoint presence; it does not yet prove original water/badwater flow layout, crop pads, or storage inventory.
- 2026-05-03 coordinator verification: `bun test tests/generate-wildfire-scenario-save.test.ts` passed with 5 tests, and `git diff --check` passed for the generator/test/doc/ticket scope.
