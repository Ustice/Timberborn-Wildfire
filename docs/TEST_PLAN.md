# Wildfire Test Plan

## Scope

Validation should prove the shared packed data model, deterministic scenario inputs, shader execution, compact delta readback, and Timberborn adapter behavior.

## Current Automated Coverage

- Packed cell round-trips and field setters.
- Burning-threshold helper behavior.
- Seeded scenario catalog coverage.
- Scenario dimension and seed overrides.
- Seeded sparse layout determinism.
- CLI fixture export shape and deterministic JSON output.
- Shader snapshot harness contract: CLI fixture loading, buffer-grid creation from fixtures, stable accepted-snapshot JSON shape, actionable snapshot diffs, and explicit current execution blocker.
- Unity batchmode shader execution harness: opt-in local test loads `FireSim.compute`, dispatches a seeded `TWF-000` fixture, reads back final packed cells, compact deltas, and visual-field checksum through the existing shader snapshot harness.
- GPU visual field wrapper contract: `wildfire.visual_fields` is allocated as one `float4`-equivalent entry per packed cell, full-grid dispatch receives the visual buffer, shader source writes visual samples from post-step packed cell values, and deterministic tests cover fire, smoke, ash, and visibility derivation.
- Timberborn cell mapping scaffold: deterministic terrain/building/resource/water source folding into packed cells, sorted `SetCell` change emission, field-width clamping, water overlay behavior, and out-of-bounds source rejection.
- Timberborn QA command bridge scaffold: read-only `status` and `help` commands, simulator runtime state when available, searchable command request/result tokens, and explicit no-arbitrary-execution command dispatch.
- Timberborn deploy pipeline scaffold: Bun/TypeScript deploy script, generated Wildfire manifest, managed assembly staging into `~/Documents/Timberborn/Mods/Wildfire/Scripts`, private FireSim and diagnostic AssetBundle staging into `~/Documents/Timberborn/Mods/Wildfire/ComputeShaders`, local build/deploy lock, dry-run/help output, and running-game guard for real deploy/remove.
- Timberborn fixed-cadence dispatch scaffold: adapter initialization from mapped cells through an injected GPU simulator factory, external change registration through `IGpuFireSimulator.RegisterChange`, centralized cadence options, one dispatch per processed game update, compact-delta return/subscription surface, command-bridge status fields, and lifecycle log tokens for attach/init/change/wait/dispatch/readback/failure events.
- Timberborn delta-consumer scaffold: `TimberbornFireSystem.Tick()` passes compact `CellDelta` records into a Timberborn-owned C# consumer that classifies changed-cell state, updates debug visual state by cell index, routes visual-effect, gameplay-consequence, and alert hook records to adapter sinks, emits `wildfire_timberborn_delta_consumer_completed`, and exposes last-consumer counters through the QA status token.
- Timberborn compute-backed simulator factory: live adapter loads `wildfire_compute_mac` from the deployed AssetBundle, creates Unity `ComputeBuffer` resources, dispatches `ApplyExternalChanges` and `SimulateFullGrid`, reads compact deltas, and initializes `TimberbornFireRuntime` from real terrain sources supplied by `MapSize` and `ITerrainService`.
- Runtime diagnostics: Unity and Timberborn GPU paths emit concise `wildfire_*` tokens for simulator initialization/disposal, queued change batches, dispatch kernel start/completion with elapsed milliseconds, compact delta readback counts, listener notification counts, and adapter startup/shutdown without logging per-cell changes.

Run:

```bash
dotnet test
```

Run the shader snapshot harness slice:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
```

Run the real Unity compute-shader execution harness locally:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled
```

## CLI Fixture Export

