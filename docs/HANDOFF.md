# Wildfire Handoff

## Current State

- Wildfire repository scaffolded in `~/repos/wildfire`.
- Core packed-cell, grid, GPU simulator contract, delta, listener, and change-registration types exist.
- CLI scenario preview exists with named seeded scenarios, seed/dimension/layer options, and packed-cell-derived rendering.
- Unity now has compute-buffer, shader-dispatch, change-upload, compact-delta, visual-field, and snapshot-harness scaffolds; it should remain the GPU adapter.
- Timberborn now has deterministic cell-mapping adapter scaffolds; it should remain a host adapter.
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
- Later tickets are dependency-gated behind live Timberborn UI access, QA utility setup, fixed-cadence Timberborn dispatch, delta consumers, diagnostics, and optional optimization.

## Next Exact Action

Continue with:

- No dependency-ready tickets remain except blocked `TWF-013`.
- Unblock and dispatch `TWF-013` when Timberborn UI access is available.
- After `TWF-013` is done, unblock `TWF-014`; after `TWF-014`, unblock `TWF-015`.
- `TWF-008`, `TWF-009`, `TWF-010`, `TWF-011`, and `TWF-012` remain in `01-todo` until their dependencies are ready to move.

## Known Gaps

- `FireSim.compute` has not been compiled or executed by Unity/CI; the repo still lacks a Unity batchmode project, `UnityEngine.ComputeShader` dispatcher, standalone shader compiler, and GPU readback runner.
- Timberborn adapters are deterministic scaffolds only; live Timberborn API binding and live-game validation have not been run.
- No live Timberborn build/deploy pipeline exists yet.
