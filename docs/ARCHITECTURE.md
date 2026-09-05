# Wildfire Architecture

Wildfire separates simulation state and rules from host gameplay. The shared shader is authoritative for fire spread. There are currently two C# compute orchestration implementations, so shared shader ownership does not imply that the tests exercise the live game's upload and readback path.

## Ownership and entry points

| Project | Responsibility | Entry points |
| --- | --- | --- |
| Core | Packed formats, grid, parameters, change/delta contracts, deterministic fixtures | [FireSimContracts.cs](../src/Wildfire.Core/FireSimContracts.cs), [PackedCell.cs](../src/Wildfire.Core/PackedCell.cs) |
| CLI | Seeded scenario preview and fixture export | [Program.cs](../src/Wildfire.Cli/Program.cs) |
| Unity | Portable compute abstractions, dispatch orchestration, shader sources, batchmode fixture execution | [UnityComputeFireSimulator.cs](../src/Wildfire.Unity/UnityComputeFireSimulator.cs), [FireSim.compute](../src/Wildfire.Unity/FireSim.compute) |
| Timberborn | Native compute binding, world import, runtime lifecycle, gameplay, presentation, persistence, QA bridge | [Runtime/](../src/Wildfire.Timberborn/Runtime/), [Simulation/](../src/Wildfire.Timberborn/Simulation/) |

Core references neither Unity nor Timberborn. The Unity project uses allocator and dispatcher interfaces that can be exercised without native Unity APIs. Timberborn currently references Core and binds real Unity APIs directly; it does not delegate its tick to `UnityComputeFireSimulator`.

## Runtime flow

```text
Timberborn world observations and events
  -> importer and cell/material mapping
  -> simulator initialization or queued FireSimChange inputs
  -> ApplyExternalChanges kernel
  -> SimulateFullGrid kernel
  -> compact delta readback and cell/transport buffer swap
  -> consequence consumers and field observations
  -> gameplay, presentation, alerts, persistence
```

[TimberbornFireRuntimeInitializer](../src/Wildfire.Timberborn/Runtime/TimberbornFireRuntimeInitializer.cs) resolves readiness, imports the world, and creates the simulator. [TimberbornFireRuntime](../src/Wildfire.Timberborn/Runtime/TimberbornFireRuntime.cs) implements the native game lifecycle and coordinates services. [TimberbornFireSystem](../src/Wildfire.Timberborn/Runtime/TimberbornFireSystem.cs) wraps the simulator and drives delta consumers and QA operations.

[TimberbornComputeFireSimulator](../src/Wildfire.Timberborn/Simulation/TimberbornComputeFireSimulator.cs) owns native buffers, shader binding, upload, dispatch, readback, and buffer swapping. The Unity abstraction has parallel responsibilities through [ComputeBufferGrid](../src/Wildfire.Unity/ComputeBufferGrid.cs) and dispatcher contracts. Changes to protocol or tick order must account for both implementations and the [Unity batchmode runner](../src/Wildfire.Unity/UnityBatchmodeProject/Assets/Editor/FireSimBatchmodeRunner.cs).

## Boundary decisions

- Hosts queue changes through the simulator contract. Native buffer writes belong inside the simulator binding, not gameplay services.
- Fire rules live in the shader. C# fixture and visual helpers do not constitute another gameplay simulation.
- Listener-generated changes apply after the current notification pass, on a subsequent tick.
- Cell deltas identify packed-cell transitions; transport consumers need transport observations. Do not assume every field change is represented by a changed packed cell.
- Ash services and visual projections derive from simulator transport. They must not introduce an independent ash inventory in the world.
- Presentation can aggregate and interpolate fields. A visual failure must be distinguishable from simulation or consequence failure.
- Persistence captures state for save/load. Routine field observations should have a separate contract so their cost and lifetime are explicit.

## Adapter services

[Mapping](../src/Wildfire.Timberborn/Mapping/) converts terrain, water, resources, buildings, and footprints into simulation inputs. Resource accounting belongs in the shared adapter catalog. [Consequences](../src/Wildfire.Timberborn/Consequences/) resolves cell changes to game entities and performs native actions. [Ash](../src/Wildfire.Timberborn/Ash/) maintains the derived ash read model and queues collection/application effects. [Beavers](../src/Wildfire.Timberborn/Beavers/) translates field exposure into host behavior. [Visuals](../src/Wildfire.Timberborn/Visuals/) owns particles, field renderers, and overlays.

The [QA bridge](../src/Wildfire.Timberborn/Qa/) exposes a bounded command vocabulary through file polling. Some stimulus handling still lives in runtime classes. The [persistence layer](../src/Wildfire.Timberborn/Persistence/) connects simulator and adapter state to native save/load. These are existing boundaries, not a claim that the runtime classes are already narrowly scoped.

## Compatibility and failure handling

Native references are presently configured in [Wildfire.Timberborn.csproj](../src/Wildfire.Timberborn/Wildfire.Timberborn.csproj) against the local macOS Steam installation. Runtime [compatibility probes](../src/Wildfire.Timberborn/Compatibility/) record service, member, compute, and asset availability. Compile-time API changes can prevent those probes from running at all.

Failures should name the failing capability and preserve evidence. The appropriate response depends on state ownership: a missing compute capability prevents valid simulation; a presentation failure can be isolated; a partially applied inventory or entity mutation requires explicit recovery semantics. Visibility alone does not make a destructive operation transactional. Compatibility and consequence refactors must test those boundaries.

## Validation boundaries

The test projects separate portable contracts, Unity shader execution, and native Timberborn integration. Hosted CI runs the checked-in portable and script tests; shader cases explicitly report skipped when disabled. Fake dispatcher tests verify orchestration expectations, while enabled shader harness runs execute shader behavior. Native API and consequence behavior still requires the native suite and targeted game validation. See [TEST_PLAN.md](TEST_PLAN.md).

This map describes the source reviewed on 2026-09-04. Keep it synchronized with changes to project dependencies and execution ownership. The [previous architecture record](history/2026-09-04/ARCHITECTURE.md) preserves superseded implementation plans.
