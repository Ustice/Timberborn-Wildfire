---
ticket: TWF-050
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-046
  - TWF-049
write_scope:
  - src/Wildfire.Timberborn/**
  - scripts/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-050-harden-gpu-asset-failure-modes.md
---

# TWF-050: Harden GPU Asset Failure Modes

## Goal

Make missing, incompatible, or failed GPU and AssetBundle paths fail safely with actionable logs instead of crashing or silently pretending the simulator works.

## Why

Wildfire depends on a real compute bundle and GPU path. Release needs clear behavior when the bundle is missing, stale, incompatible, or the GPU simulator cannot initialize.

## Requirements

- Handle missing compute bundle, invalid diagnostic bundle, incompatible AssetBundle, shader load failure, kernel lookup failure, buffer allocation failure, and dispatch/readback failure.
- Preserve the rule that release cannot fall back to a fake C# fire simulator.
- Report degraded or unavailable simulator state through status or QA readiness.
- Keep error logs concise and searchable.
- Ensure deploy scripts validate bundle manifests before staging.
- Add deterministic tests for failure classification and status reporting where possible.
- Document QA commands and expected failure tokens in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-046` proves the healthy live loop.
- `TWF-049` provides compatibility probe structure for degraded modes.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start from `src/Wildfire.Timberborn/TimberbornComputeFireSimulator.cs`, `src/Wildfire.Unity/UnityComputeFireSimulator.cs`, `src/Wildfire.Unity/ComputeBufferGrid.cs`, `scripts/deploy-timberborn-mod.ts`, and the compatibility result shape introduced by `TWF-049`.
- Preserve the release rule: there is no fake C# simulator fallback for healthy gameplay. Failure should mark the simulator unavailable or degraded with actionable status and logs.
- Classify failures separately for missing bundle, stale or wrong platform bundle, diagnostic bundle issue, shader asset missing, kernel lookup failure, unsupported compute shaders, buffer allocation failure, dispatch failure, and readback failure.
- Deploy script checks should validate expected bundle files, manifest files, required assets, and target platform before staging, while runtime checks should protect the loaded save from unhandled exceptions.
- QA evidence should include one healthy loaded-save path and one intentional failure path with copied `Player.log`, command/status output, and proof that no unhandled exception or fake success was produced.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if TypeScript scripts change.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture one healthy path and at least one intentional failure-path evidence run without new unhandled exceptions.

## Notes

- Safe failure is acceptable. Silent fake success is not.
- Fresh review passed for worker fix `015c7b2ae1383f386c410b055a41b8f3cd217bb0` after earlier review failures; reviewer confirmed cached-shader reuse now requires current bundle signature, global loaded-bundle scanning is gone, deploy validation is honest about layout-only platform validation, and deterministic tests cover failure classification and readiness tokens.
- Live QA evidence root: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-050-20260503-015623`.
- Healthy path passed in `09-healthy-qa-readiness-after-speed-controls.txt`: `loaded_game_ready=true`, `simulator_integrated=true`, `compatibility_probe_status=compatible`, `gpu_simulator_status=ready`, `gpu_simulator_failure_kind=None`, `tick_count=33`.
- Required intentional failure-path QA failed. The missing-compute-bundle injection loaded/stalled without an actionable Wildfire failure token in `19-failure-token-scan-before-restart.txt`, command polling timed out in `18-failure-qa-readiness-after-load-stall.txt`, and captured logs did not prove `gpu_simulator_status=unavailable` or a classified missing-bundle failure. This must return to `03-in-progress` for a product/harness fix and then pass fresh review before another QA/integration attempt.
- Cleanup restored `/Users/jasonkleinberg/Documents/Timberborn/Mods/Wildfire/ComputeShaders/wildfire_compute_mac`; `19-restore-compute-bundle.txt` and `17-coordinator-restore-compute-bundle.txt` show checksum `290c097c12ec885263eb472e20a4741d9cfbb9a24f65a396baed57390982ee79`. `21-coordinator-final-timberborn-cleanup.txt` terminated the stalled Timberborn process.
- Worker fix `41fc92f` addresses the failed QA evidence path by adding mod-startup GPU asset preflight logging before game-context runtime integration. The intended failure-path tokens are `wildfire_timberborn_mod_asset_preflight`, `wildfire_timberborn_compute_asset_unavailable failure_kind=MissingComputeBundle`, and `wildfire_timberborn_runtime_simulator_unavailable phase=mod_startup_preflight`.
- Returned to `04-verify` after the failed-QA worker fix; requires fresh review before another QA run.
- Fresh review passed for `41fc92f`, but the next live QA failed before exercising the missing-compute path. Evidence root: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-050-20260503-qa-20260503T062945Z`.
- QA blocker: `01-deploy.txt` shows `scripts/deploy-timberborn-mod.ts` rejected the generated `wildfire_compute_mac.manifest`; after manual staging, `03-healthy-load-command.txt` reached a loaded-save screenshot but failed with `Expected loaded-save HUD before unpause, got unknown`, and `04-healthy-qa-readiness-after-ui-fail.txt` timed out. Missing-compute injection was not started because the healthy baseline did not pass.
- Current deployed compute bundle was left present; QA recorded checksum `290c097c12ec885263eb472e20a4741d9cfbb9a24f65a396baed57390982ee79` and final process/lock state in `08-final-lock-process-deployment-state.txt`.
- Returned to `03-in-progress`; next worker pass should fix the deploy validation blocker and make healthy live startup evidence rerunnable before another review/QA attempt.
- Worker fix `f93dd98` addresses the second QA blocker: deploy validation now checks Unity manifest fields and required asset paths instead of expecting the bundle filename inside the `.manifest`, and `load-latest-save-and-unpause.ts` now verifies `com.mechanistry.timberborn` is foreground before accepting live screenshots.
- Returned to `04-verify` after the deploy/load harness fix; requires fresh review before another QA run.
- Fresh review passed for `f93dd98`, and fresh QA proved the deploy blocker is fixed. Evidence root: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-050-qa-20260503T065105Z`.
- QA remains incomplete because Timberborn could not be launched or activated through Steam: `02-healthy-load.txt` failed with `Could not activate Timberborn bundle com.mechanistry.timberborn ... frontmost_bundle_id=com.valvesoftware.steam`; direct app launch produced only Unity startup/shutdown lines, and sequential `status` / `qa-readiness` probes timed out. Steam logs show a pending launch-args prompt: `LaunchApp waiting for user response to ShowGameArgs '-screen-fullscreen' '0' '-screen-width' '1920' '-screen-height' '1080'`.
- Missing-compute injection was not run because the healthy baseline could not be proven. Deployed `wildfire_compute_mac` remains present with checksum `290c097c12ec885263eb472e20a4741d9cfbb9a24f65a396baed57390982ee79`; final process and lock state are in `100-final-cleanup-state.txt`.
- Moved to `07-blocked` for live launch-environment unblock. After the Steam launch prompt/state is cleared, rerun fresh QA from a new evidence root; do not integrate until healthy GPU runtime and missing-compute failure proof both pass.
- 2026-05-03 Jason clarification: the Steam blocker was caused by launching Timberborn with command-line parameters. Rerun QA using normal Timberborn launch or attach to an already normally launched session; preserve the exact launch or attach path in the evidence.
- 2026-05-03 coordinator: moved into Sprint 8 live-QA recovery as the first shared gate. QA should rerun from a fresh evidence root and serialize all deploy/launch/missing-compute mutation work behind this ticket.
- 2026-05-03 QA rerun partial evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-050-qa-20260503T125647Z`. Deploy passed from the `TWF-050` worktree, the deployed compute bundle is present with checksum `290c097c12ec885263eb472e20a4741d9cfbb9a24f65a396baed57390982ee79`, no QA lock remained, and Timberborn was relaunched normally without command-line parameters.
- 2026-05-03 QA rerun blocker: the first QA agent became unresponsive before producing a final report. Coordinator inspection showed the run advanced to a Timberborn loading screen after startup dialog clicks, but no healthy `qa-readiness` or missing-compute failure gate was captured. Continue from a fresh or appended evidence root and do not treat this as a product failure yet.
- 2026-05-03 QA continuation result: healthy live gate still failed before missing-compute injection. `19-healthy-qa-readiness-continuation.txt` timed out waiting for `command-outbox.txt`, `20-healthy-attach-continuation.txt` stalled during Timberborn activation, and `21-stop-hung-continuation-attach-and-clean-lock.txt` removed the helper lock. Final state in `25-clean-final-process-lock-compute-state.txt` reports Timberborn still running from the normal Steam app path, no QA lock files, stale `command-inbox.txt`, no outbox, and compute bundle restored with checksum `290c097c12ec885263eb472e20a4741d9cfbb9a24f65a396baed57390982ee79`.
- 2026-05-03 coordinator: moved back to `07-blocked`. This is no longer the parameterized Steam launch prompt; the current blocker is a normal-launched Timberborn session that remains command-unresponsive. Smallest next action is to close/restart Timberborn normally, then rerun the healthy `qa-readiness --require-advanced-tick` gate before any missing-compute mutation.
- 2026-05-03 Jason direction: if the normal-launched session remains command-unresponsive, assign a worker to find the root cause and create a fix rather than treating it as environment-only. Coordinator moved this ticket back to `03-in-progress` for a command bridge responsiveness investigation and fix lane.
- 2026-05-03 QA rerun evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-050-qa-20260503T133052Z`. Deploy passed and compute bundle was not mutated, but normal `open -b com.mechanistry.timberborn` launch/load failed before `qa-readiness`: Timberborn was running while the helper saw `frontmost_bundle_id=com.valvesoftware.steam`, and `Player.log` contained only early Unity/Steam startup lines with no `wildfire_` tokens. Missing-compute QA was not run. Worker investigation should cover the launch/foreground harness path as well as command bridge consumption.
- 2026-05-03 worker fix in `~/repos/wildfire-TWF-050-command-bridge-fix`: root cause is a launch/load harness failure, not proven command bridge consumption failure. The script aborted when Steam remained transiently frontmost after normal `open -b com.mechanistry.timberborn`, before Timberborn UI/mod startup could finish. The fix keeps waiting through foreground-only screenshot failures, logs deferred activation, and reactivates Timberborn before screenshots. Worker checks passed: `bun install`, `bun run typecheck`, `bun scripts/load-latest-save-and-unpause.ts --dry-run --skip-resolution-check --skip-post-status`, `git diff --check`, and `dotnet test` with 163 tests.
- 2026-05-03 coordinator: moved to `04-verify` for review of the script-only launch/foreground fix before fresh live QA.
- 2026-05-03 review passed for the script-only launch/foreground harness fix in `~/repos/wildfire-TWF-050-command-bridge-fix`. Reviewer checked the unstaged `scripts/load-latest-save-and-unpause.ts` diff, found no blockers, and repeated `git diff --check`, `bun run typecheck`, and the dry-run load command successfully. Treat the exact unstaged worktree state as reviewed; next action is fresh live QA from that worktree, not integration.
- 2026-05-03 live QA passed from the reviewed unstaged worktree state. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-050-qa-20260503T134418Z`. Healthy loaded-save `qa-readiness --wait=6 --require-advanced-tick` returned `loaded_game_ready=true`, `simulator_integrated=true`, and `tick_count=19`; GPU runtime reported `compatibility_probe_status=compatible`, `gpu_simulator_status=ready`, and `gpu_simulator_failure_kind=None`; intentional missing-compute QA reported `compatibility_probe_status=failed`, `compatibility_probe_degraded=true`, `gpu_simulator_status=unavailable`, and `gpu_simulator_failure_kind=MissingComputeBundle`; final compute bundle checksum was restored to `290c097c12ec885263eb472e20a4741d9cfbb9a24f65a396baed57390982ee79`.
- 2026-05-03 coordinator: moved to `05-integration`. No failed QA gate remains for this ticket, but integration must apply the reviewed unstaged `scripts/load-latest-save-and-unpause.ts` fix from `~/repos/wildfire-TWF-050-command-bridge-fix` before downstream live-QA tickets are moved out of blocked.
- 2026-05-03 integration complete in main checkout: `scripts/load-latest-save-and-unpause.ts` now includes the accepted foreground-tolerance harness fix. Integration checks passed: `git diff --check -- scripts/load-latest-save-and-unpause.ts`, `bun run typecheck`, and `bun scripts/load-latest-save-and-unpause.ts --dry-run --skip-resolution-check --skip-post-status`. Coordinator moved this ticket to `06-done`.
