# Wildfire Design

## 1. Overview

Wildfire is a compact, stochastic cellular automata fire and heat simulation built around one authoritative GPU execution path.

The reusable core owns packed data formats and host-facing contracts. Unity and Timberborn adapters own compute-buffer setup, shader dispatch, delta readback, visuals, and gameplay integration. Timberborn provides map data, registers external changes, and responds to simulation deltas; it must not own fire rules or mutate the grid directly.

The CLI is a scenario preview and fixture-generation tool. It should not run an alternate simulation path.

## 2. Goals

- Support grids up to `256 x 256 x 32` cells.
- Use compact per-cell storage.
- Allow deterministic stochastic behavior in compute shaders.
- Support simulation development outside Timberborn through Unity and shader-facing fixtures.
- Output changed cells as compact deltas so Timberborn updates remain minimal.
- Allow entities to register changes at any time while the sim mutates only during controlled dispatch ticks.
- Ensure each cell is processed at most once per update cycle when frontier optimization is enabled.
- Allow visual effects to be driven directly from GPU simulation output.

## 3. Non-Goals

- Do not model full fluid dynamics.
- Do not model continuous heat, fuel, or water values.
- Do not require one Unity or Timberborn entity per visual fire/smoke/ash cell.
- Do not rely on Timberborn being open for simulation development.
- Do not make the grid itself delta-only; the simulator keeps full cell buffers internally.
- Do not maintain a second C# execution path for fire spread.

## 4. Project Structure

The implementation should use Wildfire project names while preserving fire-simulation API names inside the code.

```text
Wildfire/
  src/
    Wildfire.Core/
      PackedCell.cs
      FireGrid.cs
      FireSimContracts.cs
      FireRandom.cs
    Wildfire.Unity/
      FireSim.compute
      FireVisuals.compute
      UnityComputeFireSimulator.cs
      ComputeBufferGrid.cs
      FireVisualRenderer.cs
    Wildfire.Cli/
      Program.cs
      ScenarioCatalog.cs
      CliOptions.cs
    Wildfire.Timberborn/
      TimberbornFireSystem.cs
      TimberbornTerrainAdapter.cs
      TimberbornBuildingAdapter.cs
      TimberbornWaterAdapter.cs
      TimberbornOverlayAdapter.cs
      TimberbornFireEffectsAdapter.cs
  tests/
    Wildfire.Core.Tests/
```

## 5. Cell Model

Each simulation cell is stored as a packed 16-bit value. Compute buffers may use `uint` values for alignment, with the lower 16 bits containing the packed cell.

```text
ushort cell:
bits 0-3:   fuel          0-15
bits 4-7:   heat          0-15
bits 8-9:   flammability  0-3
bits 10-11: water         0-3
bit  12:    terrain       0-1
bits 13-15: heatLoss      0-7
```

| Property | Range | Meaning |
| --- | --- | --- |
| fuel | 0-15 | Remaining burnable material. |
| heat | 0-15 | Local thermal energy band. |
| flammability | 0-3 | How easily the cell ignites. |
| water | 0-3 | Local wetness / suppression level. |
| terrain | 0-1 | Whether the cell represents solid terrain/building material. |
| heatLoss | 0-7 | How quickly the cell loses heat. |

The following values are derived, not stored:

- Burning state from fuel, heat, flammability, water, and terrain.
- Flame visual intensity from heat and burning state.
- Smoke visual intensity from burning state, fuel, and heat.
- Ash visual intensity from low/no fuel and heat history approximation.

## 6. Core Contracts

The host-facing simulation contract is explicitly GPU-oriented. Shared contracts live in `Wildfire.Core`; concrete execution lives in Unity or Timberborn adapter code.

```csharp
public interface IGpuFireSimulator
{
    int Width { get; }
    int Height { get; }
    int Depth { get; }
    void RegisterChange(FireSimChange change);
    GpuFireStepResult Tick();
    IDisposable Subscribe(IFireSimListener listener);
}

public readonly record struct GpuFireStepResult(
    IReadOnlyList<CellDelta> Deltas,
    uint Tick
);

public readonly record struct CellDelta(
    int CellIndex,
    ushort OldCell,
    ushort NewCell
);

public interface IFireSimListener
{
    void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas);
}
```

## 7. External Change Registration

