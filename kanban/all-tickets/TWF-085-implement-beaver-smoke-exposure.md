---
ticket: TWF-085
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-071
  - TWF-072
  - TWF-073
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-085-implement-beaver-smoke-exposure.md
---

# TWF-085: Implement Beaver Smoke Exposure

## Goal

Implement the normal smoke beaver behavior variant: coughing slowdown, choking incapacitation if safe, and death only after sustained severe exposure is proven safe.

## Why

Normal smoke should mostly punish staying near a smoky field over time. It needs its own ticket so it can tune duration, thresholds, work interruption, and QA evidence without getting tangled with toxic smoke or direct fire damage.

## Requirements

- Consume smoke exposure classifications from `TWF-072` through the `TWF-073` behavior harness.
- Implement coughing as a bounded slowdown or work-inefficiency effect if Timberborn exposes a safe API.
- Implement choking as incapacitated or sleep-like behavior only if Timberborn exposes a safe API.
- Implement death only after sustained severe exposure and only if the safer states are proven in live evidence.
- Cancel or interrupt unsafe work where Timberborn supports it.
- Add deterministic tests for exposure accumulation, threshold transitions, recovery or decay, and safe no-op behavior.
- Expose bounded QA/status telemetry for exposed beavers, coughing, choking, deaths, skipped unsafe APIs, and recovery.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver field contract.
- `TWF-072` provides exposure telemetry.
- `TWF-073` provides the shared behavior harness.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Use exposure accumulation over multiple simulation ticks; do not require every exposed beaver to be processed in one game tick if the adapter can batch safely.
- Coughing should be the first live proof target because it is reversible and lower risk.
- Choking should use a sleep-like or incapacitated state only after the API spike proves that state can be entered and recovered safely.
- Death must stay gated behind sustained severe exposure and separate live evidence.
- Expected counters include exposed beavers, accumulated smoke exposure, coughing entered, coughing recovered, choking entered, choking recovered, deaths, batch skips, and unsafe API skips.
- Safe no-op behavior should still record telemetry and avoid killing or immobilizing beavers.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for coughing or an explicit safe unavailable state; choking and death require separate accepted evidence before they can be called implemented.

## Notes

