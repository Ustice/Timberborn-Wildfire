# Owned crop consequence prototype

This is an exact-origin crop vertical, not production material lifecycle activation. `TimberbornOwnedCropDeltaConsumer` preflights the complete batch through the shared retained-origin registry, checks canonical crop registration and native liveness, then applies exact body damage and the raw crop sink under one resource guard. Do not chain single-family consumers for a mixed batch: its aggregate must resolve once, reduce body damage once, and invoke raw family effects under the same outer guard.

## Identity and native actions

The raw native crop API uses the carried Guid and matching crop damage key. Each action reads the current `EntityRegistry.GetEntity(Guid)`, checks initialized/deleted/Unity liveness, and verifies crop component family. It has no runtime-hash cache, registered-cell scan or current-cell replacement fallback. Hidden live A can receive its earlier event while B occupies the cell; missing A cannot redirect damage to B. Zero origin is unowned; unknown or unregistered nonzero origins reject the whole batch before mutation.

Normal and QA-selected native crop providers now share the canonical Crop Guid key. Existing SelectedCrop keys are accepted only as explicit legacy native identities; the owned route never registers that alias. Tests prove an existing alias state cannot receive a second body/effect application.

Results distinguish actual Applied, AlreadySatisfied, Unavailable, NotLive and Failed. The sink advances its yield ledger by the returned native quantity only. Already-dead crops count no new kill. Full-burn deletion, killed state, visual changes, destroyed inventory goods and removed yield are separate receipts; visual completion cannot manufacture yield loss. Unavailable actions remain retryable and callback exceptions propagate.

Partial yield loss remains an unresolved **first-release implementation gate**. Installed `Yielder.DecreaseYield` subtracts then emits YieldDecreased; `Gatherable.OnYieldDecreased` harvests the remainder into a good stack and invokes Gathered. This prototype therefore returns Unavailable for ReduceYield. It does not remove partial loss from the accepted design.

Whole-burn uses the distinct `Yielder.RemoveRemainingYield`, which zeroes yield without YieldDecreased. It records the actual positive amount removed. Required components and fully unreserved/takeable good-stack stock are checked before mutation; a reserved or nonclearable stack makes the operation Unavailable. The grower resets through its native RemoveYield method. After each callback-bearing stage, the exact owner and reused component are checked again. Stock availability is reread before each TakeConsumed; a successful native call receipts its exact requested quantity, rather than attributing arbitrary callback changes from a before/after stock difference. Nonempty stock prevents disabling the stack. Resource deletion counts as deletion, not an invented texture change.

A compound operation that loses its owner/component or encounters a changed reservation after mutation begins throws. It is not atomic and has no refund or replay guarantee. The owned caller's guard makes such uncertainty unsaveable. Legacy tree and crop entry points use explicitly named wrappers around the **same runtime NativeResourceCoordinator** used by Save; the owned aggregate must use raw APIs under one outer guard, never nest these wrappers.

## Proof and remaining native QA

- Eighteen new tests cover retained hidden A versus replacement B, deleted origins, zero/unknown/unregistered whole-batch preflight, canonical alias dedup, actual partial receipts, full-burn deletion versus visual/kill/yield, unavailable retry, and shared-guard callback failure without replay.
- Actual installed managed Yielder tests prove whole removal 5→0 and repeat→0 without a harvest callback, and DecreaseYield invokes its callback after reducing quantity. Actual LivingNaturalResource.Die emits once, returns a truthful repeat classification, and leaves IsDead changed when its callback throws. That real callback exception poisons the shared legacy guard and blocks replay/save.
- Actual EntityRegistry/EntityComponent tests prove missing, newly registered but uninitialized, and deleted rejection. They do not fake positive Unity liveness.
- Full native suite: **798 passed**, zero failures/skips, on the crop worktree with these changes. No Core, GPU or shader behavior changed.

Controller-owned copied-save QA is still required for positive scene-object resolution, new crops after initialization, drying/death/model changes, whole-yield and stock clearing, reserved-stock refusal, native resource deletion, and deletion during compound callbacks. The complete native render/delete path has not run in the game. No game, Unity, deployment or save mutation was performed for this slice.