Entities may register simulation changes at any time, but changes are not applied immediately. They are uploaded and applied at the start of the next dispatch tick.

```csharp
public readonly record struct FireSimChange(
    int CellIndex,
    ushort? SetCell = null,
    byte? AddHeat = null,
    byte? AddFuel = null,
    byte? SetWater = null,
    byte? SetFuel = null,
    byte? SetHeat = null,
    byte? SetFlammability = null,
    byte? SetHeatLoss = null,
    byte? SetTerrain = null
);
```

Rule:

Nothing mutates simulation buffers immediately except the simulator dispatch itself. This prevents recursive update chains and keeps behavior deterministic.

## 8. GPU Tick Pipeline

Each tick follows this pipeline:

1. Increment tick/generation.
2. Upload queued external changes.
3. Apply external changes pass.
4. Build candidate frontier pass.
5. Run fire simulation pass.
6. Append changed cells to the delta buffer.
7. Append next active cells.
8. Generate visual fields.
9. Read back compact gameplay deltas.
10. Notify listeners.
11. Swap current/next buffers.

Initial implementation may scan the full grid for simplicity. Frontier optimization can come later without changing host contracts.

## 9. Neighbor Model

Use a 6-neighbor 3D cellular automata model:

- Left.
- Right.
- North.
- South.
- Below.
- Above.

Optional later extensions:

- Diagonal neighbors.
- Wind-biased neighbors.
- Upward smoke spread.
- Elevation-biased heat spread.

Initial behavior should stay with 6-neighbor spread for predictability.

## 10. Deterministic Stochastic Behavior

Do not use runtime-global random state during simulation steps. Use hash-based randomness keyed by:

- Cell index.
- Tick.
- Seed.

The same hash algorithm must be available to shader code and scenario-fixture tooling so seeded scenarios remain reproducible.

```hlsl
uint Hash(uint cellIndex, uint tick, uint seed)
{
    uint x = cellIndex ^ (tick * 747796405u) ^ seed;
    x ^= x >> 16;
    x *= 0x7feb352du;
    x ^= x >> 15;
    x *= 0x846ca68bu;
    x ^= x >> 16;
    return x;
}
```

## 11. Cell Step Rules

The first implementation should be intentionally simple and live in `FireSim.compute`.

Inputs:

- Current cell.
- Neighbor cells.
- Tick.
- Seed.

Outputs:

- New packed cell.
- Optional visual-field values.
- Optional delta record.

Suggested rule outline:

```hlsl
uint StepCell(uint index, uint cell, uint tick, uint seed)
{
    uint fuel = Fuel(cell);
    uint heat = Heat(cell);
    uint flammability = Flammability(cell);
    uint water = Water(cell);
    uint terrain = Terrain(cell);
    uint heatLoss = HeatLoss(cell);

    uint neighborHeat = AverageNeighborHeat(index);
    uint burningNeighborCount = CountBurningNeighbors(index);

    heat = ((heat * 3u) + neighborHeat) / 4u;
    heat += burningNeighborCount;

    if (water > 0u)
    {
        heat = heat > water ? heat - water : 0u;
        if (heat > 8u)
        {
            water -= 1u;
        }
    }

    bool canBurn = terrain == 1u && fuel > 0u;
    uint ignitionThreshold = 12u - flammability + water;
    if (canBurn && heat >= ignitionThreshold)
    {
        uint roll = Hash(index, tick, seed) & 15u;
        uint burnChance = clamp(heat + flammability - water, 0u, 15u);
        if (roll < burnChance)
        {
            fuel = fuel > 0u ? fuel - 1u : 0u;
            heat = min(15u, heat + 2u + flammability);
        }
    }

    heat = heat > 1u + (heatLoss / 3u) ? heat - 1u - (heatLoss / 3u) : 0u;
    return Pack(fuel, heat, flammability, water, terrain, heatLoss);
}
```

The exact constants should be tuned from shader snapshots and visual validation.

## 12. Frontier And Deduplication

Most of the simulation grid is expected to be inactive.

The first shader may dispatch over the full grid. A later optimized shader should use active frontier buffers and generation stamps.

A cell should be considered a candidate if it is:

- Changed last tick.
- Externally changed this tick.
- Still active from the previous tick.
- A neighbor of any of the above.

