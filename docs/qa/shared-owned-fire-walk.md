# Shared owned-job walking protocol

Source: `022b2e5`, based on `3944545`. This is a behavior-preserving extraction for Warden, ash-harvest and borrowed-duty executors. No engine, desktop, game or deployment action was taken.

## Boundary

`TimberbornFireWalk` owns installed-path rejection/revision and the native launch/refresh protocol. Its one internal native driver binds the actual cached root `Transform`, delegates movement to the existing `TimberbornOwnedWalker`, and forwards `Walker.StartedNewPath` directly. The only caller policy is its current phase-derived Ignore/Outbound/Escape mode.

Each executor still checks the candidate route and sets its destination before launch. Arrival coordinates, phase changes, return selection, cancellation, resource reservations/receipts, timeouts and save/load fields remain in the original caller. Refresh reports rejection to the caller so its existing phase/resource-before-stop ordering remains intact. Load never starts, stops or pauses movement; existing first-owned-tick reconciliation is unchanged. Navigation exceptions propagate as before; the helper adds no recovery, resource guard or invented successful receipt.

Carry-emergency remains explicit. It can claim native delivery from a refresh callback, checks exact manager ownership and protects a foreign executor's installed path after a partial handoff. Neither its source nor `TimberbornOwnedWalker` or `FireSafetyField` changed.

Native ordering remains: `Walker.FindPath` starts its follower at IL0089, invokes StartedNewPath at IL00a1, then stores its destination at IL00a8. An unsafe callback pauses the late mover; the launch caller stops after native return. A refresh caller retains responsibility for its phase-dependent stop. Native BehaviorManager installs a returned executor only after Decide completes, so this helper does not demand manager ownership during initial launch callbacks.

## Executed proof

- Twelve combined protocol cases exercise synchronous callback ordering, safe outbound/escape routes, Running and immediate Success launch statuses, failure status, native exception propagation, revision-triggered and unsolicited path events, ignored phases, stopped-walker refresh, one unsubscribe and all three active Load paths doing no movement.
- The first draft tests failed with the absent shared boundary. A separate counterfactual changed callback Pause to Stop on the final source: the discriminating test failed *inside* native-driver launch, expecting pause but observing stop. Restoring Pause makes the suite pass. This is an intentionally invalid mutation, not a newly discovered production bug.
- Actual production Awake/native ComponentCache fixtures cover all four executor Transform bindings. For the three extracted callers they follow the cached reference into the real driver, exercise real native pause/release, prove a foreign-disabled mover stays disabled, dispatch the actual native event in an ignored phase, and verify disposing twice removes exactly our handler while another handler survives.
- Existing actual-native stop tests preserve destination/path cleanup ordering. Six resource-poison tests now pass through the real driver and still pause only an active owned mover, preserve the destination and keep saves blocked.
- **Final focused suite: 31 passed. Full native project: 1,372 passed, zero failures/skips.** Existing borrowed persistence, emergency ownership, sortie, cargo and route-policy tests remain green.

The driver protocol fixture scripts the native operation boundary; it does not run a Unity Transform.position getter or a successful full Walker.GoTo. The cached Transform fixture uses a reference sentinel only. Real native event subscription, pause/release and native stop evidence are separate from full live path travel. Installed-method IL evidence is retained in `/tmp/wildfire-native-component-startup-audit/`; test logs are in `/tmp/wildfire-shared-fire-walk/`.

## Scope and size

Five production files changed: three callers plus two new helper/driver files. Four internal types were added (mode enum, driver interface, helper, native driver), with no new public contract. New helper/driver code is 127 lines; the three callers shrink by 56 net lines: **net production +71 lines**, not a raw line-count reduction. Warden/Ash/Borrowed files are now 289/299/165 lines. The additional boundary makes native event ordering independently testable; it does not introduce generic executor, ownership or failure policies.

## Controller acceptance still required

The parent reports that the earlier deployed `85961c2` build now demonstrates charging, retreat/return activity and saved private Water1, but not actual suppression. That live result predates this extraction. Once a safe checkpoint exists, the sole controller should repeat a supplied-station response and compare fetch/approach/retreat/return/save behavior, then exercise ash and borrowed-duty trips where their prerequisites are available. Never treat protocol tests as proof of completed spraying or all three live workflows.

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj \
  --filter 'FullyQualifiedName~TimberbornFireWalkTests|FullyQualifiedName~NativeExecutorTransformTests|FullyQualifiedName~NativeResourcePoisonMovementTests|FullyQualifiedName~TimberbornOwnedWalkerTests|FullyQualifiedName~BorrowedDutyPersistenceTests|FullyQualifiedName~FireRouteSafetyTests'
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj
```
