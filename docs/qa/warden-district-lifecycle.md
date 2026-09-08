# Warden district lifecycle safety

Source: `d05c8bd` (shared guard) and `43cf606` (Warden registration), based on `5b57ec0`. This is an independent correction; it does not depend on the held fertilizer satchel or conditional ash application protocol.

## Confirmed failure and correction

On the exact earlier `2fd29af` build, actual `WardenEquipment.DeleteEntity` removed its native `DistrictResourceCounter` proxy and then called `DistrictInventoryRegistry.Remove`. An injected native `InventoryUnregistered` observer threw after counter removal. The exception escaped deletion while the shared coordinator still permitted saving. A second execution caught that exception inside `CaptureAtRest`, which also returned success. Private inventory status does not prevent native district registration events.

Registration, inventory enable, district changes and normal exit cleanup now use the existing resource guard. A failed or rejected irreversible cleanup marks that same guard indeterminate. Native death/deletion continues; local subscriptions are retired and uncertain native unregisters are never retried. There is no unguarded cleanup fallback. Busy or already-indeterminate exit may consequently retain incomplete native registration until the world is reloaded; saving and further guarded resource work remain blocked.

Native `Character.KillCharacter` sets `Alive=false` before invoking `Died`. The native `Citizen.OnDied` subscriber can run first and invoke `UnassignDistrict`, which invokes `ChangedAssignedDistrict`. The Warden district handler therefore recognizes an already-dead character and uses non-aborting exit cleanup, without waiting for its own `Died` callback.

The guard checks its indeterminate state after a callback returns. A caught lifecycle failure cannot become successful capture, transfer or simulator step, including a step returning no admission. Existing throwing actions retain their original exception; read failures alone do not poison the session. Latches release in `finally`. No water refund, consumption, second quantity ledger, executor phase or scheduling rule is introduced.

## Native regression boundary

`NativeWardenDistrictLifecycleTests` runs the production equipment and coordinator against actual installed native inventory, character, citizen, counter, registration-event and entity-deletion implementations. The native equipment initializer creates the one-unit Water inventory and its allowed-good definition. Assertions check native Water remains one after failure/death and repeated cleanup.

The three original regressions failed before the correction: unguarded deletion escaped its observer exception, capture returned success, and `Citizen.OnDied` aborted subsequent death subscribers. Expanded cases exercise:

- Actual `EntityComponent.Delete` reaches the later native Citizen cleanup and `EntityDeletedEvent` despite a Warden cleanup observer failure or busy guard.
- Actual `Character.KillCharacter` reaches later native death subscribers when Citizen's district event precedes Warden's own death callback.
- Busy capture, busy transfer, previously poisoned death, repeated delete, living district failure and successful unassignment.
- Actual `Inventory.Enable` mutates before an observer throws, and death during enable prevents subsequent district writes.
- Shared-guard capture, transfer and returned-step postconditions, original exception preservation and ordinary read-failure semantics.

The fixture supplies component caches and native dependencies explicitly. It executes actual native methods; it does not manufacture Unity liveness or execute GameObject destruction. Its original district registry/counter association is supplied to isolate the cleanup seam. Successful registration into a real rendered district, full template instantiation, and ordinary Warden travel remain live acceptance boundaries. No game, engine, desktop or deployment action was taken for this correction.

The native Character constructor sets `Alive=true` before assigning its injected dependencies. Character has no `InitializeEntity`; its Awake only resolves NamedEntity. The regression uses the actual constructor and invokes equipment initialization before Character Awake/PreInitialize, then checks postload registration is still active. The loaded value comes from Character.Load before the later initialization/postload phases, not a guessed component ordering.

## Load reset ordering

Installed `SingletonLifecycleService.LoadAll` calls `LoadSingletons`, `LoadNonSingletons`, `PostLoadSingletons`, then `PostLoadNonSingletons`. `TimberbornFireRuntime.Load` is an `ILoadableSingleton` callback. Native `WorldEntitiesLoader.LoadNonSingletons` instantiates entities and calls `EntitiesLoader.LoadAndInitialize`; its postloader calls entity `PostLoad`.

