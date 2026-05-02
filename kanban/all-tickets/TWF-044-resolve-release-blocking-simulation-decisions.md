---
ticket: TWF-044
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies:
   - TWF-042
write_scope:
   - docs/DESIGN.md
   - docs/ARCHITECTURE.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-044-resolve-release-blocking-simulation-decisions.md
---

# TWF-044: Resolve Release Blocking Simulation Decisions

## Goal

Turn the remaining release-relevant open design questions into explicit decisions or deferred non-release items.

## Why

The design currently keeps several questions open: tick cadence, diagonal spread, wind, ash storage, vertical building mapping, water semantics, heat-loss source, and full-grid versus active-frontier dispatch. Release needs the decisions that affect visible behavior to be intentional and documented.

## Requirements

- Review the open questions in `docs/DESIGN.md`.
- Decide or explicitly defer each release-relevant question.
- Keep the initial release conservative unless live evidence strongly supports more complexity.
- Document accepted decisions in `docs/DESIGN.md`.
- Update `docs/ARCHITECTURE.md` only if a boundary changes.
- Update `docs/TEST_PLAN.md` with validation expectations for accepted decisions.
- Add follow-up ticket notes only when a deferred decision needs future implementation.

## Dependencies

- `TWF-042` provides the first player-facing fire loop, making the decisions concrete enough to evaluate.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- No runtime validation is required for doc-only decision work.

## Notes

- Prefer "defer for release" over speculative complexity when the current loop can ship without it.
