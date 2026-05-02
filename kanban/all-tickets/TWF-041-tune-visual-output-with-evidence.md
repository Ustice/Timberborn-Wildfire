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

## Completion

- Ready for coordinator review and verification.
- Recommended board move: move `TWF-041` from `03-in-progress` to `04-verify`.
