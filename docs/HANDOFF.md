# Wildfire Handoff

## Current State

- Wildfire repository scaffolded in `~/repos/wildfire`.
- Core packed-cell, grid, GPU simulator contract, delta, listener, and change-registration types exist.
- CLI scenario preview exists with named seeded scenarios, seed/dimension/layer options, and packed-cell-derived rendering.
- Unity now has compute-buffer, shader-dispatch, change-upload, compact-delta, visual-field, and snapshot-harness scaffolds; it should remain the GPU adapter.
- Unity now has an opt-in batchmode shader execution harness that imports `FireSim.compute`, dispatches a seeded fixture, reads packed cells, compact deltas, and a visual checksum.
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
- `TWF-016` has a dry-run deploy pipeline integrated on `main` in commit `a2407f7`, but live deploy evidence is blocked until Timberborn can be closed or deploy-while-open is explicitly approved.
- `TWF-020` is blocked on a safe title-screen state and working screenshot capture.
- `TWF-014` is done and integrated on `main` in commit `85e5538`.
- `TWF-018` is done and integrated on `main` in commit `5b80d04`.
- `TWF-009`, `TWF-010`, `TWF-011`, `TWF-015`, `TWF-017`, and `TWF-019` remain dependency-gated.

## Next Exact Action

Continue with:

- Unblock `TWF-016` by closing Timberborn, running `bun scripts/deploy-timberborn-mod.ts --apply`, launching Timberborn, and checking `Player.log` for Wildfire folder or assembly discovery.
- Dispatch `TWF-014` next if the goal is QA automation ergonomics; it uses the coordinate guide from `TWF-013`.
- Unblock `TWF-020` with a safe title-screen state and working screenshot capture before `TWF-015`; startup automation needs title-screen coordinates that `TWF-013` intentionally avoided capturing from a running save.
- If shader behavior changes, use the opt-in Unity harness command documented in `docs/TEST_PLAN.md` to prove real execution before accepting the change.

## Known Gaps

- `FireSim.compute` has local Unity batchmode proof, but CI does not run it unless Unity Editor licensing and compute-shader capable graphics are available.
- Timberborn adapters and the QA command bridge are deterministic scaffolds only; live Timberborn API binding and live-game validation have not been run.
- A dry-run Timberborn build/deploy pipeline exists, but real deploy and `Player.log` proof are blocked while Timberborn is open; `TWF-016` owns that live proof.
- The command bridge is not yet invokable from the running game; `TWF-019` owns the narrow Timberborn binding after deploy exists.
- Opening-screen and standalone main-menu coordinates are not captured yet; `TWF-020` owns that safe title-screen pass.
