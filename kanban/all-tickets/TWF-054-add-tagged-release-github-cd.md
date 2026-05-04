---
ticket: TWF-054
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-052
  - TWF-053
write_scope:
  - .github/**
  - scripts/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-054-add-tagged-release-github-cd.md
---

# TWF-054: Add Tagged Release GitHub CD

## Goal

Add a GitHub Actions release workflow that packages Wildfire and attaches the artifact to a GitHub Release.

## Why

Steam Workshop is the official public distribution channel, but GitHub release artifacts are still useful as reproducible build evidence and rollback/archive material.

## Requirements

- Trigger on version tags and manual `workflow_dispatch`.
- Reuse the release packaging command from `TWF-053`.
- Upload the release package and logs as artifacts.
- Attach the package to a GitHub Release when running from a version tag.
- Keep secrets optional unless a later Steam publishing workflow requires them.
- Document tag naming and release workflow behavior.

## Dependencies

- `TWF-052` creates baseline GitHub Actions support.
- `TWF-053` creates the packaging command.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Validate workflow syntax locally if a suitable tool is available.
- Run the workflow manually in GitHub when credentials and repository access are available.

## Notes

- This ticket does not publish to Steam Workshop.
