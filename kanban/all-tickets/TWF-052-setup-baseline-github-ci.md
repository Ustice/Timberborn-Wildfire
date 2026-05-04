---
ticket: TWF-052
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-051
write_scope:
  - .github/**
  - scripts/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-052-setup-baseline-github-ci.md
---

# TWF-052: Set Up Baseline GitHub CI

## Goal

Add a GitHub Actions CI workflow that runs the release-relevant checks available on a normal hosted runner.

## Why

Release needs repeatable checks before packaging. The real Unity shader harness may remain local or opt-in because it needs Unity licensing and graphics access, but the standard build, tests, TypeScript checks, and package dry-run should run automatically.

## Requirements

- Add a GitHub Actions workflow for pull requests and pushes.
- Install and use `bun`, not `npm`.
- Run `dotnet test`.
- Run `bun run typecheck`.
- Run deploy or packaging dry-run checks when they can run without Timberborn or Unity licensing.
- Document which shader or live Timberborn checks remain local/QA-only.
- Cache dependencies only when it keeps the workflow simple and reliable.

## Dependencies

- `TWF-051` settles the release performance scope before CI becomes a release gate.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `dotnet test`.
- Validate the workflow syntax locally if a suitable tool is available.

## Notes

- Do not try to make live Timberborn QA run in hosted CI.
