# Desired material reconciliation planner

`TimberbornDesiredMaterialReconciliation.Plan` is an internal, unbound planner over the existing
native registry, explicit canonical owner retentions, and a simulator implementing both complete
snapshot and material handoff contracts. It captures exactly one validated complete checkpoint.
It allocates no identities, stores no fuel/history ledger, executes no step and publishes no readiness.

The retired-only test prototype missed a surviving owner moving to another cell and an unowned
Water-to-Badwater change: both counterfactual tests returned null before this implementation.
Evidence: `/tmp/wildfire-desired-material-reconciliation/counterfactual-red.log`. The old prototype
remains unchanged as independent prior evidence.

The planner checks exact grid dimensions, canonical retention for every binding, and every desired
contributor, including hidden ones. Every coordinator known/archive query must agree with the
captured authority; restore requests retain the coordinator's exact single-use archive object.
The caller must hold the shared native guard and supply a supported, fully observed native world.
This API does not independently observe native presence, eligibility, inventory compatibility or
state/profile policy. Those cannot be inferred from a retained Guid alone.

Every cell participates in the comparison. Same active desired target/slot retains current GPU
state. A known slot elsewhere uses same-batch captured-source transfer and closes its source
replacement recursively; inactive known slots use exact archives. Never-active slots use the
registry's verified current first-activation builder. Unknown or inconsistent history never falls
back to Fresh. A lower contributor wins before any environmental baseline.

For unowned cells, compare packed terrain/flammability plus companion class, capacity, ash quality
and contamination behavior. Current HLSL persistent companion assignments occur in material handoff;
these fields are static profile attributes. Ignore packed fuel/burning, companion burn history and
all ambient fields. A changed static baseline uses the existing typed baseline request; an unchanged
baseline never replays initial wetness/soil or clears externally changed fuel. Same-owner profile
changes remain a caller policy gate, not a reason to refill or refresh material here.

Tests execute the existing Core coordinator through its scripted receipt backend: moves, swaps,
overlapping footprint source closure, exhausted archive reveal, first activation, full/overfull
queued-input ordering and exact old-origin slots, capacity rejection before upload, unchanged
ambient/progress fields, malformed authority, hidden canonical-owner coverage and shape mismatch.
The scripted backend is not a GPU fire implementation; no new engine proof is claimed.

The returned batch is a proposal, not permission to bypass native observation or step delivery.
Existing `Consumer.ConsumeStep` remains the guarded execution boundary. After accepted handoff and
native effects, derive a new plan from current facts; null alone does not certify unsupported native
states as ready. No runtime/session/provider activation or additional execution guard was added.