Use the CLI fixture exporter when shader tests need deterministic packed-cell inputs without launching Timberborn:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=mixed-terrain --seed=42 --width=32 --height=18 --depth=3 --layer=0 --export-fixture=artifacts/mixed-terrain.fixture.json
```

The JSON fixture contains:

- `formatVersion`.
- Scenario name and seed.
- Grid `width`, `height`, and `depth`.
- Selected layer `index`, flat `offset`, and `cellCount`.
- Packed cell value metadata with `valueType: "uint16"` and index order `x + y * width + z * width * height`.
- Full-grid packed cell `values` in flat index order.

Fixture files are deterministic for the same scenario, seed, dimensions, and layer. Shader harnesses should load the JSON, upload the `values` array as the initial packed grid, and use the selected layer metadata only as the preview or snapshot slice.

## Shader Snapshot Coverage

Current `TWF-002` coverage proves the wrapper dispatch contract in .NET tests only. `TWF-006` adds the fixture-driven snapshot harness shape in `Wildfire.Unity`: it reads CLI fixture JSON, creates a `ComputeBufferGrid` from the fixture cells, defines the accepted snapshot JSON shape, compares final packed grids, per-tick compact deltas, and optional visual checksums, and exposes an `IShaderSnapshotExecutor` boundary.

The harness command validates shape, comparison, blocker handling, and Unity executor failure surfacing:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
```

`TWF-018` adds `UnityBatchmodeShaderSnapshotExecutor`, which launches `src/Wildfire.Unity/UnityBatchmodeProject` in Unity batchmode. The Unity Editor runner copies the repository `FireSim.compute` into the temporary project asset area, imports it as a real `ComputeShader`, dispatches `SimulateFullGrid` for a seeded `TWF-000` fixture, reads the append-buffer delta counter and records, reads final packed cells, reads visual fields, and writes snapshot JSON back through the existing `ShaderSnapshotHarness`.

The real shader execution test is opt-in because it requires a local Unity Editor installation, licensing, and compute-shader capable graphics access. CI should keep running the normal .NET harness tests unless the runner image explicitly provides Unity and a graphics device. Use `WILDFIRE_UNITY_EXECUTABLE` when Unity is not installed at the default macOS Hub path.

`TWF-004` adds .NET coverage for the compact delta readback wrapper: `wildfire.deltas` is allocated through the append-buffer abstraction, its append counter is reset before dispatch, the append counter is read after dispatch, compact `CellDelta` records are decoded, and subscribed listeners are notified from the readback result. Those wrapper tests remain contract-only; use the TWF-018 Unity batchmode harness for HLSL compile/runtime proof.

`TWF-005` adds .NET coverage for the visual-field data path only: the visual field is a `float4`-equivalent buffer handle, dispatch records carry it to the compute boundary, and shader source writes the visual sample from packed cell output. The TWF-018 Unity batchmode harness proves shader visual-field readback via checksum, but rendered pixels, GPU texture binding, and material sampling still need later visual validation.

Future GPU validation should add accepted shader snapshot fixtures for:

- Single ignition point.
- Line of fuel.
- Water barrier.
- Vertical fuel column.
- Sparse forest.
- Building cluster.
- Mixed terrain/fuel/water.

For each accepted snapshot, record:

- Scenario name.
- Seed.
- Grid dimensions.
- Tick count.
- Final packed cell grid.
- Per-tick compact delta counts.
- Per-tick compact delta records for changed cells only, with old and new packed values.
- Evidence that the append-buffer counter is reset before each dispatch/readback cycle.
- Visual field checksum or image artifact when useful.

Update snapshots intentionally only after reviewing the diff scenario by scenario. Regenerate the CLI fixture, run the shader snapshot command, inspect final packed-cell differences and per-tick delta differences, and commit the changed accepted snapshot JSON with the rule or shader change that justifies it. Avoid broad visual-only approval for behavior changes.

## Timberborn Validation

Live Timberborn validation should start only after the GPU simulator and adapter path can:

- Upload terrain/building/water cells.
- Register external heat and water changes.
- Dispatch on a fixed cadence.
- Read compact deltas.
- Update overlays or effects from changed cells.
- Apply gameplay consequences from deltas.

Current `TWF-007` coverage proves the mapper contract in .NET tests only. It does not prove live Timberborn API binding, map-service discovery, terrain-height queries, building footprint extraction, vegetation/resource component lookup, water-depth sampling, mod loading, or in-game dispatch because the repository still has no Timberborn mod project reference or live-game harness wired to this adapter scaffold.

