# Owned transition normalization and storage contributions

The owned consequence entry points now share full-batch origin and transition validation before any
body/native effect. This requires GPU-emitted `(TargetId, SlotId, CellIndex)` provenance. Slot zero on
an owned row is explicitly unsupported legacy data; the adapter never guesses a slot from the current
cell. Retained native footprint bindings validate pairs even when hidden, removed, or restored. The
lookup is derived from the existing durable bindings and carries no GPU history or remaining fuel.

For each exact target/slot/physical-cell key, the first packed transition is accepted. A subsequent row
whose old state equals the previous accepted new state is accepted, including a genuine repeated loss
after a fuel gain. Otherwise, only an exact previously accepted packed row is suppressed as replay;
an unexplained discontinuity rejects the entire batch. Heat/water/gain rows remain until validation is
complete. Interleaved keys are independent. Relocation starts a separate physical-cell chain because a
material handoff retains destination environmental state. No current-cell occupant is consulted.

This is a per-delivery consistency check. It cannot discover a missing prefix/suffix or a replayed
whole earlier tick. GPU overflow/capacity/readback completeness and delivery ownership remain upstream
obligations. See [actual slot protocol proof](delta-slot-provenance.md).

Owned body damage sums all accepted positive-loss contributions and clamps once to remaining body
capacity. One event/write is produced per affected body; its source cell comes from the largest
individual loss, then greatest heat, then lowest cell index. `DuplicateCellSuppressedCount` reports the
normalizer's actual replay count (including nonfuel rows), independently of family
`CoalescedCellCount`. Replay telemetry covers the preflighted batch even when some owners are no longer
live. The explicitly legacy cell-routed reducer retains its previous per-cell maximum behavior because
it lacks originating slot history; this work does not label that route complete.

Storage processes accepted contributions in stable cell-index order, preserving emission order within
each cell. It reads exact native stock again for each contribution, uses existing per-owner/resource
fractional credit, consumes first, and emits hazards only for actual removals. A whole item belongs to
the contribution completing its fuel cost; that contribution supplies the hazard location. Earlier
credit may originate elsewhere or in an earlier tick. There is no new saved location or quantity ledger.
Body-capacity clamping does not erase raw storage fuel-loss budget. Positive cold fuel loss remains
eligible under the existing policy; heat-only changes and gains spend no budget.

This can produce multiple blasts at different cells for separate completed contributions. Earlier
callbacks may change or remove the native owner before later contributions. Frozen-stock arithmetic
matches the same ordered contributions delivered separately, including across OWNED2 codec/new-session
restore; full gameplay timing equivalence is not claimed. Exceptions after native mutation retain the
same resource guard's indeterminate/save rejection behavior without refund or retry.

Validation: full native adapter suite passed 929 tests, zero failures/skips. New regressions cover
packed loss/heat/loss, genuine loss/gain/repeated loss, replay suppression, same-target slot replacement,
relocation, whole-batch malformed/unknown-slot rejection, retained binding rollback, three-slot body
aggregation/clamping, representative source, fractional credit conservation, completion-cell hazards,
callback disappearance/failure and real OWNED2 codec/new-session continuation. These managed tests are
not a game session. Licensed GPU/native factory proof is separately controller-owned. No production
owned-world activation, generation-aware yield policy, legacy replay ledger, or cargo policy is added.
