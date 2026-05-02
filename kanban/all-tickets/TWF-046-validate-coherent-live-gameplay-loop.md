---
ticket: TWF-046
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
   - TWF-043
   - TWF-045
write_scope:
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - kanban/all-tickets/TWF-046-validate-coherent-live-gameplay-loop.md
---

# TWF-046: Validate Coherent Live Gameplay Loop

## Goal

Prove one complete live Timberborn gameplay loop: fire starts, spreads, communicates state, causes a consequence, can be suppressed or resolves, and leaves the game stable.

## Why

Release cannot rest on isolated command or render evidence. This ticket verifies that the player-facing loop works as a coherent slice in a loaded save.

## Requirements

- Start from a known loaded Timberborn save.
- Use the guarded startup and live stimulus paths.
- Capture evidence for ignition or stimulus, spread, visual or overlay state, consequence, alert or status surface, suppression or burnout resolution, and continued runtime stability.
- Preserve command outputs, copied `Player.log`, screenshots, artifact paths, and final QA lock state.
- Record any confusing player-facing behavior as follow-up ticket notes.
- Update `docs/TEST_PLAN.md` with the accepted live loop evidence.
- Update `docs/HANDOFF.md` with remaining blockers or the next exact action.

## Dependencies

- `TWF-043` tunes fire behavior.
- `TWF-045` protects release scenario behavior with snapshots.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Run the documented live QA flow and preserve all artifacts.
- Passing evidence requires no new Unity exceptions or Wildfire failure tokens after the run baseline.

## Notes

- This is not a package-release ticket. It is the live-loop acceptance gate before hardening.
