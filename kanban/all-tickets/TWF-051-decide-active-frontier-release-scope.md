---
ticket: TWF-051
agent_level: Medium
role: reviewer
requires_qa: false
doc_only: true
dependencies:
  - TWF-034
  - TWF-046
write_scope:
  - docs/DESIGN.md
  - docs/HANDOFF.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-011-active-frontier-optimization.md
  - kanban/all-tickets/TWF-051-decide-active-frontier-release-scope.md
---

# TWF-051: Decide Active Frontier Release Scope

## Goal

Make the release decision for `TWF-011`: promote active-frontier optimization, keep full-grid dispatch for release, or require more profiling.

## Why

Active frontier is a real complexity increase. The design allows full-grid dispatch first, and `TWF-034` exists to gather measured evidence. Before release hardening completes, the project needs an explicit performance decision rather than a lingering maybe.

## Requirements

- Review `TWF-034` profiling evidence and `TWF-046` live-loop evidence.
- Compare observed map size, dispatch/readback time, delta count, and player-visible cost against release needs.
- Recommend one of:
  - Promote `TWF-011` to ready for release.
  - Keep `TWF-011` deferred until after release.
  - Add one more profiling ticket with exact missing evidence.

- Update `TWF-011` notes with the decision.
- Update `docs/DESIGN.md`, `docs/TEST_PLAN.md`, or `docs/HANDOFF.md` only where the release decision changes guidance.

## Dependencies

- `TWF-034` records live dispatch profiling.
- `TWF-046` proves the live loop under meaningful workload.

## Role

- Reviewer.
- Follow [tech-lead.md](../roles/tech-lead.md).

## Verification

- Run `git diff --check`.
- No runtime validation is required for the decision itself.

## Notes

- Do not implement active frontier in this ticket.
- 2026-05-05 reviewer decision: keep `TWF-011` deferred until after the first release. `TWF-034` profiling already showed acceptable full-grid dispatch on the preserved `128x128x23` live save, and `TWF-046` proved the coherent live gameplay loop without dispatch/readback becoming player-visible cost. No extra profiling ticket is needed for release. Revisit active frontier only after larger-map, sustained high-delta, or consequence-heavy profiling shows a measured bottleneck.
