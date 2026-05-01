# Wildfire Handoff

## Current State

- Wildfire repository scaffolded in `~/repos/wildfire`.
- Core packed-cell, grid, GPU simulator contract, delta, listener, and change-registration types exist.
- CLI scenario preview exists with named seeded scenarios, seed/dimension/layer options, and packed-cell-derived rendering.
- Unity now has compute-buffer, shader-dispatch, change-upload, compact-delta, visual-field, and snapshot-harness scaffolds; it should remain the GPU adapter.
- Unity now has an opt-in batchmode shader execution harness that imports `FireSim.compute`, dispatches a seeded fixture, reads packed cells, compact deltas, and a visual checksum.
- Timberborn now has deterministic cell-mapping adapter scaffolds; it should remain a host adapter.
- Timberborn QA now has an in-process command bridge and a live Timberborn file binding with read-only `status` and `help` commands, searchable result tokens, unit coverage for the command safety boundary, and live `Player.log` proof from a loaded save.
- Timberborn menu coordinate capture now covers startup Mods, post-startup load, exit-to-main confirmation, standalone main menu, main-menu Load Game, in-game Escape, in-game Load Game, Mods dialog, and HUD targets at 1920 x 1080, with screenshot evidence in `docs/reference/screenshots/timberborn-menu-coordinate-guide/`.
- The deploy pipeline now emits Timberborn-facing `netstandard2.1` assemblies and has live `Player.log` proof that Timberborn discovers `Wildfire (v0.1.0.0)` and loads the `Wildfire testing` save without the earlier `.NET 10` `System.Linq` crash.
- The old alternate C# execution path and its snapshot tests were removed; future simulation work should target the GPU simulator path only.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- The fresh compute-first ticket set exists as `TWF-000` through `TWF-022`.
- `kanban/by-status` is the sprint source of truth.
- `TWF-000` through `TWF-007` are done and integrated on `main`.
- `TWF-012` is done and integrated on `main` in commits `6043959`, `1feb25e`, and `499040d`.
- `TWF-013` is done with an explicit boundary: opening screens and standalone main-menu coordinates are split to `TWF-020`.
- `TWF-008` has useful fixed-cadence runtime scaffolding integrated on `main` in commits `b5eb8fe` and `e98e932`, but it is blocked until the deployed Timberborn mod can load the compute-backed simulator.
- `TWF-016` is done: deploy works, copies `netstandard2.1` assemblies, and `Player.log` shows `Wildfire (v0.1.0.0)` after restart.
- `TWF-020` is done: startup Mods, post-load, exit confirmation, standalone main menu, and main-menu Load Game coordinates are captured.
- `TWF-014` is done and integrated on `main` in commit `85e5538`.
- `TWF-018` is done and integrated on `main` in commit `5b80d04`.
- `TWF-019` is done and integrated on `main` in commit `a7ed538`; live Timberborn `status` invocation returned `wildfire_command_result success=true` from the `Wildfire testing` save.
- `TWF-021` has useful compute-backed simulator factory and runtime initializer scaffolding integrated on `main` in commit `ce1951f`, but it is blocked because Unity rejects the generated `wildfire_compute_mac` bundle during manual `AssetBundle.LoadFromFile`.
- `TWF-022` is ready and owns the AssetBundle compatibility fix needed to unblock `TWF-021` and then `TWF-008`.
- `TWF-015` and `TWF-017` are dependency-ready.
- `TWF-009`, `TWF-010`, and `TWF-011` remain dependency-gated behind real `TWF-008` simulator integration and diagnostics/profiling.

## Next Exact Action

Continue with:

- Dispatch `TWF-022` next if the goal is making live `status` report real simulator fields; `TWF-021` and `TWF-008` cannot close until `wildfire_compute_mac` loads inside Timberborn.
- Dispatch `TWF-017` or `TWF-015` next if the goal is durable QA automation; both have their coordinate/deploy prerequisites ready.
- Use `bun scripts/invoke-timberborn-command.ts status --wait=6` against a loaded Timberborn save when you need to confirm the in-game Wildfire command bridge is alive.
- If shader behavior changes, use the opt-in Unity harness command documented in `docs/TEST_PLAN.md` to prove real execution before accepting the change.

## Known Gaps

- `FireSim.compute` has local Unity batchmode proof, but CI does not run it unless Unity Editor licensing and compute-shader capable graphics are available.
- The Timberborn command bridge currently reports placeholder simulator fields until `TWF-022` fixes compute AssetBundle loading, `TWF-021` attaches the compute-backed simulator, and `TWF-008` can finish live fixed-cadence validation.
