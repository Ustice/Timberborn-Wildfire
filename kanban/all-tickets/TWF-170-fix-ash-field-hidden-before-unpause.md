---
ticket: TWF-170
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-078
  - TWF-159
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-170-fix-ash-field-hidden-before-unpause.md
---

# TWF-170: Fix Ash Field Hidden Before Unpause

## Goal

Fix the bug where the ash field does not show until after the game unpauses.

## Why

Ash is an aftermath/readability surface. A loaded paused game should show existing ash immediately instead of requiring simulation advancement or an unpause tick before the player can see it.

## Requirements

- Reproduce the issue from current `main` on a save or QA setup with existing ash while the game is loaded and paused.
- Determine whether the missing pre-unpause presentation is caused by simulator readback timing, ash read-model sync, visual-field binding, renderer/presenter gating, or Timberborn pause-state update order.
- Make existing ash presentation initialize from persisted/simulator ash state at loaded-save readiness, before the first unpaused simulation tick.
- Preserve simulator-owned ash authority. Do not add a second authoritative ash store to force presentation.
- Ensure pre-unpause presentation does not create duplicate ash, collectable goods, or extra simulator mutations.
- Expose or reuse status counters that can prove ash presentation is initialized while paused.
- Add deterministic coverage for ash presentation initialization from existing state without requiring an advancing tick.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-078` owns simulator-backed ash read-model verification.
- `TWF-159` owns driving ash presentation from simulator state.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- This is a presentation/initialization bug, not a request to change ash growth, decay, collection, or contamination semantics.
- Prefer a loaded-save or runtime-initialization refresh path over special-casing the QA command bridge.
- Check paused loaded-save screenshots for the whole scene so the fix does not introduce stale overlays, hidden fields, or duplicated ash visuals.

## Verification

- Run `git diff --check`.
- Run targeted ash field/presentation tests.
- Run `dotnet test`.
- Live QA must load a save with existing ash, remain paused, and capture screenshot/status/log evidence that ash is visible before unpause; then unpause and prove the ash state remains stable without duplication.

## Notes

- Created on 2026-05-20 from Jason's bug report that ash fields are not showing before the game unpauses.
