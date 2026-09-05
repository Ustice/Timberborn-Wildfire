# Coordinator

Use the shared rules in [AGENTS.md](../../AGENTS.md) and the [issue workflow](../github-issue-workflow.md). The coordinator owns the combined outcome, not a mandatory ceremony.

## Coordinate Useful Work

- Agree on the task boundary and completion condition. Break substantial work into independent scopes; delegate where this enables useful concurrent progress.
- Give each agent an objective, write ownership, branch/worktree, verification expectations, and reporting owner. Use the [assignment template](../assignment-packet-template.md) as needed.
- Continue investigation, implementation, review, and integration in an unclaimed scope. Coordinate any shared-file changes before editing and serialize integration.
- Keep compressed `CONTEXT.md` state for allocations, decisions, evidence, and next actions. Report meaningful progress and blockers; avoid copying every tool run into issues or checklists.
- Use one live controller for Timberborn deployment, launch/restart, and input. Parallel agents can prepare fixtures, inspect code, or analyze evidence without competing for the session.
- Distinguish an actual prerequisite from an integration-order preference. Keep useful work moving while dependent decisions are resolved.

## Review And Closeout

Inspect agent results and the integrated changes. Use independent review when risk or uncertainty warrants it, and fix small integration problems directly within agreed ownership. Substantial independent changes can be delegated again. Verify after integration where combining work could change behavior.

An assignment names who updates issues; the coordinator is the default owner. Record accepted results and explicit remaining work. Do not close issues or claim task completion while required gates remain failed or unverified. Report the exact next action for blockers.

Commit coherent progress and preserve unrelated changes. Clean up worktrees only after their work is preserved and no agent still owns active changes. When paused, stop new dispatches and collect commits, validation state, and next actions into a concise handoff.

Discuss recurring process friction when there is evidence and a useful remedy. A separate retrospective, Process Reviewer, or structured feedback report is optional.
