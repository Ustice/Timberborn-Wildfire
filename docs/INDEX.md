# Wildfire Documentation

Use the entry point that matches the work. Source and fresh validation establish implemented behavior; design decisions explain intent; dated records preserve earlier evidence without defining current machine or backlog state.

## Current guides

- [Design](DESIGN.md): simulation model, packed formats, and gameplay boundaries.
- [Architecture](ARCHITECTURE.md): project ownership, actual execution paths, and compatibility seams.
- [Source map](source-map.md): concepts mapped to code and tests.
- [Validation](TEST_PLAN.md): portable checks, shader execution, native game QA, and evidence.
- [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues): active backlog and acceptance state.
- [Issue workflow](../kanban/github-issue-workflow.md): coordinating issue-backed work.

## Focused references

- [Ash decisions](ash-simulation-model.md) and [steam decisions](steam-simulation-model.md).
- [QA tooling](qa-tooling.md) and [Timberborn deploy pipeline](reference/timberborn-deploy-pipeline.md).
- [Versioning](release/versioning.md) and [Workshop packaging/publication](release/workshop.md).
- [Native API reference](reference/timberborn-native-api-reference.md), [API support requests](reference/native-api-support-requests.md), and [blueprint reference](reference/blueprint-reference.md).
- [Timberborn UI](reference/timberborn-ui.md), [status icon design](reference/status-icon-design-language.md), and [debug panels](timberborn-debug-panels.md).
- [Menu reference](timberborn-menu-coordinate-guide.md) and [bottom-menu reference](timberborn-bottom-menu-guide.md). Captured coordinates describe their recorded scene, not a current window.

## Design records and history

The [2026-09-04 archive](history/2026-09-04/README.md) preserves earlier design plans, release descriptions, milestone lists, handoffs, and detailed validation evidence. Their original observation dates remain intact. Archive dates do not renew old test results or authorize old procedural instructions.

Earlier file-kanban tickets and evidence manifests live on branch `archive/file-kanban-2026-05-23`; see the [kanban archive guide](../kanban/README.md). Active task state belongs in the worktree's ignored `CONTEXT.md`, with durable results moved into issues, PRs, or dated reports.
