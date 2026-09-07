> Historical snapshot archived on 2026-09-04 from repository base `551e8ef`. The dates inside this record identify its original observations; the archive date is not a new validation run. Implementation descriptions, commands, statuses, and instructions below may be superseded. Use the [current documentation index](../../INDEX.md) for current guidance.

# World Consequence First Pass

This historical record captures the first design pass for fire consequences beyond the fire field itself. It includes proposed gameplay behavior and native API observations; it is not a claim that every proposal was implemented.

## Visual Consequence Plan

This proposal replaced an older temporary-ash approximation with persistent aftermath consumed by Timberborn consequence services. GPU visual channels remained useful for live fire, smoke, steam, and debug overlays.

The plan has five connected surfaces:

1. Ground scorch from fire contact.
2. Entity burn state for structures, crops, and trees.
3. Ash and contaminated ash deposition from smoke fields.
4. Soil moisture, water evaporation, and contamination interactions.
5. Repair, recovery, growth, and feedback after fire danger ends.

Implementation should keep the simulation core host-agnostic. The GPU simulator owns heat, fuel, water, smoke, toxic smoke, and compact deltas. Timberborn adapters own consequence state, entity API calls, textures, overlays, repair gates, soil/contamination integration, and live persistence.

### Ground Scorch

Any tile that has active fire should show blackened ground. The color target is very dark gray with a small brown component, closer to charred soil than pure black.

Scorch strength should be based on the maximum heat value observed for that ground cell over the last in-game day. This means the Timberborn-side consequence service needs a day-scale rolling heat-history field rather than reading only the current packed cell. A reasonable first representation is a per-ground-cell ring buffer or decayed maximum:

- Record `max_heat_last_day` from compact deltas and periodic field samples.
- Normalize the value to a scorch weight from `0` to `1`.
- Decay or roll the value out after one in-game day unless refreshed by new heat.
- Persist only non-zero or recently touched entries.
- Use deterministic coordinate noise to feather edges and avoid tile-perfect squares.

Rendering should prefer built-in Timberborn terrain/material systems. The target approach is similar to soil moisture and soil contamination: a ground texture overlay or shader/material layer whose weight is driven by a field, not one placed entity per tile. If direct use of native soil overlay systems is not available, use a local overlay service that imitates their map-driven, noisy, feathered blending pattern.

### Structure Burn State

Structures on fire should cease operations immediately. Any beaver working in the structure should abandon work and have a high chance of injury from either the `Burned` or `Coughing` debuff, depending on whether the exposure is heat/flame or smoke/toxic smoke.

As structure fuel is consumed, the structure enters a `Burned` state using the same number of phases as construction. This is a rollback of construction value, not direct deletion. Each time fuel is burned, a proportional amount of construction material is destroyed. Destruction thresholds should match construction thresholds so the visual stage and repair requirement are legible to players.

Burned structure visuals should use the same model as the corresponding construction or incomplete phase, with a burned texture applied. The asset workflow should be:

- Locate the source texture files for each target structure or material family.
- Generate burned texture variants from those source textures.
- Preserve Timberborn-facing names and source texture traceability.
- Use burned variants only through a reversible visual/material path.
- Record attribution and generated-asset provenance where required by release packaging.

Structures can be repaired only after fire and dangerous heat are gone. Repair should require the destroyed construction materials again, and move visuals forward through the same phase thresholds used during construction.

### Crop Burn State

When a crop burns, it dies. Crops should have a `Burned` state that replaces the normal dead texture with a burned texture. When all fuel has burned, the crop should be destroyed rather than lingering as harvestable or normal dead vegetation.

Crop consequence state should consume the burn-damage foundation but be stricter than the older yield-loss-only wording:

- First fire exposure marks the crop as dead or burn-damaged.
- Fuel loss destroys proportional yield or crop value.
- The visual state switches to a burned dead texture when the crop reaches the accepted burned threshold.
- Full fuel depletion removes or destroys the crop.

### Tree Burn State

Trees have a moisture-sensitive progression. When a tree burns and the moisture in the simulation is below the desert threshold, the tree should be in a drying state before the visibly burned progression takes over.

Accepted thresholds:

- Any active burn can start the drying state when moisture is below the desert threshold.
- At one third fuel consumed, the tree dies and enters a `Burned` state with the tree texture replaced by a burned texture.
- At full fuel depletion, the tree enters a burned remnant state using the stump model with a burned texture.

The tree lane should keep cuttable yield, visual state, and model switching separate:

- Yield loss follows burn damage and resource fuel value.
- Death and model/state changes use Timberborn APIs.
- Burned textures are generated from located source textures, like structures.
- Stump remnant presentation should preserve native stump placement and collision expectations where possible.

