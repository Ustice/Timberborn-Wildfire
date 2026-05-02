---
ticket: TWF-041
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-039
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-041-tune-visual-output-with-evidence.md
---

# TWF-041: Tune Visual Output With Evidence

## Goal

Tune fire, smoke, ash, and visibility output using shader snapshots and live Timberborn visual evidence.

## Why

The design says exact visual constants should be tuned from shader snapshots and visual validation. After the visual field is bound, this ticket makes the output reviewable and repeatable instead of relying on subjective live impressions alone.

## Requirements

- Review visual-field channel derivation for fire, smoke, ash, and visibility.
- Add or update accepted shader snapshot evidence for at least two meaningful scenarios.
- Capture live visual evidence from a loaded Timberborn save.
- Keep packed-cell storage unchanged unless a design decision explicitly changes it.
- Document accepted constants, commands, artifact paths, and interpretation in `docs/TEST_PLAN.md`.
- If ash requires stored burn history, record that as a design decision or follow-up instead of silently adding a field.

## Dependencies

- `TWF-039` provides the visual-field binding needed for live validation.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture screenshots or artifacts showing the tuned output in Timberborn.

## Notes

- This ticket may decide that ash remains a temporary heat/fuel approximation for now.

## Worker Notes

- Worktree: `~/repos/wildfire-TWF-041`.
- Branch: `codex/TWF-041`.
- Changed files:

   - `src/Wildfire.Unity/FireVisualField.cs`.
   - `src/Wildfire.Unity/FireSim.compute`.
   - `tests/Wildfire.Core.Tests/FireVisualFieldTests.cs`.
   - `tests/Wildfire.Core.Tests/UnityShaderExecutionHarnessTests.cs`.
   - `docs/TEST_PLAN.md`.
   - `kanban/all-tickets/TWF-041-tune-visual-output-with-evidence.md`.

- Tuned fire/smoke/ash/visibility output without changing packed-cell storage or adding a burn-history field.
- Accepted constants:

   - Fire: base `0.45`, heat weight `0.55`.
   - Smoke: base `0.12`, fuel weight `0.52`, heat weight `0.24`.
   - Ash: base `0.18`, inverse-fuel weight `0.5`, heat weight `0.32`.
   - Visibility: heat weight `0.55`, smoke weight `0.9`, ash weight `0.8`, raw fire still allowed to dominate visibility.

- Interpretation:

   - Hot burning cells remain fire-dominant.
   - Heavy-fuel cells at ignition can become smoke-dominant before peak fire.
   - Low-fuel residual-heat terrain gets stronger temporary ash.
   - Heat-only visibility is weighted down to reduce visual noise in the pooled presentation lane.

- Ash design decision: ash remains a temporary heat/fuel approximation for this ticket because `PackedCell` has no stored burn-history field. Persistent ash should be a follow-up design/storage decision rather than an implicit field addition.
- Kept gameplay consequences separate: the tuned visual output still flows through `VisualFields` and the TWF-039/TWF-040 presentation surfaces; no `Wildfire.Core` rules or Timberborn gameplay consequence logic were changed.

## Evidence

- Focused visual tests passed:

   - `dotnet test --filter FullyQualifiedName~FireVisualFieldTests`.