Current `TWF-012` and `TWF-019` coverage adds an in-process command bridge plus a narrow Timberborn game-context file binding. QA can invoke read-only `status`, `qa-readiness`, or `help` from a loaded game by writing one command to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt`, or by running:

```bash
bun scripts/invoke-timberborn-command.ts status
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
```

The Timberborn adapter polls that inbox from `TimberbornQaCommandFileBridge`, forwards the command to `TimberbornQaCommandBridge`, deletes the inbox, and writes the latest result to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-outbox.txt`. The script and bridge expose only known read-only commands. Unknown manual inbox commands are rejected by the bridge and logged as failures rather than executed.

Current `TWF-008` coverage adds a Timberborn game-context runtime singleton for fixed-cadence dispatch. `TimberbornFireRuntime` is the command-bridge state provider, so `status` and `qa-readiness` report `bridge_alive=true`, `runtime_loaded`, `loaded_game_ready`, `simulator_integrated`, dimensions, `tick_count`, `queued_changes`, and `last_delta_count` after a simulator is attached. When no simulator factory has been attached by the live host yet, simulator fields intentionally return `placeholder` and `loaded_game_ready=false`.

Current `TWF-009` coverage adds the first Timberborn delta-consumer path. Each dispatched `GpuFireStepResult` is consumed in C#, not in the compute shader, and updates adapter-side debug visual state only for cells whose compact deltas changed fuel, heat, water, or burning state. The same pure decision layer now routes hook records for visual-effect updates, gameplay consequences, and alerts so later Timberborn services can bind real game behavior without taking ownership of fire rules or mutating the core grid. Live QA should search for `wildfire_timberborn_delta_consumer_completed` and verify `last_delta_consumer_changed_cells`, `last_delta_consumer_debug_visual_cells`, `last_delta_consumer_started_burning`, `last_delta_consumer_fuel_depleted`, `last_delta_consumer_visual_effect_events`, `last_delta_consumer_gameplay_consequences`, and `last_delta_consumer_alerts` in `status` or `qa-readiness` results. A valid loaded-save run may still report zero changed cells until the save or QA setup produces external heat/fuel changes.

`qa-readiness` is intentionally a loaded-game readiness probe, not a UI automation command. It does not navigate menus, click Timberborn UI, load saves, delete saves, invoke arbitrary `VisualElement` callbacks, mutate the Wildfire grid, or trigger debug/destructive actions. Treat `success=true` as "the command was handled safely"; treat `loaded_game_ready=true` plus numeric dimensions and tick fields as the loaded-game readiness signal. For live QA that needs to prove fixed-cadence dispatch is advancing, unpause the loaded save first and add `--require-advanced-tick`; the command script then fails unless the result includes `tick_count` greater than `0`.

Current `TWF-021` coverage adds the live compute-backed attachment path. `TimberbornFireRuntimeInitializer` builds the initial `FireGrid` from `MapSize.TerrainSize`, converts terrain cells from `ITerrainService.GetAllHeightsInCell(...)` through `TimberbornTerrainAdapter`, and initializes the runtime through `ITimberbornFireSimulatorFactory`. The factory manually loads `ComputeShaders/wildfire_compute_mac`, creates a real Unity `ComputeShader` simulator, and leaves fire-spread behavior in `FireSim.compute`.

Current `TWF-023` coverage resolves the previous live blocker. The Wildfire batchmode builder already matched the official `~/repos/timberborn-modding/Assets/Tools/Editor/Scripts/ModBuilding/AssetBundleBuilder.cs` call shape: explicit `AssetBundleBuild[]`, `BuildAssetBundleOptions.None`, and `BuildTarget.StandaloneOSX`. The missing compatibility requirement was the Unity built-in package `com.unity.modules.assetbundle` in the minimal batchmode project's `Packages/manifest.json`. Without that package, Unity emitted `'AssetBundle' is not supported because the module AssetBundle is disabled in the build.` and Timberborn rejected the generated bundle. With the package present, the warning is gone and Timberborn loads the bundle.

The deploy script now builds and stages two private bundles: `wildfire_compute_mac` containing `Assets/WildfireGenerated/FireSim.compute`, and `wildfire_diagnostic_mac` containing `Assets/WildfireGenerated/Diagnostic.txt`. The Timberborn loader probes the diagnostic text bundle before loading the compute bundle, so live logs separate "all Wildfire bundles fail" from "ComputeShader bundles fail" without replacing or faking simulator behavior.