### Ash Deposition And Soil Effects

Smoke and toxic smoke should deposit ash into simulator transport state as they diffuse. The ash field is gameplay aftermath, not just the temporary GPU visual ash channel.

Ash should visually work like soil moisture and soil contamination: a texture or field-weighted overlay over the ground with deterministic progressive noise so transitions look natural. Prefer built-in Timberborn field, shader, material, and texture systems before custom meshes or per-cell objects. The implementation should imitate native map-driven blending even if direct access to the native soil overlay system is unavailable.

Ash quality:

- `fertile`: from ordinary plant, crop, tree, wood, paper, or other clean organic burn sources.
- `spent`: visible inert ash with no growth benefit.
- `contaminated`: from toxic smoke, badwater-adjacent burn sources, contaminated soil, contaminated water, or contaminated goods.

Contaminated ash should increase ground-soil contamination or poison the soil. Fire should never reduce contamination.

Fertile ash should increase plant growth rate by `10%` while active, subject to caps and decay so burning land is not always optimal. This is a field effect, not an inventory good in the first pass. Collection and application can remain later work.

### Moisture, Evaporation, And Water

Wildfire should evaporate soil moisture directly from heat exposure. This should be a Timberborn adapter consequence of heat fields, not a new fire-spread rule owned by Timberborn.

The first direct-moisture target is soil moisture. A later nice-to-have is increasing evaporation of standing water in the heat field.

### Additional Domain And API Notes

The original implementation packets added these details to the consequence designs above:

- Burn damage resolves occupied cells to stable entity identity, capacity, fuel value, flammability, and accounted resources. Multiple footprint cells must not apply the same damage twice in a dispatch. Structure construction losses and stored inventory losses are separate accounts.
- Ash quality is a derived view of simulator transport: uncontaminated ash is fertile, contaminated ash is tainted. Growth speed was proposed to cap at 1.10 while fertile ash remains active, without a yield bonus or a benefit from tainted ash. Ash overlays use deterministic coordinate noise and sparse state; the temporary GPU visual ash channel is not gameplay storage.
- Burned texture variants retain source-texture provenance and stable material identifiers for structures, crops, trees, and stump remnants. Visual replacement does not replace the native construction or plant state machine.
- Soil-moisture reads and mutations require valid terrain cells. Soil moisture and standing-water volume are separate native surfaces. Proposed evaporation is bounded by heat exposure, preserves badwater identity, and emits clean steam without cleansing contamination.
- Workplace exposure aggregates danger from burning footprints. Smoke drives coughing and recovery after exposure clears; toxic smoke accelerates respiratory exposure; active flame takes priority for work interruption and heat injury. Exposure accumulation, cooldown, hysteresis, and recovery remain distinct from native injury or contamination effects. A single-frame observation is not sustained exposure.
- Player feedback aggregates consequence classes over time and throttles repeated aftermath events. Beaver danger and explosive hazards retain higher priority than ordinary plant-loss summaries.
- The proposed large-map scenario was 256x256 with connected and sparse fuel, water and badwater, representative structures and storage, firebreak gaps, and repeatable camera lanes. It exercised a local forest fire without requiring a whole-map burn.

### 2026-05-24 Native Construction Failure

The historical handoff recorded a crash while BuildExecutor finished construction: `InvalidOperationException: Field of LackOfResourcesStatus named _activePredicate isn't null`. The preceding log repeatedly reported `wildfire_timberborn_structure_burned_visual_applied` for lodges, foresters, bakeries, and district-center cells. The error archive was `error-report-2026-05-24-15h53m09s.zip`. This raised a concern that native construction rollback had left inconsistent save state; the record did not establish whether affected saves were corrupted or identify a verified root cause.

## Stored Items And Explosives

Stored items should burn as inventory contents, not as part of the storage building's construction value, while still contributing blueprint-derived packed fuel to the storage cell before simulation. A warehouse, pile, or tank can therefore have two separate burn consequences:

- The structure loses construction-material value through the burn damage service.
- The stored contents add `FuelValue` to available cell fuel, then lose item counts through resource fuel accounting as that fuel burns.

The resource catalog should carry at least `fuelValue`, `flammability`, `smokeProfile`, and `burnResidueQuality`. Metal should be non-burnable or effectively inert. Logs, planks, gears, paper, books, food packaging, and similar dry goods should contribute fuel. Food should usually be low-flame but smoke-producing unless a specific good deserves special behavior.

Construction materials can reuse the same catalog. Building burn capacity should start from the resources invested in construction, with non-burnable resources excluded from fuel burn but still potentially left as unusable or repair-required structure value. This keeps metal from powering the fire while still allowing a metal-containing building to be damaged by the loss of its wood, paper, or plank components.

