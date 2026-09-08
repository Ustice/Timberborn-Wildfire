# Owned native consequence batch prototype

`TimberbornOwnedDeltaConsumer` is the opt-in integration boundary for exact native Tree, Crop,
Stockpile, and Structure origins. It is not bound to the production legacy delta dispatcher.

## Delivery contract

- Resolve every nonzero GPU origin through the retained material registry and its explicit canonical
  Guid/family registration before applying any effect. Unknown, conflicting, aliased, or missing live
  body registrations reject the entire batch. Origin zero remains an explicit unowned count.
- Probe physical native body existence independently of effect availability. A live Structure with
  no enabled SimpleOutput inventory still receives body damage. The native probe rereads
  `EntityRegistry.GetEntity(Guid)` and requires an initialized, undeleted, live entity.
- Run the shared exact-owner damage reducer once across all live owners, summing accepted fuel-loss transitions per owner
  after full target/slot/cell chain validation and exact replay suppression. Then call the raw tree, crop, and storage
  sinks; do not chain standalone family consumers or invoke legacy cell-based structure rollback.
- Supply the same `NativeResourceCoordinator` that guards world saves. Its existing
  `INativeResourceMutationGuard` surrounds the whole body/effect pass. A callback exception after
  an earlier mutation poisons the session and forbids retry/save; there is no speculative refund.
  Save, reentrant delivery, and registration changes cannot interleave native delivery callbacks.
- Native effect APIs recheck the exact current owner before each mutation. If an earlier family
  callback deletes a later owner, that later effect reports NotLive/skips without resolving a
  replacement at the old cell. The body reducer may already have recorded that owner's damage;
  the operation is ordered, not a rollback transaction.

The result includes body damage, individual tree/crop summaries, actual inventory removals and
hazards, and Structure owners whose rollback is unavailable. Configured family capabilities mean
an adapter is present; native partial yield and other unavailable actions retain their truthful
per-action results. `StructureRollbackSupported` is false even when that Structure's storage burns.
Tree/crop `CoalescedCellCount` counts extra cell contributions grouped into one compound body effect.
This applies to both owned and legacy family summaries. It is separate from the damage reducer's
`DuplicateCellSuppressedCount`, which reports discarded repeated damage rows. Coalescing effects does
not discard damage from distinct burning cells.

## Evidence

Ten new tests cover mixed families with one body pass, Structure body/inventory independence,
trailing unknown and missing registration rejection before effects, missing adapter rejection,
shared poison after a simulated tree mutation and callback throw, later-owner deletion without
crop/inventory mutation, callback save/registration guards, canonical family conflict rejection,
and the actual installed native EntityRegistry/entity-state negative liveness paths.

The nine orchestration cases use deterministic host fakes plus the real shared resource guard.
They do not establish live game event timing or positive Unity object liveness. Existing tree,
crop, storage, native inventory/reservation, and callback fixtures remain the evidence for their
individual native seams. No game, deployment, or Unity run was performed for this aggregate.

Commands:

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --filter FullyQualifiedName~OwnedConsequenceBatch --nologo
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --nologo
```

Result: full native suite **831 passed**, zero failures or skips; focused aggregate suite **9 passed**.

Local logs: `/tmp/wildfire-origin-routing-8a3be7b/aggregate-routing-tests.log` and
`/tmp/wildfire-origin-routing-8a3be7b/aggregate-full-tests.log`.

## Remaining activation boundaries

Production activation must publish the simulator's complete material history together with actual
native Guid/local-slot bindings, origin-family registrations, and consequences. Retained material
bindings alone do not identify historical canonical consequence families; hidden/deleted owner
registrations must remain available for queued origins. Do not infer them from current occupants.

The private OWNED2 payload now preserves typed storage fractional credit, canonical retained/retired
owners, immutable body profiles, natural request progress and desired presentation. Complete capture
holds the shared guard across all reads, and restore stages a new session. See [history proof and
remaining native reconciliation limits](owned-consequence-history.md). This does not authorize
replaying quantities or reconstructing saved capacity from the remaining native yield.

Structure rollback/repair and additional native owner families remain outside this route. Their
migration must use exact ownership and the same preflight/guard/reducer boundary, not the legacy
spatial rollback API. Natural ignition and civilian injury activation remain gated separately.

## Damage batching correction

The previous body reducer discarded burns in other occupied cells whenever they appeared in the
same batch. A three-cell Pine losing eight fuel in each cell received eight damage together versus
24 across three ticks. Distinct-cell aggregation now records 24 in either case, clamps to the body's
remaining capacity, and writes one event/state per body. Repeated identical owner/cell reports do not
spend the same damage twice. Regression tests reproduce the earlier failures and cover capacity and
repeated reports. This changes actual multi-cell damage and may require later balance adjustment.

The later [transition normalization change](owned-transition-normalization.md) also handles multiple
legitimate changes to one local slot and distinct slots occupying one cell around a handoff. It keeps
complete packed chains, suppresses only provable exact replay, and rejects unexplained discontinuities
before effects. Storage now consumes each accepted contribution with exact receipt quantities and
completion-cell hazard attribution. The legacy cell-routed path still lacks complete slot provenance.
