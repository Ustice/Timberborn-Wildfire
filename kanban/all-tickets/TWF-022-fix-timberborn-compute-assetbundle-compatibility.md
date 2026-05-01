---
ticket: TWF-022
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-021
write_scope:
   - src/Wildfire.Timberborn/**
   - src/Wildfire.Unity/**
   - scripts/deploy-timberborn-mod.ts
   - docs/TEST_PLAN.md
---

# TWF-022: Fix Timberborn Compute AssetBundle Compatibility

## Goal

Make the deployed `wildfire_compute_mac` compute shader bundle load successfully inside Timberborn so `TimberbornComputeFireSimulatorFactory` can attach the real compute-backed simulator.

## Why

`TWF-021` proved the live adapter reaches the manual compute bundle load path, but Unity rejects the generated bundle with an incompatibility error even though both Timberborn and the builder report Unity `6000.3.6f1`. This blocks `TWF-021`, `TWF-008`, and all downstream live simulator validation.

## Requirements

- Investigate why Timberborn rejects `ComputeShaders/wildfire_compute_mac` despite matching Unity version strings.
- Compare the generated Wildfire bundle against installed working Timberborn mod bundles.
- Fix the AssetBundle build target, build options, asset import metadata, naming, compression, or packaging path as evidence indicates.
- Keep the bundle outside Timberborn's built-in `AssetBundles/` auto-load folder unless evidence shows that auto-load is required and safe.
- Do not replace the compute shader with a no-op, dispatch-only, or duplicate C# simulator.
- Preserve the private manual load path in `TimberbornComputeFireSimulatorFactory` unless a better Timberborn-native resource path is proven.
- Document the accepted packaging rule in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-021` supplies the private compute bundle deploy path and live failure evidence.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run the Unity shader harness affected by bundle changes.
- Run `bun scripts/deploy-timberborn-mod.ts --apply --clean`.
- Restart Timberborn, click through startup screens, load or continue the Wildfire save, then run `bun scripts/invoke-timberborn-command.ts status --wait=6`.
- Capture `Player.log` evidence for `wildfire_timberborn_compute_asset_loaded`, runtime simulator initialization, dispatch/readback tokens, and `wildfire_command_result ... simulator_integrated=true`.

## Notes

- Known failure from `TWF-021`: `The AssetBundle 'wildfire_compute_mac' could not be loaded because it is not compatible with this newer version of the Unity runtime. Rebuild the AssetBundle to fix this error.`
- Player.log and the Unity builder both reported `6000.3.6f1`, so do not assume the editor version is wrong without new evidence.
- Worker update 2026-05-01:
   - Tried an explicit AssetBundle build map with `ChunkBasedCompression`, `ForceRebuildAssetBundle`, `StrictMode`, and `StandaloneBuildSubtarget.Player`.
   - Live `bun scripts/invoke-timberborn-command.ts status --wait=6` still returned `simulator_integrated=false`.
   - Player.log changed from the compatibility error to `The AssetBundle 'wildfire_compute_mac' can't be loaded because another AssetBundle with the same files is already loaded.` after `wildfire_timberborn_compute_asset_load_started`.
   - This suggests the generated bundle is now compatible enough to pass the original version check, but the loaded game has a duplicate bundle/file identity conflict. The next pass should unload stale bundles on initialization failure or make the built bundle identity/path unique across redeploys before attempting another manual load.
- Worker update 2026-05-01 follow-up:
   - Found the deployed `~/Documents/Timberborn/Mods/Wildfire/ComputeShaders/wildfire_compute_mac.manifest` described Mod Settings UI assets, not `Assets/WildfireGenerated/FireSim.compute`; the deployed bundle was `16K` and did not match the current Unity build output SHA. This explains the duplicate-loaded-files error as a stale or wrong deployed bundle, not as successful FireSim compatibility.
   - Added deploy validation so `bun scripts/deploy-timberborn-mod.ts --apply --clean` refuses a compute bundle whose manifest does not contain `Assets/WildfireGenerated/FireSim.compute`.
   - Added runtime loader guardrails: scan already-loaded bundles for a real `FireSim.compute`, reuse only that matching bundle, and unload only bundles the Wildfire loader owns on failure/dispose.
   - Retried the Unity builder with the official-style `BuildPipeline.BuildAssetBundles(output, buildMap, BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX)` overload. Clean deploy copied matching source/target bundle SHA `cb824a5bdfed742f014790f9d6c7b9a2250be8c7d0f01eaea5f1764f63a4809f`; the deployed manifest contains only `Assets/WildfireGenerated/FireSim.compute`.
   - Live Timberborn still reports `simulator_integrated=false`; `Player.log` shows `wildfire_timberborn_compute_asset_loaded_bundle_scan count=4`, then `The AssetBundle 'wildfire_compute_mac' could not be loaded because it is not compatible with this newer version of the Unity runtime. Rebuild the AssetBundle to fix this error.`
   - Unity build log still emits `'AssetBundle' is not supported because the module AssetBundle is disabled in the build.` and `'AssetBundleManifest' is not supported because the module AssetBundle is disabled in the build.` even after trying the official-style overload. This is now the smallest remaining blocker.
   - Split follow-up: `TWF-023` should prove a Timberborn-loadable compute shader bundle from the official modding pipeline or identify the required Unity project/module setting. Do not fake simulator success.
