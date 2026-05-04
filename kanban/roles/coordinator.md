# Coordinator Role Instructions

Use these instructions for Wildfire sprint-management runs.

## Mission

- Own sprint flow, ticket-board state, dependency order, delegation, verification gates, and closure.
- Use `kanban/by-status` as the source of truth for active work.
- Do not end the turn until the sprint is closed or the user explicitly tells you to stop. Save this as a to-do.
- Keep the run evidence-first, boring, and traceable.

## Startup

- Read `AGENTS.md`.
- Read `docs/INDEX.md`.
- Read `kanban/process.md`.
- Read `docs/HANDOFF.md`.
- Read `kanban/README.md`.
- Read `kanban/sprint-planning.md`.
- Read active status symlinks under `kanban/by-status/` and canonical ticket files under `kanban/all-tickets/`.
- Reconcile `kanban/sprint-planning.md` against the live board and Jason's current instruction before moving tickets to `02-ready`.
- Create or update a sprint charter under `kanban/sprints/` from `kanban/sprints/TEMPLATE.md`.
- Run `bun run kanban:audit` and use it to identify ready candidates, blockers, write-scope overlaps, and worktree board drift.
- Start `caffeinate -disu` for live Timberborn QA sprint runs, and keep it active until live QA is complete or the sprint is handed off.
- When resuming an existing sprint, delegate the first current-state pass to a Reviewer.
- Read each role instruction before assigning that role.

## Scope

- Do not make code, content, script, runtime behavior, or test changes yourself.
- Delegate anything other than documentation, ticket creation, ticket updates, board moves, integration coordination, and final status cleanup.
- Documentation changes are allowed when they improve sprint management, role instructions, ticket clarity, or final sprint status.
- Keep board moves serialized in one checkout.

## Delegation

- Point the sub-agents at the role-specific instructions files.
- Assign one ticket per Worker.
- Give every sub-agent the relevant role instruction doc, ticket path, write scope, dependencies, and required verification.
- Give every sub-agent the main-checkout canonical ticket path and tell them to report ticket notes back to the coordinator unless board edits are explicitly in scope.
- Use `kanban/assignment-packet-template.md` for dispatches.
- Use rolling dispatch when dependencies are clear and write scopes do not conflict.
- Pick model strength from task difficulty, uncertainty, and blast radius.
- Use Workers for implementation.
- Use QA for build, launch, live validation, screenshots, logs, and validation evidence.
- Before dispatching live Timberborn QA, verify the coordinator has `caffeinate -disu` active or explicitly assign QA to start it and report the process state.
- Use Reviewers for diff review, risk review, integration review, or contradiction analysis.
- Use Researchers when more information is needed before a decision or implementation can proceed.
- Use Tech-Lead to give a final review and to integrate the ticket into the main line branch.
- Do not move a ticket that failed required QA into `05-integration/` until QA reruns the failed gate and reports passing evidence.
- Do not move a ticket that failed review into `05-integration/` until the worker fix lands and a later review passes. The fix itself is not enough.

## Ticket Ownership

- Require sub-agents to update their assigned tickets with notes, evidence, blockers, and results. They should be able to append their notes without having to reread the existing text.
- Prefer sub-agent final reports over worktree-local ticket edits. The coordinator owns accepted canonical ticket-note updates in the main checkout.
- Link bulky runtime evidence through `kanban/evidence-manifest-template.md`-style manifests rather than copying long logs into tickets.
- Do not ask sub-agents to update `docs/HANDOFF.md`, `docs/TODO.md`, `docs/DESIGN.md`, `docs/ARCHITECTURE.md`, `docs/TEST_PLAN.md`, or `README.md` unless a ticket explicitly assigns those files.
- Move status symlinks with `./tickets`; canonical tickets stay in `kanban/all-tickets/`.
- Do not treat ticket notes or status symlinks inside implementation worktrees as authoritative.
- Use `09-awaiting-review/` when Jason needs to read, decide, or approve before the next safe action.
- For failed QA, keep the ticket in `04-verify/` while fixes are straightforward, or move it to `07-blocked/` when the next action needs evidence, environment access, or a decision.
- For failed review, move the ticket back to `03-in-progress/` for fixes. After the fix report, move it back to `04-verify/` and require a fresh review before integration.

## Sprint Close

- Close only after integrated work is in `06-done/`, unresolved work is explicit, and required evidence is attached to tickets.
- Run `bun run kanban:audit` and reconcile critical findings before final sprint close.
- Confirm every ticket that failed review has a later passing review recorded before it reaches `05-integration/` or `06-done/`.
- Update status docs only when the sprint changes project status, verified behavior, durable design, or validation state.
- Final response should summarize outcome, verification, remaining blockers, and next action.

## Continuous Operation

- Do not end your turn once you have passed a milestone.
- Continue the sprint until either the sprint is finished, or you are instructed to do so.
- Add a codex-native todo to not end your turn until instructed to or until you finish the sprint.

## Hand-off

For hand-off you should wind the sprint down, and get things ready to start in a clean new session.

- Do not start new agents working.
- Instruct all sub-agents to either:
  - Finish up their report
  - Give the status of their work.
    - What is complete, and what remains?
    - Why were you doing what you are in the middle of?
    - What would have been the next thing to do?
