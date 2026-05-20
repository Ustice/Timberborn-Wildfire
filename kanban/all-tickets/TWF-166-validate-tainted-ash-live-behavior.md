---
ticket: TWF-166
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-078
  - TWF-082
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-166-validate-tainted-ash-live-behavior.md
---

# TWF-166: Validate Tainted Ash Live Behavior

## Goal

Prove the narrow tainted-ash behavior that was split out of `TWF-078` and `TWF-082`: contaminated fire aftermath produces tainted ash, tainted ash is distinguishable from clean fertile ash, and tainted ash does not enter the fertile-ash collection or growth path.

## Why

Jason confirmed the clean `TWF-078` and `TWF-082` gates, but tainted ash has not been tested. Keeping tainted ash inside those tickets would make accepted clean-ash behavior look blocked. This ticket keeps the unproven contaminated-ash surface honest without reopening the broader ash and fertile-ash work.

## Requirements

- Start from current `main` and the accepted clean-ash behavior.
- Use a contaminated source, contaminated affected cell, badwater-adjacent target, or another explicit live route that should classify ash as tainted.
- Prove tainted ash creation through simulator/readback status and copied `Player.log` evidence.
- Prove tainted ash is not collectable as `FertileAsh`.
- Prove tainted ash does not apply the clean-ash growth bonus.
- Prove tainted ash presentation or status is distinguishable from clean ash, or record the exact missing presentation surface.
- Preserve native gameplay safety: do not reduce native contamination or mutate badwater/contamination state unless a safe API is explicitly proven.
- Keep tainted ash decay and water washout out of scope unless `TWF-164` has landed first.
- Keep broad contamination-aware consequence design in `TWF-079`; this ticket is only the live tainted-ash verification slice.

## Dependencies

- `TWF-078` confirms the clean ash read model and persistence path.
- `TWF-082` confirms the clean fertile-ash collection/application path.
- `TWF-079` owns the broader contamination-aware consequence semantics, but this ticket can verify the current narrow tainted-ash surface before that broader work lands.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Reuse existing QA commands and telemetry where possible before adding new surfaces.
- If the current command set cannot reliably create tainted ash, add the smallest allowlisted QA route needed to target a contaminated source and report why the existing route was insufficient.
- Use separate evidence roots from clean-ash QA so contaminated and uncontaminated outcomes are not mixed.
- Keep `TimberbornAshFieldService` as a derived read model. Do not add a second tainted-ash authority.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck` if TypeScript QA scripts change.
- Live QA must include copied `Player.log`, command transcripts, status or `qa-readiness` output showing tainted ash counters, and screenshot or recording evidence if presentation is visible.
- Passing live QA must prove no `FertileAsh` collection from tainted ash and no clean growth bonus from tainted ash, or record precise safe-unavailable telemetry for the missing native API.

## Notes

- 2026-05-20 created after Jason confirmed `TWF-078` and `TWF-082` but said tainted ash has not been tested and should be split into a narrower ticket.
- 2026-05-20 worker/reviewer pass: `qa-delta-stimulus tainted-ash` is an artificial simulator ash-contamination injection route for downstream tainted-ash behavior. Prefer `qa-delta-stimulus contaminated-tree` when a contaminated tree exists; use `tainted-ash` only to prove no `FertileAsh` collection, no clean growth bonus, and distinct tainted-ash status/presentation. Do not treat the artificial route as proof that contaminated fuel or source classification naturally produced tainted ash.
- 2026-05-20 live QA passed from `~/repos/wildfire-TWF-166-tainted-ash-live` branch `codex/TWF-166-tainted-ash-live`; evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-166-live-20260520T033350Z/`. `caffeinate -disu` was active, deploy used `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60`, and the loaded save was `~/Documents/Timberborn/ExperimentalSaves/Fuel/2026-05-19 18h22m, Day 5-15.autosave.timber`. `qa-delta-stimulus contaminated-tree` succeeded, targeting tree cell `x=44 y=23 z=4` / index `11194`; no `tainted-ash` fallback was used.
- 2026-05-20 live QA evidence: baseline `commands/04-baseline-qa-readiness.txt` showed loaded/command-responsive runtime with clean ash (`ash_field_entries=747`, `ash_field_fertile_cells=747`, `ash_field_tainted_cells=0`). After `commands/05-qa-delta-stimulus-contaminated-tree.txt` and `commands/06-qa-readiness-after-contaminated-tree.txt`, status reported `ash_field_entries=749`, `ash_field_fertile_cells=748`, `ash_field_tainted_cells=1`, `ash_field_growth_candidate_cells=748`, `ash_field_growth_skipped_tainted_cells=1`, `tainted_ash_poison_candidate_cells=1`, `tainted_ash_poison_skipped_no_safe_api=1`, and `fertile_ash_collection_skipped_tainted_or_spent_cells=1`. Final `commands/07-final-status.txt` preserved the same tainted counters at tick `16952`.
- 2026-05-20 live QA artifacts: copied `Player.log` is `logs/Player.log`; visible evidence is `screenshots/02-after-contaminated-tree-tainted-ash.png`; load/startup screenshots and status transcripts are under `load/2026-05-20T03-38-18-411Z/`. Player log contains `wildfire_timberborn_runtime_ready`, `wildfire_command_bridge_ready`, `wildfire_timberborn_runtime_simulator_initialized`, repeated `wildfire_timberborn_ash_field_updated ... tainted_ash_cells=1`, `wildfire_timberborn_tainted_ash_soil_poisoning_applied ... skipped_no_safe_api=1`, and `wildfire_timberborn_fertile_ash_collection_applied ... skipped_tainted_or_spent_cells=1`. QA result: pass for live downstream tainted-ash gate, with status-distinct rather than visually distinct tainted presentation; recommend `05-integration`.
