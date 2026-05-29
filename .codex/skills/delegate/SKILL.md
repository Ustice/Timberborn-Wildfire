---
name: ticket-agent
description: Work one assigned Wildfire GitHub issue in ~/repos/wildfire as a Worker, QA, Reviewer, or Researcher.
---

# Ticket Agent

## Workflow

Use this skill only for a specific assigned Wildfire GitHub issue or explicitly assigned historical `TWF-*` ticket.

1. Start in `~/repos/wildfire` or the assigned issue worktree.
2. Read `AGENTS.md`, `docs/INDEX.md`, `kanban/github-issue-workflow.md`, and the assigned GitHub issue.
3. Determine the role from the assignment or issue body.
4. Read the matching role doc.
5. Stay inside the issue write scope and role instructions.
6. Update the assigned GitHub issue with notes, evidence, blockers, and results.
7. Do not change issue status labels unless the coordinator explicitly assigns that status update.

## Role Dispatch

- Worker: implement the scoped issue, run required checks, and report changed files, checks, unknowns, and evidence.
- QA: own launch/runtime validation, screenshots, logs, pass/fail evidence, failure classification, and the smallest QA-tool improvement needed to make repeated validation reliable.
- Reviewer: review architecture, correctness, tests, evidence, risk, and integration order.
- Researcher: answer the assigned unknown from repo state, docs, source, public sources, or archived notes.

## Guardrails

- Do not update status docs unless the ticket explicitly includes those files in `write_scope`.
- Do not broaden the task beyond the assigned issue.
- Do not overlap another worker's write scope unless the coordinator approves it.
- If blocked, update the assigned issue with what was tried, missing evidence or decision, and the smallest concrete unblock request.
