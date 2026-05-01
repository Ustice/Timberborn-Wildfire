# Wildfire Architecture

Wildfire separates simulation authority from host integration. There is one authoritative GPU simulation path.

## Ownership

- `Wildfire.Core` owns packed cells, grid helpers, deterministic fixture hashing, change records, delta records, and GPU simulator contracts.
- `Wildfire.Cli` owns seeded scenario preview and fixture input inspection.
- `Wildfire.Unity` owns compute buffers, HLSL rule translation, shader dispatch, compact delta readback, and GPU visual fields.
- `Wildfire.Timberborn` owns terrain/building/water adapters, event registration, overlay updates, pooled effects, alerts, and gameplay consequences.

## Boundary Rules

- Hosts call `RegisterChange`; they do not mutate cells directly.
- Listeners may register changes, but those changes apply on the next tick.
- `Wildfire.Core` does not reference Unity or Timberborn.
- GPU visuals may be driven by simulation buffers, but gameplay changes flow through C# deltas.
- Fire spread rules live in compute shaders, not in a second C# execution path.

## Data Flow

```text
Host map/events
  -> FireSimChange queue
  -> GPU fire sim dispatch
  -> packed cells, visual fields, and CellDelta list
  -> host listeners
  -> overlays, effects, damage, alerts
```

## First Implementation State

The repository currently has a core scaffold with:

- `PackedCell`
- `FireGrid`
- `FireRandom`
- `IGpuFireSimulator`
- `GpuFireStepResult`
- `CellDelta`
- `FireSimChange`
- `IFireSimListener`

Unity and Timberborn projects are placeholders with adapter-facing names so compute and host integration can grow without moving the core boundary.
