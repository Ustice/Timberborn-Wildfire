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

| Property     | Range | Meaning                                                      |
| ------------ | ----- | ------------------------------------------------------------ |
| fuel         | 0-15  | Remaining burnable material.                                 |
| heat         | 0-15  | Local thermal energy band.                                   |
| flammability | 0-3   | How easily the cell ignites.                                 |
| water        | 0-3   | Local wetness / suppression level.                           |
| terrain      | 0-1   | Whether the cell represents solid terrain/building material. |
| heatLoss     | 0-7   | How quickly the cell loses heat.                             |

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
VisualField:        RWStructuredBuffer<float4>
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

### Current Full-Grid Shader Baseline

`FireSim.compute` currently exposes one `SimulateFullGrid` kernel with `8 x 8 x 4` thread groups. The Unity wrapper dispatches enough groups to cover the full grid, then swaps current and next cell buffers after dispatch. C# does not execute fire-spread rules for parity.

The first shader translates the rule pseudocode with these implementation details:

- Neighbor heat averages only in-bounds neighbors, so edge and corner cells divide by the number of real neighbors instead of a fixed six.
- `burnChance` is saturated through `min` and unsigned subtraction guards instead of relying on pseudocode `clamp` behavior.
- GPU delta records are padded to four `uint` fields so the current `wildfire.deltas` buffer stride stays 16 bytes.
- External change upload remains a follow-up pass owned by `TWF-003`.
- Visual-field writes now happen in the full-grid kernel from the post-step packed cell value.

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

Use a four-channel visual field for cell-layer visuals. The current repository does not reference UnityEngine, so the first implementation uses an `RWStructuredBuffer<float4>` abstraction with one entry per cell. A future Unity binding can expose the same layout through `Texture2DArray<float4>` or copy the buffer into a texture for material sampling.

Suggested channels:

- R: fire intensity.
- G: smoke intensity.
- B: ash intensity.
- A: heat intensity or visibility.

Current derivation:

- Fire intensity comes from burning state and heat.
- Smoke intensity comes from burning state, remaining fuel, and heat.
- Ash intensity comes from terrain cells with low/no fuel and residual heat. Because `PackedCell` has no burn-history field, ash disappears after heat decays; this is a temporary approximation, not stored ash state.
- Alpha is visibility, the maximum of heat, fire, smoke, and ash intensity.

Fire and smoke presentation should be field-based rather than per-tile. The visual field is still sampled at cell resolution, but renderer and effect systems should cluster, blur, threshold, or otherwise aggregate neighboring intensity into larger coherent regions. A convincing fire front should look like connected flames and smoke volumes, not hundreds of small independent tile effects.

Compact deltas should wake or bound visual regions, but they should not dictate one effect object per changed cell. The presentation layer can place a small number of pooled anchors, meshes, particles, or material-driven volumes over active field regions and scale them by sampled intensity, spread, and region size.

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

## 20. World Consequence Design

Fire consumes burnable world value. The simulator owns fire fields and compact deltas; Timberborn-owned consequence services translate those deltas into resource loss, disabled work, construction rollback, beaver status effects, ash fields, alerts, visuals, and persistence.

The current first-pass planning bridge for stored goods, scenario-save generation, and faction fire-response ideas lives in [world-consequence-first-pass.md](world-consequence-first-pass.md).

### Burn Damage State

Burn damage is instance state attached to spec-backed entities, not static spec data. `BuildingSpec`, `HarvestableSpec`, `CuttableSpec`, and similar Timberborn specs can declare static burn material descriptors, but each live building, plant, crop, or tree needs its own persisted burn damage state.

For plants, crops, and trees, damage capacity is based on resource yield. As fuel burns away, the corresponding harvest or cutting yield is reduced. Full burn damage destroys the remaining yield, kills the plant, and switches the entity to its accepted dead, burned, or charred presentation.

