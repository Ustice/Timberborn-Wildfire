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

The [protocol](../src/Wildfire.Core/FireSimGpuProtocol.cs) encodes all supported cell and transport overrides, including smoke and contamination. `AddWater` occupies bits 23–24 of `AddFields`; the four-word/16-byte command layout is unchanged. Both compute backends and the fixture runner pass those same encoded words to the shared shader. Delta capacity is `cellCount + changeCapacity`: external changes and simulation may each append a record for the same cell. Consumers must not assume one record per cell per tick. Delta rows use five uints/20 bytes: index, old packed cell, new packed cell, originating target id and originating local-slot id. The identities are sampled when the GPU emits the transition, so a material handoff cannot make an earlier record refer to the incoming slot. Readback preserves emitted order; order across different simulation cells is unspecified.

[TimberbornComputeFireSimulator](../src/Wildfire.Timberborn/Simulation/TimberbornComputeFireSimulator.cs) implements the native backend. [ComputeBufferGrid](../src/Wildfire.Unity/ComputeBufferGrid.cs) supports the portable Unity backend. Changes to bindings also need the [batchmode runner](../src/Wildfire.Unity/UnityBatchmodeProject/Assets/Editor/FireSimBatchmodeRunner.cs) checked against the shared format.

## Material ownership and native consequences

[FireSimMaterialHandoffSession](../src/Wildfire.Core/FireSimMaterialHandoffSession.cs) owns accepted active slots, known identities, inactive GPU archives and attempted transaction tokens. Whole-batch validation and GPU receipts govern material changes; a desired native projection is not an accepted simulator state. Hidden material retains its actual fuel and companion fields. Administrative handoffs do not create burn-damage deltas. See [the handoff protocol and executed proof](qa/material-handoff-prototype.md).

[TimberbornNativeMaterialRegistry](../src/Wildfire.Timberborn/Mapping/TimberbornNativeMaterialRegistry.cs) owns durable Guid/local-footprint bindings and desired native projections. Local slot identity survives movement, rotation and overlap; retained target lookup includes removed owners. These bindings do not own remaining fuel or authorize replacing a known slot with fresh material.

[TimberbornOwnedDeltaConsumer](../src/Wildfire.Timberborn/Consequences/Owned/TimberbornOwnedDeltaConsumer.cs) resolves a complete batch to canonical native owners before effects. Complete target/slot/cell transition chains are validated before mutation; exact replay is distinguished from genuine loss after gain. One body-damage pass sums accepted losses before raw tree, crop and storage effects under the same resource guard used by world saves. Storage processes each contribution against current native stock and emits hazards from actual completed removals at that contribution's cell. Each native mutation rechecks the original entity. A callback can delete a later target; that does not authorize an effect on its replacement. Body liveness is independent of inventory availability, and structure rollback remains explicitly unavailable in this route.

[Current material compilation](../src/Wildfire.Timberborn/Mapping/TimberbornMaterialProjectionCompiler.cs) derives a definition and footprint from captured native body and stock facts. It receives no damage-accounting selections or remaining-fuel authority. The initial-body compiler separately selects accounting inputs once; restored body profiles remain saved authority even when current harvest or stock is smaller. Known slots always use retained GPU material. Missing profiles, unsupported roles and lifecycle transitions remain explicit admission gaps.

The [initial session constructor](qa/owned-initial-session-proof.md) stages capture, material projection, a fixed environmental baseline, initial ambient fields, bindings, accounting and a witnessed consumer under one guard. Backend readback must match the entire intended snapshot, and a final native/environment capture must match the observation used to construct it. The [actual Unity/Metal probe](qa/owned-initial-session-engine-proof.md) establishes construction and encoded save/restore fidelity with supplied native state; it does not establish loaded-colony behavior or restored-world readiness.

