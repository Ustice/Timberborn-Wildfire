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

## Implementation Notes

- Start from `src/Wildfire.Timberborn/TimberbornFireRuntimeInitializer.cs`, `src/Wildfire.Timberborn/TimberbornComputeFireSimulator.cs`, `src/Wildfire.Timberborn/TimberbornGpuVisualFieldSurface.cs`, `src/Wildfire.Timberborn/TimberbornPooledFireSmokeAshEffects.cs`, `src/Wildfire.Timberborn/TimberbornPlayerFireAlerts.cs`, and `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`; those are the current Timberborn API contact points that need searchable compatibility evidence.
- Keep the probe owner in `Wildfire.Timberborn`; the core simulator should only receive ordinary contracts, not Timberborn service availability decisions.
- Prefer a small result model that can be unit-tested without Unity objects: probe id, required or optional, status, fallback or degraded mode, and log token.
- Treat missing optional visual, alert, settings, or QA surfaces as degraded when the runtime can still load safely; treat missing compute support, required shader assets, or required terrain/grid surfaces as simulator unavailable rather than fake success.
- QA evidence should include `Player.log` tokens for each probe, the loaded-save `qa-readiness` or `status` output, and the exact degraded feature list if any probe does not pass.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live `Player.log` probe tokens from a loaded save.

## Notes

- Reflection is allowed only behind this kind of narrow probe layer when no stable public surface exists.
- Worker commit `cca15c654dc7d85787a3aadc0d8b2d3c6d38b3ae` passed fresh Tech-Lead re-review after an earlier failed review. The review confirmed required probe failures gate runtime readiness, building-burnout probes are optional/degraded, compute bundle probing is honest for this ticket, and no `Wildfire.Core` rule leakage was introduced.
- Live QA passed on 2026-05-03 with evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-049-compatibility-live-20260503T051809Z`.
- Live probe summary reported `status=compatible`, `degraded=false`, `required_passed=5/5`, `optional_passed=8/8`, and `degraded_features=none`.
- Per-lane probe results were present and passed for `terrain`, `building_burnout`, `compute`, `diagnostic_assets`, `visual_effects`, and `player_alerts`.
- Runtime proof included `wildfire_timberborn_diagnostic_asset_loaded`, `wildfire_timberborn_compute_asset_loaded`, `wildfire_timberborn_gpu_factory_created`, `wildfire_timberborn_gpu_simulator_initialized`, dispatch/readback tokens, and `qa-readiness` fields `compatibility_probe_status=compatible`, `compatibility_probe_degraded=false`, `compatibility_probe_required_passed=5`, `compatibility_probe_required_total=5`, `compatibility_probe_optional_passed=8`, `compatibility_probe_optional_total=8`, and `compatibility_probe_degraded_features=none`.
- QA found no failed required probes and no Wildfire compatibility/runtime failure tokens. Generic copied-log scans still show pre-Wildfire Unity `gpath.c` assertions.
