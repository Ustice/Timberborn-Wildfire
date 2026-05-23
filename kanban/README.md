# Wildfire Ticket Board

> Historical note: open-family tickets were migrated to GitHub Issues on 2026-05-23. Use <https://github.com/Ustice/Timberborn-Wildfire/issues> as the active backlog. This file board remains useful for historical ticket evidence and migration archaeology. See [github-issue-migration.md](github-issue-migration.md).

This directory is the historical file-based board for Wildfire work.

Canonical historical ticket files live in `all-tickets/`. Status directories live under `by-status/` and contain symlinks to those canonical files.

Do not move status symlinks for new work. Update GitHub issue status labels instead.

Use [github-issue-workflow.md](github-issue-workflow.md) for active work. Use [process.md](process.md), [sprint-planning.md](sprint-planning.md), and [sprints/](sprints/) only when reconstructing historical board state.

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

Historical tickets use one Markdown file per ticket in `all-tickets/`. Keep canonical filenames stable unless pruning the historical board.

Status was intentionally not stored in ticket frontmatter. The status symlink records final migrated board state; GitHub labels are active status now.

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

## Historical Coordinator Rules

- Use GitHub Issues as the source of truth for active issue state.
- Read [roles/coordinator.md](roles/coordinator.md) before starting or resuming a sprint.
- Read [github-issue-workflow.md](github-issue-workflow.md) before selecting work.
- Use [github-issue-migration.md](github-issue-migration.md) to map historical `TWF-*` ids.
- Do not end the coordination turn until the issue sweep is closed or the user explicitly tells you to stop.
- Do not make code, content, script, runtime behavior, or test changes directly as the coordinator.
- Assign one issue per sub-agent with role instructions, explicit write scope, dependencies, and verification.
- Use [assignment-packet-template.md](assignment-packet-template.md) when dispatching sub-agents.
- Do not overlap worker write scopes unless unavoidable.
- Require sub-agents to update assigned issues with notes, evidence, blockers, and results.
- Keep GitHub issue status-label changes serialized.
- Review diffs before integration.
- Use [roles/reviewer.md](roles/reviewer.md) for ticket gate reviews and [roles/tech-lead.md](roles/tech-lead.md) for broader architecture or integration-order review.
- If review fails, keep the issue open and require a fresh passing review after fixes.
- Never close an issue that failed required QA until the same required QA gate has passed with evidence.
- Integrate accepted work in dependency order.
- Remove worktrees after their work is merged into the mainline.
- Keep status docs as milestone or sprint-close surfaces, not per-agent scratchpads.

## Board Audit

Run:

```bash
bun run kanban:audit
```

The audit is read-only and historical. It reports dependency-ready tickets, blocked tickets, likely write-scope overlaps, missing ticket hygiene, broken symlinks, missing dependency files, and unexpected `kanban/` changes in sibling worktrees. Use it only when maintaining the historical board.

## Blocked And Deferred Work

- Label blocked issues with `status:blocked` and comment with what was tried, missing evidence, and the smallest concrete unblock action.
- Use issue comments when Jason needs to read and decide.
- Label deferred issues with `status:deferred` when the work is valid but should wait for a future milestone.
