---
ticket: TWF-126
agent_level: Low
role: worker
requires_qa: false
doc_only: true
dependencies: []
write_scope:
  - kanban/all-tickets/**
  - kanban/by-status/**
---

# TWF-126: Add Ticket Implementation Notes Hygiene Pass

## Goal

Bring open world-consequence tickets up to junior-ready detail before they are assigned.

## Why

The consequence design is now broad enough that tickets need more than goals and requirements. Each assignee should have enough references, likely files, telemetry expectations, and safe blocker criteria to work without rediscovering the design conversation.

## Requirements

- Review open tickets related to burn damage, ash, contamination, field visuals, beaver exposure, stored goods, scenario saves, and future suppression.
- Add or tighten an `Implementation Notes` section where the ticket needs source pointers, service boundaries, telemetry fields, QA commands, or safe no-op rules.
- Preserve completed tickets as historical records.
- Do not create duplicate tickets when an unimplemented ticket can be edited.
- Keep child tickets linked to their parents and parent tickets dependent on their children.
- Run symlink and markdown hygiene checks after edits.

## Dependencies

- None.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Keep this pass to canonical ticket prose under `kanban/all-tickets/**`; do not move board symlinks, edit implementation code, or update runtime/project docs.
- Prioritize Sprint 5 tickets that are already ready or in progress, then tighten near-term child tickets that are waiting on those results.
- Add source pointers, ownership boundaries, safe blocker language, QA commands, telemetry fields, and artifact expectations only where they reduce assignment ambiguity.
- Preserve completed tickets as historical records and avoid rewriting old outcome notes.

## Verification

- Run `git diff --check`.
- Verify every new status symlink resolves to a canonical ticket.

## Notes

- This ticket tracks the broad pass. Some high-priority implementation notes may be added before this ticket is assigned.
- Worker pass added `Implementation Notes` to the Sprint 5 lifecycle, compatibility, burnout, recording, settings, resource-catalog, and scenario-save tickets prioritized by the assignment: `TWF-047`, `TWF-049`, `TWF-050`, `TWF-064`, `TWF-065`, `TWF-093`, `TWF-094`, `TWF-095`, `TWF-096`, `TWF-097`, `TWF-114`, `TWF-118`, and `TWF-119`.
- Verification from this prose-only pass: `git diff --check` passed, status symlink resolution check found no broken links, and `bun run kanban:audit` reported `critical_findings=0`.
- Remaining missing `Implementation Notes` reported by `bun run kanban:audit` are lower-priority or future-sprint tickets outside this pass's completed slice; coordinator can dispatch a follow-up board hygiene ticket if those should be tightened before assignment.
