# Warden potential coverage display

The station implements native `IBuildingWithRange` for a selection display of its existing 20-tile potential response sphere. This is geometric coverage, not a navigation promise: a safe route and approach are still required. Selection/dispatch policy, range, resource handling, and simulator reads are unchanged.

## Native contract and coordinates

- `RangedEffectBuildingUIConfigurator.ProvideTemplateModule` decorates `IBuildingWithRange` with `BuildingWithRangePreviewUpdater`. Its `BuildingWithRangeUpdateService` handles native entity registration/deletion, selection, visible-level changes, and construction-mode changes. The native `RangeTileMarkerService`/`AreaTileDrawer` owns rendering.
- Native range groups union all buildings with the same `RangeName`. Selecting a Warden station therefore shows the potential coverage of registered, finished Warden stations, not exclusively the selected station. No object highlighting is requested.
- `StackableBlockService.GetGroundOrStackableBlocks(columns, finishedOnly: true)` yields visible walkable surfaces. `TerrainService.OnGround(point)` tests terrain below the point and air at the point. `IsVisibleStackableAt` checks a stackable block at `point.z - 1` (installed IL offsets `001f`–`002c`). These points already have surface height; adding another level would be incorrect.
- Filtering maps each native surface to `(x + .5, z, y + .5)`, exactly as `WardenTargetSelector` maps fire cells. The center is the same `station.Access.Accesses[0]` used by `WardenExecutor`. Native drawing centers its tiles through `GridToWorldCentered`.
- Candidate columns are clamped to native `MapSize.TotalSize`. The native surface service determines vertical visibility. Drawing performs no path search or field observation; recalculation cost follows the native range UI and its group size.

## Lifecycle boundary

The first draft supports selection of placed, finished stations with native access. Preview, unfinished, and empty-access instances return no surfaces. It does not invent a placement-preview anchor from the footprint center. The native preview decorator is still supplied by the game; a future placement display needs separate proof of final access during that lifecycle.

## Validation and remaining proof

`NativeWardenRangeTests` passes 12 focused managed cases. It executes actual native template composition to prove exactly one range component and the game's preview updater; actual native terrain getters prove solid-versus-surface height; supplied native component state proves the early lifecycle exclusions without invoking rendering or surface services. Geometry cases include horizontal/vertical radius edges, a 12–16–20 diagonal, and an off-level exclusion. Candidate enumeration covers every in-range map column in its fixture.

These tests do not instantiate a complete native world or render the overlay. Live QA must select a placed station, inspect ground and platform coverage at visible-level boundaries, compare the 20-tile edge to an eligible target, inspect multiple-station union behavior, and confirm deselection/placement preview cleanup. A highlighted tree or tile is not proof that an actual responder can navigate safely to it. No engine, game, or deployment was run for this source change.
