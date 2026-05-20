---
ticket: TWF-079
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-072
  - TWF-078
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-079-add-contamination-aware-fire-consequences.md
---

# TWF-079: Add Contamination Aware Fire Consequences

## Goal

Make fire consequences respect contamination without ever treating fire as a decontamination mechanic.

## Why

The design decision is explicit: fire never reduces contamination. Contaminated fuel may burn, contaminated water may suppress, and contaminated aftermath may be tainted, but soil, water, goods, plants, buildings, and beavers are not cleansed by fire.

## Requirements

- Keep contamination state Timberborn-owned; do not add contamination storage to `PackedCell`.
- Detect contaminated burn sources or contaminated affected cells through safe Timberborn adapter surfaces.
- Produce `tainted` ash instead of `fertile` ash when contaminated burn sources or contaminated soil are involved.
- Treat badwater or contaminated water as suppression input without converting it to safe water.
- Classify toxic smoke exposure for beaver telemetry and behavior tickets where safe field data exists.
- Provide contamination-aware field classifications used by `TWF-086` rather than implementing the toxic smoke beaver behavior here.
- Preserve native badwater contamination, treatment, and graphics paths only if live API tests prove them safe.
- Add deterministic tests for tainted ash classification, no-decontamination behavior, badwater suppression semantics, and toxic exposure classification where possible.
- Expose bounded QA/status telemetry for contaminated burn sources, tainted ash, toxic smoke exposure, and skipped unsafe API paths.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-072` provides beaver exposure telemetry that contaminated smoke may feed.
- `TWF-078` provides ash quality storage.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start by discovering read-only Timberborn adapter surfaces for contaminated soil, contaminated water, badwater, contaminated goods, and contaminated plant/building state.
- The invariant is strict: fire and high heat must not reduce contamination values.
- Badwater and contaminated water may suppress fire as water-like input, but the result should remain contaminated or tainted in aftermath classification. They must not create toxic or contaminated steam.
- Toxic smoke should be exported to telemetry and `TWF-086`; this ticket should not own beaver behavior.
- Expected counters include contaminated burn sources, contaminated affected cells, badwater suppression inputs, tainted ash classifications, toxic smoke cells, and skipped unsafe API reads.
- Safe no-op behavior should preserve vanilla contamination unchanged and skip only the Wildfire consequence branch that cannot be proven safe.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence or explicit safe-unavailable telemetry for at least one contamination-aware fire interaction.

## Notes

- 2026-05-17 coordinator/code reconciliation: part of this ticket landed through the off-sprint ash implementation. Simulator ash contamination now flows into the Timberborn ash read model as `tainted`, `TimberbornTaintedAshSoilPoisoningService` reports tainted-soil poisoning attempts, and status/QA tokens expose tainted ash counters. Remaining scope still includes broader contamination-aware fire interactions: badwater/contaminated-water suppression semantics, toxic smoke classification for beaver telemetry, and live proof that native contamination is not reduced.
- 2026-05-19 design correction: there is no toxic or contaminated steam in Wildfire. Keep contamination work on contaminated smoke, tainted ash, badwater suppression semantics, and no-decontamination proof.
- Do not add any implicit high-heat cleanup or sterilization behavior.
- Toxic smoke beaver behavior belongs to `TWF-086`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Contamination Interaction".
- 2026-05-20 clarification: "what is missing" means only the broad contamination contract that is not already covered by tainted ash and fertile ash tickets: prove fire never decontaminates native contamination, decide/read badwater or contaminated-water suppression as water-like input without creating toxic steam, and expose contaminated-smoke/toxic-exposure classifications for `TWF-086` if safe field data exists. Ash/water washout is split to `TWF-167`; tainted ash live proof remains `TWF-166`.
- 2026-05-20 Sprint 12 research pass: current code already has simulator ash contamination, tainted ash classification, contaminated-source/affected-cell detection, clean-steam separation, and beaver toxic-smoke exposure status tokens. After `TWF-166`, keep this ticket focused on no-decontamination proof, observable badwater/contaminated-water suppression telemetry, contaminated-source/affected-cell counters, and the end-to-end classification route that `TWF-086` can consume.
- 2026-05-20 review pass after repair: the first review rejected over-broad suppression counters and conflated burn-source/affected-cell contamination. The repaired diff passed review by separating `*_water_like_map_cells` from actual suppression inputs, reporting safe-unavailable for QA-injected water, and deriving contaminated burn-source counts from ash source events instead of soil contamination. Next gate is live QA.
- 2026-05-20 QA blocked before behavior gate: deployed `~/repos/wildfire-TWF-079-contamination-aware` from `codex/TWF-079-contamination-aware` with `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=30`, but Timberborn did not reach a visible window, fresh Wildfire runtime tokens, or command-responsive loaded save. Both bundle launch and `steam://rungameid/1062090` produced a `Timberborn` process with macOS `windows=0`; copied `Player.log` stopped after Steam client connection, and `bun scripts/invoke-timberborn-command.ts status --wait=5` timed out. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-079-contamination-aware-qa-20260520T040919Z/evidence-manifest.md`. The TWF-079 live behavior gate is not passed and must rerun from a loaded, command-responsive save before integration.
- 2026-05-20 live QA passed after Jason loaded `Fuel - Fuel` with trees available to burn. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-079-contamination-aware-rerun-20260520T061602Z/`. The command bridge proved the deployed TWF-079 worktree was command-responsive, `qa-water-suppression-stimulus contaminated-tree` selected a contaminated tree without native decontamination, and `qa-delta-stimulus contaminated-tree` produced tainted ash from contaminated affected-cell fire behavior. Key proof: `native_decontamination_attempts=0`, `contamination_fire_contaminated_affected_map_cells=258`, `contamination_fire_badwater_water_like_map_cells=36`, `contamination_fire_contaminated_water_like_map_cells=36`, `contamination_fire_badwater_suppression_inputs=0`, `contamination_fire_contaminated_water_suppression_inputs=0`, `contamination_fire_water_suppression_input_safe_unavailable=36`, `ash_field_entries=2`, `ash_field_tainted_cells=1`, `tainted_ash_poison_candidate_cells=1`, `tainted_ash_poison_skipped_no_safe_api=1`, and `Player.log` tick 230 recorded `ash_field_contaminated_affected_cells=1` with `ash_field_tainted_cells=1`. Startup media was deleted after proof compaction, leaving manifests, command transcripts, deploy/load logs, copied `Player.log`, and cleanup proof.
