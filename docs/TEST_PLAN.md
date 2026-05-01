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

Run:

```bash
dotnet test
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

Current `TWF-002` coverage proves the wrapper dispatch contract in .NET tests only. The repository does not yet contain a Unity batchmode, DirectX Shader Compiler, or shader snapshot command that can compile or execute `FireSim.compute` outside Unity.

`TWF-004` adds .NET coverage for the compact delta readback wrapper: `wildfire.deltas` is allocated through the append-buffer abstraction, its append counter is reset before dispatch, the append counter is read after dispatch, compact `CellDelta` records are decoded, and subscribed listeners are notified from the readback result. This does not prove HLSL compile/runtime behavior because the repository still has no Unity runtime, Unity batchmode project, or standalone compute-shader compiler harness.

Future GPU validation should add shader snapshot fixtures for:

- Single ignition point.
- Line of fuel.
- Water barrier.
- Vertical fuel column.
- Sparse forest.
- Building cluster.
- Mixed terrain/fuel/water.

For each fixture, record:

- Scenario name.
- Seed.
- Grid dimensions.
- Tick count.
- Final packed cell grid.
- Per-tick compact delta counts.
- Per-tick compact delta records for changed cells only, with old and new packed values.
- Evidence that the append-buffer counter is reset before each dispatch/readback cycle.
- Visual field checksum or image artifact when useful.

Snapshot differences should be reviewed scenario by scenario. Avoid broad visual-only approval for behavior changes.

## Timberborn Validation

Live Timberborn validation should start only after the GPU simulator and adapter path can:

- Upload terrain/building/water cells.
- Register external heat and water changes.
- Dispatch on a fixed cadence.
- Read compact deltas.
- Update overlays or effects from changed cells.
- Apply gameplay consequences from deltas.
