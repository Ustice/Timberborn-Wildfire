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
- `TWF-003`, `TWF-005`, and `TWF-007` are ready.
- `TWF-013` is blocked until Timberborn UI is open at the intended QA resolution.
- Later tickets are dependency-gated behind fixture export, Unity compute buffers, shader dispatch, compact delta readback, Timberborn adapters, and QA utility setup.

## Next Exact Action

Continue with:

- Dispatch `TWF-003` next for external change upload.
- `TWF-004` remains blocked on `TWF-003` and must handle append-buffer counter reset before compact delta readback is accepted.
- `TWF-005` can run in parallel with `TWF-003` only if write scopes avoid `docs/TEST_PLAN.md` conflicts.
- `TWF-007` can run in parallel if its worker avoids overlapping `docs/TEST_PLAN.md` changes.
- Unblock and dispatch `TWF-013` when Timberborn UI access is available.

## Known Gaps

- GPU compute simulator is not implemented.
- Timberborn adapters are not implemented.
- No live Timberborn build/deploy pipeline exists yet.