- Accepted shader snapshot artifacts:

   - Root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/`.
   - `single-ignition`, seed `21`, grid `5x5x1`, ticks `2`: `single-ignition-seed21-5x5x1-tick2.capture.json`, checksum `visual-fnv1a32:8710B4BB`.
   - `line-of-fuel`, seed `42`, grid `12x5x1`, ticks `4`: `line-of-fuel-seed42-12x5x1-tick4.capture.json`, checksum `visual-fnv1a32:BFDB9857`.
   - Unity logs `single-ignition-unity.log` and `line-of-fuel-unity.log` include `phase=compile`, `phase=buffer`, `phase=dispatch`, and `phase=readback` `status=ok` tokens.

- Opt-in shader harness passed:

   - `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled`.

- Live Timberborn evidence:

   - Root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-live-20260502T213953Z`.
   - Fresh deploy: `deploy-output.txt` shows `dotnet build Wildfire.slnx --configuration Debug`, rebuilt `wildfire_compute_mac` / `wildfire_diagnostic_mac`, staged the mod from `~/repos/wildfire-TWF-041`, and released the deploy lock.
   - Loaded save: `latest-save-startup/2026-05-02T21-40-20-246Z/latest-save-startup-summary.txt`, with post-unpause status at `tick_count=3`.
   - Stimulus/readiness outputs: `qa-delta-stimulus-output.txt`, `qa-readiness-after-delta-output.txt`, `status-after-delta-output.txt`, `qa-delta-stimulus-second-output.txt`, and `qa-readiness-second-after-delta-output.txt`.
   - `qa-readiness-after-delta-output.txt` reported `last_delta_consumer_visual_effect_events=1`, `visual_field_surface_bound=true`, `visual_field_surface_cells=376832`, `active_pooled_fire_effects=1`, `updated_visual_regions=1`, `pooled_fire_effect_presentation_failures=0`, `pooled_fire_effects_visible_enabled=true`, `pooled_fire_effects_native_prefab_resolved=true`, and `pooled_fire_effects_native_prefab=CampfireFire`.
   - Copied log: `Player.log`; extracted proof: `relevant-player-log-tokens.txt`.
   - Log proof includes the TWF-039 surface bind/update tokens, TWF-040 pooled effect sink token, `wildfire_timberborn_pooled_fire_effect_native_prefab_resolved kind=fire prefab=CampfireFire`, and `wildfire_timberborn_pooled_fire_effects_updated` ticks with `active_pooled_effects=1`, `updated_visual_regions=1`, `presentation_failures=0`.
   - Screenshot artifacts: `live-after-tuned-delta-effects.png` and `live-after-second-tuned-delta-effects.png`.
   - Final cleanup: `final-lock-process-state.txt` shows the QA lock directory empty and no Timberborn process remaining after the run.

## Blockers

- None for implementation.
- Visual review caveat: the bounded QA stimulus is brief and settles quickly; the strongest live proof is the paired screenshot artifact plus command/log counters showing native `CampfireFire` effect resolution and active pooled visual output. Broader aesthetic approval across map scales remains QA/product review work.

## QA Results