For buildings, damage capacity is based on the resources invested in construction. Each consumed resource should have a fuel score and flammability score so wood, planks, treated planks, paper, food, metal, contaminated goods, and non-burnable materials can be tuned independently. As building fuel is consumed, the building loses construction value rather than simply disappearing.

Once a building is damaged or on fire, it should close and become unusable. Beavers should not repair it while fire or dangerous heat remains. After the fire is out, repair should work like construction: missing resources must be delivered again, and graphics should return through construction-stage visuals as each repaired level is reached. Burned incomplete forms can be retextured to show charred aftermath.

Stored items burn as inventory contents, not as part of the storage building's construction value. A warehouse, pile, or tank can therefore lose structural construction value through building burn damage while also losing stored logs, planks, paper, food, explosives, or other goods through inventory accounting. Explosives and volatile goods should start as bounded hazardous contents: high flammability, a short unstable threshold, stock destruction, and a limited heat or fire pulse into nearby simulation cells. They should not start as arbitrary physics blasts, terrain mutation, or direct entity deletion.

Dynamite, double dynamite, triple dynamite, detonators, and tunnels are infrastructure explosives, not stored goods. They should share the stored-goods hazardous threshold language, but the implementation path is separate because Timberborn exposes native explosion and terrain-affecting components for them. The first fire integration should arm or trigger native dynamite only through the `Dynamite` API after a sustained heat threshold, mirror the danger into the fire field as a bounded heat pulse, and log every triggered or skipped target. Detonators should be treated as trigger devices: fire may disable or prematurely arm them only after a safe `Detonator` wrapper proves recoverability. Tunnels should not be terrain-mutated by fire in the first gameplay consequence pass; fire can mark them unstable and emit heat/visibility consequences, while actual `Tunnel.Explode` terrain destruction requires a separate live-QA gate and an opt-in setting.

Explosion-related settings should default conservative: native dynamite triggering disabled until live proof, tunnel terrain destruction disabled, bounded fire-field pulse enabled only for accepted explosive targets, and maximum pulse radius fixed to one neighbor shell for the first release candidate. Required telemetry should distinguish `explosive_infrastructure_considered`, `explosive_infrastructure_armed`, `explosive_infrastructure_triggered`, `explosive_infrastructure_native_triggered`, `explosive_infrastructure_heat_pulse_cells`, `explosive_infrastructure_skipped_no_safe_api`, `explosive_infrastructure_skipped_setting_disabled`, and `tunnel_destruction_deferred`.

Multi-cell entities need mapping in both directions. A Timberborn entity can occupy several simulation cells, and a changed cell must be able to resolve back to the owning burn-damage entity without applying duplicate damage. Tall buildings, large buildings, large plants, and vertical footprints should roll up cell damage into one coherent instance state.

### Beaver Field Effects

Beaver effects are split by danger type and by release risk. The simulator owns the fire, smoke, ash, steam, heat, and wetness fields; Timberborn reads those fields and applies beaver-facing consequences. Timberborn must not infer new fire rules, mutate packed cells, or use hidden status changes that the player cannot understand.

The first release should implement a staged evidence ladder:

1. Exposure telemetry and player feedback.
2. Work interruption and avoidance.
3. Reversible debuffs.
4. Incapacitation.
5. Death.

Each later stage requires deterministic coverage plus live proof that the Timberborn API path is recoverable and understandable. If a desired consequence has no safe native API, it should remain a logged safe no-op instead of going through reflection.

Field meanings for beavers:

