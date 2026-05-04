---
ticket: TWF-007
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-000
write_scope:
  - src/Wildfire.Timberborn/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
---

# TWF-007: Build Timberborn Cell Adapters

## Goal

Create Timberborn adapters that convert terrain, buildings, resources, and water into packed fire cells. The adapters should prepare initial and updated cell data for the GPU simulator without owning fire rules.

## Why

Timberborn has game-specific concepts, but the simulator only understands packed cells. This adapter layer is the translation boundary that keeps Timberborn from becoming the simulation owner.

## Requirements

- Add terrain-to-cell mapping.
- Add building-to-cell mapping.
- Add resource or vegetation-to-cell mapping where current APIs allow it.
- Add water or wetness-to-cell mapping where current APIs allow it.
- Keep mappings deterministic and documented.
- Avoid direct simulation-buffer mutation from Timberborn systems.
- Add unit tests around mapping logic when it can be isolated from Timberborn APIs.
- Record any Timberborn API unknowns or blockers in the ticket notes.

## Dependencies

- `TWF-000` fixture export can provide examples for packed cell expectations.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run live Timberborn validation only if the local environment is ready.

## Notes

- If API discovery is needed, keep that discovery small and document exact type/member names.

## Worker Notes

- Added `TimberbornFireCellMapper` plus terrain, building, resource, and water source adapters under `src/Wildfire.Timberborn`.
- The mapper groups adapter-facing sources by `FireGrid` index, deterministically applies material priority `building > resource or vegetation > terrain > empty`, applies water/wetness as suppression only, clamps values to packed-cell field widths, and emits either initial packed cells or sorted `FireSimChange` records with `SetCell` populated.
- Updated `TimberbornFireSystem` so mapped cell observations are registered through `IGpuFireSimulator.RegisterChange` instead of mutating simulation or GPU buffers directly.
- Added isolated unit coverage for deterministic source ordering, initial-cell packing, sorted `SetCell` update generation, clamping, water overlay behavior, and out-of-bounds rejection.
- Updated `docs/ARCHITECTURE.md` and `docs/TEST_PLAN.md` with the mapping boundary and validation limits.
- Tech-lead rejection fix: terrain wetness now contributes through the water overlay path independently of material priority, so selected building/resource material keeps its fuel/flammability/heat-loss fields while wet terrain still sets packed water.
- Added regression coverage for wet terrain plus selected building/resource material with no explicit water source.

## Evidence

- `git diff --check`: passed.
- `dotnet test`: passed, 57 tests.
- `dotnet build Wildfire.slnx`: passed, 0 warnings and 0 errors.
- Integrated on `main` in commit `1e2339d`.
- Coordinator verification after integration: `git diff --check`, `dotnet test` with 57 tests, and sequential `dotnet build Wildfire.slnx` all passed.

## Blockers

- Live Timberborn validation was not run. The repository has no wired Timberborn mod project reference or live-game harness for this scaffold yet, so current proof is limited to deterministic .NET mapper tests plus solution build checks.
