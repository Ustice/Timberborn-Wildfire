# Wildfire Architecture

Wildfire separates simulation authority from host integration.

## Ownership

- `Wildfire.Core` owns packed cells, deterministic rules, candidate selection, active frontier tracking, deltas, and listener notifications.
- `Wildfire.Cli` owns terminal display, test scenarios, seed/config input, and tuning workflows.
- `Wildfire.Unity` will own compute buffers, HLSL rule translation, CPU/GPU snapshot comparison, and GPU visual fields.
- `Wildfire.Timberborn` will own terrain/building/water adapters, event registration, overlay updates, pooled effects, alerts, and gameplay consequences.

## Boundary Rules

- Hosts call `RegisterChange`; they do not mutate cells directly.
- Listeners may register changes, but those changes apply on the next tick.
- `Wildfire.Core` does not reference Unity or Timberborn.
- GPU visuals may be driven by simulation buffers, but gameplay changes flow through C# deltas.
- CPU backend remains the debug and fallback authority until GPU parity is proven.

## Data Flow

```text
Host map/events
  -> FireSimChange queue
  -> FireSim tick
  -> packed cells and CellDelta list
  -> host listeners
  -> overlays, effects, damage, alerts
```

## First Implementation State

The repository currently has a CPU core scaffold with:

- `PackedCell`
- `FireRandom`
- `FireRules`
- `CpuFireSimulator`
- `IFireSimulator`
- `CellDelta`
- `FireSimChange`
- `IFireSimListener`

Unity and Timberborn projects are placeholders with adapter-facing names so Phase 3 and Phase 4 can grow without moving the core boundary.
