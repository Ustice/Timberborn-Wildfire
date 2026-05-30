---
name: kanban
description: Coordinate Wildfire GitHub Issues in ~/repos/wildfire, including migrated kanban-ticket cleanup and multi-agent issue workflow.
---

# Coordinate GitHub Issues

## Migration Note

The Wildfire file kanban board was migrated to GitHub Issues on 2026-05-23. Use GitHub Issues as the active backlog. Use branch `archive/file-kanban-2026-05-23` only for historical evidence, migration reconciliation, or explicit file-board cleanup.

## Workflow

Use this skill only for the Wildfire repo at `~/repos/wildfire`.

1. Start in `~/repos/wildfire`.
2. Read `AGENTS.md`, `docs/INDEX.md`, `docs/HANDOFF.md`, `kanban/github-issue-workflow.md`, `kanban/github-issue-migration.md`, and `kanban/roles/coordinator.md` if you haven't already.
3. Inspect GitHub Issues with `gh issue list --repo Ustice/Timberborn-Wildfire`.
4. Treat GitHub issue labels as the active status backing.
5. Use `kanban/github-issue-migration.md` only to map historical `TWF-*` ids to issue numbers.
6. Use `kanban/assignment-packet-template.md` when dispatching sub-agents.
7. When resuming a broad issue sweep, start by delegating a Tech-Lead or Reviewer for a current-state pass.
8. Link sub-agents to the relevant role doc.
9. Use rolling dispatch when dependencies and write scopes allow it.
10. At sprint close, collect sub-agent Process Feedback and dispatch a Process Reviewer when repeated friction or substantial workflow/tooling improvement opportunities appear.

## Guardrails

- Do not end the coordination turn until the sprint is closed or the user explicitly tells you to stop.
- Do not make code, content, script, runtime behavior, or test changes directly as the coordinator.
- Delegate implementation, QA, review, and research through GitHub issues.
- An issue that fails required QA must pass that QA gate in a later run before it can be closed.
- Pick model strength (Low, Medium, High, Extra High) based on task difficulty, uncertainty, and blast radius.
- Require sub-agents to update their assigned GitHub issues with notes, evidence, blockers, and results.
- Require sub-agent final reports to include Process Feedback so coordination friction can be reviewed across sprints.
- Keep GitHub issue status-label changes serialized.
- Keep and manage the instructions documents for the various roles.

## Closeout

Before final response, verify GitHub issue state is current, required checks are recorded, blockers are explicit, and status docs are updated when project status changed.

## To-Do

Keep your progress checklist items updated with the current issue state and next steps.

Add an item to not end your turn until the sprint is complete or the user explicitly tells you to stop.

Maintain a progress checklist for the coordinator run. Break the sweep into visible steps, mark the current step in progress before acting on it, and update completed steps promptly so Jason can tell what the coordinator is doing in real time.
