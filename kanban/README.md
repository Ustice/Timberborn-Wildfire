# Wildfire Ticket Board

This directory is the permanent file-based board for Wildfire work.

Canonical ticket files live in `all-tickets/`. Status directories live under `by-status/` and contain symlinks to those canonical files.

Move status symlinks instead of moving the ticket files.

Use [process.md](process.md) to start or resume a multi-agent coordination run. Use [sprint-planning.md](sprint-planning.md) to choose sprint boundaries before moving tickets into `02-ready/`. Use [sprints/TEMPLATE.md](sprints/TEMPLATE.md) for sprint charters and [assignment-packet-template.md](assignment-packet-template.md) for sub-agent dispatch.

Role instructions live under [roles/](roles/): Coordinator, Worker, QA, Reviewer, Tech-Lead, and Researcher.

## Status Flow

```text
01-todo -> 02-ready -> 03-in-progress -> 04-verify -> 05-integration -> 06-done
                                      \-> 07-blocked
                                      \-> 08-deferred
                                      \-> 09-awaiting-review
```

## Status Meaning

- `01-todo/` contains scoped work that is not dependency-ready.
- `02-ready/` contains work whose dependencies are complete enough to assign.
- `03-in-progress/` contains work owned by an active worker.
- `04-verify/` contains work a worker says is complete, pending review or QA. If review fails, the ticket returns to `03-in-progress/` for fixes and must pass a later review before integration. If QA fails, the ticket stays in or returns to `04-verify/` or moves to `07-blocked/`; it must not advance to integration until the failed QA gate passes.
- `05-integration/` contains verified work waiting to be merged or reconciled. Work that failed review or required QA is not verified until the relevant gate passes after fixes.
- `06-done/` contains integrated work with required checks complete.
- `07-blocked/` contains work that cannot proceed without evidence, environment access, or a decision.
- `08-deferred/` contains real work that should wait for a later milestone.
- `09-awaiting-review/` contains work that needs Jason to read, decide, or approve.

## Ticket Format

Use one Markdown file per ticket in `all-tickets/`. Keep canonical filenames stable. Start from [all-tickets/TEMPLATE.md](all-tickets/TEMPLATE.md).

Status is intentionally not stored in ticket frontmatter. The status symlink is the source of truth.

```markdown
---
ticket: TWF-000
agent_level: Low
role: worker
requires_qa: false
doc_only: false
dependencies: []
write_scope:
  - docs/**
---

# TWF-000: Short Imperative Title

## Goal

State the outcome.

## Requirements

- Keep requirements testable and scoped.

## Verification

- Run `git diff --check`.
- Run `dotnet test` unless this is documentation-only.

## Notes

- Capture unknowns, decisions, and evidence links.
```

## Coordinator Rules

- Use this board as the source of truth for active ticket state.
- Read [roles/coordinator.md](roles/coordinator.md) before starting or resuming a sprint.
- Read [sprint-planning.md](sprint-planning.md) before selecting the sprint slice.
- Create or update a sprint charter from [sprints/TEMPLATE.md](sprints/TEMPLATE.md) before moving tickets into `02-ready/`.
- Run `bun run kanban:audit` during startup and closeout.
- Do not end the coordination turn until the sprint is closed or the user explicitly tells you to stop.
- Do not make code, content, script, runtime behavior, or test changes directly as the coordinator.
- Assign one ticket per sub-agent with role instructions, explicit write scope, dependencies, and verification.
- Use [assignment-packet-template.md](assignment-packet-template.md) when dispatching sub-agents.
- Do not overlap worker write scopes unless unavoidable.
- Require sub-agents to update assigned tickets with notes, evidence, blockers, and results.
- Keep canonical ticket-note updates and status symlink moves in the main checkout unless a board-maintenance ticket explicitly says otherwise.
- Review diffs before integration.
- Use [roles/reviewer.md](roles/reviewer.md) for ticket gate reviews and [roles/tech-lead.md](roles/tech-lead.md) for broader architecture or integration-order review.
- If review fails, move the ticket back to `03-in-progress/` for fixes or otherwise keep it out of `05-integration/`. After fixes land, the ticket must return to `04-verify/` and pass review again before integration.
- Never move a ticket that failed required QA into `05-integration/` until the same required QA gate has passed with evidence.
- Integrate accepted work in dependency order.
- Remove worktrees after their work is merged into the mainline.
- Keep status docs as milestone or sprint-close surfaces, not per-agent scratchpads.

## Board Audit

Run:

```bash
bun run kanban:audit
```

The audit is read-only. It reports dependency-ready tickets, blocked tickets, likely write-scope overlaps, missing ticket hygiene, broken symlinks, missing dependency files, and unexpected `kanban/` changes in sibling worktrees. Use `bun run kanban:audit -- --strict` when critical findings should fail the command.

## Blocked And Deferred Work

- Move blocked tickets to `07-blocked/` with what was tried, missing evidence, and the smallest concrete unblock action.
- Move user-decision tickets to `09-awaiting-review/` when Jason needs to read and decide.
- Move deferred tickets to `08-deferred/` when the work is valid but should wait for a future milestone.
