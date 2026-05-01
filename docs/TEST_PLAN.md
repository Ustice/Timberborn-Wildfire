# Wildfire Test Plan

## Scope

Validation should prove the shared packed data model, deterministic scenario inputs, shader execution, compact delta readback, and Timberborn adapter behavior.

## Current Automated Coverage

- Packed cell round-trips and field setters.
- Burning-threshold helper behavior.
- Seeded scenario catalog coverage.
- Scenario dimension and seed overrides.
- Seeded sparse layout determinism.

Run:

```bash
dotnet test
```

## Shader Snapshot Coverage

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
