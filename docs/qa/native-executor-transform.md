# Native executor Transform binding

Source correction: `6d11fbe`, based on `79fa04a`. No receipt or fertilizer prerequisites.

Subsequent structure: [shared owned-job walking](shared-owned-fire-walk.md) moves the Warden/Ash/Borrowed cached Transform binding into one native driver; emergency retains its explicit binding. The historical correction and executed counts below remain unchanged.

## Actual live failure

The controlled Iron Teeth Warden test on deployed `2fd29af` reached its first response at simulation ticks 188–192 and failed while launching the walk to the station. Native `Walker.FindPath` invoked `WardenExecutor.OnStartedNewPath`, which threw `NullReferenceException`. This was before the first fetch; the station still had 20 Water. It does not establish water charging, suppression or a completed trip.

Evidence: `/tmp/wildfire-warden-staffing-live/Player-first-response-failure.log` and `first-response-stack.txt`. The copied exact deployed adapter SHA256 is `6b4141d2a7b8b86e2b6fe53d86499cfcad5ee2d4779d9d81fbba27d91175685f` (module ID in the stack: `ce8a3965b85a4ee1b2c997dade2ead6b`).

Mono reports block offset `0031`. Exact deployed IL first reads `_field.Revision` at `0027`, then the block at `0031` loads `_transform` at `0039` and dereferences `Transform.position` at `003e`. The location is a block offset, not literally a `position` instruction at `0031`.

## Native contract and fix

`BaseComponent.GetComponent<T>()` reads Timberborn's registered component list through `ComponentCache.GetCachedComponent<T>()`. It does not retrieve the cache's separate Unity root Transform. `BaseComponent.Transform` returns `ComponentCache.CachedTransform` directly.

Native `ComponentCache.Initialize` assigns `CachedTransform` from its Unity transform before initializing the BaseComponents. Awake dispatch happens subsequently. A native root Transform can therefore be present while the generic custom-component lookup returns null; waiting for a later path callback cannot repair the wrong lookup.

All four analogous executor lookups now bind `_transform = Transform`:

- Warden response
- Ash harvest
- Borrowed duty
- Loaded-carry emergency

The change preserves every existing physical `.position` read, installed-path prefix check, revision check and navigation rule. It does not replace physical coordinates with Navigator access positions or alter ownership, inventory, phases or arrival semantics. The adapter source audit found these four `GetComponent<Transform>()` calls and no further instance of the same lookup.

## Executed regression and remaining live boundary

`NativeExecutorTransformTests` runs each actual production constructor and Awake against the installed native ComponentCache/TypeIndexMap/GetComponent/Transform getters and actual native path-event subscription. Its supplied cache has a reference sentinel in the native CachedTransform field and no Transform decorator in the component list. Both getter results are checked, then the actual executor field must retain the native cached reference.

All four cases failed before the fix: Awake stored null despite the populated native Transform property. They passed afterward; the combined focused native Transform and existing FireSafety suite passed 17 tests. The fixture does not call sentinel `.position`, fake Unity liveness or claim a rendered GameObject. It proves the exact reference-binding contract; real engine coordinates and successful travel remain controller acceptance tests.

Logs and exact deployed/native IL: `/tmp/wildfire-warden-native-transform/`. No game, Unity, desktop or deployment action was taken. The controller should resume from the preserved healthy supplied-station save and repeat the controlled response, not treat the exception save as a completed job state.

Full native project on `6d11fbe`: **1,352 passed, zero failures or skips**.

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj \
  --filter 'FullyQualifiedName~NativeExecutorTransformTests|FullyQualifiedName~FireSafety'
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj
```
