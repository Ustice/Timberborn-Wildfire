---
ticket: TWF-000
agent_level: Low
role: worker
requires_qa: false
doc_only: false
dependencies: []
write_scope:
   - path/or/glob
---

# TWF-000: Short Imperative Title

## Goal

State the outcome in one or two sentences.

## Requirements

- Add concrete requirements.
- Keep the work scoped enough for one worker.

## Dependencies

- List ticket ids, evidence, or decisions that must exist first.

## Role

- Assign one of `worker`, `qa`, `reviewer`, `researcher`, or `coordinator`.
- Link the relevant file in `kanban/`.

## Verification

- Run `git diff --check`.
- Run `bun run test` unless this is a documentation-only ticket.
- Run live Timberborn QA only when the ticket explicitly requires it.

## Notes

- Add implementation hints, unknowns, and evidence links.
