# Owned tree consequence prototype

This is an origin-routing vertical, not production material lifecycle activation. `TimberbornOwnedTreeDeltaConsumer` is deliberately separate from the legacy cell-routed `TimberbornFireDeltaConsumer` and is not wired into the runtime initializer. It can invoke only exact-target body damage and tree consequences; an origin without a registered tree binding is explicitly unsupported before any row in that batch mutates state. Other native families must migrate before a mixed production world can use it.

## Identity and lifecycle boundary

`CellDelta.TargetId` now survives `TimberbornFireCellDeltaDecision.FromDelta`. The owned consumer resolves every nonzero ID through retained `TimberbornNativeMaterialRegistry.TryResolveOrigin`, then checks the explicitly registered Guid's canonical tree damage key. Zero is unowned; unknown nonzero is a consistency failure. A registered hidden tree does not need current cell ownership or active footprint membership to receive its earlier delta. Deleted or not-initialized trees skip without damage or a native success result. Bindings remain available after live damage registration removal so deletion cannot redirect a delayed row to a replacement.

The exact-target damage entry reuses the existing maximum-damage-per-target reduction, including heat/cell tie breaking. Tree candidates aggregate against that exact state. No simulator rule, damage amount, or tree stage threshold changed. Register new trees after their Guid-based body registration exists; registrations cannot change during this consumer's delivery. Full event-driven material/body registration remains future activation work.

The native tree actuator now resolves `EntityRegistry.GetEntity(Guid)` for each consequence and checks native initialized/deleted/object liveness plus the tree component family. Startup enumeration restores existing leftover visuals only; it no longer authorizes later targets through runtime-hash caches. Consequences carry the native Guid, validated against their canonical family key. The existing legacy tree sink derives it strictly from the already adopted Guid registration format, never from coordinates or old hashes. This repairs the current tree Guid/hash mismatch even before the owned consumer is activated.

Known disappearance returns Applied=false/Failed=false. A definite failed kill stops before its visual follow-up; native exceptions still propagate. This is not a claim that native multi-action consequences are crash-atomic or safely replayable. The native tree's existing yield-loss acknowledgement and already-terminal semantics are unchanged; neither is new proof of a physical yield mutation.

## Source structure

Tree rendering/native actions moved out of the mixed `TimberbornRuntimeBurnedTextures.cs` into `TimberbornTextureTreeBurnConsequenceApi.cs`. Tree consequence contracts moved into `TimberbornTreeBurnContracts.cs` to keep the changed sink below 600 lines. Crop/texture derivation remain for their later family work.

## Validation and limits

- 14 new owned-route tests cover old A/new B at the same cell, maximum-per-owner dedup, hidden A, removed A with B present, restored retained bindings, zero/unknown/unmigrated origins, dynamic registration, per-action disappearance, definite failure, callback exception, and registration reentrancy.
- Two managed native fixtures execute installed EntityRegistry and EntityComponent state getters. They verify post-construction registry lookup and missing/deleted/not-initialized rejection, and rejection of hash/mismatched Guid consequences before mutation. Every action on a deleted origin returns no applied result.
- Full native managed suite: 758 passed, zero failed/skipped, on the origin-tree worktree after these tests. No shader/Core/backend changes.
- Positive live Unity object/component resolution, drying/death/leftover visuals, actual native creation and callback deletion remain game QA. Tests do not create fake Unity liveness or claim a live tree trip. No game, Unity, deployment, or save mutation was performed for this slice.

When production origin activation is ready, verify copied-save A/B replacement and hidden-owner scenarios through a controller-owned native run, including a new tree created after initialization and deletion between tree stages. Confirm old-origin events never reach the replacement. Mixed-family delivery must remain explicitly unavailable until each direct native owner sink has migrated; this prototype has no cell fallback switch.