The ordinary runtime world-load reset therefore precedes Warden initialization and postload registration. It does not later clear a newly detected entity-registration failure. Existing reset calls remain limited to actual world load/unload. This is installed-native IL evidence; no new world-loading policy or reset bypass was added.

## Independent review disposition

Actual tools-disabled Claude reviewed the immutable `2fd29af` packet: exit 0, `is_error=false`, 289.428 seconds. Its two lifecycle allegations were disproved: native entity loading invokes `IPostInitializableEntity` (including the helmet), and native `BehaviorManager` saves/loads the registered `IExecutor` directly. Neither warrants an added persistence interface or callback.

Its useful recommendation is to extend existing composed station/adult contract fixtures, checking exact native roles and deliberate multiplicity as a unit. Presence checks do not replace actual lifecycle and live job proofs. No further missing native decorator was established by this review.

Full original prompt, output, terminal record, independent IL and counterexample: `/tmp/wildfire-warden-composition-claude/REPORT.md`. Its prompt's historical public-input method label was inaccurate; the actual boundary was `DistrictInventoryPicker.InventoryIsTaking` → `IInventoryValidator.ValidInventory`, supplied by `Emptiable`. The preserved review report records that erratum rather than rewriting the prompt.

Native assembly SHA256 manifest: `/tmp/wildfire-warden-district-lifecycle/native-sha256.json`. Regression logs: `/tmp/wildfire-warden-composition-proof/`.

## Executed validation

At `052d702`, 28 focused lifecycle/resource tests passed and the full native project passed **1,336 tests, zero failures or skips**. The original three-case red run and all final logs are preserved in `/tmp/wildfire-warden-composition-proof/`. Commands:

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj \
  --filter 'FullyQualifiedName~NativeWardenDistrictLifecycleTests|FullyQualifiedName~NativeLifecycleInvalidationTests|FullyQualifiedName~NativeResourceTransactionTests'
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj
```

## Best-effort cleanup diagnostic

Follow-up source `5e8a65f` retains the original swallowed observer exception in one best-effort warning:

```text
wildfire_warden_equipment_lifecycle status=indeterminate operation=unregister_district error=<original exception and stack>
```

The shared guard is invalidated before formatting or logging. The existing `_exited` latch prevents repeat attempts; a failed formatter, throwing logger or logger reentry cannot escape native death/deletion. There is no persisted cause ledger or additional notification state. The existing generic recovery notification remains separate.

The production constructor is unchanged. Installed Bindito `ConstructorRetriever` scans `Instance|Public|NonPublic` constructors (flags 52) and rejects multiple parameterful constructors, so an internal test constructor would not be safe. An actual native `GetEligibleConstructor` regression verifies the sole existing public coordinator constructor remains selected. The warning uses the existing `UnityTimberbornFireLogSink.Warning` through a private delegate; native fixtures replace that delegate and never call unsupported Unity logging methods.

The original missing-diagnostic run failed its three new recording/logger cases while the native constructor check passed. Expanded validation covers the exact original exception/stack, throwing and reentrant loggers through actual `Character.KillCharacter` and `EntityComponent.Delete`, and exception-formatting failure. Focused lifecycle/resource tests: **35 passed**. Logs: `/tmp/wildfire-warden-district-lifecycle/diagnostic-{red,green,full-native}.log`.

Full native suite on `5e8a65f`: **1,343 passed, zero failures or skips**. No engine/deployment action was taken for the diagnostic follow-up.

## Shared implementation follow-up

The corrected native district protocol now lives in [PersonalInventoryDistrictRegistration](personal-inventory-district-registration.md). Earlier counts and source hashes above remain historical evidence. Both native lifecycle suites run against the shared implementation; cargo, admission policy and public constructors remain component-owned.
