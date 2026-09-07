# Native owned-walk stop boundary

The reviewed macOS binaries are fingerprinted by `TimberbornOwnedWalker`. This adapter is only for movement currently owned by a Wildfire executor. It neither cancels arbitrary jobs nor modifies goods/reservations.

## Why the public stop pair is unsafe

Installed IL proves the following sequence:

1. `PathFollower.StopMoving` clears `_pathCorners`, then stops its animator.
2. `Walker.Tick` checks `Stopped` (destination is null), then `IsOutsideAndReachedDestination`, then `_stopNextTick`.
3. For an outside beaver, `IsOutsideAndReachedDestination` calls `PathFollower.ReachedLastPathCorner`, which dereferences `_pathCorners` without a null guard.

Therefore `PathFollower.StopMoving(); Walker.StopNextTick();` leaves a non-null destination paired with a null path and can throw before the pending stop flag is considered.

The native private `Walker.StopMoving` clears destination and stop flag first, then calls PathFollower.StopMoving. The shared adapter invokes that exact signature only after the reviewed WalkingSystem, CharacterMovementSystem, TickSystem and BaseComponentSystem assembly hashes match. There is no new patching dependency.

## Path callback and scheduler timing

`Walker.FindPath` starts PathFollower, emits StartedNewPath, THEN stores its destination, recalculates bounds and checks arrival using PathFollower. Neither private StopMoving nor the old public pair may clear the path inside a normally returning StartedNewPath callback.

On rejected paths, `RejectRoute` disables only that walker's late `WalkerMover`, recording whether this adapter performed the disable. It leaves the installed path structurally intact. The owned Launch/RefreshPath caller then calls Stop after the native call returns. Only a validated fresh owned launch, or a completed stop followed by release to native roots, releases this adapter's mover pause.

Native `NavMeshObserver.OnNavMeshUpdated` merely records bounds. Its ordinary Tick refreshes the route. `WalkerMover` is ILateTickable; `TickableEntityLifecycleManager` sorts ordinary tickables before late ones. `TickableEntity` rereads each component's Enabled immediately before invocation, via MeteredTickableComponent. Thus callback-time disabling prevents the rejected native refreshed path from being advanced by the late mover, even when NavMeshObserver runs after a Wildfire ordinary interceptor. Wildfire's owning executor must still check its rejection latch before interpreting native walk completion.

BaseComponent.DisableComponent updates ComponentCache and flips Enabled. ComponentCache only updates frame-update adapters for IUpdatableComponent/ILateUpdatableComponent; WalkerMover implements neither. Do not generalize this proof to arbitrary components. A mover already disabled by another owner is never re-enabled by this adapter. Death does not release the pause.

This is a simulation tick ordering guarantee from inspected IL. Existing animation interpolation and live game event behavior still require native QA; this is not a promise of zero-frame exposure or full civilian safety. Unreviewed game builds reject before new movement ownership.

## Evidence and tests

2026-09-07 read-only installed IL: Walker.Tick/FindPath/StopMoving/IsOutsideAndReachedDestination; PathFollower.StartMovingAlongPath/StopMoving/ReachedLastPathCorner; NavMeshObserver.Tick/OnNavMeshUpdated; WalkerMover.Tick; TickableEntity.TickTickableComponents; MeteredTickableComponent.Enabled; BaseComponent.EnableComponent/DisableComponent; ComponentCache.AddEnabledComponent/RemoveDisabledComponent.

`TimberbornOwnedWalkerTests` loads the actual installed managed types into an isolated context, with uninitialized field fixtures rather than game entities. It verifies mover Enabled reaches the scheduler wrapper, then deliberately leaves the animation dependency absent: native Stop fails after clearing destination/path, the original failure propagates, the mover remains paused, and a following native Walker.Tick safely returns without dereferencing the missing path. This proves the managed stop-state ordering even when final animation cleanup fails. It does not prove successful live animator cleanup, routing, rendering or save/reload.
