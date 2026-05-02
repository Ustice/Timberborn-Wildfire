---
ticket: TWF-050
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-046
   - TWF-049
write_scope:
   - src/Wildfire.Timberborn/**
   - scripts/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-050-harden-gpu-asset-failure-modes.md
---

# TWF-050: Harden GPU Asset Failure Modes

## Goal

Make missing, incompatible, or failed GPU and AssetBundle paths fail safely with actionable logs instead of crashing or silently pretending the simulator works.

## Why

Wildfire depends on a real compute bundle and GPU path. Release needs clear behavior when the bundle is missing, stale, incompatible, or the GPU simulator cannot initialize.

## Requirements

- Handle missing compute bundle, invalid diagnostic bundle, incompatible AssetBundle, shader load failure, kernel lookup failure, buffer allocation failure, and dispatch/readback failure.
- Preserve the rule that release cannot fall back to a fake C# fire simulator.
- Report degraded or unavailable simulator state through status or QA readiness.
- Keep error logs concise and searchable.
- Ensure deploy scripts validate bundle manifests before staging.
- Add deterministic tests for failure classification and status reporting where possible.
- Document QA commands and expected failure tokens in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-046` proves the healthy live loop.
- `TWF-049` provides compatibility probe structure for degraded modes.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck` if TypeScript scripts change.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture one healthy path and at least one intentional failure-path evidence run without new unhandled exceptions.

## Notes

- Safe failure is acceptable. Silent fake success is not.
