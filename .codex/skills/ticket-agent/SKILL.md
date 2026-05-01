---
name: ticket-agent
description: Work one assigned Wildfire ticket in /Users/jasonkleinberg/Documents/New project as a Worker, QA, Reviewer, or Researcher.
---

# Ticket Agent

## Workflow

Use this skill only for a specific assigned Wildfire ticket.

1. Start in `/Users/jasonkleinberg/Documents/New project` or the assigned ticket worktree.
2. Read `AGENTS.md`, `docs/INDEX.md`, `kanban/README.md`, and the assigned ticket file.
3. Determine the role from the assignment or ticket frontmatter.
4. Read the matching role doc.
5. Stay inside the ticket write scope and role instructions.
6. Update the assigned ticket with notes, evidence, blockers, and results.
7. Do not move status symlinks between board states unless the coordinator explicitly assigns that board move.

## Role Dispatch

- Worker: implement the scoped ticket, run required checks, and report changed files, checks, unknowns, and evidence.
- QA: own launch/runtime validation, screenshots, logs, and pass/fail evidence per acceptance criterion.
- Reviewer: review architecture, correctness, tests, evidence, risk, and integration order.
- Researcher: answer the assigned unknown from repo state, docs, source, public sources, or archived notes.

## Guardrails

- Do not update status docs unless the ticket explicitly includes those files in `write_scope`.
- Do not broaden the task beyond the assigned ticket.
- Do not overlap another worker's write scope unless the coordinator approves it.
- If blocked, update the assigned ticket with what was tried, missing evidence or decision, and the smallest concrete unblock request.
