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

## Unity Compute Buffer Scaffold

`Wildfire.Unity` owns `ComputeBufferGrid`, the first GPU-side allocation boundary. The grid records width, height, depth, and checked cell count, then allocates named buffers for current cells, next cells, queued changes, delta output, generation state, and visual fields.

Initial fixture-style `ushort` packed cells are uploaded to current and next cell buffers as `uint` values. The packed cell payload remains in the lower 16 bits, leaving the upper bits available for future GPU-side bookkeeping without changing the core `PackedCell` contract.

The plain solution build does not reference UnityEngine APIs yet. Buffer allocation therefore flows through `IComputeBufferAllocator` and `IComputeBufferHandle`, so Unity can later provide real compute-buffer handles while tests use deterministic recording handles. Fire-spread rules are still owned by future compute shaders, not by the C# scaffold.
