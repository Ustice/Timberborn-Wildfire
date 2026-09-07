# Wildfire Architecture

Wildfire separates simulation state and rules from host gameplay. Core owns the shared GPU protocol and tick coordinator; Unity and Timberborn supply compute backends. The shared shader remains authoritative for fire spread.

## Ownership and entry points

| Project | Responsibility | Entry points |
| --- | --- | --- |
| Core | Packed formats, parameters, fixtures, queued changes, GPU encoding, tick coordination | [FireSimContracts.cs](../src/Wildfire.Core/FireSimContracts.cs), [FireSimStepCoordinator.cs](../src/Wildfire.Core/FireSimStepCoordinator.cs), [FireSimGpuProtocol.cs](../src/Wildfire.Core/FireSimGpuProtocol.cs) |
| CLI | Seeded scenario preview and fixture export | [Program.cs](../src/Wildfire.Cli/Program.cs) |
| Unity | Portable buffer abstractions, compute backend, shader sources, batchmode fixture execution | [UnityComputeFireSimulator.cs](../src/Wildfire.Unity/UnityComputeFireSimulator.cs), [FireSim.compute](../src/Wildfire.Unity/FireSim.compute) |
| Timberborn | Native compute backend, world import, lifecycle, gameplay, presentation, persistence, QA bridge | [Runtime/](../src/Wildfire.Timberborn/Runtime/), [Simulation/](../src/Wildfire.Timberborn/Simulation/) |

Core references neither Unity nor Timberborn. Both C# simulator implementations use the same `FireSimStepCoordinator`, `FireSimChangeQueue`, and `FireSimGpuProtocol`. Native buffer allocation, shader binding, and readback remain backend responsibilities. The Unity batchmode runner binds the shared protocol separately for shader fixtures.

## Simulation flow

```text
Host inputs -> shared queued-change batch
  -> reset delta counter
  -> upload and apply external changes
  -> consume uploaded batch and advance tick
  -> simulate full grid
  -> read compact deltas
  -> swap cell/transport buffers
  -> read optional collection receipt and commit admitted host input
  -> notify listeners
```

The [coordinator](../src/Wildfire.Core/FireSimStepCoordinator.cs) owns this order and exposes `IFireSimStepBackend`. The [queue](../src/Wildfire.Core/FireSimChangeQueue.cs) preserves commands beyond the upload capacity, removes invalid indices when the batch is consumed, and rejects stale or foreign batches. Once external changes have been applied, later-stage failure does not replay those inputs. That policy does not make a GPU tick transactional or guarantee safe retry after every failure.

`IFireSimStepInputSimulator.TryTickWithInput` admits one extra input only when the actual next batch has space. Rejection returns null without ticking; the caller can run the ordinary tick. An admitted input is appended after queued commands but never stored in their queue. Its host commit callback runs after readback and buffer swap, before listeners, even when the input causes no delta. Reentrant ticks are rejected; callbacks may queue inputs for a later tick.

An admitted-step failure reports `NotApplied` before entry to the backend apply stage, `Indeterminate` from entry to apply through an incomplete host commit, or `Committed` after the callback finishes. The original exception is retained. This is a delivery boundary, not rollback: after `Indeterminate`, the host must stop dispatch and prevent inconsistent saves. `Committed` must never replay or rewind host consumption, even if a listener fails. Native stock, worker state, and save guards remain host responsibilities. Invalid cell indices are rejected before admission.

`IFireSimAshCollectionSimulator.TryCollectAsh` uses that same admission and commit boundary for conditional clean-ash removal. The GPU records the amount actually removed in the admitted command; the coordinator validates its identity and receipt before invoking the host callback. Generic queued commands cannot request collection. Later simulation changes do not change that receipt, so host production does not infer removal from a stale observation or a final field difference. See [the receipt protocol](reference/ash-collection-receipts.md) for bit layout, failure semantics, and executed shader evidence. The [native ash-worker prototype](qa/ash-native-harvest-prototype.md) consumes that receipt; gameplay validation remains separate.

The [protocol](../src/Wildfire.Core/FireSimGpuProtocol.cs) encodes all supported cell and transport overrides, including smoke and contamination. `AddWater` occupies bits 23–24 of `AddFields`; the four-word/16-byte command layout is unchanged. Both compute backends and the fixture runner pass those same encoded words to the shared shader. Delta capacity is `cellCount + changeCapacity`: external changes and simulation may each append a record for the same cell. Consumers must not assume one record per cell per tick.

