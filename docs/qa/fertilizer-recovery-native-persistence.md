# Active fertilizer recovery: native component persistence proof

On 2026-09-08, a CPU-only licensed Unity fixture at immutable `683a5063e0bc1ba43851bf7fdcf8f62ddeb08c32` proved active recovery persistence through the actual native manager, reference serializers, inventories and PostLoad hooks. The inactive recovery prototype remains unbound in production; no route, recovery admission or game save was exercised.

## Inputs and runtime

Fresh Release build in `/Users/jasonkleinberg/repos/wildfire-worktrees/fertilizer-recovery-engine-683a506`: zero warnings/errors. Unity6000.3.6f1 ran `-batchmode -nographics`, `graphics=Null`, with installed Timberborn1.1.2.4 managed assemblies (game Unity6000.5.5f1). The exact production native DLL stayed outside Assets. No shader, compute dispatch, game, Steam, desktop or deployment was involved.

| Input | SHA-256 |
| --- | --- |
| Wildfire.Timberborn.dll | `1ba1b705f6e0ac6512d693b1bd8c6896740da9cd8b7b5b9da7149e6f3cdc993d` |
| Wildfire.Core.dll | `04072fb84d38336e467967b1750a73e4c3a1afa19ae4864c2fb80592bf64c3f0` |
| Corrected probe source | `c63169915220a7d831ef9b7cb5bbe8d8413112da7bf7136849ac16e3dd9c9928` |

Artifacts: `/tmp/wildfire-fertilizer-recovery-engine-corrected/` contains exact command, complete log, hashes, installed native assembly fingerprints, source in `UnityProject/Assets/Editor/FertilizerRecoveryPersistenceProbe.cs`, three actor/destination serialized artifacts and three loaded-state observations. The original failed attempt is preserved separately in `/tmp/wildfire-fertilizer-recovery-engine-run/`.

## Actual native chain

Each case constructed a source actor/destination and a distinct reconstructed actor/destination as real Unity GameObjects. Their actual native ComponentCaches carried supplied initialized EntityComponent identities, living Character/Mortal, Citizen, native inventories/GoodReserver, BehaviorManager/BehaviorAgent, recovery root/executor, and native walker dependencies. Actual manager/root/executor Awake methods ran; executor Awake bound the real cached Transform and subscribed to the native walker event. Positive BaseComponent truth and non-deleted native entities were asserted.

The adult root list and active-return state were explicitly supplied fixture inputs. Actual root PostInitialize inserted recovery into the reviewed native root order and reported RecoveryReady. Native root behaviors were not asked to arbitrate or launch a job. This proves installation into that supplied list, not full native template construction.

Native inventory calls seeded one satchel ash unit and reserved one destination capacity unit. Actual `BehaviorManager.Save` invoked recovery Save; actual `Inventory.Save` and `GoodReserver.Save` used native EntitySaver/SerializedEntity. The actor save contains:

- RunningBehavior: actor Guid plus `FertilizerRecoveryRoot`; RunningExecutorId: `FertilizerRecoveryExecutor`; ReturnToBehavior:false.
- Recovery component: exactly Active:true, Hours:0.25 and the named destination Inventory reference.
- Satchel Inventory physical storage: one FertileAsh.
- GoodReserver: the same destination reference, one FertileAsh, FixedAmount:true, ConsumeGood:false.

After fresh native initialization, actual Inventory.Load, GoodReserver.Load and BehaviorManager.Load used native EntityLoader. The manager resolved the reconstructed actor's root and discovered its exact IExecutor by saved name. Both recovery and reservation references resolved to the reconstructed destination, never an old object. Before PostLoad, recovery was active with deferred-replan flag, physical ash remained1 and native reserved capacity was0. The saved reservation record existed independently of its not-yet-reapplied capacity.

Actual `EntityComponent.PostLoad` then enumerated the actor's real IPostLoadableEntity components. Both fresh order permutations—satchel before reserver, and reserver before satchel—produced reserved capacity **0→1**, unchanged satchel1/destination0, active recovery and an unpoisoned guard. The native GoodReserver death subscription appeared exactly once. Each actor received PostLoad once; idempotence was not asserted.

After those positive cases, a third fresh reconstruction omitted destination registry membership. Native loaders made both destination references obsolete/null and left reserved capacity0, while preserving the beaver-owned running behavior/executor, active state and private ash1. No Tick was invoked, so missing-destination cancellation or successful first-owned replan remains unproved.

## Failure, correction and outcome

Attempt1 exited1 before restore because the fixture used ordinary JsonConvert on SerializedEntity. That exported only its public Id/TemplateName and omitted native components, causing an artifact assertion to fail. Native setup/save had completed; this did not demonstrate a production save defect. Cleanup completed1/1 graph and2/2 GameObjects before exit.

The single causal correction uses actual native `WorldSerializer.SerializeEntity` and `SerializedObjectReaderWriter.WriteJson`, plus native component/property APIs for exact recovery-key/reference assertions. `fixture-only.diff` preserves the change. The native SerializedEntity objects were passed to EntityLoader; the exported JSON is faithful evidence, not a separately tested JSON-decoding roundtrip.

The corrected run passed all three cases and exited0. Cleanup completed **6/6 graphs and12/12 GameObjects, errors0**, before Editor Exit. It explicitly released fixture reservations, disposed the subscribed walk helpers and detached satchel/reserver lifecycle subscriptions; it did not claim full EntityService deletion. No persistent-allocation notice appeared. Full logs retain nonblocking license-token refresh and Unity service-request timeout messages.

This proof closes component-level active manager/reference loading and native reservation PostLoad accounting. It does not establish production decorators, TryLaunch/route selection, district finding, complete character/template initialization, first owned Tick, full world serialization/loading or gameplay activation. No production code changed. The controller returned to waiting for manual desktop unlock, with installed14ef76e still first for the copied returning-save/helmet test.
