# Wildfire Process Runbook

Use this runbook when starting or resuming a Wildfire multi-agent sprint.

Project-local Codex skills live under [../.codex/skills/](../.codex/skills/). Use `coordinate-sprint` for sprint management and `ticket-agent` for one assigned Worker, QA, Reviewer, or Researcher ticket.

## Quick Kickoff Prompt

```text
Start a Wildfire coordination run in /Users/jasonkleinberg/Documents/New project.

Read AGENTS.md, docs/INDEX.md, docs/HANDOFF.md, kanban/README.md, kanban/roles/coordinator.md, active status symlinks under kanban/by-status/, and canonical ticket files under kanban/all-tickets/.

Use kanban as the source of truth. Reconcile current work, then assign the next dependency-ready ticket. Keep the run boring, traceable, and evidence-first.

Do not end the coordination turn until the sprint is closed or the user explicitly tells you to stop. Do not make code changes as the coordinator; delegate implementation, QA, review, and research through tickets.
```

## Startup Checklist

- [ ] Read `AGENTS.md`.
- [ ] Read [INDEX.md](INDEX.md).
- [ ] Read [HANDOFF.md](HANDOFF.md).
- [ ] Read [../kanban/README.md](../kanban/README.md).
- [ ] Read [../kanban/roles/coordinator.md](../kanban/roles/coordinator.md).
- [ ] Read [../kanban/roles/worker.md](../kanban/roles/worker.md).
- [ ] Read [../kanban/roles/qa.md](../kanban/roles/qa.md).
- [ ] Read [../kanban/roles/tech-lead.md](../kanban/roles/tech-lead.md).
- [ ] Read [../kanban/roles/researcher.md](../kanban/roles/researcher.md).
- [ ] Review active status symlinks under `kanban/by-status/` and canonical ticket files under `kanban/all-tickets/`.
- [ ] Run `git status --short --branch`.
- [ ] Run `git worktree list`.
- [ ] Compare `docs/HANDOFF.md` ticket state with `find kanban -maxdepth 2 \( -type f -o -type l \)`.
- [ ] Start from `docs/HANDOFF.md` Next Exact Action unless the board has newer evidence.

## Run Loop

1. Reconcile `04-verify/` and `03-in-progress/` tickets against worker reports, commits, current diffs, and ticket notes.
2. If resuming a sprint, delegate a Reviewer current-state pass before assigning new work.
3. Move dependency-ready tickets to `02-ready/`.
4. Assign one ticket per sub-agent with a link to their file in `kanban/roles`, write scope, dependencies, and verification.
5. Move assigned tickets to `03-in-progress/`.
6. While workers run, dispatch newly unblocked tickets when write scopes do not conflict.
7. Wait for sub-agent reports and ticket updates.
8. Move completed implementation tickets to `04-verify/`.
9. Send runtime or live-game tickets to QA when required.
10. Move accepted tickets to `05-integration/`.
11. Tech-Lead should review integrate accepted work in dependency order.
12. Rerun required checks after integration.
13. Move integrated tickets to `06-done/`.
14. Tear down merged worktrees.
15. Move user-decision tickets to `09-awaiting-review/` with the exact ask and blocked downstream tickets.

## Roles

- [Coordinator](../kanban/roles/coordinator.md) owns board state, dependency order, delegation, integration, and sprint-close status docs.
- [Worker](../kanban/roles/worker.md) owns one ticket implementation in one worktree.
- [QA](../kanban/roles/qa.md) owns build, launch, runtime validation, screenshots, logs, and evidence.
- [Reviewer](../kanban/roles/tech-lead.md) owns risk review, architecture review, diff review, and integration recommendations.
- [Researcher](../kanban/roles/researcher.md) owns focused unknowns that block decisions or implementation.

## Verification Rules

- Run `git diff --check` for all integrated work.
- Run `bun run test` for code, content, script, or behavior tickets.
- Run live Timberborn validation only after a ticket explicitly requires it.
- Documentation-only tickets do not need runtime validation when they only change docs.

## Documentation Ownership

- Tickets are the per-agent work surface.
- `docs/HANDOFF.md` is the current status and next-action surface.
- `docs/DESIGN.md` owns durable product and simulation design.
- `docs/ARCHITECTURE.md` owns durable system boundaries.
- `docs/TEST_PLAN.md` owns validation strategy and accepted evidence.
- `docs/TODO.md` owns milestone status.
- Historical or noisy sprint detail belongs in `docs/ARCHIVE/`.

## Sprint Close

- Move integrated work to `06-done/`.
- Move unresolved work to `07-blocked/`, `08-deferred/`, or `09-awaiting-review/` with concrete next actions.
- Update handoff, milestone, design, architecture, and test docs only when project state changed.
- Final response should summarize outcome, verification, blockers, and next action.
