---
ticket: TWF-040
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-039
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-040-add-pooled-fire-smoke-ash-effects.md
---

# TWF-040: Add Pooled Fire Smoke Ash Effects

## Goal

Add a bounded pooled-effects layer that presents fire, smoke, and ash from simulator output without creating one effect object per simulated cell.

## Why

The design explicitly avoids one entity per visual fire, smoke, or ash cell. Once the visual field reaches Timberborn, the adapter needs a pooled presentation layer that looks alive while staying bounded and easy to disable during QA.

## Requirements

- Drive effect placement or intensity from visual-field output and compact deltas where appropriate.
- Use pooling or another bounded strategy with clear maximum counts.
- Prefer Timberborn-native visual assets or effect patterns before custom approximations.
- Expose counters for active pooled effects and updated visual regions.
- Add deterministic tests for selection, pooling limits, and update routing where possible.
- Document screenshot and log evidence expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-039` provides the Timberborn visual-field binding.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live screenshots, relevant status counters, copied `Player.log`, and final lock state.

## Notes

- Keep this focused on presentation. Fire spread, material tuning, and gameplay damage belong to other tickets.

## Worker Notes

- Worktree: `~/repos/wildfire-TWF-040`.
- Branch: `codex/TWF-040-pooled-effects`.
- Implemented a bounded `TimberbornPooledFireSmokeAshEffectSink` in `src/Wildfire.Timberborn/TimberbornPooledFireSmokeAshEffects.cs`.
- Routed compact delta visual-effect events through the TWF-039 `ITimberbornGpuVisualFieldSurface` inspection API, with `MaxActiveEffects=64` and `MaxUpdatedVisualRegionsPerDispatch=128` defaults.
- Added active pooled effect and updated visual region counters to `status` / `qa-readiness` result tokens.
- Bound the pooled effect sink from `TimberbornFireRuntime` as a presentation-only visual-effect lane; no `Wildfire.Core` simulation rules, spread tuning, material tuning, or gameplay damage changes were made.
- The live presenter prefers loaded native-looking Timberborn/Unity prefab names such as `CampfireFire`, `Sparks_Trail`, `SmelterSmoke`, and `SteamEngineSmoke`; if no native prefab resolves, visible effects are explicitly disabled and logged instead of using invisible fallback anchors.
- Added stable last-nonzero visual update telemetry so QA can prove a successful visual update even after later empty ticks zero `updated_visual_regions`.
- Isolated visual presentation exceptions so gameplay/building/alert consequences and compact-delta telemetry continue when the visual sink or presenter fails.
- Removed the parameterless `TimberbornFireRuntime` constructor so Bindito sees a single public constructor with `ITimberbornGpuVisualFieldSurface`.
- Corrected pooled effect placement to preserve fire-grid `X/Y/Z` on Timberborn/Unity `x/y/z` axes.
- Tightened pooled slot reuse so an existing slot is reused only when the rendered effect kind and resolved native prefab name still match.
- Added deterministic coverage for visual-field sample selection, coordinate mapping, Unity axis placement, kind/prefab slot-reuse compatibility, pool caps/replacement, per-dispatch visual-region limits, zero-event dispatch completion, DI-safe construction, visual exception isolation, disabled native-prefab behavior, stable last-nonzero telemetry, and QA token counters.
- Updated `docs/TEST_PLAN.md` with required screenshot, counter, `Player.log`, and QA lock evidence for live QA.

## Evidence

- `git diff --check` passed.
- `dotnet test` passed: 124 passed, 0 failed.
- `dotnet build Wildfire.slnx` passed: 0 warnings, 0 errors.

## Blockers

- None for implementation.
- Live rendered-pixel approval remains QA-owned because this worker pass proves routing, pooling, exception isolation, native-resolution telemetry, and counters, not in-game screenshot quality.
- If live logs show `pooled_fire_effects_visible_enabled=false` or `pooled_fire_effects_native_prefab_resolved=false`, QA should treat visible effects as blocked by native-prefab resolution rather than accepting counters alone.

## Completion

- Ready for coordinator review and live QA verification.

## QA Notes

- QA role run from worktree `~/repos/wildfire-TWF-040` on 2026-05-02.
- Artifact root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z`.
- Fresh deploy command: `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=60`.
- Load/unpause commands:
  - `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --lock-timeout=60 --artifacts-dir="$ARTIFACT/latest-save-startup"` reached the main menu but timed out waiting for loaded-save after repeated documented `main.continue` clicks. Evidence: `load-latest-save-and-unpause-output.txt`, `latest-save-startup/2026-05-02T21-19-51-157Z/fast-frame-samples.csv`, and `latest-save-startup/2026-05-02T21-19-51-157Z/fast-frame-174.png`.
  - Manual QA fallback under the shared lock used the documented main-menu Load Game path: `main.load_game`, `main_load.settlement_first_row`, `main_load.save_first_row`, and `main_load.load_selected_save`. Evidence: `manual-load-game-output.txt`, `manual-load-dialog.png`, and `manual-load-selected-after-30s.png`.
  - `bun scripts/load-latest-save-and-unpause.ts --attach --wait=120 --lock-timeout=60 --artifacts-dir="$ARTIFACT/latest-save-attach-after-manual-load"` passed with `screen=loaded-save`, `post_status_ok tick_count=3`, and copied Player log evidence under `latest-save-attach-after-manual-load/2026-05-02T21-26-49-295Z/`.

