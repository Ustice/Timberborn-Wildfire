# Wildfire Design

## 1. Overview

This document specifies a compact, stochastic cellular automata fire and heat simulation intended to run in three contexts:

1. CLI test harness: fast iteration and debugging without launching Timberborn.
2. Unity prototype: GPU compute shader development and visualization testing.
3. Timberborn mod: final gameplay integration.

The simulation should be implemented as a reusable core system with host-specific adapters. Timberborn should not own the simulation rules. It should provide map data, register external changes, and respond to simulation deltas.

## 2. Goals

- Support grids up to `256 x 256 x 32` cells.
- Use compact per-cell storage.
- Allow deterministic stochastic behavior.
- Allow simulation testing outside Timberborn.
- Support CPU simulation for debugging and tests.
- Support GPU compute simulation for Unity/Timberborn rendering performance.
- Output changed cells as deltas so Timberborn updates remain minimal.
- Allow entities to register changes at any time, while the sim mutates only during controlled ticks.
- Ensure each cell/entity is processed at most once per update cycle.
- Allow visual effects to be driven directly from GPU simulation output.

## 3. Non-Goals

- Do not model full fluid dynamics.
- Do not model continuous heat, fuel, or water values.
- Do not require one Unity or Timberborn entity per visual fire/smoke/ash cell.
- Do not rely on Timberborn being open for simulation development.
- Do not make the grid itself delta-only; the simulator keeps full cell buffers internally.

## 4. Project Structure

The implementation should use Wildfire project names while preserving fire-simulation API names inside the code.

```text
Wildfire/
  src/
    Wildfire.Core/
      PackedCell.cs
      FireGrid.cs
      FireRules.cs
      IFireSimulator.cs
      CpuFireSimulator.cs
      CellDelta.cs
      FireSimChange.cs
      FireSimEvents.cs
    Wildfire.Unity/
      FireSim.compute
      FireVisuals.compute
      UnityComputeFireSimulator.cs
      ComputeBufferGrid.cs
      FireVisualRenderer.cs
    Wildfire.Cli/
      Program.cs
      TerminalRenderer.cs
      CliInput.cs
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

Each simulation cell is stored as a packed 16-bit value.

```text
ushort cell:
bits 0-3:   fuel          0-15
bits 4-7:   heat          0-15
bits 8-9:   flammability  0-3
bits 10-11: water         0-3
bit  12:    terrain       0-1
bits 13-15: heatLoss      0-7
```

### Stored Properties

| Property | Range | Meaning |
| --- | --- | --- |
| fuel | 0-15 | Remaining burnable material. |
| heat | 0-15 | Local thermal energy band. |
| flammability | 0-3 | How easily the cell ignites. |
| water | 0-3 | Local wetness / suppression level. |
| terrain | 0-1 | Whether the cell represents solid terrain/building material. |
| heatLoss | 0-7 | How quickly the cell loses heat. |

### Derived Properties

The following are not stored directly:

| Derived Value | Derived From |
| --- | --- |
| Burning state | fuel, heat, flammability, water, terrain |
| Flame visual intensity | heat, burning state |
| Smoke visual intensity | burning state, fuel, heat |
| Ash visual intensity | low/no fuel, heat history approximation |

This keeps the simulation compact and avoids storing flames, smoke, or ash directly.

## 6. Packed Cell Helpers

```csharp
public static class PackedCell
{
    public static ushort Pack(
        int fuel,
        int heat,
        int flammability,
        int water,
        int terrain,
        int heatLoss)
    {
        return (ushort)(
            ((fuel         & 0b1111) << 0)  |
            ((heat         & 0b1111) << 4)  |
            ((flammability & 0b11)   << 8)  |
            ((water        & 0b11)   << 10) |
            ((terrain      & 0b1)    << 12) |
            ((heatLoss     & 0b111)  << 13)
        );
    }

