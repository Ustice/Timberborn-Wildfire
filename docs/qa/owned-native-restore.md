# Native definition evidence for owned restoration

Complete restoration must preserve the saved body's accounting definition when native quantities change.
The earlier fresh-definition path selected positive current Yielder amounts: an initial five-unit body
with capacity 15 could become a freshly calculated capacity 9 after its native yield fell to three.
Initial eligibility also excluded some retained leftovers. Neither fact makes the saved body invalid.

## Authority and format

Outer WF2 remains unchanged. Private OWNED schema 3 adds one complete `OwnedNativeDefinitionSet` to
the existing history. OWNED1 lacks consequence history; OWNED2 has history but explicitly lacks native
compatibility evidence. Both remain readable and retain their original encoding, but `PrepareRestore`
rejects them before native capture or backend creation. Current native facts cannot manufacture their
missing historical evidence. The production runtime still preserves/rejects owned payloads before its
legacy initializer; this slice does not activate owned loading.

Saved `OwnedBodyAccountingProfile` retains the accounting stacks, capacity and coefficients. BURN is
the sole damage ledger. Restoration recalculates the saved definition under the current calculator and
requires exact agreement before applying BURN. It neither resizes the body to current stock/yield nor
replays applied fire loss into native quantities.

The separate static witness contains exact Guid/spec, native body shape, configured burnable profile,
transformed-independent local footprint, named yield roles with declared goods/amounts/RemoveOnCut,
and static building cost. Null cost means nonbuilding; an empty building cost is meaningful. Current
yield, inventory quantity, Enabled flags, timers and reservations are excluded. The complete set must
match exactly the retained owners and their saved local-slot bindings. Retired owners have no witness
or BURN body; their canonical origin and effect history remain retained.

`CreateWithNativeDefinitions` uses the existing registry and consumer. It accepts settled initial facts
and already-chosen accounting, captures witnesses once, and rejects attempts to manufacture them for
already-damaged bodies. Later unwitnessed admission is explicitly unsupported. This is not a second
owner registry, remaining-fuel ledger, or decision about initial plant/harvest material policy.

## Restore and capture boundary

`TimberbornOwnedWorldSession.PrepareRestore` holds the same runtime `CaptureAtRest` exclusion scope
across paired validation, native reads, body construction, backend allocation and history staging.
Its native callback receives the grid and exact retained Guids. The native provider's
`CaptureRetainedBodies` reads those Guids directly, including disabled and leftover bodies, without
applying fresh-admission eligibility or taking a nested guard. It rechecks native references, placement
and facts. The caller must execute this after native entity loading settles on the native thread.

Fresh body state is built from saved accounting plus validated current spatial facts. No supplied live
body service is mutated. Every required retained owner must still be Live before the unpublished bundle
returns. Every retired owner must be exactly Absent from the native registry: Deleted, Uninitialized
and invalid native references are not absence. Failed deletion can leave a registry entry that native
saving still includes. Late staging failure disposes the new backend.

Witnessed capture also rejects a vanished retained owner rather than emitting a complete save already
known to be unrestorable. Explicit settled retirement uses the canonical retention transition; reads
never silently retire owners or discard BURN. Read failure releases the guard without poisoning it.
Reentrant guarded mutation is rejected across both simulator capture and native fact/liveness reads.
Actual mutation failures continue to use the shared unsafe-save boundary.

Final publication and retired-material GPU detachment remain separate activation work. This API does
not permit ordinary steps through a retired-but-still-active GPU owner merely because the bundle can
represent that checkpoint. Presentation restoration remains the separate quantity/death-free API.

## Evidence

The isolated combined native suite passed **1012 tests**, zero failures/skips, at `bc7467e`, including:

- Saved initial five/current three restores capacity 15; original partial three restores capacity 9.
  Current zero or disabled yield does not change accounting. Saved capacity tampering rejects.
- Installed native Yielder methods establish three saved/restored units with declared five. Restore
  leaves both quantities unchanged and emits zero YieldAdded/YieldDecreased callbacks. This exercises
  the named Yielder seam, not whole-world component lifecycle or positive Unity object lookup.
