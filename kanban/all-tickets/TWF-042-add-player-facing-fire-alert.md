---
ticket: TWF-042
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-036
   - TWF-037
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/reference/timberborn-ui.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-042-add-player-facing-fire-alert.md
---

# TWF-042: Add Player-Facing Fire Alert

## Goal

Add one clear player-facing alert or status surface that reports active fire risk or active fire consequences from simulator output.

## Why

The design includes alert updates in the Timberborn integration flow. Once deltas affect buildings and overlays, players need a Timberborn-native way to notice and inspect the condition without relying only on QA command output.

## Requirements

- Drive the alert from compact deltas, consequence counters, or derived active-fire state.
- Use Timberborn-native UI or alert patterns documented in the repo where possible.
- Keep the alert informational and bounded; do not add broad command/debug UI.
- Add deterministic tests for alert-state transitions where possible.
- Document live QA expectations, screenshots, and status evidence in `docs/TEST_PLAN.md`.
- Update `docs/reference/timberborn-ui.md` only if new UI guidance or discovered patterns should be durable.

## Dependencies

- `TWF-036` provides a real building consequence.
- `TWF-037` provides an inspection surface for changed fire state.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live screenshots, `Player.log` evidence, and command/status output proving the alert corresponds to simulator state.

## Notes

- Keep this to one alert or status surface. Full UI polish and settings can come after the first player-visible loop exists.

## Worker Notes

- Implemented one bounded player-facing alert surface in the Timberborn adapter: compact delta alert events are aggregated per dispatch and sent through Timberborn `QuickNotificationService.SendWarningNotification(...)` as a native quick warning.
- Alert text reports new fire count, burned-out cell count, and max heat; the sink sends at most one warning per dispatch and records notification/failure counters without mutating simulator state or adding command/debug UI.
- Wired the alert sink into `TimberbornFireRuntime` alongside existing delta consumers and exposed QA status fields for last player alert tick, started-fire count, fuel-spent count, max heat, notification send state, notification count, presentation failures, and last message.
- Added deterministic unit coverage for aggregated warning transitions, zero-alert dispatch behavior, presentation-failure isolation, and QA result-token fields.
- Updated `docs/TEST_PLAN.md` with live QA screenshot, `Player.log`, and `status` / `qa-readiness` evidence expectations.
- Updated `docs/reference/timberborn-ui.md` with durable guidance for transient hazard notifications via Timberborn quick notifications.

Changed files:

- `src/Wildfire.Timberborn/Wildfire.Timberborn.csproj`
- `src/Wildfire.Timberborn/TimberbornDeltaConsumers.cs`
- `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`
- `src/Wildfire.Timberborn/TimberbornPlayerFireAlerts.cs`
- `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`
- `tests/Wildfire.Core.Tests/TimberbornPlayerFireAlertTests.cs`
- `tests/Wildfire.Core.Tests/TimberbornPooledFireSmokeAshEffectTests.cs`
- `tests/Wildfire.Core.Tests/TimberbornQaCommandBridgeTests.cs`
- `docs/TEST_PLAN.md`
- `docs/reference/timberborn-ui.md`
- `kanban/all-tickets/TWF-042-add-player-facing-fire-alert.md`

Evidence:

- `git diff --check` passed.
- `dotnet test` passed: 128 passed, 0 failed, 0 skipped.
- `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.

Remaining QA:

- Live Timberborn QA still required because `requires_qa: true`.
- QA should deploy/load Timberborn, trigger an alert-producing delta with `qa-delta-stimulus` or `qa-building-burnout-stimulus`, capture a screenshot of the native quick warning, copy `Player.log`, and capture `status` or `qa-readiness` output showing nonzero alert counters and `player_fire_alert_notification_sent=true`.

Blockers:

- No Worker-side blockers found.

Completion:

- Worker implementation is complete and ready for coordinator review / QA handoff.

## QA Notes 2026-05-02

Result: failed live validation before stimulus; recommend moving back to `03-in-progress` for a Worker fix, not to integration or done.

Live QA commands:

- `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=30`
- `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --artifacts-dir "$ARTIFACT_ROOT/latest-save-startup" --lock-timeout=30`
- `bun scripts/invoke-timberborn-command.ts status --wait=3`

Artifacts:

- Artifact root: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z`
- Deploy log: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/deploy-apply-clean.txt`
- Startup/load log: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/load-latest-save-and-unpause-output.txt`
- Crash screenshot: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/live-crash-screen.png`
- Copied Player log: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/Player.log`
- Key Player log extract: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/player-log-key-evidence.txt`
- Timberborn error report copy: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/error-report-2026-05-02-18h16m05s.zip`
- Status-after-crash output: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/status-after-crash-output.txt`
- Final lock/process state: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-20260502T221513Z/final-lock-process-state.txt`