- Active fire: immediate life-threatening zone. Release scope is avoidance, work cancellation, alert telemetry, singed or burned exposure, and later death only after sustained exposure proof.
- Heat: non-flame thermal danger. Release scope is path-cost or avoidance telemetry, work interruption, singed exposure at high heat, and recovery once heat falls.
- Smoke: respiratory danger from burning fuel. Release scope is exposure telemetry and coughing slowdown if a safe debuff path exists.
- Toxic smoke: respiratory danger from contaminated burn sources or badwater-adjacent fire. Release scope is stronger exposure weighting and explicit toxic telemetry; native contamination effects are allowed only after live API proof.
- Steam: white smoke-like suppression byproduct from heated water. Release scope is visual and mild respiratory/visibility telemetry, not injury by default.
- Toxic steam: badwater or contaminated-water steam. Release scope is toxic respiratory telemetry and optional native contamination path after proof.
- Ash aftermath: simulator-owned ash field aftermath. Release scope is path/feedback telemetry only; ash resource, hazard, collection, and fertility behavior should read simulator ash state through adapter services.
- Wet suppression fields: safer from flame but may create steam. Release scope is reduced burn danger plus steam exposure, not a beaver penalty by itself.

Smoke and toxic air use a respiratory progression:

- Coughing: slowdown and work inefficiency.
- Choking: incapacitated behavior, preferably using a sleep-like state if that is the safest Timberborn API path.
- Death: only after sustained severe exposure, trapped behavior, or accepted live evidence that the transition is safe.

Heat and flame use a burn progression:

- Singed: normal injury-style debuff.
- Burned: contamination-like severe injury that prevents work until treated or healed.
- Death: only after sustained direct heat or flame exposure, and only after avoidance, work cancellation, debuff, and incapacitation paths are tested.

Active fire cells should be forbidden or heavily avoided pathing zones when Timberborn exposes a safe pathing hook. Hot, smoky, or toxic cells should interrupt work and increase path cost. Beavers assigned to burning buildings, crops, trees, storage, or infrastructure should abandon the job when their target footprint is actively dangerous. The first implementation should aggregate exposure by beaver and tick, apply hysteresis so beavers do not flicker between states, and expose status counters for observed, avoided, interrupted, coughing, choking, singed, burned, death-candidate, skipped-no-safe-api, and recovered beavers.

Release-scope behavior is conservative:

- Required: exposure telemetry by field class, work-cancellation decisions, safe no-op counters, and player-facing aggregated danger feedback.
- Accepted if API-safe: avoidance/path-cost hints, coughing slowdown, singed injury, and burned severe injury.
- Deferred: automatic death, forced incapacitation, native contamination coupling, beaver health effects from ash, firefighting panic, faction-specific response behavior, and arbitrary path graph mutation.

Validation should use the real field outputs, not hard-coded per-beaver triggers. Deterministic tests can emulate field samples, but live QA must prove that a real fire or suppression event produces beaver exposure telemetry before any behavior is accepted.

### Ash And Fertility

Ash is one simulated field. Ash amount and contamination belong to simulator-owned transport state, and visual ash is presentation of that state. See [ash-simulation-model.md](ash-simulation-model.md) for the current ash authority, naming, and packed-field direction.

Fertile ash is not a separate simulated ash kind. In simulator state, fertile ash means ash with no contamination. `FertileAsh` is the Timberborn good produced from harvesting uncontaminated ash, with `1 FertileAsh` good equal to 1 uncontaminated ash unit harvested from the field.

For the first gameplay pass, uncontaminated ash can increase plant growth speed. Beavers can collect uncontaminated ash as a `FertileAsh` good and place it back onto player-designated planting areas. Controlled burns can become a useful strategy for fuel management and soil improvement, but ash production and application should have caps, decay, or application limits so burning land is not always optimal.

Fertile ash placement should use Timberborn-style area tools instead of automatic spreading from storage. Add two toolbar options:

- `Fertilize crops` in the `Fields` tool group. This tool designates crop planting squares and is serviced by Farmhouse-style workers for plantables whose resource group is `Farmhouse`.
- `Fertilize trees and bushes` in the `Forestry` tool group. This tool designates forestry planting squares and is serviced by Forester-style workers for plantables whose resource group is `Forester`, including berry bushes, coffee bushes, dandelions, and trees.

