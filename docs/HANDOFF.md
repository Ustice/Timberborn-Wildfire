# Wildfire Handoff

## Current State

- Wildfire repository scaffolded in `~/repos/wildfire`.
- Core packed-cell, grid, GPU simulator contract, delta, listener, and change-registration types exist.
- CLI scenario preview exists with named seeded scenarios, seed/dimension/layer options, and packed-cell-derived rendering.
- Unity now has compute-buffer, shader-dispatch, change-upload, compact-delta, visual-field, and snapshot-harness scaffolds; it should remain the GPU adapter.
- Timberborn now has deterministic cell-mapping adapter scaffolds; it should remain a host adapter.
- Timberborn QA now has an in-process command bridge scaffold with read-only `status` and `help` commands, searchable result tokens, and unit coverage for the command safety boundary.
- Timberborn menu coordinate capture now covers the in-game Escape menu, Load Game dialog, Mods dialog, and HUD targets at 1920 x 1080, with screenshot evidence in `docs/reference/screenshots/timberborn-menu-coordinate-guide/`.
- The old alternate C# execution path and its snapshot tests were removed; future simulation work should target the GPU simulator path only.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- The fresh compute-first ticket set exists as `TWF-000` through `TWF-020`.
- `kanban/by-status` is the sprint source of truth.
- `TWF-000` through `TWF-007` are done and integrated on `main`.
- `TWF-012` is done and integrated on `main` in commits `6043959`, `1feb25e`, and `499040d`.
- `TWF-013` is done with an explicit boundary: opening screens and standalone main-menu coordinates are split to `TWF-020`.
- `TWF-008` is parked in `07-blocked` after useful scaffolding on branch `codex/TWF-008-fixed-cadence` at commit `732779729afb3f77904b93c2132e12c5d2ef587c`; live validation needs the deploy path from `TWF-016`.
- `TWF-014`, `TWF-016`, `TWF-018`, and `TWF-020` are dependency-ready.
- `TWF-009`, `TWF-010`, `TWF-011`, `TWF-015`, `TWF-017`, and `TWF-019` remain dependency-gated.

## Next Exact Action

Continue with:

- Dispatch `TWF-016` next if the goal is live Timberborn proof; it creates the build/deploy path needed before `TWF-008` or `TWF-019` can be validated in-game.
- Dispatch `TWF-014` next if the goal is QA automation ergonomics; it uses the coordinate guide from `TWF-013`.
- Dispatch `TWF-020` before `TWF-015`; startup automation needs title-screen coordinates that `TWF-013` intentionally avoided capturing from a running save.
- Dispatch `TWF-018` when the goal is proving actual shader execution rather than live Timberborn integration.

## Known Gaps

- `FireSim.compute` has not been compiled or executed by Unity/CI; the repo still lacks a Unity batchmode project, `UnityEngine.ComputeShader` dispatcher, standalone shader compiler, and GPU readback runner.
- Timberborn adapters and the QA command bridge are deterministic scaffolds only; live Timberborn API binding and live-game validation have not been run.
- No live Timberborn build/deploy pipeline exists yet; `TWF-016` owns that blocker.
- The command bridge is not yet invokable from the running game; `TWF-019` owns the narrow Timberborn binding after deploy exists.
- Opening-screen and standalone main-menu coordinates are not captured yet; `TWF-020` owns that safe title-screen pass.
