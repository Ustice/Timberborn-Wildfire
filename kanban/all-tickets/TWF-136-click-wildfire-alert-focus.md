---
ticket: TWF-136
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-042
write_scope:
  - src/Wildfire.Timberborn/TimberbornPlayerFireAlerts.cs
  - src/Wildfire.Timberborn/TimberbornFireRuntime.cs
  - src/Wildfire.Timberborn/WildfireConfigurator.cs
  - src/Wildfire.Timberborn/Wildfire.Timberborn.csproj
  - tests/Wildfire.Core.Tests/TimberbornPlayerFireAlertTests.cs
  - tests/Wildfire.Core.Tests/TimberbornPooledFireSmokeAshEffectTests.cs
---

# TWF-136: Click Wildfire Alert To Focus Fire

## Goal

Let the player click the wildfire-start notification and have the camera center on the fire cell that triggered the alert.

## Requirements

- Preserve the existing player-facing wildfire warning text and warning notification behavior.
- Track a deterministic focus target for each alert dispatch, using the first newly burning cell in the aggregated alert.
- Keep the behavior in the Timberborn adapter layer; the simulation core must not own UI, notification, or camera behavior.
- Convert the stored fire cell index back into Timberborn grid/world coordinates before moving the camera.
- Avoid breaking existing alert counters, QA status output, pooled visual effects, release settings, or command bridge behavior.

## Dependencies

- `TWF-042` must remain integrated because this extends the existing player-facing fire alert.
- Live QA needs a save or QA command path that can trigger at least one new burning cell and display the wildfire alert.

## Role

- QA owns live Timberborn validation before merge.
- Worker implementation is already present on branch `codex/TWF-136-click-wildfire-alert-focus`.

## Verification

- Run `git diff --check` for the touched files.
- Run `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj`.
- Deploy/load Timberborn with the branch build.
- Trigger a wildfire start notification.
- Click the visible wildfire notification and confirm the camera target moves to the burning cell or its immediate cell center.
- Capture `Player.log` evidence for `wildfire_timberborn_player_fire_alert_focus_ready` and, after clicking, `wildfire_timberborn_player_fire_alert_focused`.

## Notes

- Deterministic tests currently cover alert aggregation and focus target selection.
- The implementation uses a Timberborn adapter-side focus overlay because `QuickNotificationService` exposes text-only warnings.
- QA should pay attention to overlay alignment with the quick notification at the current UI scale. If the click target is slightly offset, keep this ticket in verify with the screenshot/log evidence rather than merging immediately.
- 2026-05-03 QA blocker found during `TWF-089` live retry: the reconciled checkout deployed cleanly, but Timberborn crashed after Continue before command responsiveness because `Player.log` reported `BinditoException: TimberbornPlayerFireAlertCameraFocus has more than one parameterful constructors.` Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-reconciled-20260503T165753Z/`.
- 2026-05-03 coordinator: moved back to `03-in-progress`. Fix the Bindito constructor ambiguity before any live QA can pass from the main checkout. Keep the fix scoped to the Timberborn adapter alert-focus surface and preserve existing alert counters, command bridge behavior, pooled visuals, and release settings.
- 2026-05-03 worker fix: collapsed `TimberbornPlayerFireAlertCameraFocus` to a single public Bindito constructor while preserving the internal Unity-backed alert-focus logger. Root cause was two public parameterful constructors, so Bindito could not choose one when resolving the singleton after Continue.
- 2026-05-03 worker verification: `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter TimberbornPlayerFireAlertTests` passed with 4 tests, full `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj` passed with 198 tests, and `git diff --check` passed. Added a deterministic constructor-contract regression. Ready for review; live startup/click QA remains required after review passes.
- 2026-05-03 review: passed with no blocking findings. Review confirmed `TimberbornPlayerFireAlertCameraFocus` now has exactly one public parameterful constructor, keeps the Unity-backed logger internal, and remains wired through the Timberborn adapter alert-focus surface. No regressions found to alert counters, QA status, command bridge, pooled visuals, or release settings.
- 2026-05-03 review verification: `git diff --check` passed; `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter TimberbornPlayerFireAlertTests` passed with 4 tests; full `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj` passed with 198 tests. Live startup/click validation may rerun.
- 2026-05-03 worker: root cause was `TimberbornPlayerFireAlertCameraFocus` exposing both a two-parameter Bindito constructor and a three-parameter test/logging constructor. Bindito could not choose a singleton constructor during game startup.
- 2026-05-03 worker: fixed by collapsing `TimberbornPlayerFireAlertCameraFocus` back to one public parameterful constructor that accepts `UILayout` and `CameraService` and keeps the Unity-backed alert-focus logger internally. Added a deterministic constructor-contract regression test.
- 2026-05-03 worker verification: `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter TimberbornPlayerFireAlertTests` passed, 4 tests. `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj` passed, 198 tests. `git diff --check` passed.
- 2026-05-03 reviewer pass: verified `TimberbornPlayerFireAlertCameraFocus` now exposes one public Bindito constructor, the Unity-backed focus logger remains internal, and alert focus behavior stays in the Timberborn adapter. No blocking findings. Existing alert counters, QA status surface, command bridge, pooled visuals, and release settings were not changed by the constructor fix. Evidence: `git diff --check` passed; `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter TimberbornPlayerFireAlertTests` passed, 4 tests; full `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj` passed, 198 tests. Live QA may rerun startup/click validation.
- 2026-05-03 QA rerun PASS from main-equivalent checkout `fcab01f54f209acc10540663d2580a3f58debc38`: confirmed `caffeinate -disu` PID `94422` was active; deployed with `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout=120`; loaded latest save with `bun scripts/load-latest-save-and-unpause.ts --launch --wait=240`; startup reached loaded save with command bridge responsive and no repeat `BinditoException` / constructor ambiguity in `Player.log`. `qa-delta-stimulus` produced the visible warning `Wildfire alert: 1 new fire. Max heat 15.` and `Player.log` recorded `wildfire_timberborn_player_fire_alert_focus_ready cell_index=188480`. A tight retry waited for `focus_ready` and clicked the visible alert/focus region; `Player.log` recorded `wildfire_timberborn_player_fire_alert_focused cell_index=188480 x=64 y=64 z=11`. Final `qa-readiness --require-advanced-tick` passed with `bridge_alive=true`, `loaded_game_ready=true`, `simulator_integrated=true`, `tick_count=39`, `player_fire_alert_notifications=1`, and no presentation failures. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-136-qa-20260503T172225Z/`; key artifacts: `deploy-transcript.txt`, `load-latest-save-transcript.txt`, `focus-click-tight-retry-transcript.txt`, `qa-readiness-final-transcript.txt`, `Player.log`, `alert-tight-ready.png`, `alert-tight-after-clicks.png`, and `final-qa-lock-process-state.txt`. Final process state recorded no QA lock files and Timberborn PID `43324`.
