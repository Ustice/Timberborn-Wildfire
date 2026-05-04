---
ticket: TWF-091
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-035
  - TWF-065
  - TWF-043
write_scope:
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-091-tune-structure-vertical-fire-behavior.md
---

# TWF-091: Tune Structure Vertical Fire Behavior

## Goal

Tune fire behavior for multi-cell structures and vertical fuel columns.

## Why

Tall and wide structures should burn coherently without adding building-specific fire rules to Timberborn. Vertical spread and footprint mapping need focused evidence before structure damage tickets build on them.

## Requirements

- Preserve the 6-neighbor 3D model and vertical footprint mapping.
- Compare vertical fuel column and building cluster scenarios.
- Keep structure behavior driven by packed cells and GPU rules.
- Tune only when deterministic shader evidence and live recordings support the change.
- Keep construction rollback and material-loss consequences out of this ticket.
- Document accepted evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-035` provides material and vertical footprint mapping.
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
- QA must capture recording evidence for one vertical or multi-cell structure scenario.

## Implementation Notes

- Preserve the packed-cell and GPU-owned behavior boundary; Timberborn structure data should only shape adapter inputs.
- Test vertical fuel-column behavior separately from construction rollback or material-loss consequences.

## Notes

- Structure consequences belong to `TWF-077`.
- 2026-05-03 coordinator: moved to `03-in-progress` for Sprint 6 worker implementation in `~/repos/wildfire-TWF-091` on branch `codex/TWF-091-structure-vertical-fire`. The worktree is based on reviewed `TWF-090` commit `79aa895778271819312f58e1159a10158aa289ad`, stacked on reviewed `TWF-089` and `TWF-088`; this ticket cannot integrate before upstream live QA blockers and its own live vertical or multi-cell structure recording gate are resolved.
- 2026-05-03 worker result: deterministic evidence complete in commit `83af04b10a05ef192bd9461ca0b90ae35fff5abd`. The worker accepted current structure/vertical constants unchanged and made no production rule changes.
- 2026-05-03 worker evidence: added `AcceptedStructureVerticalSnapshotsPinPackedCellSixNeighborBehavior` to pin `vertical-fuel-column` changed cells to `[7, 11, 12, 13, 17, 37]` each tick for bounded six-neighbor z-axis spread and `building-cluster` changed cells to `[63, 76, 77, 78, 91]` each tick for cardinal footprint behavior with diagonals unchanged. Evidence uses committed release captures under `tests/Wildfire.Core.Tests/ShaderSnapshots/release/`.
- 2026-05-03 worker verification: targeted `dotnet test --filter FullyQualifiedName~AcceptedStructureVerticalSnapshotsPinPackedCellSixNeighborBehavior` passed, full `dotnet test` passed with 136 tests, and `git diff --check` passed. The opt-in Unity shader harness was not rerun because no shader behavior changed.
- 2026-05-03 worker blocker: live Timberborn vertical or multi-cell structure recording remains required, and integration remains blocked behind upstream live gates for `TWF-088`, `TWF-089`, and `TWF-090`.
- 2026-05-03 coordinator: moved to `04-verify` for review. Do not move to `05-integration` unless review passes, upstream live blockers are accepted, and this ticket's live vertical or multi-cell structure recording gate passes.
- 2026-05-03 review: passed review on commit `83af04b10a05ef192bd9461ca0b90ae35fff5abd` with no findings. Review confirmed the patch only changes deterministic tests and `docs/TEST_PLAN.md`; no production shader, Timberborn adapter, suppression, burn-duration, consequence, construction rollback, or structure-accounting code changed.
- 2026-05-03 review verification: `git diff --check 79aa895..83af04b` passed, targeted `dotnet test --filter FullyQualifiedName~AcceptedStructureVerticalSnapshotsPinPackedCellSixNeighborBehavior` passed, and full `dotnet test` passed with 136 tests. Snapshot spot-checks matched `docs/TEST_PLAN.md`: `vertical-fuel-column` changed cells `[7, 11, 12, 13, 17, 37]` on every tick, and `building-cluster` changed cells `[63, 76, 77, 78, 91]` on every tick.
- 2026-05-03 coordinator: moved to `07-blocked` because upstream live blockers for `TWF-088`, `TWF-089`, and `TWF-090` must resolve first, and this ticket still requires one live Timberborn vertical or multi-cell structure recording plus command/status/log artifact proof.