Both tools should share one designation and application backend. Dragging over squares creates fertile-ash application designations; a remove mode clears them. Workers should consume `FertileAsh` from available inventory, queue a bounded ash increase for the designated cell, and then let simulator ash state and the growable adapter apply the growth benefit. Designations on contaminated ash cells should remain blocked and must not consume goods unless a later explicit decontamination mechanic is designed.

The toolbar tools should expose enough status to explain the job loop: crop designations, forestry designations, pending cells, applied cells, consumed goods, skipped cells with no goods, skipped cells with no eligible growable, contaminated-ash-blocked cells, and safe-API failures. This is player-designated field work, not a second fire simulation rule and not a separate growth multiplier outside simulator ash state.

Underbrush, grass, and overgrowth are future fuel-load mechanics. A later design can model overgrown trees or irrigated fields as fast-burning surface fuel while mature trees burn more slowly. That should stay separate from the first ash-field implementation.

### Contamination Interaction

Fire never reduces contamination.

Contaminated burnable material can burn, but it should produce toxic smoke and tainted ash instead of fertile ash. Contaminated plants, crops, and trees lose their resources and die like normal plants, but their aftermath should not create a growth bonus while contamination remains. Contaminated buildings and contaminated stored goods can contribute tainted residue or exposure when burned.

Contaminated soil remains contaminated after fire. Ash deposited on contaminated soil should be `tainted`, not `fertile`, unless a later explicit decontamination mechanic changes that.

Water suppresses fire and can create steam. Badwater or contaminated water should also suppress fire, but heating it should create toxic steam or toxic smoke exposure. Fire should not turn badwater into safe water.

Beavers exposed to contaminated smoke, toxic steam, or tainted aftermath can advance the respiratory or burn progressions faster and may also use Timberborn-native badwater contamination effects if live API tests prove that path safe. Wildfire should reuse native contamination graphics and treatment flows where possible, but it should not silently conflate ordinary smoke, burn injury, and native contamination if the player needs to understand the cause.

### Player Feedback

Player feedback should aggregate consequences instead of spamming one alert per burned tree or crop. Alerts and status surfaces should distinguish at least these classes:

- Active fire or new fire.
- Building damaged or closed by fire.
- Plant, crop, or resource loss.
- Beaver danger, injury, incapacitation, or death.
- Fertile ash or tainted ash aftermath when that becomes gameplay-relevant.

### Active Fire Response

Active suppression is future work, but it should preserve faction identity and use simulation inputs rather than giving Timberborn adapter code ownership of fire rules.

Ironteeth should favor Fire Wardens: higher construction and resource cost, fewer beavers required, protective clothing, and sprayer-like water application. Folktails should favor Fire Bells and bucket brigades: lower resource cost, more beaver labor, nearest natural water first, stored water fallback, and one-beaver-per-dump suppression. Emberpelts should favor direct tail-stamping suppression that is effective but carries higher singed or burned injury risk.

Fans and constructible fire berms are separate tactical tools. Fans should interact with smoke fields first and only later with fire airflow if the simulation can express the tradeoff. Fire berms are a cleaner spread-control mechanic and can block or reduce spread through a non-burnable constructed barrier.

## 21. Implementation Phases

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

## 22. Testing Strategy

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

## 23. Important Design Rules

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

## 24. Release Simulation Decisions

The initial release should stay conservative and ship the already-proven GPU path unless later live evidence creates a specific blocker. These decisions close the release-blocking questions from `TWF-044`; deferred items remain valid future work but should not block Sprint 4 or the first public release.