Explosives should be treated as hazardous stored goods, not ordinary fuel. The first behavior should be:

- High flammability once exposed to heat or flame.
- A short armed/unstable threshold so it is not a random instant deletion.
- A bounded heat pulse matching the unstable-core pattern.
- Entity destruction in blast radius through the wrapped native Timberborn `ExplosionService` affected-tile pipeline.
- Stock destruction when the threshold is reached.
- A bounded heat and fire pulse into nearby simulation cells.
- Optional structure damage only through the same burn-damage service used by all structures.

We should not start with arbitrary physics blasts, displaced terrain, or direct entity deletion. Stored explosive goods should use the same bounded heat pulse pattern as unstable cores and the named Timberborn explosion adapter for blast-radius destruction.

## Dynamite, Detonators, And Tunnels

Runtime survey found native Timberborn surfaces for the explosive infrastructure lane:

- `Dynamite.Folktails`, `DoubleDynamite.Folktails`, and `TripleDynamite.Folktails` all carry `DynamiteSpec`, cost `Explosives`, and have native `Dynamite.Trigger()`, `TriggerDelayed(int)`, `Disarm()`, and `Detonate()` methods. Their blueprint depths are `1`, `2`, and `3`.
- `Detonator.Folktails` carries `DetonatorSpec`, costs `MetalBlock`, `Explosives`, and `Extract`, and is constrained to sit on `Dynamite`, `DoubleDynamite`, or `TripleDynamite`. Runtime methods include `Arm()`, `Disarm()`, and `Evaluate()`.
- `Tunnel.Folktails` costs `Explosives`, `Extract`, and `Plank`, carries `TunnelSpec`, has a native `Tunnel.Explode()` method, and names `Platform.Folktails` as its tunnel-support template.
- `ExplosionService`, `ExplosionOutcomeGatherer`, and `ExplosionVulnerable` prove Timberborn owns real explosion, affected-tile, object-destruction, character, and terrain-physics behavior. Wildfire should not reimplement that as a fake delete path.

Accepted first contract:

- Stored `Explosives` and `Fireworks` are inventory contents, separate from placed infrastructure.
- Placed dynamite is an armed explosive infrastructure target. Fire exposure can advance an arming threshold and, if the release setting allows it, call a wrapped native `Dynamite.TriggerDelayed(...)` or `Trigger()` path. The same event should enqueue a bounded heat pulse into the Wildfire sim so the field remains visually and mechanically coherent.
- Detonators are trigger devices, not fuel. Fire can disable them; premature arming needs a separate wrapper because automation state and recoverability are risky.
- Tunnels are special terrain-affecting infrastructure. Fire can damage or mark them unstable in the first implementation, but native `Tunnel.Explode()` and terrain mutation stay behind a separate opt-in setting because it changes terrain.
- Direct terrain deformation, broad physics blasts, and direct entity deletion are not allowed from generic fire deltas. Those behaviors must go through named native wrappers, settings, telemetry, and live proof.

Required settings:

- `explosive_infrastructure_enabled`
- `native_dynamite_trigger_enabled`
- `tunnel_terrain_destruction_enabled`
- `explosive_infrastructure_armed_threshold_ticks`
- `explosive_infrastructure_pulse_heat`
- `explosive_infrastructure_pulse_radius`, initially fixed to `1`

Required telemetry:

- `explosive_infrastructure_considered`
- `explosive_infrastructure_armed`
- `explosive_infrastructure_triggered`
- `explosive_infrastructure_native_triggered`
- `explosive_infrastructure_heat_pulse_cells`
- `explosive_infrastructure_skipped_setting_disabled`
- `tunnel_destruction_deferred`

The first dynamite implementation disabled native triggering by default. The adapter resolves placed `Dynamite` components from exposed compact deltas, reads native `Dynamite.Depth`, tracks sustained exposure by stable target id, suppresses duplicate cells in one dispatch, and pushes a bounded heat pulse back into Wildfire through queued `FireSimChange` values. `Dynamite.TriggerDelayed(...)` is present only behind `native_dynamite_trigger_enabled`; `Detonate()` remains out of bounds for generic fire deltas.

The stored-explosives implementation used the native blast pipeline. Timberborn does not expose a clean public `Explode(center, radius)` method, but `ExplosionOutcomeGatherer.GetAffectedTilesPerRadius(...)` and the private `ExplosionService.ProcessAffectedTiles(...)` are the native unstable-core blast-radius path. Wildfire wraps that exact path in a small Timberborn adapter and fails loudly if the signature moves.

The first detonator implementation resolved and deduplicated exposed components.

