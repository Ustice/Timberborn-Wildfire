# Wildfire Handoff

## Current State

- Wildfire repository scaffolded in `~/repos/wildfire`.
- Core packed-cell, grid, GPU simulator contract, delta, listener, and change-registration types exist.
- CLI scenario preview exists with named seeded scenarios, seed/dimension/layer options, and packed-cell-derived rendering.
- Unity and Timberborn projects are placeholders; they should remain host adapters.
- The old alternate C# execution path and its snapshot tests were removed; future simulation work should target the GPU simulator path only.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- The fresh compute-first ticket set exists as `TWF-000` through `TWF-015`.
- `kanban/by-status` is the sprint source of truth.
- As of this handoff, no tickets are assigned yet.
- `TWF-000` and `TWF-013` have no ticket dependencies and are the first candidates for `02-ready`.
- Later tickets are dependency-gated behind fixture export, Unity compute buffers, shader dispatch, compact delta readback, Timberborn adapters, and QA utility setup.

## Next Exact Action

Start with:

- Move dependency-ready tickets into `kanban/by-status/02-ready/`.
- Dispatch `TWF-000` first for CLI fixture export.
- Dispatch `TWF-013` when Timberborn UI access is available for QA coordinate capture.

## Known Gaps

- GPU compute simulator is not implemented.
- Timberborn adapters are not implemented.
- No live Timberborn build/deploy pipeline exists yet.
