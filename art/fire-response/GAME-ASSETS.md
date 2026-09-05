# Native game asset preview

## Build

Clone the official exporter into `~/repos/timbermesh`. The tested source revision is `f768b573963d45b3b17f0334e0cdb64c17a8e794` (Mechanistry Timbermesh 1.2.0).

```sh
blender --background --factory-startup --python scripts/art/export_fire_response.py
```

This reads the individual GLBs and writes `game-mod/`: eight native Timbermeshes, placeable blueprint definitions, two native comparison references, localization and a standalone mod manifest. `integration.json` records exported triangle counts, materials and footprint dimensions. The exporter merges static parts into one node per asset while retaining material submeshes.

## Material and coordinate boundary

The game exports intentionally use native material names verified in the shipped example Blender file and the official ShantySpeaker Timbermesh. The original GLBs retain the authored colors and textures. Native materials can change the apparent color and texture placement; these need visual review in the game.

The official exporter converts Blender `(x,y,z)` into Unity `(-x,z,-y)`. Before export, each model is moved from its centered art origin into the positive footprint quadrant after conversion and raised to put its lowest point on the ground. The small gear displays use their original scale, not the enlarged overview scale.

## Install and review

Install `game-mod/` as `~/Documents/Timberborn/Mods/WildfireAssetPreview` while Timberborn is stopped and enable **Wildfire Asset Preview**. Coordinate installation and startup through the shared Wildfire QA lock. The items appear in Decorations with **[Art Preview]** names, are free and place finished. Both factions have access for comparison. Native Lodge and Barrack reference items load the game's models without copying them into this repository.

```sh
bun scripts/prepare-fire-response-review.ts "<50x50 Fuel fixture.timber>"
```

The review utility validates flat support for each pad, copies the source archive into a new QA evidence directory, clears a bounded comparison area in the copy, adds the eight assets and two native references, and creates a uniquely named save in **Wildfire Asset Review**. The source save is never overwritten. Review stays on this disposable save.

## Scope

These are real native renderable and selectable preview entities. The station and bell do not employ beavers or suppress fire. Gear is displayed as standalone objects; wearable attachment, character clearance, animation and gameplay integration remain separate work. The footprint and collision boxes are provisional, conservative preview bounds. These blueprints are intentionally separate from the shipping Wildfire mod.