    public static int Fuel(ushort cell)         => (cell >> 0)  & 0b1111;
    public static int Heat(ushort cell)         => (cell >> 4)  & 0b1111;
    public static int Flammability(ushort cell) => (cell >> 8)  & 0b11;
    public static int Water(ushort cell)        => (cell >> 10) & 0b11;
    public static int Terrain(ushort cell)      => (cell >> 12) & 0b1;
    public static int HeatLoss(ushort cell)     => (cell >> 13) & 0b111;

    public static ushort SetFuel(ushort cell, int fuel)
    {
        return (ushort)((cell & ~0b0000_0000_0000_1111) | ((fuel & 0b1111) << 0));
    }

    public static ushort SetHeat(ushort cell, int heat)
    {
        return (ushort)((cell & ~0b0000_0000_1111_0000) | ((heat & 0b1111) << 4));
    }

    public static ushort SetWater(ushort cell, int water)
    {
        return (ushort)((cell & ~0b0000_1100_0000_0000) | ((water & 0b11) << 10));
    }

    public static bool IsBurning(ushort cell)
    {
        int fuel = Fuel(cell);
        int heat = Heat(cell);
        int flammability = Flammability(cell);
        int water = Water(cell);
        int terrain = Terrain(cell);
        int ignitionThreshold = 12 - flammability + water;
        return terrain == 1
            && fuel > 0
            && heat >= ignitionThreshold;
    }
}
```

## 7. Memory Budget

Maximum grid size:

```text
256 x 256 x 32 = 2,097,152 cells
```

At 16 bits per cell:

```text
2 bytes/cell ~= 4 MB per buffer
Double-buffered ~= 8 MB
```

GPU implementations may store each cell in a `uint` instead of a `ushort` for buffer alignment simplicity:

```text
4 bytes/cell ~= 8 MB per buffer
Double-buffered ~= 16 MB
```

Both are acceptable. CPU and CLI should use `ushort[]`. GPU can use `uint` buffers with the lower 16 bits containing the packed cell.

## 8. Simulator Interfaces

```csharp
public interface IFireSimulator
{
    int Width { get; }
    int Height { get; }
    int Depth { get; }
    ReadOnlySpan<ushort> Cells { get; }
    void RegisterChange(FireSimChange change);
    FireStepResult Tick();
    IDisposable Subscribe(IFireSimListener listener);
}

