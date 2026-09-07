# Initial environment projection

`TimberbornInitialEnvironmentProjection.Project` consumes one immutable native environment capture.
It supplies the existing registry's typed baseline and a separate, once-only ambient field overlay.
`SourceCapture` binds the result to the exact copied facts used by initial formation; final native
revalidation and simulator publication remain the formation coordinator's responsibility.

| Captured fact | Material baseline | Initial fields |
| --- | --- | --- |
| Physical solid voxel | Terrain, packed Terrain=1 | Zero |
| Open surface soil, including submerged soil | Terrain, packed Terrain=0 | Existing native soil moisture/contamination quantizers |
| Positive liquid overlap without soil | Water or Badwater when contamination is positive | Wetness=3; no invented soil contamination |
| Other open cell | Empty | Zero |

Liquid contacts retain raw column contamination and exact positive geometric overlap separately.
Overlap uses floor-relative double arithmetic so even `float.Epsilon` at an elevated floor remains
positive. Depth is clipped to both the column ceiling and simulation grid. Overflow does not add
volume; neither a dry column span nor a water source footprint invents contact. Positive liquid
overlapping physical solid rejects the whole projection. Dry column metadata can span solid cells.

`OverlayInitialFields` validates exact grid dimensions and both array lengths, then returns copied
arrays changing only packed water and companion soil contamination. Resolved body material and raw
unrelated bits survive. Target/slot identities are outside this API. The overlay is initial formation
only: removing a contributor later reveals material through the handoff protocol while retaining
current environmental fields. Reapplying these initial readings would incorrectly reset that state.

Focused managed tests cover stacked columns, exact and fractional depths, tiny positive depth and
contamination, ceiling/grid clipping, dry/solid contradictions, soil quantizers, and owned material
overlay with no caller array mutation. This pure mapping does not add runtime resupply, native water
effects, lifecycle activation, natural fuel policy, or a second persistent registry.
