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
- GPU visual field wrapper contract: `wildfire.visual_fields` is allocated as one `float4`-equivalent entry per packed cell, full-grid dispatch receives the visual buffer, shader source writes visual samples from post-step packed cell values, and deterministic tests cover fire, smoke, ash, and visibility derivation.
- Timberborn cell mapping scaffold: deterministic terrain/building/resource/water source folding into packed cells, sorted `SetCell` change emission, field-width clamping, water overlay behavior, and out-of-bounds source rejection.
- Timberborn QA command bridge scaffold: read-only `status` and `help` commands, placeholder simulator state before TWF-008 integration, searchable command request/result tokens, and explicit no-arbitrary-execution command dispatch.
- Timberborn deploy pipeline scaffold: Bun/TypeScript deploy script, generated Wildfire manifest, managed assembly staging into `~/Documents/Timberborn/Mods/Wildfire/Scripts`, local build/deploy lock, dry-run/help output, and running-game guard for real deploy/remove.

Run:

```bash
dotnet test
```

Run the shader snapshot harness slice:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
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

Current `TWF-002` coverage proves the wrapper dispatch contract in .NET tests only. `TWF-006` adds the fixture-driven snapshot harness shape in `Wildfire.Unity`: it reads CLI fixture JSON, creates a `ComputeBufferGrid` from the fixture cells, defines the accepted snapshot JSON shape, compares final packed grids, per-tick compact deltas, and optional visual checksums, and exposes an `IShaderSnapshotExecutor` boundary for the future real GPU runner.

The harness command currently validates shape and blocker handling:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
```

Shader execution remains blocked in this repository because there is no Unity batchmode project, `UnityEngine.ComputeShader` dispatcher, or standalone compute-shader compiler/readback runner. Do not add C# fire-spread parity rules to fill that gap. Enable execution by implementing `IShaderSnapshotExecutor` with a real Unity or compiler-backed GPU dispatcher, then point the harness at accepted fixture snapshots.

`TWF-004` adds .NET coverage for the compact delta readback wrapper: `wildfire.deltas` is allocated through the append-buffer abstraction, its append counter is reset before dispatch, the append counter is read after dispatch, compact `CellDelta` records are decoded, and subscribed listeners are notified from the readback result. This does not prove HLSL compile/runtime behavior because the repository still has no Unity runtime, Unity batchmode project, or standalone compute-shader compiler harness.

`TWF-005` adds .NET coverage for the visual-field data path only: the visual field is a `float4`-equivalent buffer handle, dispatch records carry it to the compute boundary, and shader source writes the visual sample from packed cell output. This does not prove rendered pixels, HLSL compilation, GPU texture binding, or visual readback because the repository still has no Unity runtime, Unity batchmode project, `UnityEngine.ComputeShader` dispatcher, or standalone compute-shader compiler/readback runner.

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

Current `TWF-012` coverage adds an in-process command bridge scaffold only. QA or a future Timberborn binding can construct `TimberbornQaCommandBridge` and call `Execute("status")` or `Execute("help")`; both return a `TimberbornQaCommandResult` whose `ResultToken` includes searchable `wildfire_command_result`, `tick_count`, `queued_changes`, and `last_delta_count` fields. Blank command text intentionally normalizes to `status`; null command text intentionally returns a logged failure token without querying simulator state. Until TWF-008 is integrated, those simulator fields intentionally return `placeholder`.

Live in-game command validation remains blocked because the repository still has no Timberborn UI, console, file-polling, or HTTP binding that forwards game-side input to `TimberbornQaCommandBridge.Execute`. The smallest unblock is a narrow Timberborn binding that exposes only the bridge's known commands, logs `wildfire_command_request` and `wildfire_command_result`, and does not expose arbitrary code execution.

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

`Scripts/` contains the managed assemblies, following the official Timberborn mod builder's code-output convention. The script only stages known build artifacts from `src/Wildfire.Timberborn/bin/<Configuration>/net10.0/`; it does not copy `docs/`, `kanban/`, `.git/`, or other internal repository content into the deployed mod.

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
