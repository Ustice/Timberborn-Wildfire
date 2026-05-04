---
ticket: TWF-097
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-096
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-097-add-enable-disable-safety-setting.md
---

# TWF-097: Add Enable Disable Safety Setting

## Goal

Add a safe release setting that can disable new Wildfire work without corrupting the save.

## Why

Players need a conservative escape hatch. Disabling the feature should stop new fire work and report clear status without pretending active runtime state is still valid.

## Requirements

- Build on the `TWF-096` settings framework.
- Add a stable enable/disable setting with a conservative default.
- Stop new simulator dispatch or gameplay consequences safely when disabled.
- Preserve recoverable save behavior.
- Expose status or QA evidence that the disabled state is active.
- Add deterministic tests for enabled and disabled interpretation.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-096` provides the settings framework.

## Parent Reference

- Parent gate: `TWF-048`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Build only after `TWF-096` defines the settings owner and interpretation contract; this ticket should add one stable enable/disable key rather than a broader settings redesign.
- The disabled state should stop new simulator dispatches, new external changes, and new gameplay consequences as safely as possible, while preserving loadability and command/status visibility.
- Keep disabled-state interpretation in the Timberborn adapter. The core simulation should not depend on Timberborn settings APIs or save-data preferences.
- Expose the active setting through status, QA readiness, or a searchable log token so QA can distinguish intentionally disabled behavior from failed initialization.
- Passing QA should show enabled default behavior still works, disabled behavior avoids new fire work, and re-enable/save lifecycle risks remain covered by `TWF-093` through `TWF-095`.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture evidence that disabling Wildfire behaves safely.

## Notes

- Mod removal and re-enable lifecycle validation remain in `TWF-094` and `TWF-095`.
- Worker commit `8811459e37738117146842c27c176afda44660f1` adds release setting `JasonKleinberg.Wildfire.release.wildfire_enabled`. Missing defaults to enabled (`1`) to preserve current behavior; valid values are `1` enabled and `0` disabled; invalid or out-of-range values default disabled and emit `wildfire_release_setting_invalid`.
- Disabled behavior reported by the worker: `status` / `qa-readiness` stay available and expose `wildfire_enabled=false`, `loaded_game_ready=false`, and `message=wildfire_disabled`; QA simulator-change commands fail before queueing changes; runtime external change ingress no-ops with a disabled log token; fixed-cadence dispatch skips ticks with `wildfire_timberborn_dispatch_skipped_disabled`.
- Deterministic checks passed in the worktree: `git diff --check`, `dotnet test` with 144 tests, and `dotnet build Wildfire.slnx`.
- Live Timberborn QA was not attempted because the current Steam/Timberborn launch environment is blocked. This ticket requires fresh review, then live QA after launch is unblocked before integration.
- Fresh review failed with a P1 typed-settings bug: the enable/disable key is documented as an integer setting, but the Timberborn-backed settings store reads keys through `GetSafeString`. Real Timberborn integer settings use `GetSafeInt` / `PlayerPrefs.GetInt`, so a stored integer `0` could be read as fallback string `1` and leave Wildfire enabled. Return to `03-in-progress` and require a worker fix plus fresh review.
- Worker fix `2f84fe9600eca9e70501af5c63b8b35f6d7e1e0d` changes the Timberborn-backed settings store to use `GetSafeInt`, preserves missing-key defaults, documents schema version and `wildfire_enabled` as integer-backed settings, and adds deterministic `ISettings` typed-path tests proving stored integer `0` disables Wildfire even if a conflicting string value exists.
- Returned to `04-verify` after the typed-settings fix; requires fresh review before any live-QA or integration move.
- Fresh review passed for `2f84fe9600eca9e70501af5c63b8b35f6d7e1e0d`: the reviewer confirmed integer-backed keys use `ISettings.GetSafeInt`, deterministic tests cover the real typed path, disabled QA commands are gated, status/readiness remain visible, and external change/dispatch gating is present.
- Live Timberborn QA remains blocked by the Steam/Timberborn launch environment. Move to `07-blocked` until QA can capture `wildfire_enabled=false`, `message=wildfire_disabled`, rejected simulator changes without queued work, and `wildfire_timberborn_dispatch_skipped_disabled` from `Player.log`.
- 2026-05-03 coordinator update: the shared blocker has moved past the Steam launch-args prompt. This ticket should return to `04-verify` after `TWF-050` proves command-responsive loaded-save QA; it still needs disabled-state command/log proof before integration.
- 2026-05-03 live QA failed after `TWF-050` was restored. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-097-live-qa-20260503T135708Z`. Build/deploy passed, but Timberborn failed during game scene container creation with `BinditoException: WildfireReleaseSettings has more than one parameterful constructors`; loaded-save command responsiveness, disabled status output, simulator-change rejection, and `wildfire_timberborn_dispatch_skipped_disabled` were not reached. Coordinator moved this ticket back to `03-in-progress`; after a fix lands it needs fresh review and the failed live QA gate must rerun before integration.
- 2026-05-03 worker fix in `~/repos/wildfire-TWF-097`: `WildfireReleaseSettings` now has one injectable constructor taking `IWildfireReleaseSettingsStore`; the Timberborn `ISettings` adapter is bound explicitly in `WildfireConfigurator`; `WildfireReleaseSettingsInitializer` was also narrowed to avoid the same Bindito constructor-shape issue. Worker verification passed: targeted release-settings and QA-command tests with 45 tests, `git diff --check`, full `dotnet test` with 148 tests, and `dotnet build Wildfire.slnx` with 0 warnings and 0 errors. Coordinator moved this ticket back to `04-verify` for fresh review before the failed live QA gate reruns.
- 2026-05-03 fresh review passed for the Bindito constructor fix in `~/repos/wildfire-TWF-097`. Reviewer confirmed the fix is narrow, `IWildfireReleaseSettingsStore` is explicitly bound in Timberborn DI, `WildfireReleaseSettings` and `WildfireReleaseSettingsInitializer` no longer expose multiple parameterful constructors, settings behavior remains covered, and no simulation rules moved into Timberborn settings code. Reviewer repeated `git diff --check`, targeted release-settings and QA-command tests with 45 tests, full `dotnet test` with 148 tests, and `dotnet build Wildfire.slnx` with 0 warnings and 0 errors. Live disabled-state QA remains required before integration.
- 2026-05-03 live QA passed from `~/repos/wildfire-TWF-097`. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-097-live-qa-20260503T142042Z`. Build/deploy passed; normal bundle launch/load reached a loaded save; `status` returned `success=true`, `bridge_alive=true`, and `runtime_loaded=true`; `qa-readiness` returned `wildfire_enabled=false`, `loaded_game_ready=false`, and `message=wildfire_disabled`; `qa-delta-stimulus` rejected with `success=false`, `queued_changes=0`, and `message=wildfire_disabled`; `Player.log` contained `wildfire_timberborn_dispatch_skipped_disabled` and no `BinditoException`. Coordinator moved this ticket to `05-integration`.
- 2026-05-03 integration complete in main checkout. The first integration pass carried only the Bindito constructor-shape fix; a reconciliation pass then integrated the full accepted disabled-state surface, including `wildfire_enabled`, `wildfire_disabled`, `wildfire_timberborn_dispatch_skipped_disabled`, and runtime external-change skip behavior while preserving newer compatibility-probe behavior already in main. Integration checks passed: `git diff --check` and `git diff --cached --check` for owned files, targeted release-settings and QA-command tests with 46 tests, and `dotnet build Wildfire.slnx` with 0 warnings and 0 errors. Coordinator moved this ticket to `06-done`.
