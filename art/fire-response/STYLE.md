# Faction style study

## References inspected

- Installed `StreamingAssets/Modding/TimberbornExampleModels.blend`: inspected the Folktails Aquatic Farmhouse, Smelter and Rooftop Terrace collection inventory and material/image definitions. It contains dedicated base wood, irregular planks, thatch, painted metal, windows and details atlases. The attempted full farmhouse render in Blender 5.1 had abnormal object bounds and was unusable; it is not visual-match evidence.
- [Mechanistry Folktails developer log](https://steamcommunity.com/games/1062090/announcements/detail/3041598200423561694): visually inspected its lodge settlement screenshot.
- [Mechanistry Iron Teeth developer log](https://steamcommunity.com/games/1062090/announcements/detail/2987563884523313737): visually inspected its animated industrial settlement image.

The developer-log images are historical references, not screenshots of the current installed build. Subsequent live comparison used Timberborn 1.1.2.4, with the native Lodge and Barrack placed beside the eight new assets. Final evidence is in `screenshots/final-art-comparison.jpg` and `final-equipment-closeup.jpg`.

## Observations and application

| Aspect | Folktails reference | Iron Teeth reference | Application |
| --- | --- | --- | --- |
| Timber | Light siding with darker structural members | Dark timber remains prominent even on machinery buildings | Pale bell frame and bucket; dark station framing, floor and equipment backing |
| Roofs | Light timber in the lodge screenshot; dedicated thatch in the installed material inventory | Muted green roof surfaces, strong perimeter framing and truncated roof shapes | Reed bell canopy versus hipped green station roof |
| Construction detail | Readable planks, braces and small window/door details | Reinforced joints, bands, pipes and visible mechanisms | Rope lashings and deck boards versus rivets, valves, pump and motor |
| Color | Warm, light timber | Dark brown/green with orange machinery and metal accents | Shared faction palettes across buildings and carried gear |
| Surface | Grain and painted material information on simple forms | Strong material boundaries and repeated mechanical detail | Original embedded grain textures, worked edges and component-level color |

## Art decisions

The first pass made the station look like a generic metal shed. The revised station keeps wood structurally prominent and uses metal at joints and machinery. Silhouette and material separation matter more than adding polygons uniformly. Tiny rivets and stitches support close views, while roof shapes, timber tone and machinery remain readable at settlement distance.

Gear is an extrapolation from faction buildings, not a reproduction of an existing firefighter outfit. The tailored navy coat has a rounded waist, collar, open armholes, bindings and utility pockets. The helmet has a hollow crown and annular brim. Both remain static and have not been fitted to a beaver. Sprayer and helmet need native character fitting. The fan currently uses the Iron Teeth palette; the berm is neutral earth and stone.

The portable source textures and new geometry are original. The native export references installed game materials without copying their textures. No extracted game mesh or texture is included in the deliverables. The refinement module runs before export; UVs and material images are embedded in the GLBs. Source triangle counts in the manifest exclude modifiers and should not be treated as the exported triangle budget.

## Verification

Inspect the complete overview after regeneration, including roof closure, equipment placement and labels. Reimport all eight GLBs and check for UVs, loaded embedded images and mesh geometry. The portable exports were reimported for native conversion; all eight Timbermeshes loaded in-game with bounded UVs and valid material references. Visual review covered the whole scene, multiple angles and equipment close-ups. The final art pass is accepted for static presentation; wearable fitting and gameplay are separate integration gates.
