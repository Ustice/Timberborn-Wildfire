---
ticket: TWF-092
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
  - kanban/all-tickets/TWF-092-tune-burnout-cooling-behavior.md
---

# TWF-092: Tune Burnout Cooling Behavior

## Goal

Tune heat decay, burnout timing, and post-fire cooling after fuel is spent.

## Why

After fire burns out, heat should remain long enough to support smoke, danger, and aftermath readability, but not so long that every burned area feels permanently blocked.

## Requirements

- Keep heat loss material-driven unless a new design decision changes it.
- Compare cooling in single ignition, line of fuel, and building cluster scenarios.
- Tune heat decay and burnout behavior only with deterministic shader evidence and live recordings.
- Keep simulator-backed ash and fertility out of this ticket.
- Keep beaver debuff implementation out of this ticket.
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
- QA must capture recording evidence showing accepted burnout and cooling timing.

## Implementation Notes

- Tune heat decay and burnout timing from deterministic scenarios first, then require live recording evidence for player readability.
- Keep simulator-backed ash, fertility, and beaver exposure effects out of this ticket.

## Notes

- Visual ash tuning belongs to `TWF-068`; simulator-backed ash belongs to `TWF-078`.
- 2026-05-03: Sprint 6 coordinator moved this ticket to `03-in-progress` in `~/repos/wildfire-TWF-092` on branch `codex/TWF-092-burnout-cooling`, based on reviewed `TWF-091` commit `83af04b10a05ef192bd9461ca0b90ae35fff5abd` stacked on reviewed `TWF-090`/`TWF-089`/`TWF-088`.
- 2026-05-03: Integration remains blocked until upstream Sprint 6 live gates pass and this ticket has its own live burnout/cooling recording plus command/status/log artifact proof. If review fails, it must return through `03-in-progress` for fixes and then pass fresh review before any integration move.
- 2026-05-03 worker result: deterministic evidence complete in commit `36e27d87e01ce786507db925debf954452204198`. The worker accepted current burnout/cooling constants unchanged and made no production shader or Timberborn adapter behavior changes.
- 2026-05-03 worker evidence: added deterministic burnout/cooling assertions for `single-ignition`, `line-of-fuel`, and `building-cluster`, with committed captures under `tests/Wildfire.Core.Tests/ShaderSnapshots/twf-092/`. Accepted evidence records `single-ignition` first cold tick `6` with checksum `visual-fnv1a32:EC3E6705`, `line-of-fuel` last burning tick `13` and first cold tick `20` with checksum `visual-fnv1a32:3314A0C5`, and `building-cluster` first cold tick `4` with checksum `visual-fnv1a32:9E6AA4C5`.
- 2026-05-03 worker verification: `git diff --check` passed, targeted `dotnet test --filter FullyQualifiedName~AcceptedBurnoutCoolingSnapshotsCompareRequiredScenarios` passed, full `dotnet test` passed with `137` tests, and the opt-in Unity shader harness passed with `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1`.
- 2026-05-03 worker blocker: no live Timberborn evidence was claimed because launch/load remains blocked. Live burnout/cooling recording plus command/status/log artifact proof remains required before integration.
- 2026-05-03 coordinator: moved to `04-verify` for review. Do not move to `05-integration` unless review passes and the required live gate later passes with evidence.
- 2026-05-03 review: passed review on commit `36e27d87e01ce786507db925debf954452204198` with no findings. Review confirmed the changed-file set is limited to `docs/TEST_PLAN.md`, `tests/Wildfire.Core.Tests/UnityShaderExecutionHarnessTests.cs`, and committed `twf-092` snapshots; no production shader or Timberborn adapter behavior changed.
- 2026-05-03 review verification: `git diff --check 83af04b10a05ef192bd9461ca0b90ae35fff5abd..36e27d87e01ce786507db925debf954452204198` passed, targeted `dotnet test --filter FullyQualifiedName~AcceptedBurnoutCoolingSnapshotsCompareRequiredScenarios` passed, full `dotnet test` passed, and the opt-in Unity shader harness passed with `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity`.
- 2026-05-03 coordinator: moved to `07-blocked` because upstream Sprint 6 live gates and this ticket's live burnout/cooling recording plus command/status/log artifact proof remain missing.
