# Fire response assets

Original low-poly design drafts for the faction response concepts in [the design](../../docs/DESIGN.md) and GitHub issues #54–#58. These are proposed visual interpretations, not previously approved dimensions or production-ready Timberborn content.

## Files

- `fire-response.blend`: editable source scene with named component meshes and a presentation layout.
- `*.glb`: eight individual assets, each exported at its local origin before presentation placement/scaling.
- `overview.png`: rendered contact sheet. Small equipment is shown at twice its export scale.
- `manifest.json`: asset inventory and source polygon triangle counts (before export modifiers).
- `../../scripts/art/build_fire_response.py`: reproducible Blender generator.

## Asset set

| Asset | Design interpretation | Nominal size |
| --- | --- | --- |
| Fire Bell | Timber frame, reed canopy, brass bell, rope and shelf | About 1.8 × 1.2 × 2.4 units |
| Warden Station | Dark-timber station with green hipped roof, workbench and water reservoir | About 2.9 × 2.1 × 2.3 units |
| Brigade Bucket | Open wooden stave vessel with hoops and carry bail | About 0.4 units wide |
| Warden Sprayer | Backpack reservoir, straps, hose and handheld wand | About 0.9 units high |
| Warden Helmet | Orange crown, metal brim, rivets and chin strap | About 0.6 units wide |
| Warden Coat | Hollow protective sleeveless shell with fasteners and pockets | About 0.6 units wide |
| Smoke Fan | Braced industrial fan with separate rotor blades | About 1.4 × 1.1 × 1.8 units |
| Fire Berm | Repeatable packed-earth segment with stone facing | 1.0 × 0.9 × 0.65 units |

## Rebuild

From the repository root:

```sh
blender --background --factory-startup --python scripts/art/build_fire_response.py
```

Rebuilding overwrites generated assets. Save manual revisions separately or incorporate them in the generator first. Material colors and original grain textures are embedded; no external texture dependencies. Blender source is Z-up; the GLB exporter handles glTF coordinate conversion. Front faces toward Blender -Y. Building pivots sit at ground center; footprint dimensions are proposals pending blueprint decisions.

## Integration boundary

The assets are not installed or registered as game buildings. Timberborn needs Timbermesh conversion, native material mapping, blueprint footprint/access/construction setup, colliders and gameplay bindings. Clothing needs a native character reference, actual arm/ear/tail clearance, skinning and animation tests; the coat is currently a static sleeveless shell. The sprayer hose/wand is static and needs attachment and animation work. Fan blades and bell pieces are separate meshes but have no authored animation. Berm gameplay and fan airflow rules remain separate simulation work.

Review silhouette and scale in game before accepting the art. No live Timberborn compatibility is claimed by the GLB exports.

## Faction refinement

See [the style study](STYLE.md) for inspected references, faction differences, applied changes and remaining fidelity limits. `../../scripts/art/refine_fire_response.py` owns the faction material and detail pass.
