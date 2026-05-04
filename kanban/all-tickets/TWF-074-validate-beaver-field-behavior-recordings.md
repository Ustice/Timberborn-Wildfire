---
ticket: TWF-074
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-065
  - TWF-085
  - TWF-086
  - TWF-087
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
- Validate smoke, toxic smoke, and fire behavior variants separately, including explicit no-op or deferral evidence for any unsafe state.
- Preserve command outputs, copied `Player.log`, recording paths, screenshots, and final QA lock state.
- Validate the accepted `TWF-071` behavior contract against live evidence.
- Record confusing or unsafe beaver behavior as follow-up ticket notes.
- Prefer the generated world-consequence scenario from `TWF-119` when it exists; otherwise record the exact save and setup used.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-065` provides recording tooling.
- `TWF-085` implements the smoke variant.
- `TWF-086` implements the toxic smoke variant.
- `TWF-087` implements the fire and heat variant.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Passing evidence requires no new critical Unity or Wildfire failure tokens in the run window.

## Notes

- This is the player-legibility gate for beaver field behavior, not another implementation ticket.
- If the generated scenario does not exist yet, QA may use a hand-built save but must capture enough setup detail for reproduction.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