| Topic                            | Initial Release Decision                                                                                                                                                                                                                           | Evidence And Follow-Up                                                                                                                                                                                                                                                                                               |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Tick cadence                     | Keep Timberborn dispatch on the centralized fixed cadence, currently one simulator tick per second of accumulated game time.                                                                                                                       | `TimberbornFireCadence.Default` is one second, live QA/readiness tokens already report advancing dispatch ticks, and `TWF-043` may tune constants inside that cadence without changing the boundary. A release setting can expose cadence later through `TWF-048` only after live-loop validation proves it is safe. |
| Diagonal spread                  | Keep the 6-neighbor 3D model: left, right, north, south, below, and above. Do not add diagonal neighbors for the first release.                                                                                                                    | `FireSim.compute` implements exactly these six in-bounds reads. Diagonal spread would change scenario behavior, tuning, and snapshot expectations, so it is deferred until after release unless a specific accepted design ticket promotes it.                                                                       |
| Wind                             | Use Timberborn's global wind as an adapter input for fire spread and atmospheric smoke drift while keeping the core rules host-agnostic.                                                                                                            | `ITimberbornWindProvider` reads `WindService.WindDirection` and `WindStrength`, normalizes them into `FireSimWind`, and the compute shader consumes that value for downwind fire heat and smoke advection. Non-Timberborn and batchmode runs default to calm wind unless a host supplies a wind value.              |
| Ash storage                      | Keep ash out of `PackedCell`; ash amount and contamination belong to simulator transport state.                                                                                                                                                     | `PackedCell` remains the compact fire-cell state. Ash is a simulator-owned field with visual presentation and Timberborn adapters consuming or queueing changes against that state. See [ash-simulation-model.md](ash-simulation-model.md).                                                                        |
| Contamination and fire           | Fire never reduces contamination.                                                                                                                                                                                                                  | Contaminated fuel may burn, contaminated water may suppress, and contaminated aftermath may be tainted, but soil, water, goods, plants, buildings, and beavers are not cleansed by fire. Any future decontamination mechanic must be explicit and separate from wildfire behavior.                                   |
| Vertical building mapping        | Keep current footprint expansion across explicit `x`, `y`, and `z` cells. Each occupied vertical cell maps to a packed simulation cell through the Timberborn adapter.                                                                             | `TimberbornCellFootprint` and mapper tests already cover vertical expansion. This keeps tall structures understandable without adding building-specific fire rules to Timberborn.                                                                                                                                    |
| Water semantics                  | Treat water as a bounded suppression/wetness band, not fluid simulation. It may come from standing water, wet terrain, or queued suppression changes, but in all cases it only writes the packed `water` field and reduces heat/ignition pressure. | `TWF-038` live evidence proves queued `SetWater=3` suppression through the GPU path. The project non-goals still exclude fluid dynamics and continuous water values.                                                                                                                                                 |
| Heat-loss source                 | Keep heat loss material-driven for release. Timberborn terrain, resource, vegetation, and building adapters choose deterministic heat-loss bands before packing cells.                                                                             | The mapper already centralizes material bands and clamps them to the packed field width. Biome, weather, or season-driven heat loss should be future adapter input that registers changes, not core rule ownership.                                                                                                  |
| Full-grid versus active frontier | Keep full-grid dispatch for the first release. Active-frontier optimization remains deferred until post-release profiling shows a measured bottleneck on a larger map, sustained high-delta workload, or consequence-heavy live run.                 | `TWF-034` profiling on the `128x128x23` live save found full-grid dispatch acceptable, and `TWF-046` proved the coherent live gameplay loop without dispatch becoming player-visible cost. `TWF-051` therefore keeps `TWF-011` deferred for release.                                                                 |

## 25. Summary

The fire simulator should be a compact, deterministic, tick-based cellular automata system with a packed 16-bit cell format and one authoritative GPU execution path. It should expose a clean change-registration and delta-notification API so Timberborn entities can interact with it without owning simulation state.

The GPU simulator should use the packed format, produce compact deltas, and generate GPU-side visual fields. Timberborn should consume gameplay deltas through C# while visual overlays and effects are driven directly from compute shader output.
