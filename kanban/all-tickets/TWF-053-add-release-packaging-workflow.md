---
ticket: TWF-053
agent_level: High
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-050
   - TWF-052
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
   - docs/reference/timberborn-deploy-pipeline.md
   - kanban/all-tickets/TWF-053-add-release-packaging-workflow.md
---

# TWF-053: Add Release Packaging Workflow

## Goal

Create a clean release packaging workflow that produces a reviewable Wildfire mod archive.

## Why

Development deploys are useful for QA, but release needs a package that contains only shippable files: manifest, scripts, compute bundles, metadata, and supported root content. Internal docs, kanban files, source files, tests, and git metadata must stay out of the release artifact.

## Requirements

- Add a Bun/TypeScript release packaging command.
- Build Release assemblies.
- Build or validate required compute and diagnostic bundles.
- Produce a ZIP or artifact directory with the expected Timberborn mod shape.
- Exclude `docs/`, `kanban/`, `.git/`, source files, tests, and local QA artifacts.
- Validate `manifest.json`, bundle manifests, version, and required files before declaring success.
- Document the package command and output path in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-050` hardens asset failure modes.
- `TWF-052` provides baseline CI checks this workflow can later reuse.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `dotnet test`.
- Run the new package command and inspect the artifact contents.

## Notes

- Keep the package command script-owned and repeatable for agents.
