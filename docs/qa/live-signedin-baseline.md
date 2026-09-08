# Signed-in live copied-save baseline

Actual Timberborn game test completed 2026-09-07 evening local time after the user signed into Steam and opened the game. The sole controller attached existing PID4490; no duplicate launch, deployment or Unity Editor execution occurred. Game version1.1.2.4-52e959e-xsm, Unity6000.5.5f1, Metal Apple M2 Pro. Startup now explicitly logged successful Steam connection; this is separate from the earlier unauthenticated startup failures.

Evidence directory: `/tmp/wildfire-live-signedin-baseline`. Actual screenshots `loaded-world.png` and `reloaded-world.png`, full log `Player-baseline-save-reload.log`, full deployment fingerprint and `verified-save-and-deployment.json` are retained there.

## Build and mod selection

At attach, the game was at its Mods dialog with Wildfire unchecked. The controller enabled the already deployed Wildfire and preserved the existing Camera Bookmarks, Hats and Wildfire Asset Preview selections. The game then logged all four mods active. No unrelated mod selection was changed.

The deployed Wildfire v0.1.0.0 DLL SHA-256 is `f64462a2f39b56f0018b9aaccfbf1afe479bc752c2c5085d1a0cd64d17120b48`, matching the earlier staged baseline evidence associated with9cd4aea. It is **not** the current integration branch. Native/Core/compute bundles were not replaced during this run. Current source's OWNED4,33-layer and new worker implementations are not claimed live by this baseline.

## Copied-save lifecycle

The selected settlement was `Wildfire Goal QA 2026-09-07`, save `Baseline source copy`. Before loading, its bytes and original asset-review source both matched SHA-256 `5bc969e4c38a6221eba9a9496a8d9be6ebd909c91b4cdda40e043236bc5c89d6`.

Actual first load completed in8501ms. Wildfire loaded its compute/effects/visual assets, initialized50×50×23 (57,500 cells), bound visual and consequence consumers, and logged `wildfire_timberborn_runtime_initialize_completed status=ready`. Screenshot confirms the world and original preview gear are visible.

The controller created a new save `QA baseline lifecycle 2026-09-07.timber` in the same QA settlement, without overwriting the source copy. Native save completed in0.25s; Wildfire logged `persistence_saved initialization_state=Ready preserved_original=false`. The new save SHA-256 is `654f7042a8af7c5aa53e23d030bf39454be22ea0780599470b91f564995d0411`.

Reloading that exact new save completed in7291ms. Logs confirmed version1 saved state, fire-simulator state present,1512 saved burn-damage entries, restoration of tick0/57,500 cells, and runtime ready again. No exception/error appeared in this game log. Original source and original QA-copy hashes remained unchanged afterward.

## Limits and next live work

This is a paused asset-review settlement with zero beavers. No forced fire stimulus, inventory mutation, worker assignment, or new owned-runtime activation occurred. Tick0 and saved registration entries do not prove nonzero damage persistence or queued-input/GPU history preservation. The visible equipment remains preview assets; no fitted character equipment or current Warden workflow is established.

Next live tests need freshly disposable healthy Folktails and Ironteeth settlements, a separately coordinated exact current build deployment, then supported existing QA diagnostics/stimuli under explicit development access where needed. The new owned runtime remains unbound; no version1-to-owned fallback or migration is inferred. Controller remains `/root/baseline_audit`, attached to the paused QA world, and must serialize any game shutdown/build/deploy/restart with the shared lock.
