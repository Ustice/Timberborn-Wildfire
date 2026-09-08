# Warden helmet authored-origin correction

The helmet's attachment position now reverses the building-preview export translation: `(-0.5, -0.18444548547267914, -0.5)`. Rotation remains zero, scale remains one, and the native parent remains `#Head`. Mesh bytes, atlas assignments, native prefab ownership and visibility are unchanged. This corrects a proved asset-origin error; final native head fit and animated orientation still require the controller's live check.

## Geometry and native contract

`scripts/art/build_fire_response.py` constructs an identity-root helmet around its own origin: brim height 0.028–0.072, crown center height 0.07, crest center height 0.22. `refine_fire_response.py` adds the chin strap below that origin. The GLB is exported before the later display layout and equipment scale of two. No whole-beaver head-height translation is embedded. The builder explicitly calls native ear clearance provisional.

`export_fire_response.py` then treats this gear as a 1×1 building preview. It translates Blender roots by `(-size.x/2, -size.y/2, -low.z)`; the official export converts Blender axes to native `(-x,z,-y)`. Consequently the shipping helmet's vertices contain `(0.5, 0.18444548547267914, 0.5)` of preview translation. Its only timbermesh node has identity position/rotation/scale.

| Native axes | Authored GLB bounds | Shipping vertex bounds |
| --- | --- | --- |
| X | −0.300000012 to 0.300000012 | 0.199999988 to 0.800000012 |
| Y | −0.184445485 to 0.244999999 | 0 to 0.429445475 |
| Z | −0.300000012 to 0.300000012 | 0.199999988 to 0.800000012 |

Every one of 4,958 shipping vertices, after the correction, matches an authored GLB position within 5.402×10⁻⁸ units. UV seams account for differing vertex counts. This is a translation correction, with no demonstrated scale error. Source geometry does not establish the exact native `#Head` pivot or its animated axes; the correction restores authored helmet coordinates rather than claiming final fit.

Installed `TemplateAttachments.CreateAttachment` IL finds the named parent, instantiates beneath it, applies definition position/rotation with `SetLocalPositionAndRotation` (0043), then applies definition scale (0054). It does not recenter imported vertices. Native adult FarmerHat attachments also use `#Head`, zero position/rotation and unit scale. `WardenHelmetPresentation.CreateHelmet` separately verifies exactly one `#Head` under the character model and checks the resulting attachment's actual parent. This supplies no evidence of a wrong-anchor selection.

## Verification and live boundary

- `bun test tests/warden-helmet-origin.test.ts`: passes using the actual GLB, compressed protobuf timbermesh, and shipping attachment pose. It requires neither Blender nor the game. Restoring the old zero pose makes this same regression fail with maximum nearest-position error 0.500000024; restoring the correction passes.
- `dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --filter FullyQualifiedName~NativeWardenHelmetTests`: 6 passed, zero failed/skipped. Actual native JSON merge preserves all existing adult attachments; existing lazy visibility/ownership checks remain green.
- All 51 script tests pass; the blueprint check validates 375 generated blueprints. Typechecking passes after the standard Prisma client generation.
- Detailed decoded geometry, exact artifact hashes, candidate diff, and counterfactual log: `/tmp/wildfire-warden-helmet-origin/`.

The Hats mod was enabled in the preceding live setup. An orange object in a screenshot cannot establish Wildfire attachment identity by appearance alone; the export recentering proof is independent of that attribution. Keep other mods and preferences unchanged.

One controller candidate should first identify `Wildfire.WardenHelmet.IronTeeth` beneath its verified native `#Head` by attachment/render identity or controlled active-versus-Idle visibility. Then inspect the identified helmet on an unselected Warden from more than one view while standing and walking. Check brim/head overlap, strap placement, orientation and clipping. No source-only result proves native fit or authorizes further guessed offsets. No engine, Blender, desktop, or deployment action was performed for this correction.