Live evidence from 2026-05-01 after `bun scripts/deploy-timberborn-mod.ts --apply --clean`, restarting Timberborn, and continuing the Wildfire save:

- `Player.log` showed `wildfire_timberborn_diagnostic_asset_loaded bundle=wildfire_diagnostic_mac asset=assets/wildfiregenerated/diagnostic.txt text_length=33`.
- `Player.log` showed `wildfire_timberborn_compute_asset_loaded bundle=wildfire_compute_mac asset=assets/wildfiregenerated/firesim.compute`.
- `Player.log` showed `wildfire_timberborn_gpu_simulator_created width=128 height=128 depth=23 cell_count=376832` and `wildfire_timberborn_runtime_simulator_initialized width=128 height=128 depth=23`.
- After unpausing, `Player.log` showed `wildfire_timberborn_gpu_dispatch_kernel_started kernel=SimulateFullGrid tick=20 groups=16x16x6`, `wildfire_timberborn_gpu_readback_completed tick=20 delta_count=0`, and `wildfire_timberborn_dispatch_completed tick=20 delta_count=0`.
- `bun scripts/invoke-timberborn-command.ts status --wait=6` returned `wildfire_command_result command=status success=true status=success simulator_integrated=true width=128 height=128 depth=23 tick_count=20 queued_changes=0 last_delta_count=0 message=ok`.
- For `TWF-029`, `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` returned `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=178 queued_changes=0 last_delta_count=0 message=loaded_game_ready` after the loaded save was unpaused.

Search `~/Library/Logs/Mechanistry/Timberborn/Player.log` for `wildfire_command_bridge_ready`, `wildfire_command_request`, `wildfire_command_result`, `wildfire_timberborn_adapter_started`, `wildfire_timberborn_runtime_ready`, `wildfire_timberborn_runtime_initialize_started`, `wildfire_timberborn_diagnostic_asset_loaded`, `wildfire_timberborn_compute_asset_loaded`, `wildfire_timberborn_gpu_factory_created`, `wildfire_timberborn_gpu_simulator_initialized`, `wildfire_timberborn_runtime_simulator_initialized`, `wildfire_timberborn_cadence_configured`, `wildfire_timberborn_gpu_queued_changes`, `wildfire_timberborn_gpu_dispatch_kernel_started`, `wildfire_timberborn_gpu_dispatch_kernel_completed`, `wildfire_timberborn_gpu_readback_counter`, `wildfire_timberborn_gpu_readback_completed`, `wildfire_timberborn_gpu_listeners_notified`, `wildfire_timberborn_delta_consumer_completed`, `wildfire_timberborn_dispatch_started`, `wildfire_timberborn_dispatch_completed`, `wildfire_timberborn_adapter_stopping`, and `wildfire_timberborn_adapter_stopped`.

Do not satisfy this stage by attaching a dispatch-only or C# no-op simulator. Live completion requires `wildfire_command_result ... simulator_integrated=true` with numeric dimensions, `tick_count`, `queued_changes`, `last_delta_count`, and delta-consumer counters, plus Player.log evidence that the AssetBundle loaded and the real compute dispatch/readback path ran.

## Timberborn Startup Log Harness

Use the startup log harness when QA needs repeatable evidence that the deployed Wildfire mod loaded in Timberborn:

```bash
bun scripts/check-timberborn-startup.ts --attach --wait=30
```

Use `--launch` instead of `--attach` when Timberborn should be opened by bundle id:

```bash
bun scripts/check-timberborn-startup.ts --launch --wait=120
```

