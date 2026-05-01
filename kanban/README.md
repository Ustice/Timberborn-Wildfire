# Wildfire Ticket Board

This directory is the permanent file-based board for Wildfire work.

Canonical ticket files live in `all-tickets/`. Status directories live under `by-status/` and contain symlinks to those canonical files.

Move status symlinks instead of moving the ticket files. Use `./tickets move <TWF-id-or-ticket-path> <status>` for common moves.

Use [process.md](process.md) to start or resume a multi-agent coordination run.

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
- `04-verify/` contains work a worker says is complete, pending review or QA.
- `05-integration/` contains verified work waiting to be merged or reconciled.
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
- Run `bun run test` unless this is documentation-only.

## Notes

- Capture unknowns, decisions, and evidence links.
```

## Coordinator Rules

- Use this board as the source of truth for active ticket state.
- Read [COORDINATOR.md](COORDINATOR.md) before starting or resuming a sprint.
- Do not end the coordination turn until the sprint is closed or the user explicitly tells you to stop.
- Do not make code, content, script, runtime behavior, or test changes directly as the coordinator.
- Assign one ticket per sub-agent with role instructions, explicit write scope, dependencies, and verification.
- Do not overlap worker write scopes unless unavoidable.
- Require sub-agents to update assigned tickets with notes, evidence, blockers, and results.
- Review diffs before integration.
- Integrate accepted work in dependency order.
- Remove worktrees after their work is merged into the mainline.
- Keep status docs as milestone or sprint-close surfaces, not per-agent scratchpads.

## Blocked And Deferred Work

- Move blocked tickets to `07-blocked/` with what was tried, missing evidence, and the smallest concrete unblock action.
- Move user-decision tickets to `09-awaiting-review/` when Jason needs to read and decide.
- Move deferred tickets to `08-deferred/` when the work is valid but should wait for a future milestone.