[TimberbornComputeFireSimulator](../src/Wildfire.Timberborn/Simulation/TimberbornComputeFireSimulator.cs) implements the native backend. [ComputeBufferGrid](../src/Wildfire.Unity/ComputeBufferGrid.cs) supports the portable Unity backend. Changes to bindings also need the [batchmode runner](../src/Wildfire.Unity/UnityBatchmodeProject/Assets/Editor/FireSimBatchmodeRunner.cs) checked against the shared format.

## Runtime lifecycle

[TimberbornRuntimeInitialization](../src/Wildfire.Timberborn/Runtime/TimberbornRuntimeInitialization.cs) owns `Unloaded`, `WaitingForWorld`, `Initializing`, `Ready`, `Unsupported`, and `Failed` states. Only readiness waits retry automatically. Unsupported dimensions and initialization failures remain terminal until explicit load/reset; size is checked before expensive world import.

[TimberbornFireRuntimeInitializer](../src/Wildfire.Timberborn/Runtime/TimberbornFireRuntimeInitializer.cs) supplies native readiness and import inputs. [TimberbornFireRuntime](../src/Wildfire.Timberborn/Runtime/TimberbornFireRuntime.cs) owns native lifecycle and service composition. [TimberbornFireSystem](../src/Wildfire.Timberborn/Runtime/TimberbornFireSystem.cs) owns its simulator, queued host changes, dispatch, delta consumption, and the shared sustained-input scheduler.

Runtime composition builds a complete `TimberbornRuntimeBindings` value and prepares an unpublished simulator/renderer candidate. Import restoration, consequence restoration, and rendering setup complete before the live system, dispatcher, and bindings are assigned. Public dispatch remains gated by `Ready`. A preparation failure disposes candidate resources, clears transient effects, and leaves the original saved encoding available. Load and unload explicitly clear the previous session.

`TimberbornRuntimePersistence` preserves the original saved payload until a ready runtime successfully captures and encodes replacement state. Before readiness, an existing payload is written back verbatim; absent state is not replaced with an empty snapshot. A payload that cannot be read blocks save. Corrupt or unsupported encoded state fails initialization before import while preserving its original bytes. These ownership guarantees do not make later native world consequences transactional.

## Gameplay and QA inputs

[TimberbornSustainedIgnitionScheduler](../src/Wildfire.Timberborn/Runtime/TimberbornSustainedIgnitionScheduler.cs) repeats a finite input sequence and yields when other changes are already queued. It serves the player burn tool as well as QA. FireSystem advances and resets it directly.

[TimberbornQaController](../src/Wildfire.Timberborn/Qa/TimberbornQaController.cs) owns forced stimuli, pending burn-damage probes, repeated QA cycles, and burn-duration evidence. [TimberbornQaTargetSelector](../src/Wildfire.Timberborn/Qa/TimberbornQaTargetSelector.cs) selects imported or native consequence targets without queueing changes. Callers use `fireSystem.Qa`; the controller only receives grid/target observations and a queued-change boundary, not native buffer access.

At each FireSystem tick, QA prepares pending spend inputs, the shared scheduler queues eligible sustained inputs, and QA completes its cycle preparation. Simulation and delta consumers then run before successful-tick evidence advances. Reinitialization and disposal clear pending stimulus state.

[Adapter presets](../src/Wildfire.Timberborn/Simulation/TimberbornFireSimParameterPresets.cs) specify `SustainedIgnitionDispatchTicks`: 12 by default, 96 for `slow-reactable`. This is forced-input duration for tools and QA, not shader cadence. Status exposes `sustained_ignition_dispatch_ticks`. The old shader-unused `FireCellStepIntervalTicks` parameter was removed.

## Native resource work

[FireSafetyField](../src/Wildfire.Timberborn/FireSafety/FireSafetyField.cs) shares observed cells/transport, risk checks, installed-route validation and bounded escape across wardens, ash harvesters, carrying emergencies and borrowed duty. The runtime observation cache refreshes only when the simulator instance or committed field revision changes. [WardenTargetSelector](../src/Wildfire.Timberborn/FireResponse/WardenTargetSelector.cs) separately owns warden range, candidate ordering and approach geometry; its shortlist refreshes when the observed cell collection changes. Safety thresholds and native path validation remain provisional gameplay policy, with no new simulator rules or per-actor readback.

