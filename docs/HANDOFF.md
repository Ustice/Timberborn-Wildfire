# Wildfire Handoff

## Current State

- Wildfire repository scaffolded in `~/repos/wildfire`.
- Core packed-cell, grid, GPU simulator contract, delta, listener, and change-registration types exist.
- CLI scenario preview exists with named seeded scenarios, seed/dimension/layer options, and packed-cell-derived rendering.
- Unity now has compute-buffer, shader-dispatch, change-upload, compact-delta, visual-field, and snapshot-harness scaffolds; it should remain the GPU adapter.
- Unity now has an opt-in batchmode shader execution harness that imports `FireSim.compute`, dispatches a seeded fixture, reads packed cells, compact deltas, and a visual checksum.
- Timberborn now has deterministic cell-mapping adapter scaffolds; it should remain a host adapter.
- Timberborn QA now has an in-process command bridge and a live Timberborn file binding with read-only `status`, `qa-readiness`, and `help` commands, searchable result tokens, unit coverage for the command safety boundary, and live `Player.log` proof from a loaded save.
- Timberborn menu coordinate capture now covers startup Mods, post-startup load, exit-to-main confirmation, standalone main menu, main-menu Load Game, in-game Escape, in-game Load Game, Mods dialog, and HUD targets at 1920 x 1080, with screenshot evidence in `docs/reference/screenshots/timberborn-menu-coordinate-guide/`.
- Timberborn debug/developer panel reference now exists in `docs/timberborn-debug-panels.md`, with clean 1920 x 1080 screenshots under `docs/reference/screenshots/timberborn-debug-panels/`.
- The deploy pipeline now emits Timberborn-facing `netstandard2.1` assemblies and has live `Player.log` proof that Timberborn discovers `Wildfire (v0.1.0.0)` and loads the `Wildfire testing` save without the earlier `.NET 10` `System.Linq` crash.
- Timberborn now loads the private `wildfire_compute_mac` bundle as a real `ComputeShader`, initializes the compute-backed simulator, and live `status` reports `simulator_integrated=true` with dimensions `128x128x23`.
- Live Timberborn evidence now includes real compute dispatch/readback tokens such as `wildfire_timberborn_gpu_dispatch_kernel`, `wildfire_timberborn_gpu_readback_completed`, and `wildfire_timberborn_dispatch_completed`.
- The old alternate C# execution path and its snapshot tests were removed; future simulation work should target the GPU simulator path only.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- The fresh compute-first ticket set exists as `TWF-000` through `TWF-030`.
- `kanban/by-status` is the sprint source of truth.
- `TWF-000` through `TWF-008` are done and integrated on `main`.
- `TWF-009` is dependency-ready now that `TWF-008` has live simulator dispatch/readback evidence.
- `TWF-010` is done: runtime diagnostics now cover GPU lifecycle, queued changes, dispatch/readback timing, listener counts, and Timberborn adapter startup/shutdown without per-cell mapped-change spam.
- `TWF-011` remains todo until `TWF-009` proves consumer behavior and creates meaningful profiling/optimization evidence.
- `TWF-012` is done and integrated on `main` in commits `6043959`, `1feb25e`, and `499040d`.
- `TWF-013` is done with an explicit boundary: opening screens and standalone main-menu coordinates are split to `TWF-020`.
- `TWF-014` is done and integrated on `main` in commit `85e5538`.
- `TWF-015` is dependency-ready, but should follow `TWF-024` because it depends on trustworthy coordinate-guide evidence.
- `TWF-016` is done: deploy works, copies `netstandard2.1` assemblies, and `Player.log` shows `Wildfire (v0.1.0.0)` after restart.
- `TWF-017` is done: after an approved Timberborn restart, the startup harness passed against current-window launch evidence and a loaded-save `status` command returned `simulator_integrated=true`.
- `TWF-018` is done and integrated on `main` in commit `5b80d04`.
- `TWF-019` is done and integrated on `main` in commit `a7ed538`; live Timberborn `status` invocation returned `wildfire_command_result success=true` from the `Wildfire testing` save.
- `TWF-020` is done: startup Mods, post-load, exit confirmation, standalone main menu, and main-menu Load Game coordinates are captured.
- `TWF-021` is done: the Timberborn compute-backed simulator factory now loads and initializes in live Timberborn.
- `TWF-022` is done: the AssetBundle compatibility blocker was resolved by the Unity project module/package fix proven in `TWF-023`.
- `TWF-023` is done and integrated on `main` in commit `9859ceb`; live evidence shows `simulator_integrated=true`.
- `TWF-024` is done: the contaminated in-game running screenshot was replaced with a clean `1920 x 1080` Timberborn loaded-save baseline.
- `TWF-025` is done: the bottom-menu guide covers the loaded-save toolbar, submenus, `Alt-Shift-Z` developer mode, `Shift-Alt-X` debug entry, and composite screenshot references.
- `TWF-026` is done: Bun-based TypeScript type checking now exists through `bun run typecheck`, with a minimal script-scoped `tsconfig.json` and passing mainline evidence.
- `TWF-027` is done: the contaminated first worktree capture was rejected, clean debug/developer screenshots were captured in the main checkout, and `docs/timberborn-debug-panels.md` documents the visible panels, QA relevance, and unsafe mutation controls.
- `TWF-028` is done: current Wildfire QA cannot navigate Timberborn menus remotely without screen interaction; the accepted safer path is narrow allowlisted command-bridge outcomes, not generic menu driving.
- `TWF-029` is done: the read-only `qa-readiness` command is allowlisted, documented, unit-tested, and live-verified after deploy/restart with `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, and an advanced tick.
- `TWF-030` is dependency-ready: it should create the guarded latest-save startup and unpause utility using the now-verified startup harness, command bridge, and menu-coordinate evidence.

## Next Exact Action

Continue with:

- Dispatch `TWF-030` if the goal is reducing manual live-QA setup by launching/loading/unpausing the latest save through guarded Bun automation.
- Reconcile `TWF-015` against `TWF-030` before dispatching; `TWF-030` is the newer, fuller startup/load/unpause shape.
- Dispatch `TWF-009` if the goal is gameplay consequences on top of the now-live simulator dispatch and diagnostics.
- Use `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` against a loaded, unpaused Timberborn save when you need to confirm the in-game Wildfire command bridge, loaded-game runtime, simulator integration, and advancing dispatch tick are alive.
- Use `bun run typecheck` before accepting TypeScript script changes.
- If shader behavior changes, use the opt-in Unity harness command documented in `docs/TEST_PLAN.md` to prove real execution before accepting the change.

## Known Gaps

- `FireSim.compute` has local Unity batchmode proof, but CI does not run it unless Unity Editor licensing and compute-shader capable graphics are available.
- Current live dispatch evidence has `delta_count=0`; that proves dispatch/readback, not gameplay consequences. `TWF-009` must prove consumers and visible/gameplay effects.
- The Experimental Mode startup dialog was cleared during TWF-029 live QA, but that gate is not yet represented as a verified target in `docs/timberborn-menu-coordinate-guide.md`; TWF-030 should either handle it safely or update the guide with evidence.
