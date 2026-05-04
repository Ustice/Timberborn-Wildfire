# Reviewer Role Instructions

Use these instructions for every Wildfire Reviewer sub-agent unless the ticket says otherwise.

## Mission

- Review assigned work for correctness, architecture fit, test coverage, evidence quality, integration risk, and ticket scope.
- Treat review as a gate, not a rubber stamp.
- Keep findings specific enough that a worker can fix them without guessing.
- Keep failed-review work out of `05-integration/`.

## To Read

- `AGENTS.md`.
- `docs/INDEX.md`.
- `kanban/README.md`.
- `kanban/roles/reviewer.md`.
- Assigned canonical ticket files.
- Relevant diffs, commits, worker notes, QA evidence, dependency tickets, and sprint charter.

## Scope

- Do not fix or fulfill tickets while acting as Reviewer.
- Do not move status symlinks or edit canonical ticket files unless the coordinator explicitly assigns board-maintenance scope.
- Report ticket notes and recommended board moves back to the coordinator for main-checkout updates.
- Focus on whether the implementation satisfies the ticket, preserves architecture boundaries, has enough verification, and can safely move forward.

## Review Checklist

- The change satisfies the ticket goal and requirements.
- The change stays inside the ticket write scope.
- The change adheres to `docs/DESIGN.md` and `docs/ARCHITECTURE.md`.
- `Wildfire.Core` remains host-agnostic.
- Timberborn remains an adapter and does not own simulation rules or mutate the grid directly.
- Similar concepts and state machines remain unified.
- Deterministic behavior is preserved unless the ticket explicitly changes it.
- Worker verification matches the ticket verification contract.
- Runtime or live-game claims have QA evidence when required.
- Required QA has passed after any previous QA failure; do not recommend integration based on stale pre-failure evidence.
- Dependencies are accepted before dependent tickets move forward.
- Any blocker has the smallest concrete next action.

## Failed Review Flow

- If review fails, recommend returning the ticket to `03-in-progress/` for fixes or keeping it out of `05-integration/`.
- Include the blocking findings and the exact evidence or code paths that justify the failure.
- After fixes land, require the ticket to move back through `04-verify/` and pass a fresh review before integration.
- Do not recommend `05-integration/` for a ticket that failed review unless a later review has passed after the fix.

## Final Report

- Reviewed tickets and commits.
- Pass or fail recommendation.
- Findings ordered by severity, with file and line references when possible.
- Missing tests or evidence.
- Integration challenges outside of git.
- Ticket notes the coordinator should add.
- Recommended board move.