These owned components are implemented and independently exercised, but the production initializer still uses legacy world import and dispatch. Activation requires coherent native lifecycle publication and consequence-history restoration; neither the material registry nor complete GPU saves alone fulfills those requirements. [The aggregate's proof and limits](qa/owned-consequence-batch.md) describe this boundary.

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

[Beaver field dispatch](../src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldBehaviorDispatcher.cs) keeps unsupported capabilities separate from successful native effects. The worker-speed actuator supports smoke slowdowns and explicitly rejects fire-heat actuation as unsupported. Unsupported observations retain the previous successful smoke cleanup handle, without advancing its applied state or cooldown. Native failures still halt dispatch. The application budget skips cooling/unsupported actors and orders eligible work by never-applied, oldest persisted successful tick, then ID; this avoids fixed-prefix starvation across advancing ticks without a second scheduler ledger. See [dispatch proof and limits](reference/beaver-exposure-dispatch.md).

[FireSafetyField](../src/Wildfire.Timberborn/FireSafety/FireSafetyField.cs) shares observed cells/transport, risk checks, installed-route validation and bounded escape across wardens, ash harvesters, carrying emergencies and borrowed duty. The runtime observation cache refreshes only when the simulator instance or committed field revision changes. [WardenTargetSelector](../src/Wildfire.Timberborn/FireResponse/WardenTargetSelector.cs) separately owns warden range, candidate ordering and approach geometry; its shortlist refreshes when the observed cell collection changes. Safety thresholds and native path validation remain provisional gameplay policy, with no new simulator rules or per-actor readback.

[NativeResourceCoordinator](../src/Wildfire.Timberborn/Resources/NativeResourceCoordinator.cs) admits at most one Warden water input or ash collection per simulation step, alternating successful admissions between those two producer kinds. Native executors retain pending work. The shared transaction guards paired native mutations, blocks reentrant saving, and retains indeterminate state until actual world load. Its recovery notification runs independently of frozen fire dispatch.

[AshHarvestBehavior](../src/Wildfire.Timberborn/Ash/AshHarvestBehavior.cs) lives on the worker. Native `Decision.TransferNow` transfers ownership from the gatherer workplace to that behavior, so a return trip does not depend on the workplace surviving. Its registered executor owns physical approach, receipt, return and guarded deposit. Native carrier and capacity reservation hold quantity and destination; the saved phase is not a second inventory. A zero receipt creates no goods. A successful receipt creates exactly one Uncountable FertileAsh, recorded as production exactly once at deposit. Ordinary native death retains its native cargo-loss semantics. Blocked storage and critical needs remain prototype limitations documented in [the live acceptance guide](qa/ash-native-harvest-prototype.md).

## Field observations and persistence

`CellDelta` contains packed-cell transitions, not full transport state. [ITimberbornTransportFieldReader](../src/Wildfire.Timberborn/Simulation/ITimberbornTransportFieldReader.cs) provides an explicit synchronous transport snapshot. [TimberbornAshFieldSynchronizer](../src/Wildfire.Timberborn/Ash/TimberbornAshFieldSynchronizer.cs) reads it at most once per successfully synchronized tick and updates the derived ash model.

Routine ash synchronization reads one transport buffer. The observation still allocates and performs synchronous full-grid readback; it is not asynchronous or incremental. Gameplay and rendering derive ash from simulator transport and queue mutations back rather than maintaining independent authority.

[FireSimSnapshot](../src/Wildfire.Core/FireSimSnapshot.cs) captures cells, transport, companion fields, target/slot identities, inactive archives, authority tokens, parameters, seed, tick and pending ordinary inputs. Both backends restore into newly constructed simulators. Capture rejects uncertain simulator state and interleaving mutation; a legacy cells-only restore explicitly lacks complete material history. [The actual native-factory probe](qa/material-snapshot-prototype.md) verifies preserved archives and exhausted fuel after serialization and new construction.

[TimberbornOwnedMaterialSnapshot](../src/Wildfire.Timberborn/Persistence/TimberbornOwnedMaterialSnapshot.cs) pairs that simulator state with native Guid/local-slot bindings in the WF2 codec. Production saves still use WF1; the initializer preserves and refuses unsupported WF2 adoption. The private OWNED4 schema retains canonical retained/retired owners, immutable body profiles, natural request progress and typed storage credits, plus original static native-definition and inventory-declaration witnesses; BURN remains the only saved body-damage value. Exact inventory roles and component names persist even after retirement, while current quantities, reservations, recipes and availability remain outside static compatibility. OWNED1 lacks consequence history, OWNED2 lacks body witnesses, and OWNED3 lacks original inventory declarations. Their original encodings remain preserved, but none authorizes complete current-world restoration. A staged world session derives accounting from saved profiles, validates exact saved Guids and native definitions independently of current harvest quantities, isolates mutable body state and disposes its new simulator on later failure. [Native restore fixtures](qa/owned-native-restore.md) cover disabled retained bodies, reduced yields and static incompatibilities. The complete OWNED4 restore entry reconstructs the rich baseline and every supported retained desired projection without replaying ambient fields, reallocating bindings or recalculating initial accounting. New owners and unproved native states reject explicitly. The older body-only entry is named `PrepareDiagnosticRestore`; its partial registry is not an executable desired world. Material reconciliation and single publication of the restored runtime remain required. Charred presentation rehydration uses visual-only operations and never repeats harvest, death or resource destruction. [The save bridge](qa/owned-material-persistence-bridge.md) distinguishes codec proof from complete world restoration.

[Explicit native retirement](qa/owned-native-retirement-prototype.md) requires the exact native registry entry to be absent and validates canonical retention after callbacks. It preserves origins and GPU archives; it does not itself detach active GPU material. The [desired-material planner](qa/desired-material-reconciliation.md) generalizes the earlier retirement-only prototype to moves, swaps, hidden reveals and unowned baseline changes. It requires a projection for every retained owner, including hidden contributors; incomplete observations cannot authorize removal. [Four actual Metal cases](qa/desired-material-planner-gpu-proof.md) establish source closure, ordered input provenance, exhausted archive reveal and preserved baseline ambient state. The planner executes no step; the existing consumer-owned step entry supplies guarded delivery. Production must finish cleanup and native output delivery before allowing ordinary steps; unexpected GPU rejection or ambiguous delivery requires reload.

## Native equipment assets

[WardenAttachmentAssetProvider](../src/Wildfire.Timberborn/FireResponse/Presentation/WardenAttachmentAssetProvider.cs) supplies cached `GameObject` wrappers containing native `TimbermeshDescription` components for four exact Warden attachment names. It registers in Bootstrapper context so the native asset pipeline can resolve them during setup. Reset destroys the provider-owned wrappers; imported clones have their own lifetime. Native mesh data remains packaged under `Data/Equipment/FireResponse` using existing Ironteeth material names. The [isolated Unity probe](../scripts/qa/warden-attachments/README.md) covers actual asset lookup, import and reset. Character attachment, native atlas optimization and animated fit still require further proof.

## Native compatibility and consequences

[Timberborn.Managed.props](../build/Timberborn.Managed.props) supplies a shared `TimberbornManagedPath`, defaulting to the macOS Steam installation. The adapter and native tests import it. Set `-p:TimberbornManagedPath=/path/to/Managed` for another installation; missing assemblies produce a direct diagnostic. This selects assemblies, not a platform-support promise.

[TimberbornInventoryMutations](../src/Wildfire.Timberborn/Compatibility/TimberbornInventoryMutations.cs) distinguishes consuming goods, restoring existing stock, carrying uncountable harvest, and recording production at deposit. The legacy reconstruction path uses [TimberbornConstructionRebuild](../src/Wildfire.Timberborn/Compatibility/TimberbornConstructionRebuild.cs) to capture blueprint and placement before deletion. Preparation is not rollback, stock conservation or replacement-identity proof. That path is excluded from owned dispatch while exact repair lineage and material accounting are rebuilt. [Native closure fixtures](qa/native-structure-closure-proof.md) separately establish blocker ownership, player-pause independence and callback failure semantics; they do not implement material-funded repair.

[Compatibility probes](../src/Wildfire.Timberborn/Compatibility/) record runtime capabilities. Compile-time API changes may prevent probes from running. [Mapping](../src/Wildfire.Timberborn/Mapping/) translates world observations; [consequences](../src/Wildfire.Timberborn/Consequences/) perform native actions; [beaver services](../src/Wildfire.Timberborn/Beavers/) translate exposure; [visuals](../src/Wildfire.Timberborn/Visuals/) present fields. Full Unity `EntityId` values identify cached native materials and textures.

## Validation boundaries

Portable tests exercise the actual shared queue, encoder, and coordinator, plus fake-backend integration. Native tests exercise adapter contracts against installed assemblies. Enabled shader tests execute HLSL; disabled cases report skipped. Native API effects and rendered quality still need targeted game validation. See [TEST_PLAN.md](TEST_PLAN.md).

Keep this map synchronized with ownership changes. The [previous architecture record](history/2026-09-04/ARCHITECTURE.md) preserves superseded plans and execution descriptions.
