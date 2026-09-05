# Faction style study

## References inspected

- Installed `StreamingAssets/Modding/TimberbornExampleModels.blend`: inspected the Folktails Aquatic Farmhouse, Smelter and Rooftop Terrace collection inventory and material/image definitions. It contains dedicated base wood, irregular planks, thatch, painted metal, windows and details atlases. The attempted full farmhouse render in Blender 5.1 had abnormal object bounds and was unusable; it is not visual-match evidence.
- [Mechanistry Folktails developer log](https://steamcommunity.com/games/1062090/announcements/detail/3041598200423561694): visually inspected its lodge settlement screenshot.
- [Mechanistry Iron Teeth developer log](https://steamcommunity.com/games/1062090/announcements/detail/2987563884523313737): visually inspected its animated industrial settlement image.

The developer-log images are historical references, not screenshots of the current installed build. No live game session was used. Current-version material and silhouette parity still requires an in-game comparison.

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

Gear is an extrapolation from faction buildings, not a reproduction of an existing firefighter outfit. The coat remains a static stylized shell and is not ready to wear on a beaver. Sprayer and helmet need native character fitting. The fan currently uses the Iron Teeth palette; the berm is neutral earth and stone.

The textures and new geometry are original. No extracted game mesh or texture is included in the deliverables. The refinement module runs before export; UVs and material images are embedded in the GLBs. Source triangle counts in the manifest exclude modifiers and should not be treated as the exported triangle budget.

## Verification

Inspect the complete overview after regeneration, including roof closure, equipment placement and labels. Reimport all eight GLBs and check for UVs, loaded embedded images and mesh geometry. This verifies portable art exports, not Timbermesh support or game-scale fidelity.