- Static role/component/resource/declared quantity/RemoveOnCut/spec/footprint/construction changes,
  missing or duplicate facts, malformed witnesses and invalid default construction stacks reject.
- Retained leftovers restore desired presentation/history without replaying compound fire effects.
  Changed actual inventory and placement preserve saved accounting when static local facts match.
- Missing retained owners and every non-absent retired presence reject. Callback mutation during
  capture is excluded; late owner disappearance disposes the backend without poisoning a read.
- OWNED3 binary roundtrip retains static fields and the sole BURN association. Older and malformed
  payloads preserve original bytes instead of receiving invented compatibility evidence.

Tests use synthetic accounting choices to distinguish authority; they do not establish crop fuel
policy. Actual native SingletonSaver/ObjectLoader encoding coverage is inherited from the history
slice. No game, Steam, Unity, deployment, native quantity write or production binding was performed by
this implementation agent. Parent integration requires its own combined suite.

The separate controller subsequently exercised the exact unchanged retained-capture assembly
`992f7bc` in licensed Unity (exit 0): real GameObject/native ComponentCache initialization and registry
lookup, a same-Guid disabled leftover excluded by initial capture but included by retained capture,
unchanged raw native yield, destroyed-object rejection, and actual Building.Awake resolution of a
supplied BuildingSpec with Log15 cost. The fixture supplies placement, state and dependencies; it does
not execute the full template lifecycle, save restore, world publication, or a mid-capture mutation.
Its disabled Yielder reports public zero while raw quantity remains one. Full controller evidence and
limitations are in `/tmp/wildfire-retained-body-engine/REPORT.md`.

Evidence: `/tmp/wildfire-owned-native-restore-full.log` and the focused `*-tests.log`, `*-presence.log`,
`*-static.log` files under `/tmp/wildfire-owned-native-restore*`.

The authorized tools-disabled Claude proposal review produced no usable second opinion. Actual
terminal 36395 exited 1 after 41,963 ms with `is_error=true` and a provider refusal/API error. Prompt,
JSON result and stderr remain in `/tmp/wildfire-owned-restore-authority-review/`. No workaround or
rerun was attempted; the correctness claims above come from source review and the stated tests.

## Final staging consistency

Backend construction and native presence observers can invoke callbacks. Restoration now copies the first retained-body list, then captures those same required Guids again after callback-capable staging. Full body readings must agree: static definition, world/local footprint, current named yield and availability, inventory stock and availability, and construction cost. This compares two reads of one restore operation; it never compares current quantities against historical accounting or rebases that accounting.

Four counterfactual cases (changed declared definition, actual quantity, availability and captured membership) all failed against the former presence-only completion check. The corrected integrated suite passes **1,057 native tests**, including an additional mutable returned-list case: changing a caller-owned list cannot rewrite the first evidence. Rejected staging disposes the new backend once, leaves the old damage state intact and releases the read guard without poisoning. Evidence: `/tmp/wildfire-restore-staging-counterfactual.log` and `/tmp/wildfire-restore-staging-integrated.log`. These are host callback fixtures; no whole-game reload is implied.

## Backend snapshot fidelity

Initial and restored sessions now share complete snapshot comparison after backend construction. The factory receives a separate array copy, must report complete material history, and must return the expected cells, companion and transport fields, identities, tick, parameters, seed, ordered pending commands, known identities and exact archive records. Set ordering of identities/archives is immaterial; their membership and archive bytes are exact. Native final rereading follows backend readback and all other callback-capable staging.

Five counterfactual fixtures all failed against the former dimensions-only restore check: altered archived fuel, dropped pending input, dropped archive, legacy capability and mutation of factory input arrays. The corrected full native suite passes **1,118 tests**, zero failures/skips. Failure disposes the unpublished backend and leaves both saved evidence and the existing native guard usable. Evidence: `/tmp/wildfire-restore-backend-counterfactual.log` and `/tmp/wildfire-restore-backend-integrated.log`. This verifies staged backend fidelity; rebuilding desired native projections and the rich baseline on restore remains a separate activation requirement.
