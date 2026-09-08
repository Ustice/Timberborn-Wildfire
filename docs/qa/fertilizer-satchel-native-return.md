# Fertilizer satchel: positive native return in headless Unity

On 2026-09-08, the public `FertilizerSatchel.TryReturn` passed a CPU-only licensed Unity probe against immutable source `460a0cf648e91bb41a7972043a5e08f3f53da954`. This closes the positive GameObject-liveness and native capacity-release boundary left unproved by external .NET fixtures. It does not activate a fertilizer worker or demonstrate recovery movement.

## Exact runtime and source

The source was freshly built in the clean detached worktree `/Users/jasonkleinberg/repos/wildfire-worktrees/fertilizer-return-engine-460a0cf`. The separate project ran Unity **6000.3.6f1**, `-batchmode -nographics`, `graphics=Null`, using the installed Timberborn managed assemblies from game **1.1.2.4 / Unity6000.5.5f1**. The production mod DLL remained unchanged outside Assets; the existing reflection dependency loader reused native assemblies while Unity supplied its own engine runtime.

SHA-256 fingerprints:

| Input | SHA-256 |
| --- | --- |
| Wildfire.Timberborn.dll | `138ff030cff43b0d21e694c3a61b1e36143b5ebd3aab4d0221b843282d157a74` |
| Wildfire.Core.dll | `5a2ff6d6470226d21b4872ac8045b43b7369c1b3d211c1c9f9e22729448f2b28` |
| FertilizerSatchelReturnProbe.cs | `af4f3ccf5aa829f9be3c934d50e208ec65acaf59a501c7cd85206f691d1b69a9` |

Artifacts are in `/tmp/wildfire-fertilizer-return-engine-run/`: exact `command.json`, full `unity.log`, `native-build.log`, `hashes-before.json`, `hashes-final.json`, installed native assembly fingerprints, both result JSON files, and the source under `UnityProject/Assets/Editor`. The probe reused the previous two-inventory fixture's real ComponentCache attachment, reflection loader and cleanup order; it did not reuse its simulator, shader or world-session setup.

## Actual proof

Each case used two real Unity GameObjects, with actual native ComponentCache initialization and enabled BaseComponents. The actor carried a supplied initialized EntityComponent, living Character/Mortal, Citizen, GoodReserver, satchel and native inventory. A separate initialized entity owned the destination inventory. Character Alive, EntityState and component dependencies were explicit fixture setup. The actual public satchel Awake and native InventoryInitializerFactory/FertilizerSatchelInventoryInitializer performed their unchanged wiring. The fixture asserted positive native BaseComponent/GameObject truth and non-deleted native entities before the return.

The fixture seeded one private FertileAsh unit with a native inventory call. **Actual GoodReserver.ReserveCapacity** established the fixed, non-consuming destination reservation; no reservation record or reserved-capacity count was fabricated. The public return then invoked actual `GoodReserver.UnreserveCapacity`, including its positive Unity truthiness and non-deleted-entity branches, and the actual native inventory transfer.

| Observation | Positive return | Fresh disabled destination |
| --- | --- | --- |
| Public return | true | false |
| Private stock | 1 → 0 | remains 1 |
| Destination stock | 0 → 1 | remains 0 |
| Native reserved capacity | 1 → 0 | remains 1 |
| Reservation record | cleared | preserved |
| Phase commits | exactly 1 | 0 |
| Resource guard | determinate | determinate |

The positive phase callback saw both the completed transfer and cleared stock/capacity reservation records. `ThrowIfSaveUnsafe` rejected capture at that point, establishing that the phase ran inside the resource mutation guard. After return, both the save guard and an actual `CaptureAtRest` call succeeded with exactly one total unit. A second public return on the empty satchel returned false with no second phase commit. No actual game-save serialization was performed.

After observing the disabled refusal, cleanup called the actual native `UnreserveCapacity` against the still-live disabled destination and verified its reserved capacity reached zero. There was no callback-replacement/adversarial case in this bounded run.

## Outcome and limits

First attempt **passed**, process exit **0**. Cleanup finished **2/2 satchels and 4/4 GameObjects, errors0**, before `EditorApplication.Exit`; no persistent-allocation notice appeared. The existing license permitted execution without UI. The full log retains an access-token refresh error and Unity service request timeout; they did not prevent runtime execution or assertions and are not attributed to the mod.

This proves real supplied native components and resource methods in the Editor runtime. It does not prove whole-prefab initialization, district assignment, worker admission or arrival, recovery path execution, inventory/world save loading, GPU application or gameplay publication. No game, Steam, desktop, deployment or QA-save action occurred. After the engine exited, the sole-controller lock returned to awaiting manual desktop unlock; the already installed `14ef76e` returning-save/helmet test remained first in the game queue.
