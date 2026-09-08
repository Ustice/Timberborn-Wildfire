# Warden potential coverage display

The station implements native `IBuildingWithRange` for selection and placement displays of its existing 20-tile potential response sphere. This is geometric coverage, not a navigation promise: a safe route and approach are still required. Selection/dispatch policy, range, resource handling, and simulator reads are unchanged.

## Native contract and coordinates

- `RangedEffectBuildingUIConfigurator.ProvideTemplateModule` decorates `IBuildingWithRange` with `BuildingWithRangePreviewUpdater`. Its `BuildingWithRangeUpdateService` handles native entity registration/deletion, selection, visible-level changes, and construction-mode changes. The native `RangeTileMarkerService`/`AreaTileDrawer` owns rendering.
- Native range groups union all buildings with the same `RangeName`. Selecting a Warden station therefore shows the potential coverage of registered, finished Warden stations, not exclusively the selected station. No object highlighting is requested.
- `StackableBlockService.GetGroundOrStackableBlocks(columns, finishedOnly: true)` yields visible walkable surfaces. `TerrainService.OnGround(point)` tests terrain below the point and air at the point. `IsVisibleStackableAt` checks a stackable block at `point.z - 1` (installed IL offsets `001f`–`002c`). These points already have surface height; adding another level would be incorrect.
- Filtering maps each native surface to `(x + .5, z, y + .5)`, exactly as `WardenTargetSelector` maps fire cells. The center is the same `station.Access.Accesses[0]` used by `WardenExecutor`. Native drawing centers its tiles through `GridToWorldCentered`.
- Candidate columns are clamped to native `MapSize.TotalSize`. The native surface service determines vertical visibility. Drawing performs no path search or field observation; recalculation cost follows the native range UI and its group size.

## Lifecycle boundary

Placed, finished stations use actual `Accesses[0]`; unfinished or empty-access placed stations return no surfaces. A positioned preview uses native `BuildingAccessible.CalculateAccess()`; an unpositioned preview returns no surfaces. The preview path does not require finished state or an already-published access list. Neither path invents a footprint-center anchor.

The shipping station declares `ForceOneFinalAccess=true`, `LocalAccess=(1.5,0,3.5)`, and entrance grid coordinates `(1,3,0)`. Native `CalculateAccess` delegates to `BuildingAccessibleSpec.CalculateAccessFromLocalAccess`: world-to-grid conversion, `BlockObject.TransformCoordinates`, then grid-to-world conversion. The block's current native placement owns rotation, translation, and flip handling. `OnEnterFinishedState` calculates the same value and publishes it as the sole access. Shipping `Flippable=false` remains unchanged.

`BuildingAccessible.OnPostPlacementChanged` updates its private calculation but does not publish `Accessible.Accesses`; that list can still describe the previous preview placement until `OnPreviewSelect`. Calling `CalculateAccess` avoids depending on listener order. `PreviewPlacer.GetPositionedPreviews` calls `Preview.Reposition` before `PreviewShower` calls `Preview.Show`. Native `BlockObject.Reposition` updates placement and positioned blocks before notifying post-placement listeners. The native range updater marks itself dirty there; the subsequent preview selection callback registers/draws it. `RangeTileMarkerService.DrawArea` enumerates the active preview's range afresh. Hiding/exiting uses the existing native preview cleanup. The mod adds no update loop or render state.

## Validation and remaining proof

`NativeWardenRangeTests` passes 12 focused managed cases. It executes actual native template composition to prove exactly one range component and the game's preview updater; actual native terrain getters prove solid-versus-surface height; supplied native component state proves the early lifecycle exclusions without invoking rendering or surface services. Geometry cases include horizontal/vertical radius edges, a 12–16–20 diagonal, and an off-level exclusion. Candidate enumeration covers every in-range map column in its fixture.

The full native suite passes 1,331 tests with zero failures or skips on source `bf8f574`. Logs: `/tmp/wildfire-warden-range-focused.log` and `/tmp/wildfire-warden-range-full.log`.

The preview addition's managed comparison uses the actual shipping access/entrance/size values and installed native transforms, positioned entrance, `OnPostPlacementChanged`, and `OnEnterFinishedState`/`SetAccesses`. Sixteen combinations cover two translations/elevations, four rotations, and both native flip modes (flipping is a compatibility test only). All calculated anchors match native centered entrances and finished access. Seven changed placements demonstrate a stale access list before publication; preview coverage still uses the current calculation. A separate assertion changes the calculation spec after finishing to prove placed selection still uses published access. The fixture supplies managed block backing state and an actual component cache with no validators; it does not execute full preview positioning or native rendering.

On preview source `a6fb8f7`, the 13 focused tests and full 1,349-test native suite pass with zero failures/skips. Logs: `/tmp/wildfire-warden-preview-focused.log` and `/tmp/wildfire-warden-preview-full.log`. Native IL, standalone managed comparison, and exact installed assembly/spec hashes are retained in `/tmp/wildfire-preview-anchor-proof/`.

These tests do not instantiate a complete native world or render the overlay. Live QA must move and rotate the placement preview, confirm a matching anchor after construction, select a placed station, inspect ground and platform coverage at visible-level boundaries, compare the 20-tile edge to an eligible target, inspect multiple-station union behavior, and confirm deselection/tool-exit cleanup. A highlighted tree or tile is not proof that an actual responder can navigate safely to it. No engine, game, or deployment was run for these source changes.