The first tunnel implementation reported instability and deferred destruction. Exposed tunnels are resolved and deduplicated, then marked unstable while `tunnel_terrain_destruction_enabled` remains false by default. The native `Tunnel.Explode()` wrapper is isolated behind that setting and is not part of generic fire deltas, because it can mutate terrain and affect saved world state.

## Scenario Save Generator

The first generated scenario tool is `scripts/generate-wildfire-scenario-save.ts`, run with Bun. It inspects a selected known-good `.timber` archive, parses JSON through structured APIs, writes a generated output folder under the real Wildfire QA generated-scenarios root, and writes a manifest next to the generated archive.

Run shape:

```bash
bun scripts/generate-wildfire-scenario-save.ts --template "$HOME/Documents/Timberborn/ExperimentalSaves/Wildfire testing/Wildfire testing.timber" --dry-run
bun scripts/generate-wildfire-scenario-save.ts --template /path/to/template-copy.timber --output-dir "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-118-check"
```

The tool:

- Unpack or read a selected template save/map.
- Parse JSON with structured APIs.
- Generate a copy under a clearly named Wildfire test-save output folder.
- Refuse to overwrite existing saves unless an explicit flag is passed.
- Write a manifest describing the generated layout, template source, and expected validation targets.
- Support a dry-run mode that prints the planned entity counts and output paths.
- Refuse symlinked generated roots, output ancestors, output folders, and manifest paths before writing.

The first scenario targets a 50 by 50 flat map with a base height of 2 where the inspected template schema allows it. The layout splits the X axis into five north-south bands:

- `x=0..10`: land test band with trees and crop-pad intent.
- `x=11..17`: badwater channel source band.
- `x=18..29`: land separator, central structure pads, and camera lane.
- `x=30..36`: water channel source band.
- `x=37..49`: land test band with stored-water pad intent.

The north edge should be closed or bounded so the water and badwater sources are controlled. Water and badwater sources should start near the north side and drain or exit at the south side, so the flow direction is obvious in screenshots and telemetry. The top two rows can hold trees, the center rows can hold representative structures, and the southern rows can hold crops.

Proposed scenario refinements:

- Put firebreak gaps between asset classes so one row's result does not immediately invalidate the next row.
- Add duplicate structure pads for wood-heavy, mixed-material, and metal-heavy construction cases.
- Add stockpile and warehouse test pads with logs, planks, paper, food, and explosives.
- Add water tanks near one side of the settlement so bucket-brigade behavior can test stored water fallback separately from natural water.
- Add one contaminated ground lane near the badwater channel so tainted ash, toxic smoke, and steam behavior can be proven without contaminating the whole map.
- Keep a clear camera lane down the center so QA screenshots have repeatable framing.

The generator currently mutates entity placement only when matching prototype entities already exist in the template archive. It records exact blockers for terrain-channel carving, crop prototypes, storage inventory contents, and occupied target coordinates in `wildfire-scenario-manifest.json`. These manifest limitations distinguish proposed layout from actual generated content.

## Faction Fire Response Ideas

Faction suppression should stay distinct from passive world consequences. It adds player strategy and should consume the same simulation inputs and suppression output channels as water changes, instead of owning fire rules directly.

Ironteeth should get Fire Wardens. This is the capital-intensive response: protective clothing, sprayers, more building/resource cost, fewer beavers required. The gameplay effect is concentrated water application into the simulation, visible water delivery in the game world.

Folktails should get a Fire Bell. This is the labor-intensive response: one staffed bell summons nearby beavers, assigns buckets, and creates a bucket brigade from the nearest natural water source or stored water tanks when no natural source is in range. Each beaver dumps water on one target spot, so the response is powerful only when the community can mobilize enough bodies.

Emberpelts likely respond through direct stamping with tails: fast and effective, but with a higher chance of singed or burned injuries because they are physically entering the fire edge. This should be risky, dramatic, and distinct from both water infrastructure and bucket logistics.

Fans are promising, but later. They should interact with smoke fields first by blocking, redirecting, or thinning smoke. If they affect fire, they should do it through airflow-like field modifiers that can increase heat or push spread direction, which makes them more simulation-sensitive than berms.

Constructible fire berms are a cleaner earlier addition. A berm or firebreak can block or reduce spread across a line of cells, create a tactical construction choice, and fit the existing simulation as a spread-resistance modifier. It should probably be non-burnable or extremely low-fuel.

Other proposed gameplay ideas:

- Fire lookout tower that extends detection or alert range without suppressing fire directly.
- Cistern wagon or mobile tank for districts that cannot reach natural water quickly.
- Firebreak forestry job that clears overgrowth before a controlled burn.
- Ash processor that turns fertile ash into a stockpiled resource.
- Fire-resistant paving or path upgrades for critical corridors.
