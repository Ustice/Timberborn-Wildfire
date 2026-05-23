# Wildfire Process Runbook

Historical note: the file kanban board was migrated to GitHub Issues on 2026-05-23. Use this runbook only for file-board archaeology, migration reconciliation, or explicit board cleanup. Use GitHub Issues as the active backlog for new work.

Use this runbook when reconstructing a historical Wildfire multi-agent sprint.

Project-local Codex skills live under [../.codex/skills/](../.codex/skills/). Use `kanban` for GitHub issue coordination and `ticket-agent` for one assigned Worker, QA, Reviewer, or Researcher issue.

## Quick Kickoff Prompt

```text
Start a Wildfire coordination run in /Users/jasonkleinberg/repos/wildfire.

Read AGENTS.md, docs/INDEX.md, docs/HANDOFF.md, kanban/github-issue-workflow.md, kanban/github-issue-migration.md, and kanban/roles/coordinator.md.

Use GitHub Issues as the source of truth. Reconcile current work, then assign the next dependency-ready issue. Keep the run boring, traceable, and evidence-first.

Do not end the coordination turn until the issue sweep is closed or the user explicitly tells you to stop. Do not make code changes as the coordinator; delegate implementation, QA, review, and research through issues.
```

## Startup Checklist

- [ ] Read `AGENTS.md`.
- [ ] Read [../docs/INDEX.md](../docs/INDEX.md).
- [ ] Read [../docs/HANDOFF.md](../docs/HANDOFF.md).
- [ ] Read [../kanban/github-issue-workflow.md](github-issue-workflow.md).
- [ ] Read [../kanban/github-issue-migration.md](github-issue-migration.md).
- [ ] Read [../kanban/README.md](../kanban/README.md).
- [ ] Read [../kanban/sprint-planning.md](../kanban/sprint-planning.md).
- [ ] Read [../kanban/roles/coordinator.md](../kanban/roles/coordinator.md).
- [ ] Read [../kanban/roles/worker.md](../kanban/roles/worker.md).
- [ ] Read [../kanban/roles/qa.md](../kanban/roles/qa.md).
- [ ] Read [../kanban/roles/reviewer.md](../kanban/roles/reviewer.md).
- [ ] Read [../kanban/roles/tech-lead.md](../kanban/roles/tech-lead.md).
- [ ] Read [../kanban/roles/researcher.md](../kanban/roles/researcher.md).
- [ ] Review GitHub Issues with `gh issue list --repo Ustice/Timberborn-Wildfire`.
- [ ] Run `git status --short --branch`.
- [ ] Run `git worktree list`.
- [ ] Start `caffeinate -disu` for the duration of any live Timberborn QA sprint run, and record whether it is active before dispatching QA.
- [ ] Compare `docs/HANDOFF.md` issue state with `gh issue list`.
- [ ] Start from `docs/HANDOFF.md` Next Exact Action unless GitHub Issues have newer evidence.

## Run Loop

1. Reconcile assigned issues against worker reports, commits, current diffs, and issue comments.
2. If resuming a sprint, delegate a Reviewer current-state pass before assigning new work.
3. Label dependency-ready issues with `status:ready`.
4. Assign one issue per sub-agent using [assignment-packet-template.md](assignment-packet-template.md), with a link to their file in `kanban/roles`, write scope, dependencies, verification, and the GitHub issue URL.
5. While workers run, dispatch newly unblocked issues when write scopes do not conflict.
6. Wait for sub-agent reports and issue updates.
9. Before live Timberborn QA, confirm `caffeinate -disu` is active so display sleep or screensaver does not invalidate screenshot/foreground evidence.
10. Send runtime or live-game tickets to QA when required; QA owns live builds, Timberborn deploys, launches, restarts, screenshots, logs, and the shared deploy/QA lock for those checks.
11. If review fails, keep the issue open with findings attached.
12. After review fixes land, require a fresh passing review before closure.
13. If required QA fails, label the issue `status:blocked` with the failing evidence and smallest unblock action.
14. Close accepted issues only after required review has passed and required QA has passed with evidence.
15. Tear down merged worktrees.

## Shared Board Discipline

- GitHub Issues are the shared backlog authority.
- Implementation worktrees are code sandboxes, not issue authorities.
- Sub-agents should report notes, evidence, blockers, and recommended status-label changes to the coordinator.
- The coordinator should transcribe accepted notes and update GitHub issue labels.
- Ask a sub-agent to edit historical kanban tickets only when the issue explicitly includes migration-cleanup scope.
- Use [evidence-manifest-template.md](evidence-manifest-template.md) for long runtime artifacts instead of pasting bulky evidence into ticket prose.

## Roles

- [Coordinator](../kanban/roles/coordinator.md) owns issue state, dependency order, delegation, integration, and status docs.
- [Worker](../kanban/roles/worker.md) owns one issue implementation in one worktree.
- [QA](../kanban/roles/qa.md) owns live builds, Timberborn deployments, launch, runtime validation, screenshots, logs, and evidence.
- [Reviewer](../kanban/roles/reviewer.md) owns risk review, architecture review, diff review, and gate recommendations.
- [Tech-Lead](../kanban/roles/tech-lead.md) owns broader architecture review, cleanup pressure, and integration-order recommendations.
- [Researcher](../kanban/roles/researcher.md) owns focused unknowns that block decisions or implementation.

## Verification Rules

- Run `git diff --check` for all integrated work.
- Run `dotnet test` for code, content, script, or behavior issues.
- Run live Timberborn validation only after an issue explicitly requires it.
- An issue that fails review must pass another review after fixes before it can close.
- An issue that fails required QA must pass that same QA gate before it can close.
- Route live Timberborn deploy, launch, restart, and shared-lock operations through QA to avoid worker lanes deadlocking each other.
- Documentation-only tickets do not need runtime validation when they only change docs.

## Documentation Ownership

- GitHub issues are the per-agent work surface.
- [sprint-planning.md](sprint-planning.md) preserves historical sprint-boundary recommendations.
- `kanban/sprints/` preserves historical sprint charters.
- [assignment-packet-template.md](assignment-packet-template.md) owns sub-agent dispatch shape.
- [evidence-manifest-template.md](evidence-manifest-template.md) owns compact evidence references.
- `docs/HANDOFF.md` is the current status and next-action surface.
- `docs/DESIGN.md` owns durable product and simulation design.
- `docs/ARCHITECTURE.md` owns durable system boundaries.
- `docs/TEST_PLAN.md` owns validation strategy and accepted evidence.
- `docs/TODO.md` owns milestone status.
- Historical or noisy sprint detail belongs in `docs/archive/`.

## Sprint Close

- Close integrated issues.
- Label unresolved work with `status:blocked` or `status:deferred` with concrete next actions.
- Update handoff, milestone, design, architecture, and test docs only when project state changed.
- Final response should summarize outcome, verification, blockers, and next action.
