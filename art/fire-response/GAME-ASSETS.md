# Native game asset preview

## Build

Clone the official exporter into `~/repos/timbermesh`. The tested source revision is `f768b573963d45b3b17f0334e0cdb64c17a8e794` (Mechanistry Timbermesh 1.2.0).

```sh
blender --background --factory-startup --python-exit-code 1 --python scripts/art/export_fire_response.py
```

This reads the individual GLBs and writes `game-mod/`: eight native Timbermeshes, placeable blueprint definitions, two native comparison references, localization and a standalone mod manifest. `integration.json` records exported triangle counts, materials and footprint dimensions. The exporter merges static parts into one node per asset while retaining material submeshes.

## Material and coordinate boundary

The game exports intentionally use native material names verified in the installed 1.1 material inventory and faction material-collection blueprints. The preview appends the opposite faction's atlas material collection so both factions can render in one save. The original GLBs retain the authored colors and textures. `scripts/art/native_materials.py` assigns explicit atlas regions per component. Timber follows each component's long axis; metal excludes embossed atlas motifs; roof panels and battens use separate regions; navy cloth and canvas use clean Details regions; orange equipment uses a fine Plaster_Orange region; stone uses Plaster_White; earth uses opaque DirtCommon. Every exported UV is checked to remain within 0–1, because out-of-range coordinates can sample neighboring textures in the game's combined atlas. The original GLBs are not rewritten by this pass.

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
- The first pass exposed generic UV islands sampling atlas details unintentionally. The refined pass replaces them with component projections and bounded atlas regions. The station now has consistent green roof planking, and wood/metal surfaces no longer sample arbitrary atlas motifs.
- The berm uses opaque DirtCommon for packed earth and Plaster_White for facing pieces. Stone is visibly separate from the earth, and foundations share the same stone finish.
- Gear renders at its actual export size and is small compared with buildings. It remains static ground-level equipment, not fitted or animated wearables.
- Source-model ground centering and single merged mesh nodes work. Selection uses conservative box colliders; detailed collision and building construction behavior remain outside this preview.

### Refined material review

The updated eight meshes exported successfully and loaded through direct computer use. The review autosave retained the camera and paused state. Fire Berm and Smoke Fan selection were also checked. The Player log contains no exception or missing-material error, but reports `Too many atlases loaded (3)` for this combined-faction review setup. This preview is not a production atlas-budget validation.

`screenshots/atlas-refined-comparison.jpg` shows the revised set with native references; `screenshots/station-atlas-refined.jpg` shows the corrected roof. Earlier screenshots remain as before-pass evidence. No native textures are redistributed. The completed art pass uses a tailored navy coat, canvas bindings, muted orange equipment, warm metal fittings and distinct pale stone. The helmet now has an annular brim and an open, thickened dome. Final export: 29,748 triangles across eight merged nodes. The additional geometry is concentrated in the coat and helmet; building geometry is unchanged. Wearable fitting remains unverified.

Wildfire simulation remains disabled because of the separate 1.1 compatibility error. The preview remains open and paused.


## Final art acceptance

The complete set was regenerated, converted, reloaded and reviewed in the game from several angles. The final coat selection and close-up render were verified. `screenshots/final-art-comparison.jpg` records all eight assets and both native references; `screenshots/final-equipment-closeup.jpg` records the gear. The final Player log has no exception or missing-material error; the combined-faction atlas warning remains. The final visual pass is accepted for static presentation, with character fitting, animation and gameplay still outside this art preview.
