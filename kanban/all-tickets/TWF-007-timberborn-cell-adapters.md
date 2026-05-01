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