Use atomic generation stamps to admit each candidate once per tick.

```hlsl
RWStructuredBuffer<uint> QueuedGeneration;
AppendStructuredBuffer<uint> NextActive;

void EnqueueOnce(uint index, uint generation)
{
    uint oldValue;
    InterlockedCompareExchange(
        QueuedGeneration[index],
        generation,
        generation - 1,
        oldValue);

    if (oldValue != generation)
    {
        NextActive.Append(index);
    }
}
```

Newly activated cells should be queued for the next tick, not processed immediately during the current tick.

## 13. Delta Output

Each tick emits only changed cells.

```csharp
public readonly record struct CellDelta(
    int CellIndex,
    ushort OldCell,
    ushort NewCell
);
```

Deltas are used for:

- Timberborn overlay updates.
- Building damage/destruction checks.
- Terrain state changes.
- Compact gameplay readback.

The grid remains full-state internally.

## 14. Listener Notifications

Listeners receive deltas after a tick completes.

Listeners may register new changes, but those changes apply on the next tick.

```csharp
public void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas)
{
    foreach (var delta in deltas)
    {
        if (BuildingShouldCollapse(delta))
        {
            _fireSim.RegisterChange(new FireSimChange(
                CellIndex: delta.CellIndex,
                SetFuel: 0
            ));
        }
    }
}
```

Rule:

Listeners do not directly mutate simulation state. They enqueue changes for the next tick.

## 15. Scenario Preview CLI

The CLI exists to inspect seeded input grids without launching Timberborn. It should not execute fire spread.

### Requirements

- Build named seeded scenarios.
- Display one Z layer at a time through `--layer`.
- Use terminal characters and colors derived from packed cell values.
- Support dimensions and seed overrides.
- Support scenario names that map cleanly to shader fixtures.

### Command

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --layer=0
```

## 16. Unity Compute Simulator

The Unity compute simulator should use the packed cell format and HLSL rules.

### Buffers

```text
CurrentCells:       StructuredBuffer<uint>
NextCells:          RWStructuredBuffer<uint>
CurrentActive:      StructuredBuffer<uint>
NextActive:         AppendStructuredBuffer<uint>
Deltas:             AppendStructuredBuffer<CellDeltaGpu>
QueuedGeneration:   RWStructuredBuffer<uint>
VisualField:        RWTexture2DArray<float4>
```

GPU cell values use lower 16 bits:

```hlsl
uint cell = packed ushort in bits 0-15
```

### GPU Delta Record

```hlsl
struct CellDeltaGpu
{
    uint Index;
    uint OldCell;
    uint NewCell;
};
```

## 17. GPU Visual Pipeline

The compute shader can drive visuals directly.

It should not directly notify gameplay entities.

```text
FireSim.compute
  reads current cells
  writes next cells
  writes deltas
  writes visual field
        |
FireVisuals.compute or material shader
  reads visual field
        |
Overlay / smoke / fire rendering
```

Use a `Texture2DArray` for cell-layer visuals.

Suggested channels:

- R: fire intensity.
- G: smoke intensity.
- B: ash intensity.
- A: heat intensity or visibility.

Rule:

Visual effects can be GPU-driven. Gameplay/entity changes remain C#-driven through deltas.

## 18. Timberborn Integration

Timberborn should be an adapter around the GPU simulator.

### Timberborn Responsibilities

- Convert terrain/buildings/resources into packed fire cells.
- Register external changes from gameplay events.
- Run the sim on a fixed cadence.
- Consume compact deltas.
- Update overlays/effects only where needed.
- Apply gameplay consequences from deltas.

### Timberborn Should Not

- Own the fire rules.
- Mutate the fire grid directly.
- Create one game entity per fire/smoke/ash cell.
- Require the game to run for shader and scenario-fixture development.

### Example Integration Flow

```text
Building / terrain / water system
  registers FireSimChange
        |
GPU fire sim dispatch
  applies changes
  computes new state
  emits CellDelta list
        |
Timberborn listeners
  update overlays
  damage buildings
  spawn pooled effects
  update alerts
