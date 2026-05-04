---
ticket: TWF-043
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-036
  - TWF-038
  - TWF-041
write_scope:
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-043-tune-fire-game-feel-constants.md
---

# TWF-043: Tune Fire Game Feel Constants

## Goal

Tune spread speed, burn duration, heat loss, material flammability, and water suppression so the first fire loop feels coherent in live Timberborn.

## Why

After Sprint 3, fire should be visible and player-facing. The next design step is making that loop feel playable rather than merely proving the pipeline. The tuning must stay in the GPU simulation and adapter-owned material inputs, not in a parallel C# fire path.

## Requirements

- Tune shader constants or material bands for spread speed, burn duration, heat loss, flammability, and suppression.
- Keep fire-spread rules in `FireSim.compute`.
- Keep Timberborn as an adapter that supplies material and water inputs.
- Preserve deterministic stochastic behavior from seed, tick, and cell index.
- Use at least two representative scenarios and one live Timberborn run as tuning evidence.
- Record accepted constants, commands, artifact paths, screenshots, and interpretation in `docs/TEST_PLAN.md`.
- Update `docs/DESIGN.md` only for durable rule-shape or tuning-contract decisions.

## Dependencies

- `TWF-036` provides building consequences.
- `TWF-038` provides water suppression input.
- `TWF-041` provides visual tuning evidence and live visual validation.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture live screenshots, command/status evidence, copied `Player.log`, and a short note explaining whether the tuned loop is playable.

## Notes

- Do not broaden this into new mechanics such as wind or diagonal spread unless `TWF-044` has already accepted that decision.

## Worker Notes

Worker pass on 2026-05-02 in worktree `~/repos/wildfire-TWF-043`.

Changed tuning:

- `src/Wildfire.Unity/FireSim.compute` now names the game-feel constants for ignition, neighbor spread, water suppression, burn pressure, and burn heat. The rule still uses only seed, tick, and cell index for stochastic burn rolls, and fire spread remains in the compute shader.
- Neighbor heating now gives burning neighbors enough direct heat to spread out of broad grass scenarios instead of being lost to integer averaging.
- Water now applies stronger heat suppression, raises ignition threshold by two heat bands per water band, and applies a larger burn-pressure penalty before fuel consumption.
- Timberborn adapter material bands now make vegetation and stockpile resources catch more readily while wood-like buildings burn longer and less explosively:
  - Wood-like building: fuel `15`, flammability `1`, heat loss `3`.
  - Stockpile resource: fuel `8`, flammability `2`, heat loss `3`.
  - Vegetation: fuel `10`, flammability `3`, heat loss `1`.
- QA building-burnout stimulus now uses the tuned wood-like building constants for both the primed and spent cells.

Representative shader evidence:

- Artifact directory: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/`.
- `single-ignition`, seed `21`, `5x5x1`, tick `2`: visual checksum `visual-fnv1a32:50C4978E`, per-tick deltas `[5, 5]`, final hot cells `5`.
- `line-of-fuel`, seed `42`, `12x5x1`, tick `4`: visual checksum `visual-fnv1a32:120F70AE`, per-tick deltas `[5, 5, 5, 2]`, final hot cells `5`.
- `water-barrier`, seed `42`, `12x5x1`, tick `4`: visual checksum `visual-fnv1a32:40818F57`, per-tick deltas `[5, 5, 5, 5]`, final hot cells `1`.

Commands run:

- `dotnet test`
- `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled`
- `dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1.fixture.json"`
- `dotnet run --project src/Wildfire.Cli -- --scenario=line-of-fuel --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1.fixture.json"`
- `dotnet run --project src/Wildfire.Cli -- --scenario=water-barrier --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1.fixture.json"`
- Unity batchmode captures were run for the three fixtures above with `Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture`; logs and capture JSON are in the artifact directory.

Proposed doc updates for the TWF-044/doc owner:

- `docs/DESIGN.md`: no update in this worker cleanup; the branch preserves the TWF-044 release decisions from `1315a4d`.
- `docs/TEST_PLAN.md`: completed in the review cleanup as an additive TWF-043 section compatible with the TWF-044 conservative release-validation expectations.

Blockers and follow-up:

- Live Timberborn gameplay evidence was not captured in this worker pass. This ticket still needs QA for live screenshots, command/status evidence, copied `Player.log`, and a short playable-loop interpretation.

Review cleanup on 2026-05-02:

- Rebasing/updating onto `main` at `1315a4d Resolve release simulation decisions` preserved the TWF-044 docs and board state; this diff does not touch `docs/DESIGN.md` or `kanban/by-status`.
- `docs/TEST_PLAN.md` now records accepted TWF-043 constants, artifact paths, commands, interpretation, semantic snapshot outcomes, and the live screenshot/`Player.log` QA requirement.
- `UnityShaderExecutionHarnessTests` now asserts per-tick delta counts and final hot-cell counts for `single-ignition`, `line-of-fuel`, and `water-barrier` in addition to visual checksums.
- Re-run evidence: `git diff --check`, `dotnet test`, and the opt-in Unity shader harness all passed after cleanup.

## QA Notes

QA pass on 2026-05-02 against `main` commit `3c9a144e7b0af2d86b40f757294a7f3af04660f8`.

Shared deploy/QA lock:

- Preflight: `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock` was absent and `pgrep -ax Timberborn` returned no running Timberborn process.
- `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=1` acquired and released the lock normally.
- `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --artifacts-dir "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-live-20260502T225802Z-startup"` acquired and released the lock normally.
- Final lock state: `build-deploy.lock absent`.

Live artifact directory:

- `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-live-20260502T225802Z-startup/2026-05-02T22-58-02-926Z/`

Live commands and status evidence:

- `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=1`
  - Result: pass. `dotnet build Wildfire.slnx --configuration Debug` succeeded, Unity rebuilt `wildfire_compute_mac` and `wildfire_diagnostic_mac`, and the script staged the mod under `~/Documents/Timberborn/Mods/Wildfire`.
- `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --artifacts-dir "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-live-20260502T225802Z-startup"`
  - Result: pass. The guarded launcher observed startup Mods, Experimental Mode, main menu, and loaded-save screens, then unpaused the save. `latest-save-startup-summary.txt` records `wildfire_latest_save_startup_result=pass`, `simulator_integrated=true`, dimensions `128x128x23`, and post-unpause `tick_count=3`.
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick`
  - Result: pass. `command-qa-readiness-baseline.txt` records `tick_count=22`, `simulator_integrated=true`, `visual_field_surface_bound=true`, and `queued_changes=0`.
- `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick`
  - Result: pass. `command-qa-delta-stimulus.txt` records the bounded center target `target_x=64`, `target_y=64`, `target_z=11`, and `queued_changes=1`.
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-nonzero-delta`
  - Result: pass. `command-qa-readiness-after-delta.txt` records `tick_count=25`, `last_delta_count=1`, `last_delta_consumer_changed_cells=1`, `last_delta_consumer_visual_effect_events=1`, `last_player_fire_alert_started_fires=1`, `player_fire_alert_notification_sent=true`, `pooled_fire_effects_visible_enabled=true`, and `pooled_fire_effects_native_prefab=CampfireFire`.
