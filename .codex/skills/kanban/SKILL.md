---
name: kanban
description: Start or resume a multi-agent sprint in ~/repos/wildfire. Use when the user asks to coordinate, continue, run, manage, or close a sprint or ticket-board workflow.
---

# Coordinate Sprint

## Workflow

Use this skill only for the Wildfire repo at `~/repos/wildfire`.

1. Start in `~/repos/wildfire`.
2. Read `AGENTS.md`, `docs/INDEX.md`, `docs/HANDOFF.md`, `kanban/README.md`, `kanban/process.md`, and `kanban/roles/coordinator.md`.
3. Read active status symlinks under `kanban/by-status/` and canonical ticket files under `kanban/all-tickets/`.
4. When resuming an existing sprint, start by delegating a Reviewer current-state pass.
5. Treat `kanban/by-status` as the source of truth for sprint state.
6. Follow `kanban/process.md` for startup, run loop, verification, integration, and sprint close.
7. Link sub-agents to the relevant role doc.
8. Use rolling dispatch when dependencies and write scopes allow it.

## Guardrails

- Do not end the coordination turn until the sprint is closed or the user explicitly tells you to stop.
- Do not make code, content, script, runtime behavior, or test changes directly as the coordinator.
- Delegate implementation, QA, review, and research through tickets.
- Pick model strength from task difficulty, uncertainty, and blast radius.
- Require sub-agents to update their assigned tickets with notes, evidence, blockers, and results.
- Keep board moves serialized in one checkout.
- Keep and manage the instructions documents for the various roles.

## Closeout

Before final response, verify the board state is current, required checks are recorded, blockers are explicit, and sprint-close docs are updated when project status changed.