The harness serializes with deploy work through the shared QA lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, validates the documented `1920x1080` display resolution by default, captures a `Player.log` baseline before attach or launch work, activates `com.mechanistry.timberborn`, waits for required current-window `Player.log` tokens, and writes evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/<timestamp>/`.

Default required startup tokens are:

- `wildfire_command_bridge_ready`.
- `wildfire_timberborn_runtime_ready`.
- `wildfire_timberborn_diagnostic_asset_loaded`.
- `wildfire_timberborn_compute_asset_loaded`.
- `wildfire_timberborn_gpu_factory_created`.
- `wildfire_timberborn_runtime_simulator_initialized`.

Use `--require-command-status` only when a save is already loaded and the command bridge is expected to answer a read-only `status` request with `success=true` and `simulator_integrated=true`. Failure tokens after the `Player.log` baseline fail the run even when all success tokens are present. The startup harness does not click through the startup Mods dialog, load saves, unpause the simulation, or replace the live gameplay validation owned by later QA tickets. Screenshots are captured only on failure by default, or when explicitly requested with `--screenshot=always`.

## Timberborn QA Utilities

Use the local [Timberborn QA Utility skill](../.codex/skills/timberborn-qa-utility/SKILL.md) when building Bun/TypeScript scripts or guarded `cliclick`-style automation for live Timberborn QA.

UI automation must take coordinate targets from [timberborn-menu-coordinate-guide.md](timberborn-menu-coordinate-guide.md), verify the target app and expected screen before acting, and fail loudly rather than clicking through an unknown Timberborn state.

## Timberborn Deploy Pipeline

Use the deploy script to build and stage the Timberborn adapter without making Timberborn own simulation rules:

```bash
bun scripts/deploy-timberborn-mod.ts
```

The default mode is a dry-run. It acquires the shared build/deploy lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, runs `dotnet build Wildfire.slnx --configuration Debug`, and prints `wildfire-deploy` lines for the mod id, version, target directory, manifest, and each planned assembly copy.

Expected deployed folder shape:

```text
~/Documents/Timberborn/Mods/Wildfire/
  manifest.json
  Scripts/
    Wildfire.Timberborn.dll
    Wildfire.Core.dll
    Wildfire.Timberborn.pdb
    Wildfire.Core.pdb
  ComputeShaders/
    wildfire_compute_mac
    wildfire_compute_mac.manifest
    wildfire_diagnostic_mac
    wildfire_diagnostic_mac.manifest
```

`Scripts/` contains the managed assemblies, following the official Timberborn mod builder's code-output convention. `ComputeShaders/` contains the Unity-built compute shader bundle generated from `src/Wildfire.Unity/FireSim.compute` and a text-only diagnostic bundle generated by `DiagnosticTextAssetBundleBuilder`; both are intentionally outside Timberborn's built-in `AssetBundles/` auto-load folder so the adapter can load exact private paths after startup. The script only stages known build artifacts from `src/Wildfire.Timberborn/bin/<Configuration>/netstandard2.1/` and known bundles from the Unity batchmode project; it does not copy `docs/`, `kanban/`, `.git/`, or other internal repository content into the deployed mod.

The deploy script must validate generated bundle manifests before copying them. A valid compute bundle manifest includes `Assets/WildfireGenerated/FireSim.compute`; a valid diagnostic bundle manifest includes `Assets/WildfireGenerated/Diagnostic.txt`. If either manifest describes another mod's assets or a stale wrong bundle, treat the deploy as invalid and rebuild before running live QA.

Run the real deploy only when Timberborn is closed or QA explicitly approves writing while the game is open:

```bash
bun scripts/deploy-timberborn-mod.ts --apply
```

Optional command flags:

- `--configuration=Release` builds and stages Release artifacts.
- `--skip-build` reuses existing build output.
- `--skip-asset-bundle` reuses existing `wildfire_compute_mac` and `wildfire_diagnostic_mac` bundles instead of running Unity batchmode.
- `--unity-executable=/path/to/Unity` selects a Unity Editor when `WILDFIRE_UNITY_EXECUTABLE` is not set.
- `--mods-dir=/path/to/Mods` targets a non-default Timberborn Mods directory.
- `--dry-run --remove` prints the cleanup action for the deployed Wildfire folder without deleting it.

Cleanup is intentionally manual for live QA safety. Close Timberborn first, then inspect the dry-run remove output. Only remove `~/Documents/Timberborn/Mods/Wildfire` when QA no longer needs the deployed mod evidence.

Player.log proof remains a live-QA step after a real deploy. Capture `~/Library/Logs/Mechanistry/Timberborn/Player.log` evidence that Timberborn discovered the Wildfire folder or loaded `Wildfire.Timberborn.dll`; if Timberborn cannot load the assembly yet, record the exact loader error and keep the fix in `Wildfire.Timberborn` or the deploy script rather than moving fire rules into `Wildfire.Core`.