- QA date: 2026-05-02.
- QA role worktree: `~/repos/wildfire-TWF-041`.
- QA branch/commit: `codex/TWF-041` at `d877a95 Tune visual output evidence for TWF-041`.
- QA artifact root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/`.
- Worker evidence confirmed:

   - Worker shader artifacts exist under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/`.
   - Worker live artifacts exist under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-live-20260502T213953Z/`.
   - Worker screenshots `live-after-tuned-delta-effects.png` and `live-after-second-tuned-delta-effects.png` are `1920x1080` PNGs.
   - Worker copied log and command artifacts include `Player.log`, `relevant-player-log-tokens.txt`, `qa-delta-stimulus-output.txt`, `qa-readiness-after-delta-output.txt`, `status-after-delta-output.txt`, `qa-delta-stimulus-second-output.txt`, and `qa-readiness-second-after-delta-output.txt`.

- Commands run:

   - `git diff --check`.
   - `dotnet test --filter FullyQualifiedName~FireVisualFieldTests`.
   - `dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests`.
   - `dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/single-ignition-seed21-5x5x1.fixture.json"`.
   - `"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-041/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/single-ignition-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/single-ignition-seed21-5x5x1.fixture.json" --shader ~/repos/wildfire-TWF-041/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/single-ignition-seed21-5x5x1-tick2.capture.json" --ticks 2`.
   - `dotnet run --project src/Wildfire.Cli -- --scenario=line-of-fuel --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/line-of-fuel-seed42-12x5x1.fixture.json"`.
   - `"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-041/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/line-of-fuel-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/line-of-fuel-seed42-12x5x1.fixture.json" --shader ~/repos/wildfire-TWF-041/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/shader-snapshots/line-of-fuel-seed42-12x5x1-tick4.capture.json" --ticks 4`.
   - `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=120`.
   - `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=120 --artifacts-dir="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z/live/latest-save-startup"`.
   - `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=10 --require-advanced-tick`.
   - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick`.
   - `bun scripts/invoke-timberborn-command.ts status --wait=10 --require-advanced-tick`.
   - Second tight capture pass: `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=10 --require-advanced-tick`, then repeated `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=2 --require-advanced-tick` until active visual counters were observed.
   - `dotnet test`.

- Automated and shader results:

   - `git diff --check`: PASS.
   - `FireVisualFieldTests`: PASS, 5 tests.
   - `ShaderSnapshotHarnessTests`: PASS, 4 tests.
   - Full `dotnet test`: PASS, 125 tests.
   - `single-ignition`, seed `21`, `5x5x1`, ticks `2`: PASS, regenerated checksum `visual-fnv1a32:8710B4BB` at `shader-snapshots/single-ignition-seed21-5x5x1-tick2.capture.json`.
   - `line-of-fuel`, seed `42`, `12x5x1`, ticks `4`: PASS, regenerated checksum `visual-fnv1a32:BFDB9857` at `shader-snapshots/line-of-fuel-seed42-12x5x1-tick4.capture.json`.
   - Unity capture logs contain `phase=compile`, `phase=buffer`, `phase=dispatch`, and `phase=readback` `status=ok` tokens for both accepted scenarios. The logs also contain non-blocking Unity CDN timeout chatter after successful readback.

- Live Timberborn results:

   - Fresh deploy: PASS. `live/deploy-output.txt` shows Debug build success, rebuilt `wildfire_compute_mac` and `wildfire_diagnostic_mac`, staged from `~/repos/wildfire-TWF-041`, and released `build-deploy.lock`.
   - Load latest save and unpause: PASS. `live/load-latest-save-and-unpause-output.txt` shows startup Mods and Experimental Mode gates positively identified, `main.continue` clicked, loaded-save screenshots captured, unpause clicked, `post_status_ok tick_count=3`, and the lock released.
   - Fresh screenshot artifacts: PASS. `live/live-after-tuned-delta-stimulus.png`, `live/live-after-tuned-delta-readiness.png`, and `live/live-second-poll-1.png` are `1920x1080` PNGs from the loaded save.
   - Live tuned visual output: PASS. `live/qa-readiness-second-poll-1.txt` reported `last_delta_count=1`, `last_delta_consumer_visual_effect_events=1`, `visual_field_surface_bound=true`, `visual_field_surface_cells=376832`, `active_pooled_fire_effects=1`, `updated_visual_regions=1`, `pooled_fire_effect_presentation_failures=0`, `pooled_fire_effects_visible_enabled=true`, `pooled_fire_effects_native_prefab_resolved=true`, and `pooled_fire_effects_native_prefab=CampfireFire`; `live/live-second-poll-1.png` was captured in that same active-counter window.
   - Live log proof: PASS. `live/Player.log` and `live/relevant-player-log-tokens.txt` include diagnostic and compute AssetBundle load tokens, `wildfire_timberborn_gpu_visual_field_surface_bound ... channels=fire,smoke,ash,visibility`, `wildfire_timberborn_delta_consequence_sink_bound lane=pooled_fire_smoke_ash_effects`, `wildfire_timberborn_pooled_fire_effect_native_prefab_resolved kind=fire prefab=CampfireFire`, `wildfire_timberborn_qa_delta_stimulus_queued`, `wildfire_timberborn_changes_registered source=qa_delta_stimulus`, and follow-up dispatch/consumer/presentation tokens for the active visual event.

- Acceptance criteria:

   - Review/tune fire, smoke, ash, and visibility derivation: PASS. Constants are documented in `docs/TEST_PLAN.md`, mirrored in `FireVisualField.cs` and `FireSim.compute`, and covered by focused tests.
   - Add/update accepted shader snapshot evidence for at least two meaningful scenarios: PASS. `single-ignition` and `line-of-fuel` are documented and regenerated by QA with matching checksums.
   - Capture live visual evidence from a loaded Timberborn save: PASS. QA captured loaded-save screenshots and a live screenshot during an active pooled-effect counter window, backed by command outputs and `Player.log`.
   - Keep packed-cell storage unchanged unless explicitly changed: PASS. The committed files do not add packed-cell storage fields; visual derivation reads existing `PackedCell` fuel/heat/terrain state only.
   - Document accepted constants, commands, artifact paths, and interpretation in `docs/TEST_PLAN.md`: PASS.
   - Ash-storage decision explicit: PASS. `docs/TEST_PLAN.md` and this ticket both state ash is a temporary heat/fuel approximation because `PackedCell` has no burn-history field, and persistent ash needs a future storage/design decision.

- Cleanup:

   - Timberborn was quit after QA.
   - `live/final-lock-process-state.txt` shows only the locks directory and no Timberborn process. The remaining `UnityCodeModel.dll` process belongs to `~/repos/Timberborn-Prometheus`, not this QA run.

- QA result: PASS.
- Recommended board move: move `TWF-041` to `05-integration` for coordinator integration. Current checkout still has the status symlink under `kanban/by-status/02-ready/`; QA did not move board symlinks.

## Completion

- QA passed on 2026-05-02.
- Recommended board move: move `TWF-041` to `05-integration` for coordinator integration.