- `bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick`
  - Result: pass. `command-qa-water-suppression-stimulus.txt` records `target_x=64`, `target_y=64`, `target_z=11`, `set_water=3`, and `queued_water_changes=1`.
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed`
  - Result: pass. `command-qa-readiness-after-water.txt` records `tick_count=27`, `last_positive_water_changed_tick=26`, and `last_positive_water_changed_count=1`.
- Follow-up fast capture: `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick`, `screencapture -x 08-after-second-qa-delta-stimulus-fast.png`, then `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick`
  - Result: pass. `command-qa-readiness-after-second-delta.txt` records `tick_count=75`, `last_delta_count=2`, `last_delta_consumer_started_burning=1`, `last_delta_consumer_water_changed=1`, `last_delta_consumer_visual_effect_events=2`, `last_delta_consumer_alerts=1`, `player_fire_alert_notifications=2`, `active_pooled_fire_effects=1`, `pooled_fire_effects_visible_enabled=true`, and `pooled_fire_effects_native_prefab=CampfireFire`.

Copied logs and screenshots:

- Startup/load/unpause copied log: `Player.log`.
- Full live-sequence copied log: `Player-after-twf-043-live-sequence.log`.
- Follow-up copied log after second delta: `Player-after-twf-043-second-delta.log`.
- Log extracts: `player-log-token-extract.txt` and `player-log-token-extract-after-second-delta.txt`.
- Loaded-save screenshots: `04-loaded-save-before-unpause.png` and `05-loaded-save-after-unpause.png`.
- Fire-loop screenshots: `06-after-qa-delta-stimulus.png`, `07-after-qa-water-suppression.png`, and `08-after-second-qa-delta-stimulus-fast.png`.

Key copied `Player.log` evidence:

- Mod/runtime load: `wildfire_timberborn_diagnostic_asset_loaded`, `wildfire_timberborn_compute_asset_loaded`, and `wildfire_timberborn_runtime_simulator_initialized`.
- Delta stimulus: `wildfire_command_request command=qa-delta-stimulus`, `wildfire_timberborn_changes_registered source=qa_delta_stimulus`, `wildfire_timberborn_qa_delta_stimulus_queued cell_index=188480 x=64 y=64 z=11 set_cell=13311`.
- Non-zero fire dispatch: tick `24` logged `wildfire_timberborn_gpu_readback_completed tick=24 delta_count=2`, `wildfire_timberborn_pooled_fire_effect_native_prefab_resolved kind=fire prefab=CampfireFire`, `wildfire_timberborn_pooled_fire_effects_updated tick=24 active_pooled_effects=1 updated_visual_regions=2 visible_effects_enabled=true native_effect_prefab_resolved=true`, `wildfire_timberborn_player_fire_alert_updated tick=24 fire_started=1 fuel_spent=0 max_heat=15 notification_sent=true`, and `wildfire_timberborn_dispatch_completed tick=24 delta_count=2`.
- Water suppression: `wildfire_command_request command=qa-water-suppression-stimulus`, `wildfire_timberborn_changes_registered source=qa_water_suppression`, `wildfire_timberborn_qa_water_suppression_queued cell_index=188480 x=64 y=64 z=11 set_water=3 queued_water_changes=1`, and tick `26` logged `wildfire_timberborn_delta_consumer_completed ... water_changed=1`.
- Second fast delta: tick `75` logged `active_pooled_effects=1`, `started_burning=1`, `water_changed=1`, `alerts=1`, and `notification_sent=true`.

Acceptance result:

- Tune shader constants/material bands: pass from worker/review shader evidence plus live deploy of commit `3c9a144`.
- Keep spread rules in `FireSim.compute`: pass by review/work notes; QA did not find any live evidence of Timberborn-owned fire-rule mutation.
- Keep Timberborn as adapter for material/water inputs: pass. Live water proof used the bounded `qa-water-suppression-stimulus` command and showed `SetWater=3` registering through the GPU simulator path.
- Preserve deterministic stochastic behavior: pass from worker/review shader harness evidence; live QA exercised the deterministic center-cell QA commands.
- At least two representative scenarios plus one live Timberborn run: pass. The ticket already records three accepted shader scenarios, and this QA pass captured one deployed live Timberborn run.
- Record constants, commands, artifact paths, screenshots, and interpretation: pass. Worker/review updated `docs/TEST_PLAN.md`; this QA note adds the live commands, artifacts, screenshots, copied logs, and interpretation.
- Live QA evidence requirement: pass with caveat. Screenshots show the loaded save and visible player-facing Wildfire alert. The native pooled fire effect is proven by status/logs as active at ticks `24` and `75`, but it is not visually legible in the saved camera screenshots, likely because this bounded center-cell stimulus is small and transient from the current camera angle.

Playable-loop interpretation:

- The live loop is coherent enough to accept this tuning ticket: the save loads, dispatch advances, the bounded fire stimulus produces non-zero GPU deltas, the player receives a clear native alert with max heat, the native `CampfireFire` presentation path resolves without failures, and water suppression registers as a durable positive water-change event.
- The screenshots are useful for loaded-save and player-alert evidence, not for judging the full visual/gameplay feel of spreading fire. The broader visible-feel judgment should remain with `TWF-046`, where QA can use a scenario or save designed to put a multi-cell front in camera view.

Recommended board move:

- Move `TWF-043` from `04-verify` to `05-integration`.
