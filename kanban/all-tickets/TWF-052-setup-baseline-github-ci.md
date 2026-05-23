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
- 2026-05-23 worker: Added baseline GitHub Actions CI for PRs and pushes to `main`/`codex/**` branches. Hosted CI installs Bun 1.3.8, installs dependencies with `bun install --frozen-lockfile`, runs `bun run typecheck`, `bun run blueprints:check`, runs a temporary hosted-safe xUnit project against `Wildfire.Core`, and runs an explicit `--plan-only` deploy-plan check.
- 2026-05-23 worker: Full-solution `dotnet test`, Timberborn adapter builds/tests, Steam packaging/publishing, Unity AssetBundle generation, and live Timberborn QA remain local/QA-only because normal hosted runners do not have Timberborn managed DLLs, Steam, Unity licensing, or GUI access.
- 2026-05-23 review fix: `scripts/deploy-timberborn-mod.ts --dry-run` again validates required artifacts. CI uses the separate `--plan-only` mode when it only needs to inspect the hosted-safe deployment plan.
- 2026-05-23 fresh review passed after verifying the hosted-safe xUnit helper, explicit `--plan-only` deploy check, and normal dry-run artifact validation. Main-checkout integration checks passed `git diff --check`, `bun run typecheck`, `bun scripts/run-hosted-dotnet-tests.ts`, workflow YAML parse, isolated-home `--plan-only`, and full `dotnet test` with `479` tests. First GitHub-hosted Actions run remains future evidence.
