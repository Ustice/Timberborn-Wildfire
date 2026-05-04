# Wildfire Process Runbook

Use this runbook when starting or resuming a Wildfire multi-agent sprint.

Project-local Codex skills live under [../.codex/skills/](../.codex/skills/). Use `kanban` for sprint management and `ticket-agent` for one assigned Worker, QA, Reviewer, or Researcher ticket.

## Quick Kickoff Prompt

```text
Start a Wildfire coordination run in /Users/jasonkleinberg/repos/wildfire.

Read AGENTS.md, docs/INDEX.md, docs/HANDOFF.md, kanban/README.md, kanban/sprint-planning.md, kanban/roles/coordinator.md, active status symlinks under kanban/by-status/, and canonical ticket files under kanban/all-tickets/.

Use kanban as the source of truth. Reconcile current work, then assign the next dependency-ready ticket. Keep the run boring, traceable, and evidence-first.

Do not end the coordination turn until the sprint is closed or the user explicitly tells you to stop. Do not make code changes as the coordinator; delegate implementation, QA, review, and research through tickets.
```

## Startup Checklist

- [ ] Read `AGENTS.md`.
- [ ] Read [../docs/INDEX.md](../docs/INDEX.md).
- [ ] Read [../docs/HANDOFF.md](../docs/HANDOFF.md).
- [ ] Read [../kanban/README.md](../kanban/README.md).
- [ ] Read [../kanban/sprint-planning.md](../kanban/sprint-planning.md).
- [ ] Read [../kanban/roles/coordinator.md](../kanban/roles/coordinator.md).
- [ ] Read [../kanban/roles/worker.md](../kanban/roles/worker.md).
- [ ] Read [../kanban/roles/qa.md](../kanban/roles/qa.md).
- [ ] Read [../kanban/roles/reviewer.md](../kanban/roles/reviewer.md).
- [ ] Read [../kanban/roles/tech-lead.md](../kanban/roles/tech-lead.md).
- [ ] Read [../kanban/roles/researcher.md](../kanban/roles/researcher.md).
- [ ] Review active status symlinks under `kanban/by-status/` and canonical ticket files under `kanban/all-tickets/`.
- [ ] Run `git status --short --branch`.
- [ ] Run `git worktree list`.
- [ ] Start `caffeinate -disu` for the duration of any live Timberborn QA sprint run, and record whether it is active before dispatching QA.
- [ ] Compare `docs/HANDOFF.md` ticket state with `find kanban -maxdepth 2 \( -type f -o -type l \)`.
- [ ] Compare [sprint-planning.md](sprint-planning.md) with the live board and Jason's current instruction.
- [ ] Create or update the active sprint charter under `kanban/sprints/`.
- [ ] Run `bun run kanban:audit` and review ready tickets, blockers, write-scope overlaps, and sibling worktree board drift.
- [ ] Start from `docs/HANDOFF.md` Next Exact Action unless the board has newer evidence.

## Run Loop

1. Reconcile `04-verify/` and `03-in-progress/` tickets against worker reports, commits, current diffs, and ticket notes.
2. If resuming a sprint, delegate a Reviewer current-state pass before assigning new work.
3. Move dependency-ready tickets to `02-ready/`.
4. Assign one ticket per sub-agent using [assignment-packet-template.md](assignment-packet-template.md), with a link to their file in `kanban/roles`, write scope, dependencies, verification, and the main-checkout canonical ticket path.
5. Move assigned tickets to `03-in-progress/`.
6. While workers run, dispatch newly unblocked tickets when write scopes do not conflict.
7. Wait for sub-agent reports and ticket updates.
8. Move completed implementation tickets to `04-verify/`.
9. Before live Timberborn QA, confirm `caffeinate -disu` is active so display sleep or screensaver does not invalidate screenshot/foreground evidence.
10. Send runtime or live-game tickets to QA when required; QA owns live builds, Timberborn deploys, launches, restarts, screenshots, logs, and the shared deploy/QA lock for those checks.
11. If review fails, move the ticket back to `03-in-progress/` for worker fixes or otherwise keep it out of `05-integration/` with the findings attached.
12. After review fixes land, move the ticket back to `04-verify/` and require a fresh passing review before integration.
13. If required QA fails, keep the ticket in `04-verify/` for worker fixes or move it to `07-blocked/` with the failing evidence and smallest unblock action.
14. Move accepted tickets to `05-integration/` only after required review has passed and required QA has passed with evidence.
15. Tech-Lead should review integrate accepted work in dependency order.
16. Rerun required checks after integration.
17. Move integrated tickets to `06-done/`.
18. Tear down merged worktrees.
19. Move user-decision tickets to `09-awaiting-review/` with the exact ask and blocked downstream tickets.

