# Finite fertilizer native behavior persistence and cancellation

The licensed CPU Unity probe of exact `c8b1426dd157616f8ed5aefcacf5d9ae543f2e71` passed **four cases** on its first attempt. Full native BehaviorManager.Save/Load preserves the actor-owned FertilizerBehavior reference and discovers the fresh finite executor. Its first manager-owned executor Tick cancels saved intent, releases the exact restored stock reservation and conserves loaded private ash. Saved finite application intent cancels by design; native carried ash and reservation accounting remain coherent. This is Stage A of the [finite-job acceptance plan](finite-fertilizer-job.md), not Ready application, physical worker travel, shipping persistent designations or full colony reload.

## Actual native boundary

Each case constructs original and fresh restored GameObject-backed actor/source graphs with actual ComponentCache.InjectDependencies/Initialize. Adult EntityState.Initialized, living Character state, active manager ownership and finite phase are **explicit fixture state**. This does not execute full adult Entity.Initialize, native template instantiation, employment or source-access admission.

The fixture runs actual satchel InventoryInitializer/Awake, BehaviorManager.Awake, finite/recovery executor Awake, recovery root Awake/PostInitializeEntity and native Inventory/GoodReserver persistence. Its root list is supplied from the existing exact recovery-order anchors; installed recovery order is verified. No component interface is filtered out to manufacture save/reference truth.

Fetching starts with one native FertileAsh in source and one native ReserveExactStockAmount reservation (fixed, nonconsuming). Loaded cases first perform actual FertilizerSatchel.TryPickup: source1→0/private0→1, source reserved stock1→0, one completed pickup callback. Ready is then supplied explicitly as the state to save; there is no claimed source arrival, target, employer, route or application.

Actual EntitySaver/SerializedEntity, full BehaviorManager.Save, Inventory.Save and GoodReserver.Save produce the data. Native JSON artifacts show `RunningBehavior = actorGuid:FertilizerBehavior`, `RunningExecutorId = FertilizerExecutor`, and `Wildfire.FertilizerJob` with exactly Phase and Source. Native source/reservation references share the source Guid and exact inventory name. The actual native SerializedEntity object feeds EntityLoader; JSON is produced through native serialization for inspection. This is not an additional JSON-deserializer/world-save round-trip claim.

Fresh native registry/reference serializer resolves the behavior and executor to their reconstructed actor components. Actual manager Load retains phase/_restored, clears _target, and performs no resource write or movement. EntityComponent.PostLoad invokes the native reserver and satchel callbacks in the supplied order. Employer is absent in every restored graph; the cancel-only branch intentionally does not require it.

## Results

| Saved state / source resolution | PostLoad order | Actual pickup callbacks | Source / private after cancel | Source reservation before→after first Tick |
| --- | --- | ---: | --- | --- |
| Fetching / present | Reserver first | 0 | 1 / 0 | 1→0 |
| Loaded Ready / present | Satchel first | 1 | 0 / 1 | 0→0 |
| Loaded Ready / missing | Reserver first | 1 | 0 / 1 | 0→0 |
| Fetching / missing | Satchel first | 0 | 1 / 0 | 0→0 |

Missing-source variants omit the source from the restored native reference registry. Actual obsolete-reference loading resolves it null; the retained fixture source object is only a quantity control. This proves missing-reference handling, not physical source demolition or its district callbacks.

In all cases the first direct **FertilizerExecutor.Tick under actual loaded BehaviorManager ownership** returns Success, clears toIdle, drops application intent/source reference and releases the exact stock reservation if present. A second Tick changes no stock/reservation. LastOutcome stays null; no ash application was invented. Native Worker.JobRunning recognizes the actor-owned IJobBehavior before cancellation. The test does not tick the complete manager arbiter after Success or claim a subsequent native work assignment.

Loaded private stock remains1 and the actual recovery root's HasDepositRequest returns true with RecoveryReady installed. That is recovery request eligibility, not a launched recovery route or delivered deposit. No actual TakeConsumed occurs in this cancellation slice. Save admission remains safe afterward.

## Real native stop/model chain

Cancellation uses the production shared walker driver and exact fingerprint-gated native stop: Walker.StopMoving→PathFollower.StopMoving→MovementAnimator.StopAnimatingMovement→CharacterModel.ResetModelPosition / AnimatedPathFollower.Stop / CharacterRotator.ResetXRotation / StopAnimation.

The fixture calls real CharacterModel.Awake on a supplied model child/status component, CharacterRotator.Awake, MovementAnimator.Awake, and Walker.Awake→PathFollowerFactory.Create. Native model position `(1,2,3)` and rotation `(20,45,10)` are explicit controls. Load/PostLoad leave them unchanged; first cancellation produces position`(0,0,0)` and rotation`(0,45,0)`. Walker.Stopped is true and the native mover is enabled again after its owned pause. No fake Stop adapter is used.

StopAnimation executes its native idle branch because no animation was playing. This is not an actively playing timbermesh-animation/controller stop or travel-animation fit proof. The fixture does not request navigation or call Walker.GoTo.

## Cleanup and provenance

Terminal18942 exited0. Cleanup completed **8/8 graphs and 16/16 root GameObjects, 0 errors**, before Editor.Exit; each actor model child was destroyed with its root. Actual executor DeleteEntity releases/unregisters, both reservation releases run, satchel registration exits and owned death/path subscriptions are removed. No Persistent allocation notice or unexpected exception appears. A Unity Curl42 shutdown cancellation remains in the full log.

- Exact detached build path: `/Users/jasonkleinberg/repos/wildfire-worktrees/finite-fertilizer-engine-c8b1426`. Fresh Release build and metadata-only fixture compile each reported0 warnings/errors. Documentation commits follow the execution pin.
- Run/report: `/tmp/wildfire-finite-fertilizer-persistence-engine-run/` (`unity.log`, four result JSON files, actor/source native save JSON, `outcome.json`, command/controller records, unchanged before/final hashes and native dependency hashes).
- Source: `UnityProject/Assets/Editor/FiniteFertilizerPersistenceProbe.cs`; entry FiniteFertilizerPersistenceProbe.Run; environment WILDFIRE_FINITE_FERTILIZER_PERSISTENCE_PROBE_OUTPUT.
- Native mod SHA256: `22c1ae87f08e03c2b35f021c0513a4bf28cc8d1dc9f5dc58888f29bd5f8a621d`.
- Core SHA256: `2978033ff79cb386b56abf7785f116c740431af22e4160cd52bb954007934881`.
- Probe SHA256: `76793dbd5e7fc3478e18d132b5f6d326e4e646cd71552268e51fd8665d771fdc`.

Unity6000.3.6f1 ran `-batchmode -nographics`, reported graphicsNull; game engine6000.5.5f1 differs. Unmodified production native DLL stayed outside Assets and exact matching Core in Plugins. No game, Steam, desktop, deployment, compute or Stage C normal Runtime Ready application ran. No game/Editor process remained; controller returned to the pending manual-unlock state. Installed14ef76e returning-save/helmet acceptance remains first game action.
