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
- Stale `TWF-017` and `TWF-027` worktrees were inspected and removed. `TWF-017` contained older already-integrated startup-harness work, and `TWF-027` contained the contaminated screenshot already rejected in ticket notes.
- The deploy pipeline now emits Timberborn-facing `netstandard2.1` assemblies and has live `Player.log` proof that Timberborn discovers `Wildfire (v0.1.0.0)` and loads the `Wildfire testing` save without the earlier `.NET 10` `System.Linq` crash.
- Timberborn now loads the private `wildfire_compute_mac` bundle as a real `ComputeShader`, initializes the compute-backed simulator, and live `status` reports `simulator_integrated=true` with dimensions `128x128x23`.
- Live Timberborn evidence now includes real compute dispatch/readback tokens such as `wildfire_timberborn_gpu_dispatch_kernel`, `wildfire_timberborn_gpu_readback_completed`, and `wildfire_timberborn_dispatch_completed`.
- Timberborn gameplay-surface Sprint 2 is complete: material mapping has deterministic terrain/resource/building/water bands, the debug overlay updates from compact deltas, pausable buildings can be paused from fuel-depleted building deltas, and the QA water-suppression command proves a queued `SetWater=3` change through live GPU dispatch.
- The old alternate C# execution path and its snapshot tests were removed; future simulation work should target the GPU simulator path only.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- The compute-first ticket set exists as `TWF-000` through `TWF-063`.
- `kanban/by-status` is the sprint source of truth.
- `TWF-000` through `TWF-008` are done and integrated on `main`.
- `TWF-009` is done and integrated on `main` in commit `aa9a260`; live status and `Player.log` evidence prove the Timberborn delta-consumer hook path is wired and reporting.
- `TWF-010` is done: runtime diagnostics now cover GPU lifecycle, queued changes, dispatch/readback timing, listener counts, and Timberborn adapter startup/shutdown without per-cell mapped-change spam.
- `TWF-011` is deferred until the new non-zero live delta sprint creates meaningful profiling evidence that full-grid dispatch is too expensive.
- `TWF-012` is done and integrated on `main` in commits `6043959`, `1feb25e`, and `499040d`.
- `TWF-013` is done with an explicit boundary: opening screens and standalone main-menu coordinates are split to `TWF-020`.
- `TWF-014` is done and integrated on `main` in commit `85e5538`.
- `TWF-015` is deferred because `TWF-030` supersedes the older load-latest-save utility shape with the fuller launch/load/unpause workflow and overlapping `scripts/**` scope.
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
- `TWF-030` is done: the signal-driven latest-save startup utility launches Timberborn, clears startup Mods and Experimental Mode from screenshot-confirmed signals, continues the latest save, recognizes the top-HUD loaded-save state, unpauses, and records `simulator_integrated=true` status plus dispatch evidence.
- `TWF-031` is done: the guarded `qa-delta-stimulus` command is implemented, unit-tested, live-verified, and recorded with non-zero GPU readback evidence.
- `TWF-032` is done by `TWF-031` evidence: live QA captured `wildfire_timberborn_gpu_readback_completed ... delta_count=2`, `wildfire_timberborn_dispatch_completed ... delta_count=2`, and follow-up `qa-readiness` with `last_delta_count=1`.
- `TWF-033` is done: the runtime binds the first Timberborn-facing consequence through the debug visual state delta-consumer sink and live evidence shows non-zero dispatch/consumer counters with persistent `last_delta_consumer_debug_visual_cells`.
- `TWF-034` is done: worker profiling evidence from the current `TWF-031`/`TWF-032` live artifact shows full-grid dispatch is acceptable on the observed `128x128x23` loaded save, so `TWF-011` should remain deferred for now.
- `TWF-035` is done and integrated on `main` in commit `9c22c83`: Timberborn material mapping now covers deterministic terrain, stockpile resource, vegetation, wood-like building, non-burnable building, wet-cell, and vertical footprint behavior.
- `TWF-036` is done and integrated on `main` in commit `7e7cee8`: compact fuel-depleted building deltas can pause matched unpaused `PausableBuilding` instances through a bounded Timberborn-owned consequence lane, with live QA proof from `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-036-retry-20260502T190222Z`.
- `TWF-037` is done and integrated on `main` in commit `5db2490`: the debug inspection overlay stores latest packed cell state from compact changed cells, exposes updated-cell counters, and has live QA proof with `debug_visual_updated_cells` above zero.
- `TWF-038` is done and integrated on `main` in commit `00e66bb`: the QA-only `qa-water-suppression-stimulus` command queues a bounded `SetWater=3` change, rejects arguments, exposes durable positive water-change evidence, and has live QA proof from `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-038-live-retry-20260502T194053Z`.
- `TWF-039` is done and integrated on `main` in commit `2a7f013`: the live compute simulator now binds the GPU `VisualFields` buffer to a Timberborn-resolvable visual-field surface singleton, exposes bounded inspection/consumer APIs, reports QA telemetry, and has live proof from `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-039-live-final-20260502T204610Z`.
- `TWF-040` is done and integrated on `main` in commit `808d88d`: compact-delta visual events now feed a bounded pooled Timberborn presentation layer using the `TWF-039` visual-field surface, expose active/updated/last-nonzero counters, resolve the native `CampfireFire` effect in live QA, and have proof from `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-040-live-final-20260502T211935Z`.
- `TWF-041` is done and integrated on `main` in commits `3d1b910` and `942251c`: fire/smoke/ash/visibility visual constants are tuned in the C# mirror and HLSL shader, accepted shader checksum scenarios are recorded for `single-ignition` and `line-of-fuel`, ash remains an explicit derived heat/fuel approximation without packed-cell storage changes, and live QA proof is under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-qa-20260502T215231Z`.
- `TWF-042` is done and integrated on `main` in commit `fde36e6`: compact-delta alert decisions now feed one bounded Timberborn quick-warning player alert, QA telemetry reports alert counters, and live QA proof under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-042-live-rerun-20260502T222801Z` shows `Wildfire alert: 1 new fire. Max heat 15.` with matching status counters.
- `TWF-043` and `TWF-044` are ready for Sprint 4: game-feel tuning and release-blocking simulation decisions can start in parallel with separate write scopes.
- `TWF-045` and `TWF-046` remain todo for Sprint 4: release scenario snapshots wait on `TWF-043` and `TWF-044`; coherent live-loop validation waits on `TWF-043` and `TWF-045`.
- `TWF-047` through `TWF-051` are todo for Sprint 5: save/reload disable/re-enable validation, release settings, compatibility probes, GPU/asset failure hardening, and the active-frontier release decision.
- `TWF-052` through `TWF-063` are todo for Sprint 6: GitHub CI/CD, release packaging, release graphics/metadata, player docs, changelog/versioning, cross-platform bundle validation, clean-install release-candidate QA, release debug-surface scrub, license/attribution, support templates, and Steam Workshop distribution as the official first channel.
- QA now owns live Timberborn builds, deployments, launches, restarts, and the shared deploy/QA lock for verification runs.

## Next Exact Action

Continue with:

- Start Sprint 4 with `TWF-043` and `TWF-044`: tune fire game-feel constants while resolving release-blocking simulation decisions. Keep `TWF-045` waiting until both land.
- Keep `TWF-015` deferred unless `TWF-030` is abandoned; it is superseded by the newer startup/load/unpause utility.
- Keep `TWF-011` deferred. `TWF-034` worker profiling over `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z/Player.log` observed ticks `1..43`, `43` full-grid dispatches, median dispatch wrapper time `2.737 ms`, p95 `4.846 ms`, max `6.014 ms`, and `SimulateFullGrid` kernel timing median `0.019 ms`, p95 `0.039 ms`, max `0.057 ms`. The non-zero stimulus ticks were `30:2@5.937 ms`, `31:1@4.308 ms`, `32:1@1.86 ms`, `33:1@3.055 ms`, and `34:1@4.178 ms`, with consumer counters reaching `max_changed_cells=2`, `max_visual_effect_events=2`, `max_gameplay_consequences=1`, and `max_alerts=1`.
- Use `bun scripts/summarize-dispatch-profile.ts "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-031-live-20260502T143543Z"` to regenerate the TWF-034 timing summary from the preserved artifact.
- Use `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` against a loaded, unpaused Timberborn save when you need to confirm the in-game Wildfire command bridge, loaded-game runtime, simulator integration, and advancing dispatch tick are alive.
- Use `bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick` followed by `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed` when you need to re-prove the suppression-registration path.
- Use `bun run typecheck` before accepting TypeScript script changes.
- If shader behavior changes, use the opt-in Unity harness command documented in `docs/TEST_PLAN.md` to prove real execution before accepting the change.

## Known Gaps

- `FireSim.compute` has local Unity batchmode proof, but CI does not run it unless Unity Editor licensing and compute-shader capable graphics are available.
- Historical TWF-009 evidence had `delta_count=0`; TWF-031 now proves the live stimulus, GPU readback, dispatch, and status path can report non-zero deltas.
- The player-legibility sprint is complete: GPU visual-field binding, bounded pooled native presentation, visual tuning evidence, and one player-facing alert/status surface are integrated and live-verified.
- TWF-030's live run proves the guarded startup/load/unpause workflow, but the broader Timberborn UI is not exhaustively mapped; add settings/new-game/map-editor/deeper Mods coordinates only through explicit QA passes.
- `TWF-009` intentionally provides Timberborn adapter hook surfaces; concrete binding to Timberborn damage/effect/alert services is follow-up work.
