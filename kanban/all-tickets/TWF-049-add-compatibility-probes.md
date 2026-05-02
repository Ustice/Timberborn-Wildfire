---
ticket: TWF-049
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-046
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/ARCHITECTURE.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-049-add-compatibility-probes.md
---

# TWF-049: Add Compatibility Probes

## Goal

Centralize Timberborn API compatibility checks and log clear degraded-mode evidence when required services or members are unavailable.

## Why

Release will sit on version-sensitive Timberborn APIs for terrain, buildings, effects, alerts, settings, and asset loading. Compatibility logic should be explicit and searchable, not scattered across gameplay code.

## Requirements

- Identify the Timberborn services, components, and asset-loading paths Wildfire depends on for release.
- Add narrow compatibility probes for version-sensitive or reflection-backed access.
- Prefer public APIs and game-owned services where available.
- Log which probe passed, which fallback was used, and whether the feature is degraded.
- Keep fire rules out of compatibility code.
- Add deterministic tests for probe result handling where possible.
- Update docs with the compatibility boundary and QA log tokens.

## Dependencies

- `TWF-046` establishes the full feature surface that needs compatibility coverage.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live `Player.log` probe tokens from a loaded save.

## Notes

- Reflection is allowed only behind this kind of narrow probe layer when no stable public surface exists.
