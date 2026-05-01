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
- `TWF-000` is done and integrated on `main` in commit `1dadc50`.
- `TWF-001` is in progress for Unity compute buffer scaffolding.
- `TWF-007` is ready.
- `TWF-013` is blocked until Timberborn UI is open at the intended QA resolution.
- Later tickets are dependency-gated behind fixture export, Unity compute buffers, shader dispatch, compact delta readback, Timberborn adapters, and QA utility setup.

## Next Exact Action

Continue with:

- Reconcile `TWF-001` when the worker reports back.
- Dispatch `TWF-007` only if a parallel Timberborn adapter worker can stay out of `docs/ARCHITECTURE.md` while `TWF-001` is active, or after `TWF-001` lands.
- Unblock and dispatch `TWF-013` when Timberborn UI access is available.

## Known Gaps

- GPU compute simulator is not implemented.
- Timberborn adapters are not implemented.
- No live Timberborn build/deploy pipeline exists yet.
