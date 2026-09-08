# Warden behavior ownership across station deletion

The Warden executor and private water inventory live on the beaver, but the old admission returned a bare `Decision.ReleaseWhenFinished(executor)`. Native `WorkerRootBehavior` then made the station the saved running behavior. Deleting that station while its executor remained active could produce a save whose executor state was present but could not be restored.

## Native evidence

Inspected installed Timberborn managed assemblies on 2026-09-08, against source base `2c2ee874ddc785909fa7cc4bb1ca485ab2c95b39`:

- `WorkerRootBehavior.WorkAtWorkplace`, IL `0075–0078`, wraps the workplace result with `Decision.TransferNow(workplaceBehavior, decision)`.
- `Decision.TransferNow`, IL `0000–000b`, preserves an already supplied behavior; otherwise it uses its caller's behavior.
- `BehaviorManager.SaveRunningBehavior`, IL `0000–000b`, checks native `BaseComponent` truthiness before saving the reference. `BaseComponent.op_Implicit` uses the backing Unity GameObject's truthiness.
- `BehaviorManager.SaveRunningExecutor`, IL `0000–0036`, independently saves the executor identity, elapsed time and executor state. It does not require a surviving behavior.
- `EntityService.Delete` calls entity deletion, removes the entity from `EntityRegistry`, then destroys its GameObject. `EntityComponent.InternalDelete` calls deletion hooks on that entity's components. The beaver's Warden executor is not a station component.
- `ReferenceSerializer` persists behavior references as entity Guid plus named component. Missing entity lookup returns an obsolete reference.
- `BehaviorManager.LoadRunningExecutor`, IL `000d–0013`, refuses executor lookup and `IExecutor.Load` unless `_runningBehavior` is non-null. This is the decisive continuity boundary.

Without a reload, native `TickRunningExecutor` does not require a live behavior reference. The Warden tick can detect the missing station, release its reservation and plan retreat. That runtime path does not repair the save dependency: a save during the active return can still lose executor continuation. Exact deletion/return timing in a loaded game remains a live check.

## Correction

`WardenBehavior : Behavior, IJobBehavior` is attached to the adult beaver. Successful station admission returns the marker's explicit ownership transfer. Native WorkerRoot's outer transfer retains that marker. It reports native work, then releases normally after its executor finishes; no new root arbitration or job policy is introduced.

Executor persistence keys, response phases, resource transfers, water quantity, helmet ownership and movement are unchanged. Existing valid saves with a live station behavior continue to load through the same executor contract. This change governs new admissions; it does not migrate or repair already-orphaned historical saves.

The defect concerns active executor continuity, not demonstrated loss of private water. Native inventory persistence is separate. A skipped executor load leaves the sortie un-restored even if its water inventory survives.

## Validation and limits

`WardenBehaviorOwnershipTests` executes installed native methods:

- Native adult template expansion includes exactly one marker.
- Nested `Decision.TransferNow` preserves the actor marker, executor and release semantics; actual `Worker.JobRunning` remains true through `IJobBehavior`.
- Actual manager executor-save calls the production Warden save even with no behavior. Actual executor-load skips a missing behavior; both the legacy live-station reference and new marker restore the production executor to Returning with deferred route replanning.
- Actual native reference serialization binds the new marker to the worker Guid. With supplied registry membership containing only that worker, native deserialization resolves the marker and reports the old station reference obsolete.

The initial regression run was **3 failures / 2 passes**, including the missing native template marker; tests needing the absent new type failed at construction. After correction and the explicit legacy-reference control, **6 focused cases pass**. Full Release native suite: **1,449 passed, zero failures/skips**, exit 0. No Unity, game, desktop, deploy or QA-save mutation was performed for this source correction.

The fixtures supply component caches, manager state and registry membership. They do not fabricate Unity GameObject liveness or execute whole-world deletion/save/loading. The deleted-GameObject save branch is established by native IL; the missing-reference and executor-load consequences are executed independently. A later copied-world test should demolish a station during an active return, save/reload, and verify retained charge plus safe continuation. The separately installed `14ef76e` returning-save/helmet test remains the first pending live task after manual desktop unlock.

Artifacts: `/tmp/wildfire-warden-behavior-owner-audit/` contains native IL, native assembly SHA-256 values, `red.log`, `green.log`, and `full-native.log`. The initial focused green log predates the stronger worker-Guid roundtrip; the full native log includes that final test.