## Shared Board Discipline

- `~/repos/wildfire/kanban/by-status` and `~/repos/wildfire/kanban/all-tickets` are the shared board authority.
- Implementation worktrees are code sandboxes, not board authorities.
- Sub-agents should report notes, evidence, blockers, and recommended board moves to the coordinator.
- The coordinator should transcribe accepted notes and move status symlinks in the main checkout.
- Ask a sub-agent to edit canonical tickets directly only when the ticket explicitly includes board-maintenance scope.
- Use [evidence-manifest-template.md](evidence-manifest-template.md) for long runtime artifacts instead of pasting bulky evidence into ticket prose.

## Roles

- [Coordinator](../kanban/roles/coordinator.md) owns board state, dependency order, delegation, integration, and sprint-close status docs.
- [Worker](../kanban/roles/worker.md) owns one ticket implementation in one worktree.
- [QA](../kanban/roles/qa.md) owns live builds, Timberborn deployments, launch, runtime validation, screenshots, logs, and evidence.
- [Reviewer](../kanban/roles/reviewer.md) owns risk review, architecture review, diff review, and gate recommendations.
- [Tech-Lead](../kanban/roles/tech-lead.md) owns broader architecture review, cleanup pressure, and integration-order recommendations.
- [Researcher](../kanban/roles/researcher.md) owns focused unknowns that block decisions or implementation.

## Verification Rules

- Run `git diff --check` for all integrated work.
- Run `dotnet test` for code, content, script, or behavior tickets.
- Run live Timberborn validation only after a ticket explicitly requires it.
- A ticket that fails review must pass another review after fixes before it can move to `05-integration/`.
- A ticket that fails required QA must pass that same QA gate before it can move to `05-integration/`.
- Route live Timberborn deploy, launch, restart, and shared-lock operations through QA to avoid worker lanes deadlocking each other.
- Documentation-only tickets do not need runtime validation when they only change docs.

## Documentation Ownership

- Tickets are the per-agent work surface.
- [sprint-planning.md](sprint-planning.md) owns sprint-boundary recommendations.
- `kanban/sprints/` owns active sprint charters.
- [assignment-packet-template.md](assignment-packet-template.md) owns sub-agent dispatch shape.
- [evidence-manifest-template.md](evidence-manifest-template.md) owns compact evidence references.
- `docs/HANDOFF.md` is the current status and next-action surface.
- `docs/DESIGN.md` owns durable product and simulation design.
- `docs/ARCHITECTURE.md` owns durable system boundaries.
- `docs/TEST_PLAN.md` owns validation strategy and accepted evidence.
- `docs/TODO.md` owns milestone status.
- Historical or noisy sprint detail belongs in `docs/archive/`.

## Sprint Close

- Move integrated work to `06-done/`.
- Move unresolved work to `07-blocked/`, `08-deferred/`, or `09-awaiting-review/` with concrete next actions.
- Run `bun run kanban:audit` and reconcile critical findings.
- Update handoff, milestone, design, architecture, and test docs only when project state changed.
- Final response should summarize outcome, verification, blockers, and next action.
