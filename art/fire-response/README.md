# Fire response assets

Original low-poly assets for the faction response concepts in [the design](../../docs/DESIGN.md) and GitHub issues #54–#58. The visual pass is complete and verified in Timberborn 1.1.2.4 beside native faction models. These are static preview assets; gameplay integration remains separate.

## Files

- `fire-response.blend`: editable source scene with named component meshes and a presentation layout.
- `*.glb`: eight individual assets, each exported at its local origin before presentation placement/scaling.
- `overview.png`: rendered contact sheet. Small equipment is shown at twice its export scale.
- `manifest.json`: asset inventory and source polygon triangle counts (before export modifiers).
- `../../scripts/art/build_fire_response.py`: reproducible Blender generator.
- `game-mod/`: installed, native Timbermesh preview mod; see [build and integration notes](GAME-ASSETS.md).
- `screenshots/final-art-comparison.jpg` and `final-equipment-closeup.jpg`: final in-game evidence.

## Asset set

| Asset | Design interpretation | Nominal size |
| --- | --- | --- |
| Fire Bell | Timber frame, reed canopy, brass bell, rope and shelf | About 1.8 × 1.2 × 2.4 units |
| Warden Station | Dark-timber station with green hipped roof, workbench and water reservoir | About 2.9 × 2.1 × 2.3 units |
| Brigade Bucket | Open wooden stave vessel with hoops and carry bail | About 0.4 units wide |
| Warden Sprayer | Backpack reservoir, straps, hose and handheld wand | About 0.9 units high |
| Warden Helmet | Hollow orange crown, open metal brim, rivets and chin strap | About 0.6 units wide |
| Warden Coat | Navy sleeveless coat with shaped waist, collar, open armholes, bindings and pockets | About 0.6 units wide |
| Smoke Fan | Braced industrial fan with separate rotor blades | About 1.4 × 1.1 × 1.8 units |
| Fire Berm | Repeatable packed-earth segment with stone facing | 1.0 × 0.9 × 0.65 units |

## Rebuild

From the repository root:

```sh
blender --background --factory-startup --python-exit-code 1 --python scripts/art/build_fire_response.py
```

Rebuilding overwrites generated assets. Save manual revisions separately or incorporate them in the generator first. Material colors and original grain textures are embedded; no external texture dependencies. Blender source is Z-up; the GLB exporter handles glTF coordinate conversion. Front faces toward Blender -Y. Building pivots sit at ground center; footprint dimensions are proposals pending blueprint decisions.

## Integration boundary

All eight assets are converted, registered, installed and visually reviewed as native Art Preview entities, with provisional footprints and box colliders. Native materials use explicit atlas regions. Buildings and equipment remain static; no workers, construction phases or firefighting rules are bound to these preview entities.

Clothing still needs actual beaver arm/ear/tail clearance, attachment, skinning and animation validation. The helmet has an open underside and the coat is a hollow garment, but these are not evidence of character fit. The sprayer hose/wand is static. Fan and bell components remain separate in the editable source and merged for the static preview.

## Faction refinement

See [the style study](STYLE.md) for inspected references, faction differences, applied changes and remaining fidelity limits. `../../scripts/art/refine_fire_response.py` owns the faction material and detail pass.
