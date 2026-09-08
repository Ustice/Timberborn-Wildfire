# Explicit native owned-world domain

Source checkpoint: `10a49bd`. This is staged owned-world support, not production activation or a running full-height colony.

Owned formation and complete restore require an immutable domain reading supplied by `TimberbornInitialEnvironmentCaptureProvider.ForOwnedWorld`. The native `MapSize.TotalSize` supplies the simulation domain; its `TerrainSize` supplies terrain storage bounds. Horizontal dimensions must agree and terrain must fit within the world. The legacy provider, `RuntimeInitializer.ReadGrid`, and existing persistence codecs are unchanged. Missing domain evidence or a requested/saved grid differing from the actual native world rejects before backend allocation; no resize or save upgrade occurs.

The installed MapSize blueprint declares maximum terrain height22 and ten layers above it. Actual managed `MapSize.Initialize` produces terrain depth23 and world depth33; a second supplied native spec produces6/8, showing that the adapter does not add a fixed ten. Native positioned blocks at z32 fit the33-layer owned grid; z33 does not.

Terrain voxel reads remain bounded by native terrain allocation. Native `TerrainService.GetAllHeightsInCell` iterates z strictly below `TerrainSize.z` (installed iterator `MoveNext`, IL0095–00a8), omitting a surface ceiling exactly at that boundary. Only the owned provider instead enumerates actual thread-safe terrain columns using native count, ordinal index, floor and ceiling. It validates storage bounds, ordering and exact ceiling-to-index resolution before reading the original native soil values. The legacy iterator remains unchanged. Soil at the terrain ceiling maps to an air cell in the larger world; no soil value is synthesized. Liquid overlap is projected into the full world by the existing fixed projector.

Validation on the installed macOS assemblies:

- 51 focused tests passed: actual native domain initialization, terrain-top and hollow two-column soil, malformed count/index/ceiling rejection, exact grid reread, highest native footprint, and mandatory owned evidence.
- Full native suite:1,238 passed, zero failed/skipped.
- Counterfactual: extending the voxel loop to world depth makes the top-soil test fail with actual native `TerrainMap.UnsafeIsTerrainVoxel` array overflow. Restoring terrain-bounded reads makes the nine native-domain cases pass.
- Before the owned column change, the same top-soil fixture failed because the native iterator omitted its ceiling. The corrected path reads the supplied native moisture12/contamination0.6 at that original column index.

These are actual managed native getters/initializers and supplied native storage, not Unity execution or full native prefab/world loading. The topmost solid is a deliberate boundary fixture, not evidence that an ordinary map contains that geometry. The prior actual OWNED4 engine probe at `e5885d1` used a terrain-sized domain; a separate source-only upper-world probe is being prepared for the sole controller. No ordinary tick, runtime adoption, old-save migration or full-height performance result is claimed. A256×256×33 dense domain contains43.5% more cells than256×256×23; prior23-layer timings do not prove33-layer performance.
