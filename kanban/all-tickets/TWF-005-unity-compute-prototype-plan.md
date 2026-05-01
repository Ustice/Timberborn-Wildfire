---
ticket: TWF-005
agent_level: High
role: researcher
requires_qa: false
doc_only: true
dependencies:
   - TWF-001
   - TWF-003
write_scope:
   - kanban/all-tickets/TWF-005-unity-compute-prototype-plan.md
---

# TWF-005: Plan Unity Compute Prototype

## Goal

Prepare the GPU implementation path without outrunning CPU correctness.

## Requirements

- Define the first compute shader buffer layout in terms of the packed cell format.
- Identify CPU/GPU snapshot parity requirements.
- Decide whether the first GPU pass should scan the full grid before active-frontier optimization.
- Recommend a narrow implementation ticket for `Wildfire.Unity`.

## Dependencies

- TWF-001 and TWF-003 should define CPU behavior and snapshots first.

## Role

- Researcher using [../roles/researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check` when the ticket is updated.

## Notes

- Gameplay deltas must remain C#-driven even if visuals are GPU-driven.

## Research Findings

### First Compute Buffer Layout

Use a full-grid, double-buffered cell layout for the first `Wildfire.Unity` compute slice:

- `CurrentCells`: `StructuredBuffer<uint>`, one element per grid cell.
- `NextCells`: `RWStructuredBuffer<uint>`, one element per grid cell.
- `Deltas`: `AppendStructuredBuffer<CellDeltaGpu>`, changed cells only.
- `DeltaCount`: indirect append counter or C# append-buffer count readback, used only by the parity harness.
- `FireVisuals`: optional `RWTexture2DArray<float4>` after cell parity exists; do not make visuals part of the first correctness gate.

Cell values must keep the CPU packed format in the lower 16 bits of each `uint`:

```text
bits 0-3:   fuel
bits 4-7:   heat
bits 8-9:   flammability
bits 10-11: water
bit  12:    terrain
bits 13-15: heatLoss
bits 16-31: unused, must stay zero for parity readback
```

The first GPU delta record should mirror the C# contract with widened fields:

```hlsl
struct CellDeltaGpu
{
    uint Index;
    uint OldCell;
    uint NewCell;
};
```

For parity readback, compare only `OldCell & 0xFFFF` and `NewCell & 0xFFFF`, but fail if any high bits are non-zero. Keep the C# gameplay boundary unchanged: hosts consume C# deltas, and GPU deltas are evidence for parity until a later integration ticket decides otherwise.

### CPU/GPU Snapshot Parity Requirements

The merged CPU snapshot suite should be the first GPU oracle. The GPU harness must run the same scenario name, dimensions, seed, initial packed cells, and tick count as the CPU snapshot tests, then format the readback using the same layer and row order.

Required first parity scenarios:

- `single-ignition-point`
- `water-barrier`
- `vertical-fuel-column`

Parity should check:

- Final packed cell grid matches CPU exactly as four-character uppercase hex values.
- Per-tick delta counts match the CPU snapshot line, such as `deltas=1:1,2:1`.
- Final tick number matches CPU.
- Grid traversal order is `z`, then `y`, then `x`, with index `layerOffset + x + (y * width)`.
- Hash randomness matches `FireRandom.Hash(cellIndex, tick, seed)` bit-for-bit.
- Neighbor behavior matches the 6-neighbor CPU rule, including layer boundaries.
- CPU remains the fallback/debug authority until exact packed-cell parity is proven.

If exact parity fails, the first implementation should record each differing cell with coordinates, CPU packed value, GPU packed value, and the specific shader/rule mismatch. Do not accept broad visual-only tolerance for this ticket family.

### Full Grid Versus Active Frontier

Decision: the first GPU pass should scan the full grid.

Reasoning:

- `docs/DESIGN.md` explicitly allows the initial GPU implementation to scan the full grid before active-frontier optimization.
- Full-grid dispatch removes GPU frontier deduplication, append ordering, and atomic-generation-stamp behavior from the first correctness problem.
- The maximum design grid is `256 x 256 x 32`, which is `2,097,152` cells. With `uint` cells this is about `8 MB` per buffer and `16 MB` double-buffered, which is acceptable for a prototype parity pass.
- Active frontier is still required for CPU correctness and likely later GPU performance, but it should be a second GPU ticket after the shader proves packed-cell parity on the CPU snapshot suite.

Do not add `CurrentActive`, `NextActive`, or `QueuedGeneration` to the first implementation ticket unless a profiler proves full-grid parity is too slow to run the starter snapshots.

### Recommended Wildfire.Unity Ticket

Create a narrow implementation ticket:

```text
TWF-006: Add Full-Grid Unity Compute Snapshot Parity Harness
```

Suggested scope:

- Add `FireSim.compute` with a full-grid kernel that reads `CurrentCells`, writes `NextCells`, and appends `CellDeltaGpu`.
- Add a `UnityComputeFireSimulator` prototype that uploads CPU snapshot scenarios, dispatches one tick at a time, swaps buffers, and reads back final cells plus per-tick delta counts.
- Port only the current CPU packed-cell helpers, `FireRules.StepCell`, 6-neighbor heat/burning checks, and `FireRandom.Hash`.
- Add or document a parity runner that compares the three starter CPU snapshot scenarios exactly.
- Leave active-frontier GPU optimization, visual textures, Timberborn integration, and gameplay consumption of GPU deltas out of scope.

Acceptance criteria:

- The three starter CPU snapshot scenarios match exactly on packed final grid and per-tick delta counts.
- High 16 bits of every GPU cell and delta cell value are zero on readback.
- Any mismatch report includes scenario, tick, coordinates, CPU value, GPU value, and suspected rule area.
- CPU backend remains available and unchanged.

## Evidence

- `docs/DESIGN.md` defines the packed 16-bit cell format, allows GPU `uint` buffers with lower 16 bits populated, describes the Unity compute buffers, and says the initial GPU implementation may scan the full grid.
- `docs/ARCHITECTURE.md` assigns compute buffers, HLSL rule translation, CPU/GPU snapshot comparison, and GPU visual fields to `Wildfire.Unity`, while keeping CPU as debug/fallback authority until GPU parity is proven.
- `docs/TEST_PLAN.md` requires GPU validation to use CPU snapshots as the oracle and to compare packed GPU cells exactly before accepting bounded scenario-specific tolerances.
- `tests/Wildfire.Core.Tests/CpuSnapshotTests.cs` contains the merged starter CPU snapshot suite and its canonical formatting order.
- `src/Wildfire.Core/PackedCell.cs`, `src/Wildfire.Core/FireRules.cs`, and `src/Wildfire.Core/CpuFireSimulator.cs` define the current packed layout, rule behavior, active-frontier CPU implementation, and delta semantics that the GPU slice must mirror.

## Confidence

High for the implementation order and parity requirements because they are directly supported by current design, architecture, test-plan, CPU rules, and snapshot tests. Medium for exact Unity buffer mechanics because no Unity compute code exists yet, so append-counter/readback details should be finalized in the implementation ticket against the Unity runtime.
