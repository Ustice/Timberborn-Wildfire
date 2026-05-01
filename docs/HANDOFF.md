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
- `TWF-001` is done and integrated on `main` in commit `2915475`.
- `TWF-002` is done and integrated on `main` in commit `14f7c82`.
- `TWF-003` is done and integrated on `main` in commit `cfe8aec`.
- `TWF-004` is done and integrated on `main` in commit `8d563f4`.
- `TWF-006` is done and integrated on `main` in commit `1844b47`.
- `TWF-005` is done and integrated on `main` in commit `c5a8254`.
- `TWF-007` is done and integrated on `main` in commit `1e2339d`.
- `TWF-013` is blocked until Timberborn UI is open at the intended QA resolution.
- Later tickets are dependency-gated behind fixture export, Unity compute buffers, shader dispatch, compact delta readback, Timberborn adapters, and QA utility setup.

## Next Exact Action

Continue with:

- No dependency-ready tickets remain except blocked `TWF-013`.
- Unblock and dispatch `TWF-013` when Timberborn UI access is available.
- After `TWF-013` is done, unblock `TWF-014`; after `TWF-014`, unblock `TWF-015`.
- After live adapter/dispatch work is ready, move `TWF-008` through `TWF-012` according to their dependencies.

## Known Gaps

- GPU compute simulator is not implemented.
- Timberborn adapters are not implemented.
- No live Timberborn build/deploy pipeline exists yet.