public readonly record struct FireStepResult(
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

## 9. External Change Registration

Entities may register simulation changes at any time, but changes are not applied immediately.

They are queued and applied at the start of the next simulation tick.

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

Example:

```csharp
_fireSim.RegisterChange(new FireSimChange(
    CellIndex: index,
    AddHeat: 8
));
```

Rule:

Nothing mutates the sim immediately except the sim tick itself.

This prevents recursive update chains and makes testing deterministic.

## 10. Tick Pipeline

Each tick follows this pipeline:

1. Increment tick/generation.
2. Apply queued external changes.
3. Add changed external cells and their neighbors to the candidate set.
4. Add previously active cells and their neighbors to the candidate set.
5. Process each candidate cell once.
6. Write changed cells to the delta list.
7. Build the next active frontier.
8. Notify listeners of deltas.
9. Swap active frontier buffers.

Pseudo-code:

```csharp
public FireStepResult Tick()
{
    _tick++;
    _generation++;
    _candidates.Clear();
    _deltas.Clear();
    _nextActive.Clear();

    ApplyQueuedExternalChanges();
    AddActiveFrontierToCandidates();

    foreach (int index in _candidates)
    {
        ushort oldCell = _cells[index];
        ushort newCell = StepCell(index, oldCell, _cells, _tick, _seed);
        if (newCell != oldCell)
        {
            _cells[index] = newCell;
            _deltas.Add(new CellDelta(index, oldCell, newCell));
            EnqueueNextActiveWithNeighbors(index);
        }

        if (ShouldRemainActive(newCell))
        {
            EnqueueNextActiveWithNeighbors(index);
        }
    }

    NotifyListeners(_deltas);
    SwapActiveSets();
    return new FireStepResult(_deltas, _tick);
}
```

## 11. Active Frontier

Most of the simulation grid is expected to be inactive.

The simulator should avoid scanning the full grid whenever possible.

A cell should be considered a candidate if it is:

- Changed last tick.
- Externally changed this tick.
- Still active from the previous tick.
- A neighbor of any of the above.

### Why Deltas Alone Are Not Enough

Previous deltas approximate the active frontier, but stochastic rules can cause a cell to remain eligible without changing.

Example:

A cell is hot enough to maybe ignite. The random roll fails. The cell does not change. It still needs to be checked next tick.

Therefore, the simulator must also track active cells separately from deltas.

## 12. Processing Each Cell Once

Use a generation-stamped queue.

```csharp
private readonly int[] _queuedGeneration;
private int _generation;

private void EnqueueCandidate(int index)
{
    if ((uint)index >= (uint)_cells.Length)
        return;

    if (_queuedGeneration[index] == _generation)
        return;

    _queuedGeneration[index] = _generation;
    _candidates.Add(index);
}
```

Adding a cell and its neighbors:

```csharp
private void EnqueueCandidateWithNeighbors(int index)
{
    EnqueueCandidate(index);
    foreach (int neighbor in GetNeighbors(index))
    {
        EnqueueCandidate(neighbor);
    }
}
```

Rule:

A cell may be requested many times, but it may only be admitted once per tick.

Newly activated cells should be queued for the next tick, not processed immediately during the current tick.

## 13. Neighbor Model

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

Initial implementation should stay with 6-neighbor behavior for predictability.

## 14. Deterministic Stochastic Behavior

Do not use `Random` during simulation steps.

Use hash-based randomness keyed by:

- Cell index.
- Tick.
- Seed.

```csharp
public static uint Hash(uint cellIndex, uint tick, uint seed)
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

This lets CPU, CLI, Unity, and Timberborn produce comparable behavior.

## 15. Cell Step Rules

The first implementation should be intentionally simple.

Inputs:

- Current cell.
- Neighbor cells.
- Tick.
- Seed.

Outputs:

- New packed cell.

Suggested rule outline:

```csharp
private ushort StepCell(int index, ushort cell, ReadOnlySpan<ushort> cells, uint tick, uint seed)
{
    int fuel = PackedCell.Fuel(cell);
    int heat = PackedCell.Heat(cell);
    int flammability = PackedCell.Flammability(cell);
    int water = PackedCell.Water(cell);
    int terrain = PackedCell.Terrain(cell);
    int heatLoss = PackedCell.HeatLoss(cell);

    int neighborHeat = AverageNeighborHeat(index, cells);
    int burningNeighborCount = CountBurningNeighbors(index, cells);

    // Heat diffusion.
    heat = ((heat * 3) + neighborHeat) / 4;

    // Burning neighbors add heat pressure.
    heat += burningNeighborCount;

    // Water suppresses heat.
    if (water > 0)
    {
        heat -= water;
        if (heat > 8)
        {
            water -= 1;
        }
    }

    // Ignition / burning.
    bool canBurn = terrain == 1 && fuel > 0;
    int ignitionThreshold = 12 - flammability + water;
    if (canBurn && heat >= ignitionThreshold)
    {
        uint roll = FireRandom.Hash((uint)index, tick, seed) & 15u;
        int burnChance = Math.Clamp(heat + flammability - water, 0, 15);
        if (roll < burnChance)
        {
            fuel = Math.Max(0, fuel - 1);
            heat = Math.Min(15, heat + 2 + flammability);
        }
    }

    // Passive heat loss.
    heat -= 1 + (heatLoss / 3);
    heat = Math.Clamp(heat, 0, 15);
    fuel = Math.Clamp(fuel, 0, 15);
    water = Math.Clamp(water, 0, 3);

    return PackedCell.Pack(
        fuel,
        heat,
        flammability,
        water,
        terrain,
        heatLoss
    );
}
```

The exact constants should be tuned from the CLI.

## 16. Active Predicate

A cell should remain active if it may change in future ticks without outside input.

```csharp
private static bool ShouldRemainActive(ushort cell)
{
    int fuel = PackedCell.Fuel(cell);
    int heat = PackedCell.Heat(cell);
    int water = PackedCell.Water(cell);
    int terrain = PackedCell.Terrain(cell);
    int flammability = PackedCell.Flammability(cell);

    if (heat > 0)
        return true;

    if (water > 0 && heat > 0)
        return true;

    int ignitionThreshold = 12 - flammability + water;
    if (terrain == 1 && fuel > 0 && heat >= ignitionThreshold - 2)
        return true;

    return false;
}
```

## 17. Delta Output

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
- CLI rendering updates.
- Optional CPU readback from GPU simulation.

The grid remains full-state internally.

## 18. Listener Notifications

Listeners receive deltas after a tick completes.

```csharp
public interface IFireSimListener
{
    void OnFireSimDeltas(ReadOnlySpan<CellDelta> deltas);
}
```

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

## 19. CLI Harness

The CLI exists to test and tune the simulation without Timberborn.

### Requirements

- Run the CPU simulator.
- Display one Z layer at a time.
- Navigate layers with up/down arrows.
- Pause/resume with space.
- Quit with `q`.
- Use terminal characters and colors for state.
- Optionally redraw only cells changed by deltas.

### Command

```bash
dotnet run --project src/Wildfire.Cli -- 128 128 8
```

### Controls

| Input | Action |
| --- | --- |
| Up arrow | Layer up. |
| Down arrow | Layer down. |
| Space | Pause/resume. |
| Q | Quit. |

### Suggested Characters

| State | Character |
| --- | --- |
| Empty | Space |
| Terrain/fuel | `"` |
| Warm | `-` |
| Hot | `+` |
| Burning | `*` / `@` |
| Water | `~` |
| Burned out / ash visual | `.` / `:` |

The CLI should derive visuals from packed cell values rather than requiring stored flame/smoke/ash fields.

## 20. Unity Compute Backend

The Unity compute backend should use the same packed cell format and rules translated to HLSL.

### Buffers

```text
CurrentCells:       StructuredBuffer<uint>
NextCells:          RWStructuredBuffer<uint>
CurrentActive:      StructuredBuffer<uint>
NextActive:         AppendStructuredBuffer<uint>
Deltas:             AppendStructuredBuffer<CellDeltaGpu>
QueuedGeneration:   RWStructuredBuffer<uint>
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

### Compute Pipeline

1. Upload queued external changes, if any.
2. Apply external changes pass.
3. Build candidate frontier pass.
4. Fire simulation pass.
5. Append changed cells to delta buffer.
6. Append next active cells.
7. Generate visual field.
8. Swap current/next buffers.

Initial GPU implementation may scan the full grid for simplicity. Active frontier GPU optimization can come later.

## 21. GPU Deduplication

GPU frontier deduplication should use atomic generation stamps.

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

Depending on platform behavior, this may need to be adapted. CPU active frontier should be implemented first.

## 22. GPU Visual Pipeline

The compute shader can drive visuals directly.

It should not directly notify gameplay entities.

### Visual Pipeline

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

### Visual Texture

Use a `Texture2DArray` for cell-layer visuals.

```text
layer 0 = z0
layer 1 = z1
layer 2 = z2
...
```

Each texel corresponds to one simulation cell.

Suggested channels:

- R: fire intensity.
- G: smoke intensity.
- B: ash intensity.
- A: heat intensity or visibility.

Example HLSL output:

```hlsl
RWTexture2DArray<float4> FireVisuals;

FireVisuals[int3(x, y, z)] = float4(
    fireIntensity,
    smokeIntensity,
    ashIntensity,
    heatIntensity
);
```

Rule:

Visual effects can be GPU-driven. Gameplay/entity changes remain C#-driven through deltas.

## 23. Timberborn Integration

Timberborn should be an adapter around the simulator.

### Timberborn Responsibilities

- Convert terrain/buildings/resources into packed fire cells.
- Register external changes from gameplay events.
- Run the sim on a fixed cadence.
- Consume deltas.
- Update overlays/effects only where needed.
- Apply gameplay consequences from deltas.

### Timberborn Should Not

- Own the fire rules.
- Mutate the fire grid directly.
- Create one game entity per fire/smoke/ash cell.
- Require the game to run for core sim testing.

### Example Integration Flow

```text
Building / terrain / water system
  registers FireSimChange
        |
FireSim tick
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

## 24. Timberborn Entity/Event Examples

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

## 25. CPU First, GPU Later

Implementation should proceed in phases.

### Phase 1: CPU Core

- Implement packed cell helpers.
- Implement CPU simulator.
- Implement deterministic stochastic rules.
- Implement delta output.
- Implement active frontier.
- Implement generation-stamped dedupe.
- Add unit tests.

### Phase 2: CLI Harness

- Add terminal renderer.
- Add layer navigation.
- Add pause/resume.
- Add seed/config options.
- Add test scenarios.

### Phase 3: Timberborn CPU Backend

- Add Timberborn adapters.
- Convert map/building data into cells.
- Consume deltas.
- Update overlays/effects from deltas.
- Validate gameplay loop.

### Phase 4: Unity Compute Prototype

- Implement `.compute` shader using same cell layout.
- Compare GPU snapshots against CPU snapshots.
- Add visual texture output.
- Prototype overlay material.

### Phase 5: Timberborn GPU Backend

- Package/load compute shader.
- Use GPU visual pipeline.
- Read back compact deltas for gameplay.
- Keep CPU backend as fallback/debug mode.

## 26. Testing Strategy

### Unit Tests

Test:

- Packed cell round-trips.
- Field setters.
- Ignition threshold behavior.
- Water suppression.
- Heat loss.
- Deterministic random hash.
- Active frontier inclusion.
- Dedupe behavior.

### Snapshot Tests

Given:

- Same seed.
- Same initial grid.
- Same tick count.

CPU and GPU should produce comparable packed cell grids.

Exact matching is ideal, but if GPU rules diverge slightly due to shader limitations, snapshot differences should be understood and bounded.

### CLI Scenarios

Add seeded scenarios:

- Single ignition point.
- Line of fuel.
- Water barrier.
- Vertical fuel column.
- Sparse forest.
- Building cluster.
- Mixed terrain/fuel/water.

## 27. Important Design Rules

1. The sim owns mutation.

   Entities register changes; they do not directly mutate cells.

2. Ticks are the consistency boundary.

   Changes registered during notification apply on the next tick.

3. Each cell processes once per tick.

   Use generation-stamped deduplication.

4. Deltas are output, not storage.

   The simulator still keeps full buffers.

5. Active frontier is separate from deltas.

   Some cells remain eligible even when unchanged.

6. Randomness is deterministic.

   Use hash-based randomness, not `Random`.

7. Visuals can stay GPU-side.

   Gameplay reactions must go through C# deltas.

8. Timberborn is a host.

   The simulator should remain testable outside the game.

## 28. Open Questions

- How often should the Timberborn fire sim tick relative to game ticks?
- Should fire spread diagonally, or only through 6-neighbor adjacency?
- Should wind be part of the core sim or a modifier applied by the host?
- Should ash become a stored field later, or remain derived from fuel/heat?
- How should Timberborn buildings map to vertical cells?
- Should water represent temporary wetness, standing water, or both?
- Should heat loss be material-specific, biome-specific, or weather-driven?
- Should the GPU backend use full-grid dispatch first, then active frontier later?

## 29. Summary

The fire simulator should be a compact, deterministic, tick-based cellular automata system with a packed 16-bit cell format. It should expose a clean change-registration and delta-notification API so Timberborn entities can interact with it without owning simulation state.

The CPU backend should be implemented first for CLI testing and deterministic debugging. The GPU backend should use the same packed format and produce both deltas and GPU-side visual fields. Timberborn should consume gameplay deltas through C# while visual overlays and effects can be driven directly from compute shader output.
