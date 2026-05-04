---
ticket: TWF-090
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-038
  - TWF-065
  - TWF-043
write_scope:
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-090-tune-water-suppression-behavior.md
---

# TWF-090: Tune Water Suppression Behavior

## Goal

Tune how wetness and queued water suppression reduce ignition, heat, and burn persistence.

## Why

Suppression needs to feel meaningful and predictable. Water should slow or stop fire without turning Wildfire into a fluid simulator.

## Requirements

- Preserve the release rule that water is a bounded suppression/wetness band.
- Compare water barrier and direct suppression scenarios.
- Keep water behavior in adapter inputs and GPU rules, not Timberborn-owned fire logic.
- Tune only when deterministic shader evidence and live suppression recordings agree.
- Keep steam visual tuning out of this ticket.
- Document accepted evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-038` proves queued water suppression changes.
- `TWF-065` provides recording tooling.
- `TWF-043` provides the current game-feel baseline.

## Parent Reference

- Parent gate: `TWF-069`.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture suppression recordings plus `qa-readiness` or `status` evidence.

## Implementation Notes

- Use the existing queued water-suppression path from `TWF-038` as the adapter input surface and keep suppression behavior in GPU rules.
- Require both deterministic suppression evidence and live recording evidence before integration; keep the ticket blocked if live Timberborn cannot launch.

## Notes

- Badwater and contamination-specific consequences belong to `TWF-079` and `TWF-086`.
- 2026-05-03 coordinator: moved to `03-in-progress` for Sprint 6 worker implementation in `~/repos/wildfire-TWF-090` on branch `codex/TWF-090-water-suppression`. The worktree is based on reviewed `TWF-089` commit `082077d2b99819c4b448b0ba9fe758ed81f4f412`, which is stacked on reviewed `TWF-088`; this ticket cannot integrate before upstream live QA blockers and its own live suppression recording gate are resolved.
- 2026-05-03 worker result: deterministic evidence complete in commit `79aa895778271819312f58e1159a10158aa289ad`. Existing `FireSim.compute` water constants were accepted unchanged. Evidence supports the bounded `water=0..3` suppression band; no Timberborn-owned fire rules, steam tuning, badwater behavior, or resource accounting were added.
- 2026-05-03 worker evidence: committed dry direct-control and wet direct-suppression shader fixtures and captures under `tests/Wildfire.Core.Tests/ShaderSnapshots/twf-090/`. Dry control produced per-tick deltas `[5, 5, 5, 1]` with `12` non-target neighbor deltas; wet `water=3` direct suppression produced `[1, 1, 0, 0]` with `0` non-target deltas. Existing `water-barrier` remains accepted with `[5, 5, 5, 5]`, one hot cell, zero burning cells, and five water cells.
- 2026-05-03 worker verification: `git diff --check` passed, `git diff --cached --check` passed before commit, full `dotnet test` passed with 135 tests, targeted `dotnet test --filter FullyQualifiedName~AcceptedWaterSuppressionSnapshotsCompareBarrierAndDirectSuppression` passed, and the opt-in Unity shader harness passed. Unity logs and captures live under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-090-water-suppression/`.
- 2026-05-03 worker blocker: no live Timberborn suppression recording was captured. This ticket still depends on upstream live gates for `TWF-088` and `TWF-089`, plus its own live recording and command/status evidence.
- 2026-05-03 coordinator: moved to `04-verify` for review. Do not move to `05-integration` unless review passes, upstream live blockers are accepted, and this ticket's live suppression recording gate passes.
- 2026-05-03 review: passed review on commit `79aa895778271819312f58e1159a10158aa289ad` with no blocking findings. Review confirmed the diff is docs/tests only for deterministic evidence, current water constants remain `2/2/10/3`, dry/wet/barrier assertions match `docs/TEST_PLAN.md`, and no `FireSim.compute`, Timberborn adapter logic, spread pace, fuel duration, steam tuning, badwater/contamination behavior, or resource consequence accounting changed.
- 2026-05-03 review verification: `git diff --check 082077d2b99819c4b448b0ba9fe758ed81f4f412..79aa895778271819312f58e1159a10158aa289ad` passed, targeted `dotnet test --filter FullyQualifiedName~AcceptedWaterSuppressionSnapshotsCompareBarrierAndDirectSuppression` passed, full `dotnet test` passed with 135 tests, and `bun run kanban:audit` passed with `critical_findings=0`.
- 2026-05-03 coordinator: moved to `07-blocked` because upstream live blockers for reviewed `TWF-088` and `TWF-089` must resolve first, and this ticket still requires live Timberborn suppression recording, `qa-water-suppression-stimulus` followed by `qa-readiness` or `status` with durable water-change evidence, copied `Player.log`, recording artifact paths, and final QA lock/state evidence.
