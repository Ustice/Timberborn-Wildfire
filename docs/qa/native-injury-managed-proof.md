# Native injury: managed proof and remaining controller fixture

This is a tests-only native API proof. No automatic damage, injury dose/cadence policy, dispatch binding, UI, admission switch, or separate health ledger is introduced. Natural ignition and real injury activation remain outside release readiness until avoidance and cargo recovery are validated.

Run the bounded proof with the installed Timberborn build:

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj \
  --configuration Release --filter 'FullyQualifiedName~NativeInjury'
```

The fixture reads `StreamingAssets/Modding/Blueprints.zip` beside the configured managed assembly directory. It constructs native Blueprint/Need data using the installed Injury limits and critical/work-refusal specs. The native Blueprint constructor copies specs; the fixture uses the copied NeedSpec so `HasSpec<CriticalNeedSpec>()` is real. The installed InjurableNeedSpec marker is not exercised by this NeedManager/WorkRefuser fixture; it makes no claim about native random-injury admission. Only component dependency fields which would normally be wired by the world are supplied manually. No GameObject is created, no native liveness result is forged, and no automatic entity lifecycle is claimed.

## What executes

- Native `NeedManager.ApplyEffect(in InstantEffect)`, `Need.Apply`, and native clamping. Negative effects add to existing injury; points stay within the installed [-1,0] limits. Positive effects use native treatment polarity. Fractional test inputs are boundary probes, not adopted wildfire doses.
- Actual `NeedManager.Save` and `Load`, using native EntitySaver/EntityLoader, SerializedEntity, ObjectSaver and SerializedNeedValueSerializer. Restoring saved points then adding injury preserves preexisting injury. This tests component serialization in memory, not the whole game save pipeline.
- Missing Injury silently does nothing; disabled Injury remains registered but changes no points. A future caller must check both existence and enabled capability rather than treat absence/no change as damage applied.
- A saturated need emits no second critical-state transition. Therefore event count alone cannot establish whether an effect call completed.
- A throwing critical-state subscriber leaves the newly mutated points and suppresses later listeners. Repeating the effect adds more injury without replaying the missing state transition. Loading also sets points before emitting callbacks, and emits critical-state callbacks even if already critical; load can fail after restoring points.
- Actual `WorkRefuser.OnNeedChangedCriticalState` → `UpdateRefuseWork` → `Worker.Unemploy` → `Workplace.UnassignWorker`. Even a fractional negative Injury value triggers work refusal with the current blueprint. The worker's workplace reference is cleared and the workplace assignment list emptied before employer/worker notifications. Successful order is workplace-unassigned, worker-unemployed, refusal-changed, injury-active. Healing clears refusal but does not re-employ the old job.
- A throwing workplace-unassigned subscriber leaves injury, refusal and unemployment mutated, while worker-unemployed and injury-active listeners are skipped. Repeating injury does not repair that event chain.

The fixtures deliberately do not introduce an `ApplyOnce` token API: an attempted mutation needs real persisted attempt ownership before such a promise can be made. Native points belong to NeedManager; the future exposure module owns its observation interval/cooldown, with a distinct uncertain-operation outcome. A post-mutation exception cannot safely be relabeled unsupported or automatically retried.

## Boundaries still requiring Unity/Timberborn

The employment fixture calls real managed listeners, but it does not construct a live employee, attach every game/mod subscriber, run BehaviorManager, advance navigation, or populate a colony inventory. It therefore does not prove reservation or carried-goods conservation across the full native unemployment lifecycle, nor the next tick of Warden/Ash/Carry executors.

Native `Worker.Employed` uses BaseComponent liveness for its workplace. That path reaches cached Unity GameObject state; earlier isolated native experiments established that fabricating a GameObject outside Unity triggers engine-only Object initialization. We do not work around that with a fake truthy object. The WorkRefuser/Unemploy path itself is executable using plain native references and lists, so this proof stops after those real callbacks.

Source expectations to verify rather than count as managed gameplay proof:

- Warden notices lost employment/critical needs and retreats, retaining equipment water and releasing only its owned reservation.
- Ash cancels before collection on lost employment. Once it has a receipt and carried ash, it still returns/deposits without minting or losing units, even while no longer employed.
- CarryEmergency rejects new critical-need admissions. An already owned escape preserves cargo; its critical-needs Holding limitation is unresolved. If injury arrives before a new emergency claim, admission ordering can prevent rescue. Do not clear Injury or grant immunity based on job type to hide that gap.

## Optional controller fixture: concrete next proof

This is a proposed fixture, not an implemented QA command. The single live controller can add/use a development-only explicit process opt-in on a copied disposable save. It should select one stable native EntityId and execute exactly one public NeedManager InstantEffect, with no tick-driven damage. Record pre/post points, need enabled/critical flags, current behavior/executor, worker/workplace IDs, carried good/count, raw stock/capacity reservations and source/destination inventory totals.

Exercise the event at a real Warden fetching, approaching/spraying and returning; an Ash worker before collection and after a positive receipt; and a carrier before emergency admission, escaping and Holding. Inspect immediately after the synchronous call and after the next ordinary behavior tick. Require actual safe-route/arrival receipts, not canceled-walk completion. Save/reload healthy cases in each owned phase and verify points are restored once with native reservation/cargo ownership intact. Test partial failures only in a disposable run whose save is explicitly blocked after the injected failure; inject one temporary throwing subscriber, capture the changed points/job state, and do not retry the dose or overwrite the baseline save.

An empty-handed idle/wander evacuation is separate movement coverage. General injury integration alone does not demonstrate avoidance. Native injury visuals, medical-bed behavior, player control, death and deletion must be observed in the game before release claims.

## Recorded validation

On the installed native build, the full Release native suite passed 715 tests with zero failures/skips, including eight new Injury fixtures. Log: `/tmp/wildfire-responder-feasibility/injury-full-tests.log`. No Unity or game process was launched for this proof.
