---
ticket: TWF-089
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-065
  - TWF-043
  - TWF-046
write_scope:
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-089-tune-fuel-burn-down-duration.md
---

# TWF-089: Tune Fuel Burn Down Duration

## Goal

Tune how long fuel lasts once burning begins.

## Why

Burn duration controls whether fire feels like a brief visual flash or a sustained hazard. It also drives resource loss, building rollback, smoke duration, and aftermath timing.

## Requirements

- Keep fuel consumption rules in `FireSim.compute`.
- Compare burn-down duration across low, medium, and high fuel inputs.
- Preserve deterministic shader snapshots for accepted constants.
- Use live recordings to verify burn duration reads correctly in normal camera angles.
- Keep spread pace, suppression behavior, and visual prefab tuning out of this ticket.
- Document accepted evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides recording tooling.
- `TWF-043` provides the current game-feel baseline.
- `TWF-046` proves the coherent live loop.

## Parent Reference

- Parent gate: `TWF-069`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture recordings or shader evidence showing accepted burn-down timing.

## Implementation Notes

- Compare low, medium, and high fuel cases before tuning constants so spread pace changes from `TWF-088` do not hide burn-duration behavior.
- Keep resource consequence accounting out of this ticket; this ticket only owns simulator fuel duration.

## Notes