- Toxic smoke behavior belongs to `TWF-086`.
- Fire and heat behavior belongs to `TWF-087`.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
- Reviewed implementation is parked on `~/repos/wildfire-TWF-085-beaver-smoke` / `codex/TWF-085-beaver-smoke` commit `4bdff943`. It adds normal-smoke accumulation, bounded batch/cooldown accounting, reversible coughing safe-no-op state, recovery decay, and skipped-unsafe candidate telemetry for choking/death.
- Coordinator review reran targeted behavior and command-token tests on 2026-05-20: `dotnet test Wildfire.slnx --no-restore --verbosity minimal --filter "FullyQualifiedName~TimberbornBeaverFieldBehaviorTests|FullyQualifiedName~TimberbornQaCommandBridgeTests"` passed `106/106`. Worker also reported full `dotnet test` `455/455`, `dotnet build`, and `git diff --check`.
- Live QA is blocked: `bun scripts/invoke-timberborn-command.ts status --wait=5` wrote the command inbox but timed out waiting for `command-outbox.txt`, and the process list showed Steam without a loaded Timberborn process. No coughing/smoke behavior is accepted until the same live gate passes.
- 2026-05-20 rerun evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-085-beaver-smoke-rerun-20260520T062731Z/` clears the older command-responsiveness blocker but does not clear the ticket. `qa-delta-stimulus beaver-exposure` reached a loaded, command-responsive `Fuel with beavers` save, but the normal-smoke-only probe repeatedly reported `beaver_field_exposure_exposed_beavers=0`, `beaver_field_behavior_smoke_decisions_applied=0`, and `beaver_field_behavior_smoke_coughing_entered=0`. The later nonzero behavior counters came from a mixed/contaminated state with toxic/fire-heat counters also nonzero, so they are not accepted as the normal-smoke gate.
- Current unblock path: keep the reviewed implementation parked, but repair the live QA stimulus or run procedure so it produces clean normal-smoke exposure near a sampled beaver without crossing fire or toxic-smoke thresholds. The uncommitted work in `~/repos/wildfire-TWF-085-beaver-smoke` currently narrows the beaver stimulus cell to smoke without fire/toxic thresholds; it still needs deterministic review plus a fresh live pass, likely with a broader candidate-cell smoke cloud or a tighter immediate sampling sequence so the moving beaver remains exposed.
- 2026-05-20 unblock attempt under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-085-beaver-smoke-unblock-20260520T151633Z/` proved the broader candidate-cell smoke cloud reaches normal-smoke behavior once (`beaver_field_behavior_smoke_decisions_applied=1`, toxic/fire behavior counters `0`), but it still did not enter coughing and the live field started fires (`last_player_fire_alert_started_fires=4`). The TWF-085 worktree keeps the existing smoke/field mechanics intact and only broadens the sampled beaver candidate-cell QA target; the next live gate is redeploying that reverted worktree and rerunning the beaver-exposure command.
- 2026-05-20 reverted-smoke live test under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-085-beaver-smoke-reverted-20260520T172250Z/` confirms the deployed candidate-cell-only QA patch runs without command-bridge issues. After manual unpause, `qa_delta_stimulus_sustained_heat_completed_cycles=12`, `beaver_field_exposure_sampled_beavers=3`, `beaver_field_exposure_exposed_beavers=2`, and `beaver_field_behavior_smoke_decisions_applied=2`. The gate still fails because the same exposure reported burn/toxic cells (`beaver_field_exposure_burn_cells=3-4`, `beaver_field_exposure_toxic_cells=3-4`), fire/heat behavior dominated (`beaver_field_behavior_fire_heat_decisions_applied=64`), and coughing never entered (`beaver_field_behavior_smoke_coughing_entered=0`). Next unblock should not change smoke/field mechanics; it should either improve the QA target/procedure to find naturally clean normal-smoke exposure or explicitly split this ticket's live acceptance away from the burn/heat stimulus path.
- Jason clarified that smoke should be able to move as a spreading plume, causing cascades and expanding range rather than staying effectively cell-local. The TWF-085 worktree now has a simulator-level smoke transport patch in `src/Wildfire.Unity/FireSim.compute`: each smoke source can fan out to multiple open neighboring air cells per dispatch, with moved-out mass capped by decayed source smoke, while preserving simulator-owned transport state. Focused deterministic verification passed: `dotnet test Wildfire.slnx --no-restore --verbosity minimal --filter "FullyQualifiedName~TimberbornGpuFieldRendererTests|FullyQualifiedName~TimberbornQaCommandBridgeTests|FullyQualifiedName~TimberbornBeaverFieldBehaviorTests"` reported `114/114`.
- 2026-05-20 deployed smoke-cascade live QA under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-085-smoke-cascade-20260520T175600Z/` shows partial progress, not acceptance. The first stimulus reached a clean normal-smoke window at ticks `655-664` with `beaver_field_exposure_respiratory_cells=1`, `beaver_field_exposure_burn_cells=0`, `beaver_field_exposure_toxic_cells=0`, `beaver_field_behavior_smoke_decisions_applied=3`, and toxic/fire behavior counters still `0` during that window. However `beaver_field_behavior_smoke_coughing_entered=0`, and a second stimulus moved back into noisy heat/fire evidence (`beaver_field_behavior_fire_heat_decisions_applied=9`). The current best unblock is to keep the simulator-owned cascade movement but adjust the QA target/procedure so one sampled beaver receives sustained clean normal-smoke exposure for enough consecutive accepted decisions to enter coughing.
- Follow-up selected-tree live probes under the same evidence root tried visible trees instead of the beaver-candidate smoke target. `selected-tree-visible-20260520T180550Z/` targeted an oak seedling on the right bank and `selected-tree-bridge-20260520T180630Z/` targeted the visible oak beside the bridge. Both produced real field activity, including tree/crop consequences, water changes, and expanding fire/smoke deltas, but neither exposed sampled beavers (`beaver_field_exposure_exposed_beavers=0`, respiratory/burn/toxic exposure cells all `0` across the sampled readiness checks). This makes visible-tree ignition a poor TWF-085 acceptance path on the current save/camera area; the next proof should select or create a source based on sampled beaver position, not merely visual tree proximity.
- 2026-05-20 drift-down attempt in `~/repos/wildfire-TWF-085-beaver-smoke` increases straight-down smoke weight from `0.22` to `0.55` and adds diagonal-down candidates to smoke movement totals and moved-out accounting. Focused deterministic verification passed again (`114/114`) and the build was deployed. Live QA under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-085-smoke-drift-down-20260520T182613Z/` shows the visible selected tree still does not expose beavers, but the beaver-anchored stimulus does reach one beaver (`beaver_field_exposure_exposed_beavers=1`, `smoke_decisions_applied=1`). It still fails acceptance because the same run also reported burn/toxic exposure (`burn_cells=4`, `toxic_cells=7-8`) and fire heat decisions (`fire_heat_decisions_applied=3`), while `smoke_coughing_entered=0`.