- Runtime validation commands:
  - `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=10`.
  - `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick`.
  - `bun scripts/invoke-timberborn-command.ts status --wait=10`.
  - Delayed stability check: `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick` after later empty ticks.

- QA artifacts:
  - Fresh deploy output: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z/deploy-output.txt`.
  - Loaded/unpaused evidence: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z/latest-save-attach-after-manual-load/2026-05-02T21-26-49-295Z/latest-save-startup-summary.txt`.
  - Copied final Player log: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z/Player.log`.
  - Relevant Player log token extract: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z/relevant-player-log-tokens.txt`.
  - Command outputs: `qa-delta-stimulus-output.txt`, `qa-readiness-after-delta-output.txt`, `status-after-delta-output.txt`, and `qa-readiness-delayed-stability-output.txt`.
  - Live screenshot after native effect resolution: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z/live-after-delta-effects.png`.
  - Final lock/process state: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z/final-lock-process-state.txt`.

- Acceptance results:
  - Fresh deploy: PASS. `deploy-output.txt` shows `dotnet build Wildfire.slnx --configuration Debug`, both Unity AssetBundle builds, clean copy to `~/Documents/Timberborn/Mods/Wildfire`, `deploy_complete`, and lock release.
  - Loaded/unpaused Timberborn runtime: PASS. The launch utility's `main.continue` route timed out, but the documented Load Game fallback loaded the save; attach-mode QA then reported `screen=loaded-save` and `post_status_ok tick_count=3`.
  - TWF-039 visual-field surface tokens in copied `Player.log`: PASS. `Player.log` includes `wildfire_timberborn_gpu_visual_field_surface_bound width=128 height=128 depth=23 cell_count=376832 stride_bytes=16 channels=fire,smoke,ash,visibility` and follow-up `wildfire_timberborn_gpu_visual_field_surface_updated` tokens through tick `57`.
  - TWF-040 pooled-effect/native-resolution tokens in copied `Player.log`: PASS. `Player.log` includes `wildfire_timberborn_delta_consequence_sink_bound lane=pooled_fire_smoke_ash_effects`, `wildfire_timberborn_pooled_fire_effect_native_prefab_resolved kind=fire prefab=CampfireFire`, and `wildfire_timberborn_pooled_fire_effects_updated` tokens.
  - Required `status` / `qa-readiness` counters: PASS. After `qa-delta-stimulus`, `qa-readiness` reported `active_pooled_fire_effects=1`, `updated_visual_regions=1`, `last_nonzero_updated_visual_regions=1`, `pooled_fire_effects_visible_enabled=true`, `pooled_fire_effects_native_prefab_resolved=true`, and `pooled_fire_effects_native_prefab=CampfireFire`; `status` reported the same active/update values.
  - Stable last-nonzero visual-region telemetry: PASS. The delayed `qa-readiness` check at `tick_count=57` reported `updated_visual_regions=0` with `last_nonzero_updated_visual_regions=1` and `last_nonzero_updated_visual_regions_tick=28`.
  - Live screenshot/artifact: PASS. Native effects resolved to `CampfireFire`, and `live-after-delta-effects.png` was captured after the delta stimulus and status/readiness proof.
  - Final cleanup: PASS. `final-lock-process-state.txt` shows `lock_present=false`; Timberborn was quit after evidence capture and no `Timberborn` process remained.

- Recommended board move: move `TWF-040` from `04-verify` to `05-integration`.