- This ticket tunes simulator fuel behavior, not resource consequence accounting.
- 2026-05-03 coordinator: moved to `03-in-progress` for Sprint 6 worker implementation in `~/repos/wildfire-TWF-089` on branch `codex/TWF-089-fuel-burn-duration`. The worktree is based on reviewed `TWF-088` commit `8eff5cf6adf85cf8729ab19c1abdb592a7f549e3` so burn-duration tuning uses the current spread-pace baseline, but this ticket cannot integrate before the `TWF-088` live QA blocker is resolved.
- 2026-05-03 worker result: deterministic implementation complete in commit `082077d2b99819c4b448b0ba9fe758ed81f4f412`. Tuned only fuel burn-down duration in `FireSim.compute` by adding a separate deterministic fuel-decrement roll at `3/4` burn pressure; ignition, spread pace, water suppression, structure behavior, cooling, visuals, and Timberborn consequence accounting are unchanged.
- 2026-05-03 worker evidence: committed low/medium/high fuel burn-down shader fixtures and captures under `tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089/`. Accepted depletion ticks are low fuel `7`, medium fuel `15`, and high fuel `27`. Regenerated release shader snapshots for the accepted constants and documented evidence in `docs/TEST_PLAN.md`.
- 2026-05-03 worker verification: `git diff --check` passed, `dotnet test` passed with 134 tests, and the opt-in Unity shader harness passed. Unity logs and fixture mirrors live under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-fuel-burn-duration/`.
- 2026-05-03 worker blocker: live Timberborn burn-duration recording remains required before integration, and `TWF-089` cannot be integration-ready until the `TWF-088` live QA blocker is resolved.
- 2026-05-03 coordinator: moved to `04-verify` for review and QA triage. Do not move to `05-integration` unless review passes, `TWF-088` is accepted, and this ticket's live recording gate passes.
- 2026-05-03 review: passed review on commit `082077d2b99819c4b448b0ba9fe758ed81f4f412` with no blocking findings. Review confirmed the production change decouples heat-producing burn ticks from fuel decrement ticks while preserving the existing burn-pressure roll for heat, and found no Timberborn-owned logic, resource consequence accounting, visual prefab tuning, suppression behavior, or board-file changes.
- 2026-05-03 review verification: `git diff --check 8eff5cf6adf85cf8729ab19c1abdb592a7f549e3..082077d2b99819c4b448b0ba9fe758ed81f4f412` passed, `dotnet test` passed with 134 tests, and the opt-in Unity shader harness passed.
- 2026-05-03 coordinator: moved to `07-blocked` because live Timberborn burn-duration recording is still required and this stacked ticket cannot integrate before the `TWF-088` live QA blocker is resolved.
- 2026-05-03 coordinator update: `TWF-088` live QA passed and moved to `06-done`, so this ticket is unblocked for its required live burn-duration recording gate. Moved back to `04-verify`; implementation and review are already complete, but do not integrate until live recording evidence passes.
- 2026-05-03 QA result: live deploy/load/readiness and recording capture succeeded from `~/repos/wildfire-TWF-089` at commit `082077d2b99819c4b448b0ba9fe758ed81f4f412`, but the burn-duration recording gate did not pass. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-20260503T162018Z/`. The 35 second recording is `recordings/2026-05-03T16-22-04-247Z-high/recording.mov`, with metadata, copied `Player.log`, command output, and final process/lock state in the same evidence root. `qa-readiness --require-advanced-tick` passed at `tick_count=18`; `qa-delta-stimulus` queued fixed center cell `188480` at `x=64 y=64 z=11` with hard-coded `set_cell=13311` (`fuel=15`); follow-up status showed `started_burning=1`, `active_pooled_fire_effects=1`, native prefab `CampfireFire`, and player alert proof at tick `39`.
- 2026-05-03 QA limitation: the current allowlisted live bridge exposes only `qa-delta-stimulus`, `qa-building-burnout-stimulus`, and `qa-water-suppression-stimulus`. It does not expose low/medium/high fuel selection or per-cell fuel-total/burn-down telemetry, and the fixed `qa-delta-stimulus` did not produce `last_delta_consumer_fuel_depleted>0` during the sampled tick window `38` through `77`. Smallest owner: add a QA-only burn-duration stimulus/status surface that queues named low/medium/high fuel cells and reports durable per-target burn start/depletion ticks, or explicitly narrow this ticket's live gate to the existing deterministic shader evidence plus one medium fixed-stimulus readability recording.
- 2026-05-03 coordinator: moved back to `07-blocked` on new unblocker `TWF-134`. The shared live QA gate is healthy; the blocker is missing burn-duration-specific live proof controls and telemetry.
- 2026-05-03 unblock: `TWF-134` passed review and moved to `06-done`. Rerun this ticket's live gate using `qa-burn-duration-stimulus low`, `qa-burn-duration-stimulus medium`, and `qa-burn-duration-stimulus high`, then sample `status` or `qa-readiness` for durable `burn_duration_proof_*` fields. Acceptance requires observed depletion or an explicit design decision for any `no_depletion_timeout`.
- 2026-05-03 worker update from `TWF-134`: the missing proof surface has been implemented for review. Live QA should retry with `qa-burn-duration-stimulus low`, `qa-burn-duration-stimulus medium`, and `qa-burn-duration-stimulus high`, then sample `status` or `qa-readiness` for `burn_duration_proof_status=depleted`, target identity, target coordinates, initial fuel, burn start tick, depletion tick, and elapsed burn ticks. A `no_depletion_timeout` status remains a live QA failure for this ticket.
- 2026-05-03 QA preflight result: live low/medium/high proof was not run because no verified deploy source currently contains both the reviewed `TWF-089` `FireSim.compute` tuning and the reviewed `TWF-134` bridge command. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-retry-preflight-20260503T163733Z/`. `main` exposes `qa-burn-duration-stimulus` and `burn_duration_proof_*` in `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`, but its `src/Wildfire.Unity/FireSim.compute` still decrements fuel on every successful burn roll and lacks `FIRE_FUEL_BURN_DOWN_*`. `~/repos/wildfire-TWF-089` at `082077d2b99819c4b448b0ba9fe758ed81f4f412` has the accepted shader tuning, but its command bridge has no `qa-burn-duration-stimulus` or `burn_duration_proof_*` surface. The currently loaded Timberborn save was healthy and command-responsive under `caffeinate -disu` PID `94422`, but `help` listed only `help,qa-building-burnout-stimulus,qa-delta-stimulus,qa-readiness,qa-water-suppression-stimulus,status`, and `qa-readiness` had no `burn_duration_proof_*` fields. No depletion acceptance was attempted; smallest unblock is to integrate/rebase a single deployable source tree that contains both TWF-089 shader tuning and TWF-134 live proof command before rerunning the live gate.
- 2026-05-03 coordinator: moved back to `07-blocked` on new unblocker `TWF-135`, which owns reconciling a single deployable tree that contains both the accepted TWF-089 shader tuning and the accepted TWF-134 live proof command.
- 2026-05-03 TWF-135 worker update: this checkout now contains the reviewed `TWF-089` `FireSim.compute` fuel burn-down tuning, release shader captures, and low/medium/high `twf-089` shader fixtures/captures together with the already-present `TWF-134` `qa-burn-duration-stimulus` and durable `burn_duration_proof_*` bridge surface. Live QA can retry the low, medium, and high proof after `TWF-135` review passes.
- 2026-05-03 TWF-135 review update: reconciliation review passed with no blocking findings. Use this reconciled checkout for the next live gate: run `qa-burn-duration-stimulus low`, `qa-burn-duration-stimulus medium`, and `qa-burn-duration-stimulus high`, then preserve `status` or `qa-readiness` output showing `burn_duration_proof_status=depleted` and non-placeholder burn start, depletion, and elapsed tick fields for each target.
- 2026-05-03 QA retry result from the reconciled checkout: live burn-duration proof failed before low/medium/high sampling because the deployed mod crashed after Continue while loading the latest save. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-reconciled-20260503T165753Z/`. `caffeinate -disu` PID `94422` was active, deploy from `~/repos/wildfire` passed, Timberborn launched as PID `12585`, and the latest-save helper captured the crash screen under `latest-save-startup/2026-05-03T16-58-22-566Z/` before timing out waiting for `loaded-save`. `Player.log` reports `BinditoException: TimberbornPlayerFireAlertCameraFocus has more than one parameterful constructors.` A direct `help` command probe timed out, so command responsiveness was not proven, `qa-burn-duration-stimulus` could not be confirmed from live `help`, and no `burn_duration_proof_*` fields exist for low, medium, or high. Copied evidence includes `deploy-apply-clean.txt`, `load-latest-save-and-unpause.txt`, `help-command-probe.txt`, `Player.log`, `player-log-crash-and-command-extract.txt`, `error-report-2026-05-03-12h59m04s.zip`, screenshot samples, and final lock/process state files. This is not a `no_depletion_timeout`; it is a live startup/DI blocker that must be cleared before rerunning this ticket's low/medium/high depletion gate.
- 2026-05-03 coordinator: moved back to `07-blocked` on `TWF-136`. Rerun the low/medium/high live proof after `TWF-136` fixes the `TimberbornPlayerFireAlertCameraFocus` Bindito constructor ambiguity and review/live startup validation passes.
- 2026-05-03 coordinator update: `TWF-136` review and live QA passed. The Bindito constructor crash is cleared, startup reached loaded save, command bridge responsiveness passed, and alert focus logged `wildfire_timberborn_player_fire_alert_focus_ready cell_index=188480` plus `wildfire_timberborn_player_fire_alert_focused cell_index=188480 x=64 y=64 z=11` under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-136-qa-20260503T172225Z/`. Move this ticket back to `04-verify` and rerun the low/medium/high burn-duration proof from the reconciled checkout.
- 2026-05-03 QA final retry result: FAIL. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-final-20260503T173607Z/`. Reused the running Timberborn session because `caffeinate -disu` PID `94422` was active, Timberborn PID `43324` was loaded and command-responsive, and live `help` included `qa-burn-duration-stimulus`. Ran `qa-burn-duration-stimulus low`, `qa-burn-duration-stimulus medium`, and `qa-burn-duration-stimulus high`, polling `status` until each proof reached a terminal state, then captured final `qa-readiness --require-advanced-tick`, `Player.log`, command transcripts, final screen, and final lock/process state. All three targets started burning but failed the required depletion proof with `burn_duration_proof_status=no_depletion_timeout`: low target `188479` at `x=63 y=64 z=11`, initial fuel `4`, burn start tick `367`, depletion tick `placeholder`, elapsed burn ticks `65`; medium target `188480` at `x=64 y=64 z=11`, initial fuel `9`, burn start tick `458`, depletion tick `placeholder`, elapsed burn ticks `65`; high target `188481` at `x=65 y=64 z=11`, initial fuel `15`, burn start tick `524`, depletion tick `placeholder`, elapsed burn ticks `65`. This is not a startup, deploy, Bindito, or command-bridge setup failure; loaded-game readiness remained true and no deploy lock remained. The live gate must be fixed and rerun before integration.
- 2026-05-03 coordinator: moved back to `03-in-progress` because the live low/medium/high proof exposed a real burn-duration behavior failure. No new worker was dispatched because the sprint is paused for direct visual-effects iteration.
