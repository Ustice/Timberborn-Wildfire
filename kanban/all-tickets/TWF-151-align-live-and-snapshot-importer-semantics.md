---
ticket: TWF-151
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-139
  - TWF-140
  - TWF-146
write_scope:
  - scripts/export-timberborn-map-fixture.ts
  - scripts/compare-importer-parity.ts
  - tests/export-timberborn-map-fixture.test.ts
  - tests/compare-importer-parity.test.ts
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-151-align-live-and-snapshot-importer-semantics.md
---

# TWF-151: Align Live And Snapshot Importer Semantics

## Goal

Make `.timber` snapshot export and live Timberborn import classify terrain, entities, and water with matching semantics so `TWF-141` can accept importer parity.

## Requirements

- Decide the canonical parity unit for each material family: source count, occupied cell count, or resolved packed cell count.
- Align terrain semantics between snapshot export and live import. `TWF-141` found snapshot export counting many more terrain cells than live `ITerrainService` surface import.
- Align entity semantics between snapshot export and live import. `TWF-141` found tree, building, storage, infrastructure, water, and badwater counts diverging on the same 50x50 save.
- Preserve the shared material schema as the source of packed-cell and companion-field expectations.
- Expose enough metadata for parity reports to distinguish source counts from resolved cell counts.
- Keep Timberborn-specific API use in the adapter or scripts; do not move host rules into `Wildfire.Core`.
- Add deterministic tests that prove at least terrain, tree, building, storage, infrastructure, water, badwater, and empty cases use the same expected count semantics.

## Dependencies

- `TWF-139` provides live import.
- `TWF-140` provides snapshot export.
- `TWF-146` provides scenario checkpoints.

## Role

- Worker.
- Follow [../roles/worker.md](../roles/worker.md).

## Implementation Notes

- Start from the `TWF-141` evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-141-parity-20260504/`.
- Do not make the parity gate pass by relaxing it to vague counts.
- Prefer adding explicit `sourceCounts` versus `resolvedCellCounts` fields over overloading existing summary names.
- If live import cannot expose a comparable family yet, add precise telemetry and leave `TWF-141` blocked.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `bun test`.
- Run `dotnet test`.
- Regenerate the `TWF-141` export artifacts and confirm the parity report can compare matching count semantics.

## Notes

- 2026-05-04 `TWF-141` evidence showed the generated `TWF-133` scenario exports as `256x256x23` and exceeds the live auto-dispatch cap, while the closest accepted 50x50 Diorama save has matching dimensions but mismatched material counts between snapshot export and live import.
- 2026-05-04 first worker slice added explicit `parityCounts` to snapshot fixtures: `sourceCountsByMaterialClass`, `resolvedCellCountsByMaterialClass`, `terrainSolidVoxelCount`, `waterColumnSourceCount`, and `entitySourceCount`.
- Regenerated 50x50 Diorama fixture now proves the snapshot summary was source-oriented for entities/water and voxel-oriented for terrain. Live import still reports runtime source counts from occupied coordinates, so the remaining implementation choice is whether to align source models or expose comparable live resolved-cell counts.
- 2026-05-04 follow-up slice added live resolved-cell counters to the Timberborn importer summary and QA bridge. Live proof on the 50x50 Diorama save after redeploy reported `world_import_total_sources=4294`, source counts of terrain `2503`, tree `1311`, building `335`, storage `23`, water `32`, and badwater `90`, plus resolved counts of empty `53981`, terrain `1751`, tree `1311`, building `335`, storage `0`, water `32`, and badwater `90`. The resolved counts sum to the full `50x50x23 = 57500` field, and the storage source/resolved split shows why parity must compare explicit units instead of one overloaded material summary.
- 2026-05-04 added `scripts/compare-importer-parity.ts` so parity reports compare snapshot and live dimensions, source counts, and resolved-cell counts as separate units. Fresh comparison artifact: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-151-live-resolved-20260504/importer-parity-comparison.md`. It confirms dimensions match, but resolved cells still diverge because snapshot terrain is solid voxels while live terrain is terrain-service surface cells, and snapshot entities are mostly one saved `BlockObject.Coordinates` cell while live entities use runtime `PositionedBlocks.GetOccupiedCoordinates()`.
- 2026-05-04 aligned snapshot terrain semantics to exposed surface cells while preserving `terrainSolidVoxelCount` as diagnostic metadata. Fresh artifact: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-151-surface-terrain-20260504/importer-parity-comparison.md`. Snapshot and live terrain source counts now both report `2503`; remaining divergence is entity footprint expansion, vegetation/infrastructure classification, and water/badwater source semantics.
- 2026-05-04 aligned snapshot entity source counts to installed Blueprint footprint sizes, including BOM-tolerant Blueprint JSON loading. Fresh artifact: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-151-blueprint-footprints-20260504/importer-parity-comparison.md`. Tree source counts now match exactly at `1311`. Remaining deltas are focused on live-vs-snapshot family classification for buildings/storage/infrastructure/vegetation and water/badwater source expansion.