Acceptance criteria:

- Fresh deploy from the TWF-042 worktree: pass. The deploy log shows `dotnet build Wildfire.slnx --configuration Debug` succeeded with 0 warnings and 0 errors, Unity asset bundles rebuilt, the deployed `Wildfire` mod folder was removed and rewritten, and the deploy lock was released.
- Loaded/unpaused Timberborn runtime: fail. Timberborn reached startup and main-menu Continue, then crashed before loaded-save runtime readiness.
- Trigger `qa-delta-stimulus` or `qa-building-burnout-stimulus`: not run. The runtime never reached a loaded/unpaused command-bridge state.
- Native quick-warning / player-facing alert screenshot: fail. Captured screenshot is a Timberborn crash screen, not the expected quick warning.
- Copied `Player.log` evidence and `status` or `qa-readiness` output showing alert counters correspond to simulator state: fail. `Player.log` shows `Wildfire (v0.1.0.0)` was discovered, then `BinditoException: TimberbornFireRuntime has more than one parameterful constructors.` The post-crash `status` command timed out waiting for `command-outbox.txt`; no alert counters were available.
- No broad/debug UI needed: pass. QA used only deploy, guarded startup, screenshots, copied logs, and the file command bridge; no broad/debug UI was used.
- Final lock/process cleanup: pass after cleanup. The killed loader left `/Users/jasonkleinberg/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock` with PID `97876`; QA recorded and removed that stale lock. Final state reports no Timberborn PID, no loader PID, and no lock files.

Failure evidence:

- Crash screen text: `BinditoException: TimberbornFireRuntime has more than one parameterful constructors.`
- `Player.log` key lines: `Wildfire (v0.1.0.0)`, followed by `BinditoException: TimberbornFireRuntime has more than one parameterful constructors.`
- Likely fix area: `src/Wildfire.Timberborn/TimberbornFireRuntime.cs` constructor shape / Bindito binding after adding the `QuickNotificationService` constructor.

## Worker Follow-Up Notes 2026-05-02

- Fixed the live QA blocker by removing the extra non-public parameterful `TimberbornFireRuntime` constructor. Bindito now sees one eligible constructor: `TimberbornFireRuntime(ITimberbornGpuVisualFieldSurface, QuickNotificationService)`.
- Preserved the quick-warning alert wiring by constructing `TimberbornQuickNotificationSink` and `UnityTimberbornFireLogSink` directly inside the single public runtime constructor.
- Strengthened deterministic constructor-shape coverage so the test counts all `TimberbornFireRuntime` constructor declarations in source and rejects another internal constructor.

Follow-up evidence:

- `git diff --check` passed.
- `dotnet test` passed: 128 passed, 0 failed, 0 skipped.
- `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.

Remaining QA:

- Live Timberborn QA should rerun from a fresh deploy, confirm the Bindito constructor crash is gone, then trigger `qa-delta-stimulus` or `qa-building-burnout-stimulus` and capture the native quick-warning screenshot plus `Player.log` / `status` or `qa-readiness` alert-counter evidence.

## QA Rerun Notes 2026-05-02

Result: passed live validation after the constructor fix; recommend moving `TWF-042` to `05-integration` for coordinator integration. QA did not move board symlinks.

Live QA commands:

- `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=60`
- `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240 --artifacts-dir "$ARTIFACT/latest-save-startup" --lock-timeout=60`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick`
- `bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=10 --require-advanced-tick`
- `screencapture -x "$ARTIFACT/quick-warning-after-delta.png"`
- `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick --require-nonzero-delta`
- `bun scripts/invoke-timberborn-command.ts status --wait=10 --require-advanced-tick`
- `osascript -e 'tell application "Timberborn" to quit'`

