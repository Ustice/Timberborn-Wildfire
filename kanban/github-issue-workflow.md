# GitHub Issue Workflow

Use GitHub Issues as the active Wildfire backlog after the 2026-05-23 migration.

Repository: <https://github.com/Ustice/Timberborn-Wildfire/issues>

## Status Labels

- `status:todo`: scoped work that is not selected as an active ready item.
- `status:ready`: dependency-ready work that can be assigned.
- `status:rework`: required QA failed and the issue needs an implementation, documentation, fixture, test, or acceptance-criteria update before another QA pass.
- `status:qa-needed`: implementation or setup is ready and the next action is a focused QA retry, recording, screenshot pass, or live evidence rerun. This is an active work lane, not a stopping point.
- `status:blocked-by-environment`: Timberborn cannot load, respond, or provide a fair runtime QA target. Retry on the next run or once the game is responding again.
- `status:waiting-for-dependency`: the issue depends on another GitHub issue or intra-ticket dependency. Reassess once the dependent issue is resolved.
- `status:needs-fixture`: QA cannot make a determination because tooling, scenario data, capture support, or a fixture is missing. Create or dispatch a new GitHub issue for the smallest QA tool or fixture that can unblock the gate.
- `status:deferred`: valid future work that is intentionally out of the current milestone.
- `source:kanban`: issue migrated from the historical file-board ticket system.

Do not assign the legacy catch-all `status:blocked` label to new or updated issues. Reclassify it into one of the three specific blocked labels when touching an issue that still has it.

## Working An Issue

1. Read `AGENTS.md`, `docs/INDEX.md`, `docs/HANDOFF.md`, this workflow, and the GitHub issue.
2. Confirm the issue status label, dependencies, write scope, required QA, and verification contract.
3. Use a worktree and branch that include the GitHub issue number when implementation changes are needed. Include a `TWF-*` id only for migrated historical tickets that already have one.
4. Keep work inside the issue write scope unless Jason or the coordinator explicitly expands it.
5. Report progress, evidence, blockers, and final results as GitHub issue comments.
6. Update status labels instead of moving file-board symlinks.
7. Leave historical file-kanban material on `archive/file-kanban-2026-05-23` untouched unless the task explicitly asks for migration cleanup or archaeology.

## Creating Issues

- Use the GitHub issue number as the durable ticket identifier for newly created work.
- Do not assign new `TWF-###` names, titles, filenames, branch names, or worktree names.
- If the issue was migrated from the historical file board, keep the existing `TWF-*` id only as a historical reference and link it through `kanban/github-issue-migration.md` or the archived ticket path.
- New issue titles should describe the work directly, not start with a synthetic ticket id.

## Status Changes

- Move to `status:ready` only when dependencies are accepted and the issue is assignable.
- Move to `status:rework` when required QA fails and the issue needs a product, documentation, fixture, test, or acceptance-criteria update before QA can rerun. The issue comment must name the failing gate, evidence, required update, owner role, and rerun gate.
- Move to `status:qa-needed` when the next assignable action is QA evidence rather than implementation. The issue comment must name the specific retry target, commands or tool path, fixture/save requirements, expected evidence, and smallest pass/fail decision.
- Move to `status:blocked-by-environment` when Timberborn, Steam, display state, permissions, the shared QA lock, or local machine state prevents a fair run. The issue comment must name what failed to load or respond and the retry condition.
- Move to `status:waiting-for-dependency` when the issue is waiting on another GitHub issue or intra-ticket dependency. The issue comment must link the dependency and name the reassessment trigger.
- Move to `status:needs-fixture` when QA cannot determine pass/fail without new tooling, scenario data, capture support, or a fixture. The issue comment must link the new or requested QA-tool/fixture issue and state the gate it will unblock.
- Move to `status:deferred` when the work is real but intentionally later.
- Close the issue only after required review, tests, QA, and integration are complete.
- If required QA fails because the product, docs, fixture, test, or issue acceptance criteria need changes, keep the issue open as `status:rework` and dispatch the smallest update that can make the gate pass.
- If required QA fails but no update is needed and the next rerun is known and runnable, keep the issue open as `status:qa-needed` and dispatch that focused retry.
- If the blocker is a missing fixture, capture path, command, or evidence tool, move the issue to `status:needs-fixture` and create or dispatch the smallest QA tool/fixture ticket that removes that blocker before asking Jason to perform manual setup. The issue comment should name the new tool, command, expected evidence token, and whether failures should count as `tool_failure` or `product_failure`.
- If review fails, keep the issue open, comment with findings, and require a fresh passing review after fixes.

## Useful Commands

```bash
gh issue list --repo Ustice/Timberborn-Wildfire --label status:ready
gh issue list --repo Ustice/Timberborn-Wildfire --label status:rework
gh issue list --repo Ustice/Timberborn-Wildfire --label status:blocked-by-environment
gh issue list --repo Ustice/Timberborn-Wildfire --label status:waiting-for-dependency
gh issue list --repo Ustice/Timberborn-Wildfire --label status:needs-fixture
gh issue view 40 --repo Ustice/Timberborn-Wildfire --comments
gh issue comment 40 --repo Ustice/Timberborn-Wildfire --body "..."
gh issue edit 40 --repo Ustice/Timberborn-Wildfire --remove-label status:ready --add-label status:waiting-for-dependency
```

## Historical File Board

- `archive/file-kanban-2026-05-23:kanban/all-tickets/` preserves migrated ticket bodies and evidence history.
- `archive/file-kanban-2026-05-23:kanban/by-status/` preserves final migrated board state.
- `archive/file-kanban-2026-05-23:kanban/sprints/` preserves historical sprint charters.
- `archive/file-kanban-2026-05-23:kanban/evidence-manifests/` preserves historical evidence manifests.
- `kanban/github-issue-migration.md` maps `TWF-*` ids to GitHub issue numbers.
