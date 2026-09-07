# Native material registry prototype

This native-only prototype is not registered with Runtime, the initializer/source providers, EventBus or native persistence. It supplies executable footprint/identity/contributor mapping for the GPU material handoff integration; it does not enable live mapping, natural ignition or injury. Existing provider enumeration IDs remain a known production limitation until the complete bridge is connected.

## Concrete boundary

- `TimberbornNativeMaterialFootprint.Capture(BlockObject, FireGrid, parts)` obtains the owner Guid from that block object's EntityComponent and requires initialized, non-deleted, positioned, finished state. It calls `Project(Blocks, Placement, FireGrid)` using native `GetOccupiedCoordinates`, `Transform` and `PositionedBlocks.From`. Each slot carries its native **local** coordinate plus current world CellIndex. The full transformed set must be bijective, inside the grid and equal to the native positioned footprint; it is never silently clipped.
- `TimberbornMaterialPart` factories use existing building, infrastructure, tree, crop and stored-good adapters. A complete entity projection binds those initial definitions to one Guid and footprint. The live capture overload receives declared parts; stockpile/output/good-stack component classification and quantity refresh are not yet connected. Duplicate semantic part keys and multiple non-storage definitions reject rather than double-counting providers.
- `TimberbornNativeMaterialRegistry.Reconcile(projections, removals)` stages the complete update and validates every projection and resulting overlap before publishing it. One monotonically allocated nonzero TargetId belongs to each Guid; one monotonically allocated nonzero SlotId belongs to each local coordinate within that Guid. Hiding, removal or temporary footprint shrink retains bindings. The same entity moving/rotating/flipping or arriving in a different enumeration order keeps its IDs. A different Guid at the same world cell receives a different target token.
- `ResolveCell` returns one coherent desired material: owner/slot, packed fuel/flammability/terrain definition, companion profile and the full retained contributor list. Selection is priority, initial fuel, flammability, then stable Guid. A profile cannot come from a different winner merely because its target token is larger. Empty space has Empty profile/terrain0; solid baseline has Terrain profile/terrain1 and no native owner.

Each method here models **desired native material**, not committed simulator ownership. No registry method is a GPU handoff acknowledgment. `InitialFuel` and `PackedDefinition` describe source material only; neither represents remaining fuel. Environmental heat, water, smoke, ash and contamination are outside this definition and must be preserved by the dedicated simulator handoff.

## Explicit composite limitation

For a structure and stored-good definitions belonging to the **same native entity**, the resolver preserves the current initial scalar rule: structure fuel plus storage fuel, saturated at15; maximum flammability; structure companion profile and owner. The result explicitly marks that composite. Multiple storage-only parts use the existing maximum-contribution rule.

This is not lossless accounting of the owner's inventory quantities or consumption, and does not authorize refilling burned material after a stock change. Cross-owner structure/storage combinations reject before registry publication rather than assigning the old mixed scalar to one owner. Unrelated lower contributors remain stored and reappear when the covering contributor is removed. Revealing a contributor does not authorize recreating its initial fuel.

## Identity snapshot, not a save claim

`CaptureBindings`/`RestoreBindings` support a version1 identity table: Guid → TargetId and local coordinate → SlotId, with allocation cursors. Restore validates unique nonzero tokens, unique local coordinates and cursor bounds before publishing anything. The snapshot survives actual JSON round-trip and world enumeration changes. Current projections are re-read from settled native entities after identity restore; they are not historical fuel authority.

The snapshot contains no GPU active-cell map, raw material state, archive, damage or attempt receipt. It cannot reconstruct hidden fuel/history missing from an old save. The GPU coordinator remains the exclusive owner of known slots, active identities, available GPU-authored archives and transaction tokens. Both must eventually be captured/restored in one coherent world-save envelope. Native owner/origin routing must resolve the persisted Guid behind a delta's original TargetId, never the current cell occupant.

`CreateFirstActivationRequest` derives an incoming pair and definition from the current resolved native projection. It checks exact pair membership through complete, stable simulator authority and rejects all previously activated pairs, including exhausted archives. It allocates no bindings. A never-activated local slot of a known target can be admitted using its current definition; see [first-slot admission](first-material-slot-admission.md). Likewise, a source definition/profile change can be represented here while still being inadmissible to the current GPU Refresh mode. No fallback to original fuel, synthetic archives, token reuse or legacy owner guesses is provided.

## Validation and integration gates

The focused native suite exercises actual installed Blocks/Placement math through eight distinct rotations/flips of a sparse four-slot footprint, whole-footprint out-of-grid rejection, stable local slots through reorder/expansion/shrink, one-Guid composites, coherent profile/fuel selection, hidden contributor restoration, terrain baseline restoration, rejected move/removal/allocation atomicity, replacement Guid separation and malformed persisted bindings.

```sh
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj \
  --configuration Release --filter 'FullyQualifiedName~NativeMaterial'
```

The native footprint methods execute managed native geometry; the live capture overload's entity readiness/lifecycle is source-checked, not a constructed live-world proof. No game or Unity session was launched.

Before replacing providers or enabling event subscriptions, integrate and verify: settled native part projection for every source family; dirty-Guid coalescing and deletion footprints; complete GPU handoff admission/receipts and archives; exact original-owner consequence routing; incremental damage registration; and one atomic save envelope. Publication of desired projections must remain distinct from acknowledged active material. Read-only initialization callbacks before final Initialized, native stock changes, tree-to-stump changes and unfinished/repair transitions still need their audited reconciliation paths. The prototype does not mark any of those gaps complete.

## Recorded validation and second opinion

Full native Release suite: 726 passed, zero failures/skips, including11 material-registry/footprint tests. Logs: `/tmp/wildfire-live-mapping-audit/registry-focused-tests.log` and `registry-full-tests.log`. Parent source review corrected the empty solid baseline to Terrain profile, with a removal/reveal regression.

Actual Claude reviewed the four production prototype files with tools disabled. Terminal session1642 completed with exit0, success, one turn,63368ms and no permission denials. Main model was Sonnet5. Prompt, response and exact review are under `/tmp/wildfire-claude-review/native-material-registry-*`.

Claude reported no staging, identity-snapshot or composite-ownership defect. Its sole suggested change was to derive the terrain bit of owned buildings/resources from underlying ground. That finding was rejected: the current `TimberbornFireCellMapper.EnumerateMaterialContributions` explicitly assigns Terrain1 to both resources and buildings, so the prototype intentionally preserves that occupied-material semantic. The Terrain/Empty class pairing in the baseline contract applies to **no-owner** cells; it is not a rule that every occupied Tree/Building/Storage class must have terrain0. No new simulation rule was adopted from this suggestion.
