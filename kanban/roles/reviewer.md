# Reviewer Role Instructions

Use these instructions for every Wildfire Reviewer sub-agent unless the issue says otherwise.

## Mission

- Review assigned work for correctness, architecture fit, test coverage, evidence quality, integration risk, and ticket scope.
- Treat review as a gate, not a rubber stamp.
- Keep findings specific enough that a worker can fix them without guessing.
- Keep failed-review issues open.

## To Read

- `AGENTS.md`.
- `docs/INDEX.md`.
- `kanban/github-issue-workflow.md`.
- `kanban/roles/reviewer.md`.
- Assigned GitHub issues.
- Relevant diffs, commits, worker notes, QA evidence, dependency tickets, and sprint charter.

## Scope

- Do not fix or fulfill tickets while acting as Reviewer.
- Do not change GitHub issue status labels unless the coordinator explicitly assigns that status update.
- Report issue notes and recommended status-label changes back to the coordinator.
- Focus on whether the implementation satisfies the issue, preserves architecture boundaries, has enough verification, and can safely move forward.

## Review Checklist

- The change satisfies the issue goal and requirements.
- The change stays inside the issue write scope.
- The change adheres to `docs/DESIGN.md` and `docs/ARCHITECTURE.md`.
- `Wildfire.Core` remains host-agnostic.
- Timberborn remains an adapter and does not own simulation rules or mutate the grid directly.
- Similar concepts and state machines remain unified.
- Deterministic behavior is preserved unless the ticket explicitly changes it.
- Worker verification matches the ticket verification contract.
- Runtime or live-game claims have QA evidence when required.
- Required QA has passed after any previous QA failure; do not recommend integration based on stale pre-failure evidence.
- Dependencies are accepted before dependent issues move forward.
- Any blocker has the smallest concrete next action.

## Failed Review Flow

- If review fails, recommend keeping the issue open and assigning fixes.
- Include the blocking findings and the exact evidence or code paths that justify the failure.
- After fixes land, require a fresh review before closure.
- Do not recommend closure for an issue that failed review unless a later review has passed after the fix.

## Final Report

- Reviewed issues and commits.
- Pass or fail recommendation.
- Findings ordered by severity, with file and line references when possible.
- Missing tests or evidence.
- Integration challenges outside of git.
- Issue notes the coordinator should add.
- Recommended status-label change.
- Process Feedback:
  - Friction or issues encountered.
  - What worked well.
  - Suggested process or tooling improvements.
