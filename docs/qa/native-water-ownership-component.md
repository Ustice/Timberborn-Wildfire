# Inactive native water-input ownership component

Source checkpoints: `70a60e3`, `de85494`. This implements an explicit source component and exact scheduler installer, with **no shipping configurator, template, placement, cargo executor, withdrawal demand or suppression activation**. It follows the [native credit-boundary investigation](native-water-credit-boundary.md) and [buffer/bucket proof](native-shoreline-water-proof.md).

## Authority

`TimberbornNaturalWaterSource` owns a versioned native persisted witness: original entity Guid, fixed input coordinate and irreversible taint. Native WaterInput alone owns clean/dirty buffered volume. There is no second water balance, pending-water promise or per-beaver volume reservation.

A new source installs protection when its component is tracked, before ordinary native credit. Explicit `TryArmNewSource` is for the caller after native creation/initialization returns; it requires zero native buffers, exact fixed provider/component topology, finalized transformed coordinate, live finished native body, and exclusive registration. Any protected credit before arming permanently refuses the source, even if its buffer later returns to zero. Inherited positive buffers are refused without modification. Native placement changes after arming and native deletion immediately invalidate the source.

A restored false marker does not bootstrap new trust. Native source.Load must receive the exact native EntityLoader whose SerializedEntity reference matches that exact entity in the current native WorldEntitiesLoader's instantiated load list. All native component Load calls precede initialization. Boundary.PostLoad verifies the same still-live list, consumes source permits once and closes further restore admission in finally. Native WorldEntitiesLoader subsequently runs entity PostLoad and clears the list. A late arbitrary Load, equal-Guid copied serialization object, missing marker, missing native water save component or invalid quantity cannot authorize a source. Malformed present witness or missing required scalar keys reject through native loading rather than silently manufacturing defaults.

Native positive-buffer restore still requires real initialized Unity objects and exact body/provider identities. It has **not** been proven by this managed fixture. The staged native save/load tests author the witness and supply the native load-list membership explicitly; they do not claim to instantiate a complete real source template.

`Ready` is pure and checks current admission, including native thread, poison, live finished body, fixed identity and current exclusivity. Save checks native thread/save exclusion first and validates persistent identity/history directly; temporary gameplay admission is not interpreted as historical ambiguity. Saves do not call demand, remove water or copy quantities. This first slice conservatively taints dormant/unregistered sources at a protected credit boundary and refuses inactive positive restoration; it does not claim general unfinished/finished re-entry preservation.

## Scheduler composition and failure

The provisioned boundary implements only IPostLoadableSingleton. It owns a private, never-provisioned ITickableSingleton used only as replacement for the original WaterInputService scheduler slot. Actual native SingletonListener/SingletonRepository proof prevents a second separately enumerated tick.

Installation verifies reviewed native assembly fingerprints/private shapes, finds exactly one reference-equal original service, and preserves its MeteredSingleton position, metric and enabled flag. The same published installation is idempotent. An unexpected scheduler replacement after installation cannot be reinstalled to erase an unobserved interval. A fresh native world requires a fresh boundary/load fence. No original service rebinding, native list edits or schedule reordering occur.

No sources means no capability check or scheduler installation. A previously installed empty wrapper normally passes through once; it cannot bypass an existing indeterminate resource state by deleting the last source. Expected unsupported scheduler/provider/receipt-service topology refuses natural sources and preserves ordinary native ticking when possible, without poisoning the world merely for incompatibility.

With supported sources, one existing NativeResourceCoordinator.TransferInventory covers source validation, durable taint and the exact original native credit Tick. Only reviewed fixed/pipe coordinate providers and the actual native WaterChangeService receipt reader qualify. The native credit loop has no application callbacks in this supported graph. Genuine observer/credit failure poisons the existing resource boundary and cannot be retried, even after the failing native state is repaired or the last source is removed. Native MeteredSingleton's existing exception/metric behavior is preserved; no invented Pause-on-failure is added.

## Managed validation

**Validation:** 30 focused tests and 1,653 full native tests passed, zero failures/skips and no build warnings.

Thirty new tests exercise the actual installed native methods with supplied managed backing state:

* exact buffer predicates, zero versus positive inherited quantity;
* scheduler slot, original service reference, metric and order preservation;
* actual native provision/injection discovery excludes the boundary from standalone ticks;
* no-source and expected foreign scheduler/receipt-provider refusal without poison;
* unarmed credit cannot be forgotten after buffer depletion;
* real native credit failure, repaired receipt dictionary and last-source removal cannot enable retry;
* actual EntitiesLoader.Load, EntitySaver/EntityLoader and WorldEntitiesLoader.PostLoadNonSingletons (including native list clearing);
* complete/incomplete/missing/invalid witness and native buffer fields, tainted save, exact serialized-object identity and closed-fence rejection;
* wrong-thread save rejects before writing taint or a native save component.

Three discriminating counterfactuals are preserved in `/tmp`:

1. `/tmp/wildfire-water-ownership-retry-red.log`: after native failure, removing the last source allowed a repaired valid receipt to be credited. Old result 1 failed / 1 passed; final both pass.
2. `/tmp/wildfire-water-ownership-discovery-red.log`: actual native discovery returned original WaterInputService **and** Boundary as tickables. Private non-provisioned tick wrapper resolves it.
3. `/tmp/wildfire-water-ownership-save-red.log`: wrong-thread native Save wrote permanent taint instead of rejecting the unsuitable access window. Final dedicated-thread test rejects before mutation.

Focused log: `/tmp/wildfire-water-ownership-focused.log`. Full native log: `/tmp/wildfire-water-ownership-full.log`. Installed hashes and source recipe are under `/tmp/wildfire-folktails-bucket-route/`. This slice changes no Core/fire rules or shader code and does not run an engine.

## Remaining controller proof

Supply a real source GameObject/template with the native fixed WaterInput and real BlockObject lifecycle. Prove successful new-source arming, positive buffered false-marker full native save/load, exact source movement/deletion, actual fluid removal/credit, competitor appearance/disappearance and taint reload. Then compose the existing one-unit private native inventory conversion under the same runtime coordinator. The managed receipt fixture supplies native fluid result dictionaries; it does not prove river physics, shoreline access, movement, source placement, demand cadence or a completed firefighter trip.
