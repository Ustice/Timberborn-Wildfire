# Owned consequence history prototype

WF2 retains its outer format and sole BURN body-damage ledger. OWNED binary schema 2 adds immutable
canonical owner definitions, natural request progress and desired charred presentation, and fractional
storage burn credit. OWNED schema 1 remains readable with `HistoryCapability.Unavailable`; missing
history cannot become empty complete history. Current production runtime still preserves/rejects WF2
before its legacy initializer. This change does not activate owned world loading or partial yield.

## Captured authority

- Canonical Guid/family declarations survive native deletion. Retained bodies include immutable
  accounting profiles; retired native owners require a null profile and no BURN entry. Retention/profile
  consistency is enforced in constructors. Active/suspended cell eligibility is rebuilt independently:
  a valid previously emitted exact-owner delta must not be dropped merely because its owner is hidden.
- Natural rows retain actually applied yield loss and completed dry/death/leftover requests. They never
  restore native yield or inventory quantities. Desired charred presentation is separate from the
  in-memory renderer-applied cache. Natural history covers every declared tree/crop, including zero
  progress and tombstones; storage credit is sparse positive per-owner/resource fractional budget.
- Native stock, reservations, yield, and GPU remaining fuel are not duplicated. Credit continues to
  follow owner/resource across stock movement, preserving current fractional policy. Its stored fuel
  cost must match the current resource catalog before restored credit can be used.
- Every GPU-known origin requires a canonical consequence declaration. Every history owner requires a
  retained native binding. Live body profiles match exact spec/kind/capacity/fuel/material/resource
  definition facts; BURN keys/ranges/ticks must match exactly. Duplicate, missing, extra, hash-based,
  above-capacity or future body records reject; no legacy clamp, zero default, or dropped row applies.

## Capture and publication

`TimberbornOwnedWorldSession<TSimulator>` is a new unpublished bundle of simulator, registry, body
service and consumer. `PrepareRestore` validates the complete association and actual supplied body
definitions, clones mutable body state, creates a new simulator, restores consumer history, and returns
only after the same runtime resource guard accepts it. A late failure disposes the new simulator. Even
an accidentally reused body-definition service is not mutated during staging. The caller owns final
single-bundle publication after native entity/model loading has settled.

`Capture` copies simulator/bindings/body/history synchronously with the same guard checked before and
after. Capturing during resource conversion or consequence delivery rejects. Readback/validation failure
alone does not poison native resources; actual mutation exceptions retain the existing unsafe-save rule.
Legacy original payload retention stays unchanged until capture and encoding both succeed.

`RehydratePresentation` exposes only `ITimberbornOwnedPresentationApi`. The native implementation uses
exact Guid lookup and material/tint work; it never invokes Cut, BurnWhole, RemoveYield, Consume, Die, or
Delete. Tree leftover presentation requires the actual already-loaded native leftover model, otherwise
it returns Unavailable. Applied renderer caches start empty on restore and are marked only after a
successful presentation receipt. `TimberbornTextureTreeBurnConsequenceApi.CreateOwned` skips the legacy
constructor scan which chars all native leftovers; future owned activation must select this factory.

## Evidence and boundaries

Tests cover nonzero complete schema roundtrips (including profile fields/natural flags/credits), malformed
history and original preservation, explicit material-only rejection, exact tombstone resolution, actual
receipt/credit continuation in a new session, no compound mutation replay, profile preflight failure,
callback capture rejection, readback failure without poisoning, and disposal after late restore failure.
The latter regression first reproduced a supplied body's DamageTaken changing despite failed restore;
cloning the body service before applying saved damage fixes that boundary.

An installed native `SingletonSaver`/`ObjectLoader` test stores and reloads the complete encoded OWNED2
payload. Native constructor/negative lookup tests verify owned construction avoids a world scan and
missing original entities return NotLive before renderer access. These execute installed managed native
code, not a saved world file or a positive Unity rendering session. Positive model restoration and actual
atomic world publication remain controller-owned game/Unity validation.

Native partial-yield helper dependencies use actual receipts, but public ReduceYield remains unavailable.
The native load-order audit now finds a named Yielder may load before a GatherableYieldGrower whose ripe
Load resets yield again. This history never tries to repair that by writing the applied-loss cursor back
as a quantity. No cycle counter, native Yielder save patch, or guessed reset detection is introduced.
The separate partial-yield request lifetime and whole-component load ordering must be resolved before
activation; the persistence schema is not proof that those native lifecycle semantics are safe.

Local evidence: `/tmp/wildfire-owned-consequence-history/` (`full-tests.log`, `session-tests.log`,
`body-alias-reproduction.log`, plus the prior proposal and actual Claude review/disposition).
