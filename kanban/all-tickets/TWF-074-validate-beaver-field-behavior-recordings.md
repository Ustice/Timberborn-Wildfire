---
ticket: TWF-074
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
   - TWF-065
   - TWF-073
write_scope:
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - kanban/all-tickets/TWF-074-validate-beaver-field-behavior-recordings.md
---

# TWF-074: Validate Beaver Field Behavior Recordings

## Goal

Capture reviewable recordings proving beaver behavior in wildfire fields.

## Why

Beaver behavior is spatial and time-based. Logs and counters are not enough to judge whether beavers visibly avoid, leave, continue work, or otherwise respond to fields in a player-legible way.

## Requirements

- Use the screen recording tool from `TWF-065`.
- Capture at least one low-resolution behavior recording for timing/path review.
- Capture high-resolution clips or screenshots when player-facing details matter.
- Preserve command outputs, copied `Player.log`, recording paths, screenshots, and final QA lock state.
- Validate the accepted `TWF-071` behavior contract against live evidence.
- Record confusing or unsafe beaver behavior as follow-up ticket notes.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-065` provides recording tooling.
- `TWF-073` implements beaver field behavior.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Passing evidence requires no new critical Unity or Wildfire failure tokens in the run window.

## Notes

- This is the player-legibility gate for beaver field behavior, not another implementation ticket.
