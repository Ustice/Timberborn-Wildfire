# GitHub Issue Workflow

Use GitHub Issues as the active Wildfire backlog after the 2026-05-23 migration.

Repository: <https://github.com/Ustice/Timberborn-Wildfire/issues>

## Status Labels

- `status:todo`: scoped work that is not selected as an active ready item.
- `status:ready`: dependency-ready work that can be assigned.
- `status:blocked`: work blocked on evidence, environment access, dependencies, or decisions.
- `status:deferred`: valid future work that is intentionally out of the current milestone.
- `source:kanban`: issue migrated from the historical file-board ticket system.

## Working An Issue

1. Read `AGENTS.md`, `docs/INDEX.md`, `docs/HANDOFF.md`, this workflow, and the GitHub issue.
2. Confirm the issue status label, dependencies, write scope, required QA, and verification contract.
3. Use a worktree and branch that include the original `TWF-*` id when implementation changes are needed.
4. Keep work inside the issue write scope unless Jason or the coordinator explicitly expands it.
5. Report progress, evidence, blockers, and final results as GitHub issue comments.
6. Update status labels instead of moving file-board symlinks.
7. Leave historical file-kanban material on `archive/file-kanban-2026-05-23` untouched unless the task explicitly asks for migration cleanup or archaeology.

## Status Changes

- Move to `status:ready` only when dependencies are accepted and the issue is assignable.
- Move to `status:blocked` when the next action needs missing evidence, environment access, upstream work, or a decision.
- Move to `status:deferred` when the work is real but intentionally later.
- Close the issue only after required review, tests, QA, and integration are complete.
- If required QA fails, keep the issue open and label it `status:blocked` or leave a clear comment explaining the failed gate and smallest rerun.
- If review fails, keep the issue open, comment with findings, and require a fresh passing review after fixes.

## Useful Commands

```bash
gh issue list --repo Ustice/Timberborn-Wildfire --label status:ready
gh issue view 40 --repo Ustice/Timberborn-Wildfire --comments
gh issue comment 40 --repo Ustice/Timberborn-Wildfire --body "..."
gh issue edit 40 --repo Ustice/Timberborn-Wildfire --remove-label status:ready --add-label status:blocked
```

## Historical File Board

- `archive/file-kanban-2026-05-23:kanban/all-tickets/` preserves migrated ticket bodies and evidence history.
- `archive/file-kanban-2026-05-23:kanban/by-status/` preserves final migrated board state.
- `archive/file-kanban-2026-05-23:kanban/sprints/` preserves historical sprint charters.
- `archive/file-kanban-2026-05-23:kanban/evidence-manifests/` preserves historical evidence manifests.
- `kanban/github-issue-migration.md` maps `TWF-*` ids to GitHub issue numbers.