Artifacts:

- Artifact root: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z`
- Deploy log: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/deploy-apply-clean.txt`
- Startup/load log: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/load-latest-save-and-unpause-output.txt`
- Startup artifacts and copied early `Player.log`: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/latest-save-startup/2026-05-02T22-28-26-832Z`
- Baseline readiness output: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/qa-readiness-before-stimulus.txt`
- Stimulus output: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/qa-delta-stimulus-output.txt`
- Quick-warning screenshot: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/quick-warning-after-delta.png`
- Alert readiness output: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/qa-readiness-after-stimulus-require-nonzero.txt`
- Final status output: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/status-after-stimulus.txt`
- Copied final Player log: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/Player.log`
- Key Player log extract: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/player-log-key-evidence.txt`
- Compact alert proof extract: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/player-log-alert-proof.txt`
- Final lock/process state: `/Users/jasonkleinberg/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z/final-lock-process-state.txt`

Acceptance criteria:

- Fresh deploy from the TWF-042 worktree: pass. The deploy log shows `dotnet build Wildfire.slnx --configuration Debug` succeeded with 0 warnings and 0 errors, Unity AssetBundles rebuilt, the deployed `Wildfire` mod folder was removed and rewritten, and the deploy lock was released.
- Loaded/unpaused Timberborn runtime: pass. `load-latest-save-and-unpause.ts` reached `screen=loaded-save`, captured before/after unpause screenshots, and reported `post_status_ok tick_count=3`.
- Prior Bindito constructor crash gone: pass. Copied `Player.log` shows `Wildfire (v0.1.0.0)`, command bridge readiness, and `bindito_crash_absent=true`; it does not contain `BinditoException` or `TimberbornFireRuntime has more than one parameterful constructors`.
- Trigger `qa-delta-stimulus` or `qa-building-burnout-stimulus`: pass. `qa-delta-stimulus` returned `tick_count=23`, `queued_changes=1`, `target_index=188480`, `target_x=64`, `target_y=64`, `target_z=11`, and `set_cell=13311`.
- Native quick-warning / player-facing alert screenshot: pass. `quick-warning-after-delta.png` shows the Timberborn quick warning text `Wildfire alert: 1 new fire. Max heat 15.`
- Copied `Player.log` evidence and `status` or `qa-readiness` output showing alert counters correspond to simulator state: pass. `Player.log` shows `wildfire_timberborn_gpu_readback_completed tick=24 delta_count=2`, `wildfire_timberborn_delta_consumer_completed tick=24 ... alerts=1 max_heat=15`, and `wildfire_timberborn_player_fire_alert_updated tick=24 fire_started=1 fuel_spent=0 max_heat=15 notification_sent=true total_notifications=1 ... message="Wildfire alert: 1 new fire. Max heat 15."` Follow-up `status` reports `last_player_fire_alert_tick=24`, `last_player_fire_alert_started_fires=1`, `last_player_fire_alert_max_heat=15`, `player_fire_alert_notifications=1`, and `player_fire_alert_notification_sent=true`.
- Guarded non-zero-delta readiness: informational fail only. `qa-readiness-after-stimulus-require-nonzero.txt` exited nonzero because `last_delta_count` had already returned to `0` by tick `42`, but the same output still preserved the positive alert counters above. This does not fail TWF-042 because the ticket requires alert evidence, not the narrow `last_delta_count` timing window.
- Final lock/process cleanup: pass. After quitting Timberborn, `final-lock-process-state.txt` shows no Timberborn PID, no loader/deploy/invoke PID, and no lock files under either WildfireQA lock directory; the remaining matching process line is an unrelated `UnityCodeModel.dll` process for `~/repos/Timberborn-Prometheus`.
