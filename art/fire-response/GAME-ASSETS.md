# Native game asset preview

## Build

Clone the official exporter into `~/repos/timbermesh`. The tested source revision is `f768b573963d45b3b17f0334e0cdb64c17a8e794` (Mechanistry Timbermesh 1.2.0).

```sh
blender --background --factory-startup --python scripts/art/export_fire_response.py
```

This reads the individual GLBs and writes `game-mod/`: eight native Timbermeshes, placeable blueprint definitions, two native comparison references, localization and a standalone mod manifest. `integration.json` records exported triangle counts, materials and footprint dimensions. The exporter merges static parts into one node per asset while retaining material submeshes.

## Material and coordinate boundary

The game exports intentionally use native material names verified in the installed 1.1 material inventory and faction material-collection blueprints. The preview appends the opposite faction's atlas material collection so both factions can render in one save. The original GLBs retain the authored colors and textures. Native materials can change the apparent color and texture placement; these need visual review in the game.

The official exporter converts Blender `(x,y,z)` into Unity `(-x,z,-y)`. Before export, each model is moved from its centered art origin into the positive footprint quadrant after conversion and raised to put its lowest point on the ground. The small gear displays use their original scale, not the enlarged overview scale.

## Install and review

Install `game-mod/` as `~/Documents/Timberborn/Mods/WildfireAssetPreview` while Timberborn is stopped and enable **Wildfire Asset Preview**. Coordinate installation and startup through the shared Wildfire QA lock. The items appear in Decorations with **[Art Preview]** names, are free and place finished. Both factions have access for comparison. Native Lodge and Barrack reference items load the game's models without copying them into this repository.

```sh
bun scripts/prepare-fire-response-review.ts "<50x50 Fuel fixture.timber>"
```

The review utility validates flat support for each pad, copies the source archive into a new QA evidence directory, clears a bounded comparison area in the copy, adds the eight assets and two native references, and creates a uniquely named save in **Wildfire Asset Review**. The source save is never overwritten. Review stays on this disposable save.

## Scope

These are real native renderable and selectable preview entities. The station and bell do not employ beavers or suppress fire. Gear is displayed as standalone objects; wearable attachment, character clearance, animation and gameplay integration remain separate work. The footprint and collision boxes are provisional, conservative preview bounds. These blueprints are intentionally separate from the shipping Wildfire mod.

## In-game result — Timberborn 1.1.2.4 experimental

The generated review copy loaded successfully with **Wildfire Asset Preview**, Camera Bookmarks and Hats enabled. All eight asset shapes were visible alongside the native Lodge and Barrack references. Selecting the Warden Station showed its localized Art Preview panel and selection highlight. The reviewed session remained paused.

The preview's first startup exposed a CSV header error, fixed to `ID,Text,Comment`. The first model-loading attempt exposed faction-scoped materials, fixed by adding opposite-faction atlas materials to the preview's material collections. Subsequent loading with the existing Wildfire simulation mod hit an unrelated `MissingMethodException` for `Inventory.Take(GoodAmount)` in `TimberbornFertilizeDesignationService.UpdateSingleton`. Wildfire was disabled in the mod UI for the successful art-only review; it remains disabled for the open preview session. No source save was modified.

### Comparison findings

- Building silhouettes and overall scale are readable beside native faction models. The station is taller than the native Barrack and the bell taller than a single-storey Lodge; these proportions need art approval.
- Native material substitution is visibly inadequate as a final texture solution: generic UV islands sample atlas details unintentionally, produce oversized grain and turn the station roof blue. Author atlas-specific UVs or supply dedicated game materials before calling the art finished.
- The earth berm incorrectly looks wooden because the initial Iron Teeth mapping uses the dark-wood material for earth/stone. This is a visible material-authoring defect, not a geometry-load failure.
- Gear renders at its actual export size and is small compared with buildings. It remains static ground-level equipment, not fitted or animated wearables.
- Source-model ground centering and single merged mesh nodes work. Selection uses conservative box colliders; detailed collision and building construction behavior remain outside this preview.

See `screenshots/` for native Steam screenshots. The close-up intentionally preserves the material problems for comparison.
