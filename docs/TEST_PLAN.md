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
- Timberborn deploy pipeline scaffold: Bun/TypeScript deploy script, generated Wildfire manifest, managed assembly staging into `~/Documents/Timberborn/Mods/Wildfire/Scripts`, local build/deploy lock, dry-run/help output, and running-game guard for real deploy/remove.
- Timberborn fixed-cadence dispatch scaffold: adapter initialization from mapped cells through an injected GPU simulator factory, external change registration through `IGpuFireSimulator.RegisterChange`, centralized cadence options, one dispatch per processed game update, compact-delta return/subscription surface, command-bridge status fields, and lifecycle log tokens for attach/init/change/wait/dispatch/readback/failure events.

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

Current `TWF-012` and `TWF-019` coverage adds an in-process command bridge plus a narrow Timberborn game-context file binding. QA can invoke read-only `status` or `help` from a loaded game by writing one command to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt`, or by running:

```bash
bun scripts/invoke-timberborn-command.ts status
```

The Timberborn adapter polls that inbox from `TimberbornQaCommandFileBridge`, forwards the command to `TimberbornQaCommandBridge`, deletes the inbox, and writes the latest result to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-outbox.txt`. The script and bridge expose only known read-only commands. Unknown manual inbox commands are rejected by the bridge and logged as failures rather than executed.

Current `TWF-008` coverage adds a Timberborn game-context runtime singleton for fixed-cadence dispatch. `TimberbornFireRuntime` is the command-bridge state provider, so `status` reports `simulator_integrated=true`, dimensions, `tick_count`, `queued_changes`, and `last_delta_count` after a simulator is attached. When no simulator factory has been attached by the live host yet, those simulator fields intentionally return `placeholder`.

Search `~/Library/Logs/Mechanistry/Timberborn/Player.log` for `wildfire_command_bridge_ready`, `wildfire_command_request`, `wildfire_command_result`, `wildfire_timberborn_runtime_ready`, `wildfire_timberborn_cadence_configured`, `wildfire_timberborn_dispatch_started`, and `wildfire_timberborn_dispatch_completed`.

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
```

`Scripts/` contains the managed assemblies, following the official Timberborn mod builder's code-output convention. The script only stages known build artifacts from `src/Wildfire.Timberborn/bin/<Configuration>/netstandard2.1/`; it does not copy `docs/`, `kanban/`, `.git/`, or other internal repository content into the deployed mod.

Run the real deploy only when Timberborn is closed or QA explicitly approves writing while the game is open:

```bash
bun scripts/deploy-timberborn-mod.ts --apply
```

Optional command flags:

- `--configuration=Release` builds and stages Release artifacts.
- `--skip-build` reuses existing build output.
- `--mods-dir=/path/to/Mods` targets a non-default Timberborn Mods directory.
- `--dry-run --remove` prints the cleanup action for the deployed Wildfire folder without deleting it.

Cleanup is intentionally manual for live QA safety. Close Timberborn first, then inspect the dry-run remove output. Only remove `~/Documents/Timberborn/Mods/Wildfire` when QA no longer needs the deployed mod evidence.

Player.log proof remains a live-QA step after a real deploy. Capture `~/Library/Logs/Mechanistry/Timberborn/Player.log` evidence that Timberborn discovered the Wildfire folder or loaded `Wildfire.Timberborn.dll`; if Timberborn cannot load the assembly yet, record the exact loader error and keep the fix in `Wildfire.Timberborn` or the deploy script rather than moving fire rules into `Wildfire.Core`.
