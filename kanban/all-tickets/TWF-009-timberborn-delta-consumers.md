---
ticket: TWF-009
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-004
   - TWF-005
   - TWF-008
write_scope:
   - src/Wildfire.Timberborn/**
   - docs/TEST_PLAN.md
---

# TWF-009: Consume Deltas In Timberborn

## Goal

Use compact deltas from the GPU simulator to update overlays, effects, damage, alerts, and gameplay consequences in Timberborn.

## Why

The simulator is useful only if the game responds to changed cells. This ticket connects the changed-cell stream to visible and gameplay-relevant Timberborn behavior while keeping visuals and gameplay on their intended data paths.

## Requirements

- Subscribe Timberborn systems to simulator deltas.
- Update overlays or debug visual state only for changed cells.
- Apply building, terrain, or resource consequences from relevant deltas.
- Trigger or update pooled visual effects from changed cells and/or GPU visual fields.
- Add alert or debug telemetry for major state changes.
- Keep gameplay reactions in C# delta consumers, not inside compute shaders.
- Add tests around pure decision logic where possible.
- Capture live validation evidence when environment access is available.

## Dependencies

- `TWF-004` compact delta readback.
- `TWF-005` GPU visual field output.
- `TWF-008` Timberborn fixed-cadence dispatch.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run live Timberborn QA with logs and screenshots when available.

## Notes

- Avoid broad gameplay tuning in this ticket. Focus on proving the delta-consumer path.

## Worker Notes

2026-05-01 worker pass on `codex/TWF-009-delta-consumers`:

- Added `TimberbornFireDeltaConsumer`, a Timberborn-side C# consumer for compact `CellDelta` records returned by `TimberbornFireSystem.Tick()`.
- The consumer classifies changed cells for debug visual state only when fuel, heat, water, or burning state changes. It records latest per-cell debug visual state by `CellIndex` and emits `wildfire_timberborn_delta_consumer_completed`.
- Exposed the last consumer summary through QA status fields: `last_delta_consumer_changed_cells`, `last_delta_consumer_debug_visual_cells`, `last_delta_consumer_started_burning`, and `last_delta_consumer_fuel_depleted`.
- Updated `docs/TEST_PLAN.md` with the new TWF-009 validation tokens and expected live QA fields.
- Kept fire rules in the GPU/Core path. Timberborn only consumes deltas and records adapter-side telemetry/debug visual state.

Evidence:

- `dotnet test` passed: 73 passed, 0 failed.
- `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
- `git diff --check` passed.

2026-05-01 follow-up worker pass on `codex/TWF-009-delta-consumers`:

- Addressed reviewer scope feedback by expanding the first slice from debug-state telemetry only into explicit Timberborn adapter hook surfaces: debug visual state, visual-effect events, gameplay-consequence events, and alert events.
- Kept this as a pure adapter/delta-consumer hook slice. The concrete Timberborn damage/effect/alert services are not bound here, and this ticket should not be described as full live gameplay integration until those services consume the new sink interfaces in a later ticket.
- Added `TimberbornFireDeltaConsumerSinks` and focused event records so real Timberborn services can subscribe without owning fire rules or mutating the core grid.
- Fixed debug visual spent-fuel state so `IsSpentFuel` reflects current cell state (`NewFuel == 0`), while `FuelDepleted` remains a transition counter (`oldFuel > 0 && newFuel == 0`).
- Exposed additional QA/status counters: `last_delta_consumer_visual_effect_events`, `last_delta_consumer_gameplay_consequences`, and `last_delta_consumer_alerts`.
- Added deterministic tests for delta decision classification, summary/status telemetry, sink routing, and the spent-fuel current-state behavior.

Evidence:

- `dotnet test` passed: 78 passed, 0 failed.
- `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
- `git diff --check` passed.

Follow-up work recorded:

- Create a later Timberborn integration ticket to bind concrete game services to `ITimberbornFireGameplayConsequenceSink`, `ITimberbornFireVisualEffectSink`, and `ITimberbornFireAlertSink` after the service APIs and live validation path are available.
- That follow-up should prove actual building/resource consequence application, pooled effect updates, and user-facing alerts in a loaded Timberborn save.

## Live QA Evidence - 2026-05-01

- Live save was loaded and unpaused through the TWF-030 QA flow, then inspected from `~/repos/wildfire-TWF-030`.
- Read-only command: `bun scripts/invoke-timberborn-command.ts status --wait=6`.
- Result token included `success=true`, `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, `tick_count=195`, `last_delta_count=0`, and all `last_delta_consumer_*` fields.
- Status fields observed:

   - `last_delta_consumer_changed_cells=0`
   - `last_delta_consumer_debug_visual_cells=0`
   - `last_delta_consumer_started_burning=0`
   - `last_delta_consumer_fuel_depleted=0`
   - `last_delta_consumer_visual_effect_events=0`
   - `last_delta_consumer_gameplay_consequences=0`
   - `last_delta_consumer_alerts=0`

- `~/Library/Logs/Mechanistry/Timberborn/Player.log` showed repeated `wildfire_timberborn_delta_consumer_completed` tokens for advancing ticks, including tick `195`, plus matching `wildfire_timberborn_dispatch_completed` tokens.
- The loaded save produced zero changed cells, so this proves the Timberborn delta-consumer path is wired and reporting, not that visible gameplay consequences have non-zero events yet.

## QA Notes - 2026-05-01 Power-Interruption Resume

- QA worktree: `~/repos/wildfire-TWF-009` at `396229eaf14d0bda94fa417f778b43b35a1d428a`.
- Safe prechecks passed:

   - `git diff --check`
   - `dotnet test`, 78 passed
   - `dotnet build Wildfire.slnx`, 0 warnings and 0 errors
   - `bun scripts/deploy-timberborn-mod.ts` dry-run, with `timberborn_running=true` and no writes

- Deployment path:

   - Quit Timberborn cleanly with `osascript -e 'tell application id "com.mechanistry.timberborn" to quit'`; process exited after 1 second.
   - Ran `bun scripts/deploy-timberborn-mod.ts --apply --lock-timeout=120` from `~/repos/wildfire-TWF-009`.
   - Deploy script confirmed `timberborn_running=false`, built `Wildfire.slnx`, rebuilt the Unity AssetBundles, wrote `~/Documents/Timberborn/Mods/Wildfire/manifest.json`, copied `Wildfire.Timberborn.dll`, `Wildfire.Core.dll`, PDBs, `wildfire_compute_mac`, `wildfire_compute_mac.manifest`, `wildfire_diagnostic_mac`, and `wildfire_diagnostic_mac.manifest`, then reported `deploy_complete`.
   - Deploy lock was released. Final lock check found no `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`.

- Launch/load path:

   - Initial post-deploy launch command used the TWF-030 utility from `~/repos/wildfire-TWF-030`: `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120`.
   - Initial launch artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-39-22-819Z/`.
   - The TWF-030 utility clicked the startup Mods dialog but failed early because it still saw `startup-mods`; the game advanced shortly afterward.
   - Jason manually clicked through the remaining modal/menu path. This means the TWF-009 live validation had manual UI assistance and should not be used as full TWF-030 startup automation proof.
   - Follow-up loaded-save utility pass artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-41-12-397Z/`.
   - Follow-up utility summary reported `wildfire_latest_save_startup_result=pass`, `already_unpaused tick_count_before=64 tick_count_after=66`, `post_status ... simulator_integrated=true`, and copied dispatch evidence through tick `66`.

- Live read-only command evidence from the deployed TWF-009 mod:

   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` returned `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=37 queued_changes=0 last_delta_count=0 last_delta_consumer_changed_cells=0 last_delta_consumer_debug_visual_cells=0 last_delta_consumer_started_burning=0 last_delta_consumer_fuel_depleted=0 last_delta_consumer_visual_effect_events=0 last_delta_consumer_gameplay_consequences=0 last_delta_consumer_alerts=0 message=loaded_game_ready`.
   - `bun scripts/invoke-timberborn-command.ts status --wait=6` returned `wildfire_command_result command=status success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=53 queued_changes=0 last_delta_count=0 last_delta_consumer_changed_cells=0 last_delta_consumer_debug_visual_cells=0 last_delta_consumer_started_burning=0 last_delta_consumer_fuel_depleted=0 last_delta_consumer_visual_effect_events=0 last_delta_consumer_gameplay_consequences=0 last_delta_consumer_alerts=0 message=ok`.

- Player.log evidence:

   - Live log path: `~/Library/Logs/Mechanistry/Timberborn/Player.log`.
   - Copied log path: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-41-12-397Z/Player.log`.
   - `Player.log:532` contains the successful `qa-readiness` result with all `last_delta_consumer_*` fields.
   - `Player.log:710` contains the successful `status` result with all `last_delta_consumer_*` fields.
   - `Player.log:817`, `828`, `841`, `852`, and later lines contain `wildfire_timberborn_delta_consumer_completed ... changed_cells=0 ... visual_effect_events=0 gameplay_consequences=0 alerts=0`.
   - `Player.log:851-853` show `wildfire_timberborn_gpu_readback_completed tick=66 delta_count=0`, `wildfire_timberborn_delta_consumer_completed tick=66 ...`, and `wildfire_timberborn_dispatch_completed tick=66 delta_count=0`.

- Screenshot artifacts in `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/2026-05-02T00-41-12-397Z/`:

   - `00-current-screen-01.png`, `1920 x 1080`
   - `04-loaded-save-before-unpause.png`, `1920 x 1080`
   - `05-loaded-save-after-unpause.png`, `1920 x 1080`

## QA Result - 2026-05-01

- Delta-consumer live telemetry: pass. `wildfire_timberborn_delta_consumer_completed` is emitted during live dispatch from the deployed TWF-009 mod.
- QA status/readiness fields: pass. Both `qa-readiness` and `status` expose the required `last_delta_consumer_*` fields.
- Loaded-game simulator integration: pass. Read-only command results reported `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128 x 128 x 23`, and advancing tick counts.
- Visual/gameplay/alert sink counters: pass for active hook telemetry, all zero in this save because live dispatch returned `delta_count=0`. This is consistent with the worker note that a valid loaded-save run may report zero changed cells until QA creates heat/fuel changes.
- Manual UI assistance caveat: the post-deploy load path depended on Jason clicking through the startup/modal path after the TWF-030 launch utility failed early. This does not invalidate the TWF-009 in-game telemetry, but it should not be counted as TWF-030 fresh-launch automation proof.
- Recommended board move: move TWF-009 to `05-integration`. Track real non-zero visual/gameplay consequences and concrete Timberborn service binding in a follow-up ticket.