```

## 19. Timberborn Entity/Event Examples

### Building Receives Heat

```csharp
_fireSim.RegisterChange(new FireSimChange(
    CellIndex: buildingCellIndex,
    AddHeat: 4
));
```

### Water Is Dumped

```csharp
_fireSim.RegisterChange(new FireSimChange(
    CellIndex: targetCellIndex,
    SetWater: 3
));
```

### Building Burns Out

Listener checks deltas:

```csharp
if (PackedCell.Fuel(delta.OldCell) > 0 && PackedCell.Fuel(delta.NewCell) == 0)
{
    _buildingAdapter.NotifyBurnedOut(delta.CellIndex);
}
```

### Overlay Update

```csharp
if (VisualStateChanged(delta.OldCell, delta.NewCell))
{
    _overlayAdapter.UpdateCell(delta.CellIndex, delta.NewCell);
}
```

## 20. Implementation Phases

### Phase 1: Core Data Contracts

- Keep packed cell helpers.
- Keep grid indexing helpers.
- Define GPU simulator contracts and delta records.
- Keep deterministic hash helper for scenario fixtures.
- Keep scenario catalog tests.

### Phase 2: Unity Compute Prototype

- Implement `FireSim.compute` with the packed cell layout.
- Implement full-grid dispatch first.
- Emit compact delta records.
- Emit visual field output.
- Add shader snapshot fixtures.

### Phase 3: Scenario Preview And Fixtures

- Keep the CLI as a seeded input-grid preview.
- Export fixture grids for shader tests.
- Keep scenario generation deterministic.

### Phase 4: Timberborn GPU Integration

- Add Timberborn adapters.
- Convert map/building data into cells.
- Upload external changes.
- Dispatch the GPU simulator on a fixed cadence.
- Consume compact deltas.
- Update overlays/effects from deltas and visual fields.
- Validate gameplay loop.

### Phase 5: Visual And Performance Tuning

- Add active frontier optimization if profiling justifies it.
- Tune visual texture output.
- Tune gameplay delta readback.
- Add runtime diagnostics.

## 21. Testing Strategy

### Unit Tests

Test:

- Packed cell round-trips.
- Field setters.
- Ignition threshold helper behavior.
- Fire grid indexing.
- Scenario determinism.
- Contract-level validation.

### Shader Snapshot Tests

Given:

- Same seed.
- Same initial grid.
- Same tick count.

The compute shader should produce stable packed cell grids and compact deltas for each accepted scenario.

Snapshot differences should be understood, reviewed, and bounded by scenario.

### CLI Scenarios

Keep seeded scenarios for:

- Single ignition point.
- Line of fuel.
- Water barrier.
- Vertical fuel column.
- Sparse forest.
- Building cluster.
- Mixed terrain/fuel/water.

## 22. Important Design Rules

1. The sim owns mutation.

   Entities register changes; they do not directly mutate cells.

2. Ticks are the consistency boundary.

   Changes registered during notification apply on the next tick.

3. Each candidate cell processes once per optimized tick.

   Use generation-stamped deduplication when active frontier optimization is enabled.

4. Deltas are output, not storage.

   The simulator still keeps full buffers.

5. Active frontier is separate from deltas.

   Some cells remain eligible even when unchanged.

6. Randomness is deterministic.

   Use hash-based randomness, not runtime-global random state.

7. Visuals can stay GPU-side.

   Gameplay reactions must go through C# deltas.

8. Timberborn is a host.

   It adapts to the simulator; it does not own fire rules.

## 23. Open Questions

- How often should the Timberborn fire sim tick relative to game ticks?
- Should fire spread diagonally, or only through 6-neighbor adjacency?
- Should wind be part of the core sim or a modifier applied by the host?
- Should ash become a stored field later, or remain derived from fuel/heat?
- How should Timberborn buildings map to vertical cells?
- Should water represent temporary wetness, standing water, or both?
- Should heat loss be material-specific, biome-specific, or weather-driven?
- Should the first shader dispatch use full-grid evaluation before active frontier optimization?

## 24. Summary

The fire simulator should be a compact, deterministic, tick-based cellular automata system with a packed 16-bit cell format and one authoritative GPU execution path. It should expose a clean change-registration and delta-notification API so Timberborn entities can interact with it without owning simulation state.

The GPU simulator should use the packed format, produce compact deltas, and generate GPU-side visual fields. Timberborn should consume gameplay deltas through C# while visual overlays and effects are driven directly from compute shader output.