[NativeResourceCoordinator](../src/Wildfire.Timberborn/Resources/NativeResourceCoordinator.cs) admits at most one Warden water input or ash collection per simulation step, alternating successful admissions between those two producer kinds. Native executors retain pending work. The shared transaction guards paired native mutations, blocks reentrant saving, and retains indeterminate state until actual world load. Its recovery notification runs independently of frozen fire dispatch.

[AshHarvestBehavior](../src/Wildfire.Timberborn/Ash/AshHarvestBehavior.cs) lives on the worker. Native `Decision.TransferNow` transfers ownership from the gatherer workplace to that behavior, so a return trip does not depend on the workplace surviving. Its registered executor owns physical approach, receipt, return and guarded deposit. Native carrier and capacity reservation hold quantity and destination; the saved phase is not a second inventory. A zero receipt creates no goods. A successful receipt creates exactly one Uncountable FertileAsh, recorded as production exactly once at deposit. Ordinary native death retains its native cargo-loss semantics. Blocked storage and critical needs remain prototype limitations documented in [the live acceptance guide](qa/ash-native-harvest-prototype.md).

## Field observations and persistence

`CellDelta` contains packed-cell transitions, not full transport state. [ITimberbornTransportFieldReader](../src/Wildfire.Timberborn/Simulation/ITimberbornTransportFieldReader.cs) provides an explicit synchronous transport snapshot. [TimberbornAshFieldSynchronizer](../src/Wildfire.Timberborn/Ash/TimberbornAshFieldSynchronizer.cs) reads it at most once per successfully synchronized tick and updates the derived ash model.

Routine ash synchronization reads one transport buffer. Save capture separately reads cells and transport through the [persistence boundary](../src/Wildfire.Timberborn/Persistence/). The observation still allocates and performs synchronous full-grid readback; it is not asynchronous or incremental. Gameplay and rendering derive ash from simulator transport and queue mutations back rather than maintaining independent authority.

## Native equipment assets

[WardenAttachmentAssetProvider](../src/Wildfire.Timberborn/FireResponse/Presentation/WardenAttachmentAssetProvider.cs) supplies cached `GameObject` wrappers containing native `TimbermeshDescription` components for four exact Warden attachment names. It registers in Bootstrapper context so the native asset pipeline can resolve them during setup. Reset destroys the provider-owned wrappers; imported clones have their own lifetime. Native mesh data remains packaged under `Data/Equipment/FireResponse` using existing Ironteeth material names. The [isolated Unity probe](../scripts/qa/warden-attachments/README.md) covers actual asset lookup, import and reset. Character attachment, native atlas optimization and animated fit still require further proof.

## Native compatibility and consequences

[Timberborn.Managed.props](../build/Timberborn.Managed.props) supplies a shared `TimberbornManagedPath`, defaulting to the macOS Steam installation. The adapter and native tests import it. Set `-p:TimberbornManagedPath=/path/to/Managed` for another installation; missing assemblies produce a direct diagnostic. This selects assemblies, not a platform-support promise.

[TimberbornInventoryMutations](../src/Wildfire.Timberborn/Compatibility/TimberbornInventoryMutations.cs) distinguishes consuming goods, restoring existing stock, carrying uncountable harvest, and recording production at deposit. [TimberbornConstructionRebuild](../src/Wildfire.Timberborn/Compatibility/TimberbornConstructionRebuild.cs) captures a full blueprint and placement before deletion and validates the recreated construction state. Root, dependent, and overlapping-path requests are prepared before destructive rebuild operations. Native creation can still fail afterward; preflight is not rollback.

[Compatibility probes](../src/Wildfire.Timberborn/Compatibility/) record runtime capabilities. Compile-time API changes may prevent probes from running. [Mapping](../src/Wildfire.Timberborn/Mapping/) translates world observations; [consequences](../src/Wildfire.Timberborn/Consequences/) perform native actions; [beaver services](../src/Wildfire.Timberborn/Beavers/) translate exposure; [visuals](../src/Wildfire.Timberborn/Visuals/) present fields. Full Unity `EntityId` values identify cached native materials and textures.

## Validation boundaries

Portable tests exercise the actual shared queue, encoder, and coordinator, plus fake-backend integration. Native tests exercise adapter contracts against installed assemblies. Enabled shader tests execute HLSL; disabled cases report skipped. Native API effects and rendered quality still need targeted game validation. See [TEST_PLAN.md](TEST_PLAN.md).

Keep this map synchronized with ownership changes. The [previous architecture record](history/2026-09-04/ARCHITECTURE.md) preserves superseded plans and execution descriptions.
