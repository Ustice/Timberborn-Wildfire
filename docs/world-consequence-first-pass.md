# World Consequence First Pass

This document captures the first implementation-facing pass for fire consequences beyond the fire field itself. `docs/DESIGN.md` remains the durable design source of truth; this page is the working bridge from those decisions into tickets that a junior developer or QA agent can execute.

## Visual Consequence Plan

The accepted direction is stronger than the older temporary ash approximation in `docs/DESIGN.md`: visual aftermath should become a persistent Timberborn-side consequence surface, while the GPU visual channels remain useful for live fire, smoke, steam, and debug overlays. Prefer the rules in this section when they conflict with older wording that treats ash only as transient residual heat.

The plan has five connected surfaces:

1. Ground scorch from fire contact.
2. Entity burn state for structures, crops, and trees.
3. Ash and contaminated ash deposition from smoke fields.
4. Soil moisture, water evaporation, and contamination interactions.
5. Repair, recovery, growth, and feedback after fire danger ends.

Implementation should keep the simulation core host-agnostic. The GPU simulator owns heat, fuel, water, smoke, toxic smoke, and compact deltas. Timberborn adapters own consequence state, entity API calls, textures, overlays, repair gates, soil/contamination integration, and live persistence.

### How To Work These Tickets

Each consequence ticket should be small enough that a junior developer can answer these questions before writing code:

- What simulation input wakes this consequence?
- Which Timberborn-side service owns the persistent state?
- Which native Timberborn APIs are proven safe, and which calls must remain logged no-ops?
- Which counters prove the ticket did something?
- Which deterministic tests pass without launching Timberborn?
- What live evidence is required before the ticket can integrate?

Default implementation order:

1. Add or update a small contract type in `src/Wildfire.Timberborn/**`.
2. Add a null or safe-unavailable adapter so the live runtime can load before native APIs are proven.
3. Add a deterministic service that consumes compact deltas, visual-field samples, atmospheric-field samples, or burn-damage decisions.
4. Add telemetry/status fields and structured log tokens.
5. Add tests in `tests/Wildfire.Core.Tests/**`.
6. Wire the service into `TimberbornFireRuntime` or the existing command/status surface only after the deterministic path is covered.
7. Add live QA instructions to `docs/TEST_PLAN.md` if the ticket changes player-visible behavior.

Do not start by calling reflection or mutating live Timberborn entities. First build the deterministic service and the safe adapter interface, then bind the native API behind that interface after it has been inspected. If the API is not safe, the ticket can still pass deterministic review by reporting precise `skipped_*` counters, but it cannot claim live gameplay behavior.

Useful starting points:

- Burn damage state: `src/Wildfire.Timberborn/Consequences/TimberbornBurnDamageState.cs`.
- Structure rollback: `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs`.
- Stored goods: `src/Wildfire.Timberborn/Consequences/TimberbornStoredGoodBurnConsequences.cs`.
- Resource fuel and residue: `src/Wildfire.Timberborn/Mapping/TimberbornResourceFuelCatalog.cs`.
- Visual field sampling: `src/Wildfire.Timberborn/Visuals/TimberbornGpuVisualFieldSurface.cs`.
- Procedural effects and presentation counters: `src/Wildfire.Timberborn/Visuals/TimberbornGpuFieldRenderer.cs`.
- Field renderer: `src/Wildfire.Timberborn/Visuals/TimberbornGpuFieldRenderer.cs`.
- Beaver exposure telemetry: `src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldExposureTelemetry.cs`.
- QA command/status surface: `src/Wildfire.Timberborn/Qa/TimberbornQaCommandBridge.cs`.
- Soil moisture read probes: `src/Wildfire.Timberborn/Qa/TimberbornQaCommandFileBridge.cs`.

Use the existing test naming pattern. A new service should normally get a focused `*Tests.cs` file beside the existing Timberborn adapter tests, plus one QA command/status regression if new counters are exposed.

### Ground Scorch

Any tile that has active fire should show blackened ground. The color target is very dark gray with a small brown component, closer to charred soil than pure black.

Scorch strength should be based on the maximum heat value observed for that ground cell over the last in-game day. This means the Timberborn-side consequence service needs a day-scale rolling heat-history field rather than reading only the current packed cell. A reasonable first representation is a per-ground-cell ring buffer or decayed maximum:

- Record `max_heat_last_day` from compact deltas and periodic field samples.
- Normalize the value to a scorch weight from `0` to `1`.
- Decay or roll the value out after one in-game day unless refreshed by new heat.
- Persist only non-zero or recently touched entries.
- Use deterministic coordinate noise to feather edges and avoid tile-perfect squares.

Rendering should prefer built-in Timberborn terrain/material systems. The target approach is similar to soil moisture and soil contamination: a ground texture overlay or shader/material layer whose weight is driven by a field, not one placed entity per tile. If direct use of native soil overlay systems is not available, use a local overlay service that imitates their map-driven, noisy, feathered blending pattern.

Required proof:

- Deterministic tests for heat-history accumulation, one-day decay, persistence snapshots, and edge noise determinism.
- Live screenshots showing blackened ground under and around burned cells without relying on alert text.
- Status counters for considered cells, touched scorch cells, max heat observed, persisted entries, rendered entries, and skipped native overlay APIs.

### Structure Burn State

Structures on fire should cease operations immediately. Any beaver working in the structure should abandon work if possible and have a high chance of injury from either the `Burned` or `Coughing` debuff, depending on whether the exposure is heat/flame or smoke/toxic smoke. Injury application remains API-gated: if a safe native debuff path is unavailable, the first implementation should log the exact skipped reason and still cancel work or close the structure when that path is safe.

As structure fuel is consumed, the structure enters a `Burned` state using the same number of phases as construction. This is a rollback of construction value, not direct deletion. Each time fuel is burned, a proportional amount of construction material is destroyed. Destruction thresholds should match construction thresholds so the visual stage and repair requirement are legible to players.

Burned structure visuals should use the same model as the corresponding construction or incomplete phase, with a burned texture applied. The asset workflow should be:

- Locate the source texture files for each target structure or material family.
- Generate burned texture variants from those source textures.
- Preserve Timberborn-facing names and source texture traceability.
- Use burned variants only through a reversible visual/material path.
- Record attribution and generated-asset provenance where required by release packaging.

Structures can be repaired only after fire and dangerous heat are gone. Repair should require the destroyed construction materials again, preserve entity identity when safe, and move visuals forward through the same phase thresholds used during construction.

Required proof:

- Deterministic tests for close-on-fire, material destruction proportionality, threshold transitions, duplicate-cell suppression, repair blocking while hot, and repair eligibility after cooling.
- A texture-generation artifact trail for at least one representative structure before broad rollout.
- Live QA showing a structure closes from fire exposure, records material loss, enters an accepted burned phase or precise safe-unavailable visual state, and becomes repair-eligible only after the fire is out.

### Crop Burn State

When a crop burns, it dies. Crops should have a `Burned` state that replaces the normal dead texture with a burned texture. When all fuel has burned, the crop should be destroyed rather than lingering as harvestable or normal dead vegetation.

Crop consequence state should consume the burn-damage foundation but be stricter than the older yield-loss-only wording:

- First fire exposure marks the crop as dead or burn-damaged if the safe crop API exists.
- Fuel loss destroys proportional yield or crop value.
- The visual state switches to a burned dead texture when the crop reaches the accepted burned threshold.
- Full fuel depletion removes or destroys the crop through a native-safe path.

If Timberborn does not expose safe crop death, texture replacement, or destruction APIs, each unsafe operation should degrade to explicit telemetry rather than fake state.

Required proof:

- Deterministic tests for crop death on burn, proportional value loss, burned texture request, full-fuel destruction, and safe no-op paths.
- Live QA with at least one crop or harvestable showing nonzero crop-burn counters and either real death/visual/destruction behavior or precise skipped-unsafe-API evidence.

### Tree Burn State

Trees have a moisture-sensitive progression. When a tree burns and the moisture in the simulation is below the desert threshold, the tree should be in a drying state before the visibly burned progression takes over.

Accepted thresholds:

- Any active burn can start the drying state when moisture is below the desert threshold.
- At one third fuel consumed, the tree dies and enters a `Burned` state with the tree texture replaced by a burned texture.
- At full fuel depletion, the tree enters a burned remnant state using the stump model with a burned texture.

The tree lane should keep cuttable yield, visual state, and model switching separate:

- Yield loss follows burn damage and resource fuel value.
- Death and model/state changes use Timberborn-safe plant/cuttable APIs only.
- Burned textures are generated from located source textures, like structures.
- Stump remnant presentation should preserve native stump placement and collision expectations where possible.

Required proof:

- Deterministic tests for desert-threshold drying, one-third consumed death threshold, full-depletion stump remnant threshold, duplicate vertical footprint suppression, and safe-unavailable API behavior.
- Live QA with one tree target showing nonzero burn telemetry and either real burned/stump visual behavior or precise skipped-unsafe-API evidence.

### Ash Deposition And Soil Effects

Smoke and toxic smoke should deposit ash into simulator transport state as they diffuse. The ash field is gameplay aftermath, not just the temporary GPU visual ash channel.

Ash should visually work like soil moisture and soil contamination: a texture or field-weighted overlay over the ground with deterministic progressive noise so transitions look natural. Prefer built-in Timberborn field, shader, material, and texture systems before custom meshes or per-cell objects. The implementation should imitate native map-driven blending even if direct access to the native soil overlay system is unavailable.

Ash quality:

- `fertile`: from ordinary plant, crop, tree, wood, paper, or other clean organic burn sources.
- `spent`: visible inert ash with no growth benefit.
- `contaminated`: from toxic smoke, badwater-adjacent burn sources, contaminated soil, contaminated water, or contaminated goods.

Contaminated ash should increase ground-soil contamination or poison the soil through the safest native contamination path. Fire should never reduce contamination. If direct contamination mutation is unsafe, record tainted ash separately and expose telemetry until a safe adapter path is proven.

Fertile ash should increase plant growth rate by `10%` while active, subject to caps and decay so burning land is not always optimal. This is a field effect, not an inventory good in the first pass. Collection and application can remain later work.

Required proof:

- Deterministic tests for smoke-to-surface deposition, toxic-smoke contaminated ash classification, clean-source fertile ash classification, deterministic noise weights, persistence, decay, and growth multiplier clamping.
- Live QA showing a visible ash overlay and status counters for deposited fertile, spent, and contaminated ash, or explicit native-overlay and contamination API blockers.

### Moisture, Evaporation, And Water

If Wildfire can safely evaporate soil moisture, it should do so directly from heat exposure. This should be a Timberborn adapter consequence of heat fields, not a new fire-spread rule owned by Timberborn.

The first direct-moisture target is soil moisture. A later nice-to-have is increasing evaporation of standing water in the heat field. Both should be API-gated:

- Soil moisture evaporation may be implemented when a safe mutable soil-moisture path is found.
- Standing-water evaporation should wait until water-volume mutation and save/reload behavior are proven.
- Badwater or contaminated water should remain contaminated; heating it can create steam and contaminated ash, but steam itself is clean and must not carry contamination or cleanse the water.

Required proof:

- Reflection or adapter evidence for the safe mutable API before mutation.
- Deterministic tests around heat threshold, evaporation amount, no-op behavior, and contamination invariants.
- Live QA proving soil moisture or water changes only after a safe API path is available.

### Ticket Reconciliation

Existing tickets map to this plan, but several need sharpening before assignment or integration. Treat the packets below as the junior-ready work order. If a packet conflicts with the existing ticket file, update the ticket before dispatching the worker.

#### `TWF-066`: Fire Presentation Baseline

Purpose:

- Keep active fire legible at normal Timberborn gameplay camera scale.
- This ticket is a dependency for consequence recordings, not a burn-state ticket.

Start from:

- `src/Wildfire.Timberborn/Visuals/TimberbornGpuFieldRenderer.cs`.
- `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`.

Do:

- Keep fire presentation separate from smoke, steam, ash, structure damage, crop damage, and tree damage.
- Preserve procedural fire particles and any accepted field renderer/debug split.
- Keep compact deltas as wake/bounds signals, not one visible object per changed cell.
- Record high-resolution live screenshots and recordings that show fire without relying on alert text.

Do not:

- Change fire-spread rules, fuel burn-down, smoke tuning, ash persistence, or world consequences.

Done when:

- Deterministic presentation tests pass.
- Live QA proves fire readability at normal camera scale and status/log counters show the visual path is healthy.

#### `TWF-067`: Smoke Presentation Baseline

Purpose:

- Make smoke readable as a separate visual from active flame.
- Provide the visual source that later ash deposition can reference without making this ticket own ash storage.

Start from:

- `src/Wildfire.Timberborn/Visuals/TimberbornGpuFieldRenderer.cs`.
- `src/Wildfire.Timberborn/Visuals/TimberbornGpuVisualFieldSurface.cs`.
- `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`.

Do:

- Tune smoke selection, lifetime, alpha, size, and motion.
- Keep smoke derived from simulator visual and atmospheric outputs.
- Add counters that distinguish smoke events from fire and ash events.

Do not:

- Deposit simulator-backed ash.
- Apply beaver coughing behavior.
- Create contamination state.

Done when:

- Deterministic tests prove smoke stays distinct from fire.
- Live QA captures smoke as a readable field or plume with status/log evidence.

#### `TWF-068`: Simulator-Backed Ash Overlay

Purpose:

- Re-scope this ticket from temporary visual ash tuning into visible ash overlay work that reads simulator ash state through the Timberborn read model.

Start from:

- `src/Wildfire.Timberborn/Visuals/TimberbornGpuVisualFieldSurface.cs`.
- `src/Wildfire.Timberborn/Visuals/TimberbornGpuFieldRenderer.cs`.
- `docs/ARCHITECTURE.md` section "Ash Adapter Services".
- Existing renderer tests such as `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`.

Do:

- Consume simulator ash readback through the Timberborn ash read model.
- Read ash by cell, amount, contamination/quality, last updated tick/day, and decay state.
- Render ash as a terrain-hugging overlay or native-like field, not per-cell objects.
- Use deterministic coordinate noise to feather the edge.
- Keep the GPU visual ash channel as temporary/debug input only.

Do not:

- Store ash in `PackedCell`.
- Apply fertility or contamination mutation in this first overlay ticket unless the packet is explicitly expanded.
- Use one spawned entity per ash tile.

Done when:

- Tests cover simulator ash input consumption, quality-to-render weight mapping, decay visibility, noise determinism, and renderer input generation.
- Live QA captures a visible ash overlay or precise native-overlay blocker evidence.

#### `TWF-069`: Fire Behavior With Recordings

Purpose:

- Accept fire behavior tuning with deterministic shader evidence and low-resolution recordings.
- This is behavior proof, not visual polish.

Start from:

- `src/Wildfire.Unity/FireSim.compute`.
- `src/Wildfire.Core/**` parameter and field-contract types.
- `tests/Wildfire.Core.Tests/FireVisualFieldTests.cs`.
- Child tickets `TWF-088` through `TWF-092`.

Do:

- Compare single ignition, line of fuel, water barrier, sparse forest, and building cluster scenarios.
- Keep release decisions such as fixed cadence and 6-neighbor spread unless a new design decision explicitly changes them.
- Document accepted constants, recordings, and interpretations in `docs/TEST_PLAN.md`.

Do not:

- Tune fire/smoke/steam particle aesthetics.
- Implement world consequences such as crop death, ash, or beaver injury.
- Add a second C# fire-spread path.

Done when:

- Deterministic shader or fixture evidence matches the accepted child-ticket behavior.
- Low-resolution live recordings show playable spread, burn-down, suppression, vertical behavior, and cooling.

#### `TWF-070`: Steam Presentation Baseline

Purpose:

- Make steam readable as water or wetness meeting heat, distinct from smoke and fire.

Start from:

- `src/Wildfire.Timberborn/Visuals/TimberbornGpuFieldRenderer.cs`.
- `src/Wildfire.Timberborn/Visuals/TimberbornGpuVisualFieldSurface.cs`.
- `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`.

Do:

- Use water-drop or wet-hot-cell signals rather than generic smoke when selecting steam.
- Tune steam position, size, lifetime, alpha, and upward motion.
- Keep steam field-based and bounded by compact deltas.
- Add counters that distinguish steam from smoke.

Do not:

- Change water suppression semantics.
- Evaporate soil moisture or standing water.
- Add steam exposure telemetry only if it reads simulator steam state directly.

Done when:

- Tests prove steam selection remains distinct from fire and smoke.
- Live QA captures high-resolution steam evidence with status/log proof.

#### `TWF-078`: Simulator-Backed Ash Read Model

Purpose:

- Verify gameplay ash quality, decay, persistence, and first growth-bonus hooks from simulator-owned ash transport state, separately from the temporary GPU visual ash channel.

Start from:

- `docs/ARCHITECTURE.md` section "Ash Adapter Services".
- `src/Wildfire.Timberborn/Mapping/TimberbornResourceFuelCatalog.cs`.
- Burn consequence outputs from `TWF-076`, `TWF-077`, and `TWF-084`.

Inputs:

- Burn aftermath events from crops, structures, trees, and stored goods.
- Contamination classifications from `TWF-079` when available.

Do:

- Read simulator ash entries by cell with amount, contamination/quality, source metadata where available, decay, and persistence version.
- Represent adapter-facing quality as `none`, `fertile`, `spent`, or `tainted` only as a derived view.
- Treat uncontaminated simulator ash as fertile ash.
- Treat contaminated simulator ash as tainted ash.
- Expose status counters for ash cells by quality, new cells, decayed cells, persistence saves, and persistence loads.

Do not:

- Store ash in `PackedCell`.
- Seed ash read-model entries from burn consequence source events.
- Render the overlay directly if that is owned by `TWF-068`.
- Implement ash collection or manual application.
- Mutate native contamination unless the contaminated-ash ticket proves that path safe.

Done when:

- Tests cover simulator readback, quality classification, source handling, decay mutation requests, sparse persistence, and safe no-op behavior when source or contamination data is unavailable.
- Live QA proves field state for fertile and tainted ash, or records exact safe-unavailable blockers.

#### New Ticket: Scorch Heat History

Purpose:

- Track the last-day maximum heat for ground cells and drive blackened-ground scorch.

Start from:

- `src/Wildfire.Timberborn/Visuals/TimberbornGpuVisualFieldSurface.cs` for field sampling patterns.
- `src/Wildfire.Timberborn/Qa/TimberbornQaCommandBridge.cs` for status exposure.
- Existing burn-damage tests for snapshot style.

Inputs:

- Compact deltas with heat changes.
- Periodic visual-field or packed-cell samples for active fire cells.
- In-game day or tick duration from the runtime timing surface.

Do:

- Add `max_heat_last_day` state keyed by ground cell index.
- Normalize heat to a scorch weight.
- Decay or expire scorch after one in-game day.
- Persist sparse non-zero entries.
- Render or expose the field through the same overlay path used by ash if possible.

Do not:

- Change simulator heat rules.
- Mutate terrain textures directly.
- Require live Timberborn rendering before deterministic state and status counters exist.

Done when:

- Tests cover accumulation, expiry, persistence, sparse storage, and deterministic noise.
- Status counters report touched cells, max heat, active scorch entries, rendered entries, and skipped overlay API paths.

#### `TWF-156`: 256x256 Scenario Map

Purpose:

- Create a reusable `256x256` Timberborn map or save for Sprint 10 visual-consequence and aftermath QA.

Start from:

- Timberborn map editor and developer tools.
- Existing QA map/save loading utilities.
- `docs/TEST_PLAN.md` for evidence expectations.

Do:

- Create or select a `256x256` map.
- Include a local forest-fire area that is large enough to show spread but not designed to burn the entire map.
- Include connected fuel, sparse fuel, water suppression, badwater or contamination where practical, crops or harvestables, trees, structures, storage, and camera/checkpoint lanes.
- Add firebreaks or spacing so one local test does not invalidate every other area.
- Preserve exact map/save path, checksum or copied artifact, dimensions, ignition target, and camera notes.

Do not:

- Require a whole-map burn.
- Treat `50x50` smoke-test saves as satisfying the max-size gate.
- Prefer JSON mutation over map editor/dev tools when the editor can create the scenario safely.

Done when:

- Archive metadata or live status proves `256x256` dimensions.
- Live QA loads the map/save and reaches command-responsive `status` or `qa-readiness`.
- Screenshots or recordings show the local forest-fire test area and camera/checkpoint framing.

#### `TWF-075`: Burn Damage Foundation

Purpose:

- Shared instance-state foundation. This ticket is already the right base for structures, crops, trees, storage, and later burn-state tickets.

Start from:

- `src/Wildfire.Timberborn/Consequences/TimberbornBurnDamageState.cs`.
- `tests/Wildfire.Core.Tests/TimberbornBurnDamageStateTests.cs`.

Do:

- Reuse this service from downstream tickets.
- Preserve target identity, owned cells, capacity, fuel value, flammability, and accounted resources.

Do not:

- Add crop death, tree death, structure visual rollback, ash, beaver effects, or stored-good destruction here.

Done when:

- Downstream tickets can resolve a changed cell to one target and apply damage once per dispatch.

#### `TWF-076`: Crop Burn Consequences

Purpose:

- Make crops die when burned, show a burned dead texture when possible, and disappear or become destroyed when all fuel is consumed.

Start from:

- `src/Wildfire.Timberborn/Consequences/TimberbornBurnDamageState.cs`.
- Any current crop/harvestable burn consequence classes in `src/Wildfire.Timberborn/**`.
- `tests/Wildfire.Core.Tests/TimberbornCropBurnConsequenceTests.cs` if present in the branch or worktree.

Inputs:

- Burn-damage decisions for crop or harvestable targets.
- Resource fuel data from `TimberbornResourceFuelCatalog`.

Do:

- Treat first active burn as crop death when a safe native API exists.
- Destroy proportional yield or crop value as fuel is consumed.
- Request the burned-dead texture state at the accepted threshold.
- Request removal/destruction at full fuel depletion through a safe API.
- Report every unavailable native API separately.

Do not:

- Implement tree behavior.
- Implement ash fertility.
- Emit one alert per crop.
- Directly destroy Unity objects.

Done when:

- Tests cover death-on-burn, proportional value loss, burned texture request, full-depletion destruction request, duplicate-cell suppression, and safe no-op paths.
- Live QA shows nonzero crop counters and either real crop state changes or precise skipped-unsafe-API evidence.

#### `TWF-077`: Structure Burn Damage Rollback

Purpose:

- Close burning structures, destroy proportional construction materials, move through burned construction-equivalent phases, and block repair until danger is gone.

Start from:

- `src/Wildfire.Timberborn/Consequences/TimberbornStructureBurnDamageRollback.cs`.
- `tests/Wildfire.Core.Tests/TimberbornStructureBurnDamageRollbackTests.cs`.
- `src/Wildfire.Timberborn/Consequences/TimberbornBuildingBurnoutConsequences.cs`.

Inputs:

- Burn-damage decisions for structure targets.
- Construction resources from the target API.
- Heat/fire danger state for repair gating.

Do:

- Close or disable operation as soon as the structure is burning or damaged.
- Calculate material destruction proportional to fuel consumed.
- Use construction thresholds to select burned phases.
- Block repair while active fire or dangerous heat remains.
- Preserve entity identity and stored inventory.
- Ask a visual/material adapter to apply burned construction-phase textures.

Do not:

- Burn stored goods; that is `TWF-115`.
- Injure beavers directly; route workplace exposure into the beaver behavior tickets.
- Delete live entities.

Done when:

- Tests cover closure, material loss, phase thresholds, repair blocked while hot, repair eligible after cooling, multi-cell rollup, and skipped unsafe APIs.
- Live QA proves closure/material-loss counters and either burned visual phase behavior or precise safe-unavailable visual evidence.

#### New Ticket: Burned Texture Asset Pipeline

Purpose:

- Locate source textures and create burned texture variants for structures, crops, trees, and stump remnants.

Start from:

- `docs/reference/modding-guide.md` for Timberborn asset and bundle conventions.
- Asset bundle and deploy scripts under `scripts/**`.
- The relevant Timberborn extracted assets or project asset folders discovered by the worker.

Do:

- Locate source texture paths for one representative structure, one crop, one tree, and one stump.
- Generate burned texture variants from source textures.
- Preserve names that make source and target assets searchable.
- Document provenance, generation prompt/settings, and where the asset is bound.
- Add the smallest binding hook that lets gameplay tickets request a burned material by stable id.

Do not:

- Redesign models.
- Replace native construction or plant state machines.
- Make untracked one-off image files that release packaging cannot find.

Done when:

- The repo contains generated texture artifacts or documented blockers.
- A deterministic asset-manifest check can confirm every burned variant has a source texture and intended target.
- A live or rendered preview proves at least one burned texture can load.

#### `TWF-079`: Contamination-Aware Consequences

Purpose:

- Classify contaminated burn sources and affected cells without ever using fire as decontamination.

Start from:

- `src/Wildfire.Timberborn/Mapping/TimberbornResourceFuelCatalog.cs`.
- `src/Wildfire.Timberborn/Visuals/TimberbornGpuVisualFieldSurface.cs`.
- `src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldExposureTelemetry.cs`.
- `docs/ARCHITECTURE.md` section "Contamination Interaction".

Do:

- Add read-only adapter probes for contaminated soil, badwater, contaminated water, contaminated goods, and contaminated entities where safe.
- Classify ash as contaminated when source or soil is contaminated.
- Classify toxic smoke for downstream beaver telemetry.
- Assert that heat/fire never reduces contamination.
- Report skipped unsafe contamination APIs.

Do not:

- Apply beaver toxic-smoke behavior.
- Clean contamination.
- Mutate native contamination before a safe API path is proven.

Done when:

- Tests cover tainted classification, no-decontamination invariant, badwater suppression semantics, toxic field classification, and skipped unsafe reads.
- Live QA captures one contamination-aware interaction or precise safe-unavailable evidence.

#### New Ticket: Fertile Ash Growth Multiplier

Purpose:

- Apply the first gameplay benefit from fertile ash: `10%` plant growth speed increase while the ash field is active.

Start from:

- The simulator-owned ash transport field and the Timberborn ash read-model adapter.
- Timberborn plant/growable API research notes or a fresh adapter survey.
- `src/Wildfire.Timberborn/Mapping/TimberbornResourceFuelCatalog.cs` for clean residue classification.

Do:

- Read fertile ash strength from simulator ash readback through the Timberborn adapter.
- Clamp the growth multiplier to `1.10`.
- Apply only to plants or crops whose native growth API is proven safe.
- Decay or remove the bonus when ash expires.
- Report skipped unsafe growable APIs separately from no fertile ash present.

Do not:

- Make fertile ash a stored good.
- Apply yield bonuses.
- Let contaminated ash boost growth.

Done when:

- Tests cover active fertile ash, expired ash, contaminated ash, multiplier clamp, and safe no-op behavior.
- Live QA proves a safe growth API path or records exact skipped-unsafe-API evidence.

#### New Ticket: Contaminated Ash Soil Interaction

Purpose:

- Let contaminated ash poison or increase ground-soil contamination where a safe native path exists.

Start from:

- The simulator-owned ash transport field and the Timberborn ash read-model adapter.
- The contamination adapter from `TWF-079`.
- Soil/contamination API research from the worker.

Do:

- Convert contaminated ash strength into a bounded contamination effect.
- Use native contamination mutation only if it is safe and save/reload-safe.
- Preserve the no-decontamination invariant.
- Keep tainted ash as visible/persistent Wildfire state if native mutation is unavailable.

Do not:

- Make fertile ash reduce contamination.
- Mutate packed simulation cells.
- Apply beaver contamination effects here.

Done when:

- Tests cover contamination increase, no cleanup, unavailable native mutation, persistence, and status counters.
- Live QA proves mutation or precise blocker telemetry.

#### `TWF-082`: Fertile Ash Collection And Application

Purpose:

- Later feature. This is not required for the first simulator-backed ash or growth-multiplier pass.

Do:

- Keep collection/storage/application deferred until the field behavior and growth multiplier are stable.

Do not:

- Block `TWF-068` or the first fertile ash growth multiplier on inventory collection.

#### `TWF-080`: Aggregate World Consequence Feedback

Purpose:

- Present world consequences as bounded player feedback without one alert per cell or entity.

Start from:

- `src/Wildfire.Timberborn/Qa/TimberbornQaCommandBridge.cs` for status counters.
- Existing alert work from `TWF-042`.
- `docs/reference/timberborn-ui.md` for native UI patterns.

Inputs:

- Consequence events from crop, tree, structure, stored goods, ash, contamination, explosive hazard, and beaver behavior lanes.

Do:

- Aggregate by event class and time window before showing anything.
- Distinguish active fire, building closure/damage, plant/resource loss, beaver danger, explosive hazard, fertile ash, and tainted ash.
- Prefer native alert/status/notification surfaces.
- Throttle repeated common aftermath events.
- Preserve log/status-only fallback when no native UI path is safe.

Do not:

- Emit one alert per burned crop, tree, storage stack, or ash tile.
- Implement the underlying consequences.
- Hide high-priority events such as beaver danger or explosive hazards behind ordinary plant-loss summaries.

Done when:

- Tests cover aggregation, throttling, priority ordering, class selection, presentation failures, and log-only fallback.
- Live QA proves multiple consequence events produce bounded feedback with no critical log failures.

#### `TWF-081`: World Consequence Persistence QA

Purpose:

- Prove durable consequence state survives save/reload, disable/re-enable, or degrades safely.

Start from:

- `docs/TEST_PLAN.md`.
- `docs/HANDOFF.md`.
- Scenario artifacts from `TWF-119` or later generated-world-consequence scenarios.
- Status commands from `TimberbornQaCommandBridge`.

Do:

- Validate crop, tree, structure, ash, contamination-aware aftermath, stored goods, and any non-transient beaver behavior after save/reload.
- Preserve exact saves, commands, logs, screenshots, recordings, and artifact paths.
- Add production fixes only if validation exposes a real persistence gap.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

Do not:

- Treat deterministic tests as a substitute for required save/reload proof.
- Broaden behavior scope while doing QA.
- Claim persistence for a lane that only returned safe-unavailable telemetry.

Done when:

- The save/reload window has no new critical Unity or Wildfire failure tokens.
- Each durable lane either survives reload or has a documented safe degradation/blocker.

#### `TWF-084`: Tree Burn Consequences

Purpose:

- Add tree-specific drying, death, burned texture, and burned stump remnant behavior.

Start from:

- Current tree burn consequence code in `src/Wildfire.Timberborn/**`.
- `src/Wildfire.Timberborn/Tools/TimberbornSelectedTreeTargetProvider.cs`.
- `tests/Wildfire.Core.Tests/TimberbornTreeBurnConsequenceTests.cs` if present in the branch or worktree.

Inputs:

- Burn-damage decisions for tree/cuttable targets.
- Moisture or desert-threshold state from the material field, soil moisture probe, or safe adapter source.

Do:

- Start drying when the tree burns below the desert moisture threshold.
- At one third fuel consumed, mark the tree dead and request a burned tree texture.
- At full fuel depletion, request a burned stump/remnant presentation.
- Suppress duplicate damage from vertical or multi-cell tree footprints.
- Preserve cuttable yield accounting.

Do not:

- Implement crop behavior.
- Apply fertile ash.
- Directly destroy Unity objects or force stump model swaps without a safe API.

Done when:

- Tests cover dry-threshold behavior, one-third death threshold, full-depletion remnant threshold, duplicate footprint suppression, and safe-unavailable APIs.
- Live QA shows nonzero tree counters and either real state changes or precise skipped-unsafe-API evidence.

#### New Ticket: Soil Moisture Evaporation Research

Purpose:

- Find out whether Wildfire can safely reduce soil moisture from heat exposure.

Start from:

- `src/Wildfire.Timberborn/Qa/TimberbornQaCommandFileBridge.cs`.
- Timberborn DLL inspection for soil moisture services.
- `docs/reference/modding-guide.md` notes about valid soil moisture queries.

Do:

- Inspect the actual Timberborn APIs before proposing mutation.
- Record read-only, mutable, and unsafe surfaces separately.
- Write a tiny proof adapter only if the API is safe.
- Document exact class/method names and save/reload risks.

Do not:

- Mutate soil moisture during research unless the ticket explicitly authorizes a live proof.
- Query invalid non-terrain cells.

Done when:

- The ticket says either "safe mutable path found" with exact API evidence or "mutation unavailable" with exact blocker evidence.

#### New Ticket: Heat-Driven Soil Moisture Evaporation

Purpose:

- Implement direct soil moisture evaporation only after the research ticket proves a safe mutable path.

Start from:

- The research ticket output.
- The scorch heat-history service.
- Soil moisture adapter wrapper created by research.

Do:

- Evaporate soil moisture from heat exposure using bounded settings.
- Skip invalid terrain cells.
- Preserve badwater and contamination invariants.
- Expose counters for considered cells, changed cells, skipped invalid cells, and skipped unsafe APIs.

Do not:

- Change water simulation or standing water volume.
- Clean contamination.
- Treat evaporation as a fire-spread rule.

Done when:

- Tests cover heat threshold, evaporation amount, invalid terrain no-op, contamination invariant, and safe-unavailable behavior.
- Live QA proves soil moisture changes or exact blocker telemetry.

#### New Ticket: Heat-Driven Water Evaporation

Purpose:

- Nice-to-have follow-up after soil moisture. Increase evaporation of standing water in heat fields only if Timberborn water mutation is safe.

Start from:

- Water adapter code and any prior water suppression tickets.
- The soil moisture evaporation pattern.

Do:

- Research water-volume mutation first if no safe path is already proven.
- Apply bounded heat-driven evaporation.
- Preserve badwater/contaminated-water identity.
- Emit clean steam when heated water evaporates.

Do not:

- Model full fluid dynamics.
- Clean badwater.
- Mutate water outside the accepted heat field.

Done when:

- Tests cover clean water, badwater, no-decontamination, bounded evaporation, and skipped unsafe APIs.
- Live QA proves safe mutation or precise blocker telemetry.

#### `TWF-071` Through `TWF-074`: Beaver Workplace Exposure

Purpose:

- Build the shared exposure and behavior harness used by smoke, toxic smoke, and fire/heat variants.

Start from:

- `src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldExposureTelemetry.cs`.
- `tests/Wildfire.Core.Tests/TimberbornBeaverFieldExposureTelemetryTests.cs`.
- `docs/DESIGN.md` section "Beaver Field Effects".

Do:

- Feed worker exposure from burning structure footprints into the beaver exposure service.
- Cancel or interrupt unsafe work when safe.
- Apply `Coughing` for smoke/toxic-smoke exposure only after a safe debuff API is proven.
- Apply `Burned` for heat/flame exposure only after a safe injury API is proven.
- Keep all debuff outcomes reversible or explicitly timed.

Do not:

- Implement choking, death, panic, faction response, or path graph mutation in the first workplace exposure pass.
- Hard-code beaver triggers that bypass real field exposure or deterministic field fixtures.

Done when:

- Tests cover exposure aggregation, work cancellation decisions, debuff routing, cooldown/hysteresis, skipped unsafe APIs, and recovery.
- Live QA proves exposure telemetry and either real safe behavior or precise skipped-unsafe-API evidence.

#### `TWF-085`: Beaver Smoke Exposure

Purpose:

- Implement normal smoke exposure as coughing first, then choking and death only behind later safe evidence.

Start from:

- `src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldExposureTelemetry.cs`.
- The `TWF-073` behavior harness.
- `tests/Wildfire.Core.Tests/TimberbornBeaverFieldExposureTelemetryTests.cs`.

Do:

- Accumulate smoke exposure over multiple ticks.
- Apply coughing as the first live proof target if a safe slowdown or work-inefficiency API exists.
- Recover coughing when exposure clears.
- Keep choking and death gated behind separate evidence.

Do not:

- Treat normal smoke as toxic smoke.
- Apply native contamination effects.
- Kill or immobilize beavers from telemetry-only proof.

Done when:

- Tests cover accumulation, coughing threshold, recovery, choking/death gates, batching, and skipped unsafe APIs.
- Live QA proves coughing or a precise safe-unavailable state.

#### `TWF-086`: Beaver Toxic Smoke Exposure

Purpose:

- Implement toxic smoke behavior using the shared harness and contamination classifications.

Start from:

- `src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldExposureTelemetry.cs`.
- `TWF-079` contamination-aware classifications.
- The `TWF-073` behavior harness.

Do:

- Progress respiratory exposure faster than normal smoke.
- Keep toxic smoke distinguishable from clean steam in telemetry.
- Attempt native badwater contamination effects only after live API proof.
- Preserve treatment/recovery telemetry where native effects are used.

Do not:

- Clean contamination.
- Fork a separate behavior system from `TWF-073`.
- Claim native contamination behavior from telemetry-only classification.

Done when:

- Tests cover toxic threshold differences, native-effect decision logic, recovery, skipped unsafe APIs, and no-decontamination.
- Live QA proves toxic exposure behavior or exact safe-unavailable evidence.

#### `TWF-087`: Beaver Fire And Heat Exposure

Purpose:

- Implement direct fire and heat behavior: work interruption, singed first, burned only after API proof, and death as a later gate.

Start from:

- `src/Wildfire.Timberborn/Beavers/TimberbornBeaverFieldExposureTelemetry.cs`.
- The `TWF-073` behavior harness.
- Fire/heat field classifications from the visual or exposure telemetry surface.

Do:

- Treat active flame cells as the highest-priority danger.
- Interrupt unsafe work assigned to burning buildings, crops, or trees where safe.
- Apply singed as the first injury proof target.
- Apply burned only after a safe work-preventing injury path is proven.
- Keep death behind sustained exposure and separate live evidence.

Do not:

- Implement smoke coughing here.
- Add arbitrary path graph mutation.
- Kill beavers from single-frame exposure.

Done when:

- Tests cover active-flame contact, heat exposure, work interruption, singed, burned gate, recovery, and skipped unsafe APIs.
- Live QA proves singed or work interruption, or exact safe-unavailable evidence.

#### `TWF-115`: Stored Goods Burn Consequences

Purpose:

- Burn storage inventory contents separately from structure construction damage.

Start from:

- `src/Wildfire.Timberborn/Consequences/TimberbornStoredGoodBurnConsequences.cs`.
- `tests/Wildfire.Core.Tests/TimberbornStoredGoodBurnConsequenceTests.cs`.

Do:

- Keep this lane focused on inventory accounting.
- Route hazardous goods to the explosive/hazard tickets.
- Preserve structure damage separation from `TWF-077`.

Do not:

- Apply structure burned phases.
- Deposit ash directly except by queuing a bounded simulator ash mutation.
- Fake inventory loss without a safe API.

Done when:

- Tests cover partial stack burn, inert goods, hazardous routing, duplicate storage cells, and safe no-op behavior.
- Live QA proves real inventory reduction or exact safe-unavailable telemetry.

Summary of existing ticket changes:

- `TWF-066`, `TWF-067`, and `TWF-070` remain live visual tuning lanes for fire, smoke, and steam particles or fields.
- `TWF-068` should be re-scoped away from temporary visual ash only and should render simulator-backed ash through the Timberborn read model.
- `TWF-069` remains fire-behavior acceptance with recordings, not visual polish or world consequences.
- `TWF-075` is still the shared burn-damage foundation.
- `TWF-076` needs the crop rule tightened from yield-loss-plus-optional visual marking to death on burn, burned dead texture, and destruction at full fuel depletion.
- `TWF-077` should own structure closure, proportional material destruction, construction-threshold burned phases, repair gating, and burned texture application.
- `TWF-078` is the historical ash proof; simulator transport state owns ash amount, quality, decay, and persistence.
- `TWF-079` owns contamination-aware classification and should feed contaminated ash, toxic smoke, and no-decontamination invariants.
- `TWF-080` owns aggregated player feedback after source consequence lanes exist.
- `TWF-081` owns save/reload validation for durable consequence state.
- `TWF-082` remains later collection/application work; the first fertile ash pass is a field growth multiplier, not a collectible good.
- `TWF-084` needs the tree rule tightened to drying below desert moisture, death at one third fuel consumed, and burned stump remnant at full depletion.
- `TWF-085`, `TWF-086`, and `TWF-087` own the smoke, toxic smoke, and fire/heat beaver behavior variants after the shared harness exists.
- `TWF-115` stays stored-goods inventory loss and hazardous-good handoff.
- `TWF-071` through `TWF-074` should consume the structure-workplace exposure rules for worker injury, coughing, burned debuffs, and work cancellation.

New or revised tickets should be split before implementation if the current ticket wording is too broad:

- Add scorch heat-history field and blackened-ground overlay.
- Generate and bind burned texture variants for representative structures, crops, trees, and stump remnants.
- Add fertile ash growth multiplier.
- Add contaminated ash soil-contamination interaction.
- Research mutable soil-moisture and water-evaporation APIs before mutation.

## Stored Items And Explosives

Stored items should burn as inventory contents, not as part of the storage building's construction value. A warehouse, pile, or tank can therefore have two separate burn consequences:

- The structure loses construction-material value through the burn damage service.
- The stored contents lose item counts through resource fuel accounting.

The resource catalog should carry at least `fuelValue`, `flammability`, `smokeProfile`, and `burnResidueQuality`. Metal should be non-burnable or effectively inert. Logs, planks, gears, paper, books, food packaging, and similar dry goods should contribute fuel. Food should usually be low-flame but smoke-producing unless a specific good deserves special behavior.

Construction materials can reuse the same catalog. Building burn capacity should start from the resources invested in construction, with non-burnable resources excluded from fuel burn but still potentially left as unusable or repair-required structure value. This keeps metal from powering the fire while still allowing a metal-containing building to be damaged by the loss of its wood, paper, or plank components.

Explosives should be treated as hazardous stored goods, not ordinary fuel. The first safe behavior should be:

- High flammability once exposed to heat or flame.
- A short armed/unstable threshold so it is not a random instant deletion.
- Stock destruction when the threshold is reached.
- A bounded heat and fire pulse into nearby simulation cells.
- Optional structure damage only through the same burn-damage service used by all structures.

We should not start with arbitrary physics blasts, displaced terrain, or direct entity deletion. Those can come later if the Timberborn API and balance design make them safe. The first version should be deterministic, bounded, logged, and easy to disable through release settings.

## Dynamite, Detonators, And Tunnels

Runtime survey found native Timberborn surfaces for the explosive infrastructure lane:

- `Dynamite.Folktails`, `DoubleDynamite.Folktails`, and `TripleDynamite.Folktails` all carry `DynamiteSpec`, cost `Explosives`, and have native `Dynamite.Trigger()`, `TriggerDelayed(int)`, `Disarm()`, and `Detonate()` methods. Their blueprint depths are `1`, `2`, and `3`.
- `Detonator.Folktails` carries `DetonatorSpec`, costs `MetalBlock`, `Explosives`, and `Extract`, and is constrained to sit on `Dynamite`, `DoubleDynamite`, or `TripleDynamite`. Runtime methods include `Arm()`, `Disarm()`, and `Evaluate()`.
- `Tunnel.Folktails` costs `Explosives`, `Extract`, and `Plank`, carries `TunnelSpec`, has a native `Tunnel.Explode()` method, and names `Platform.Folktails` as its tunnel-support template.
- `ExplosionService`, `ExplosionOutcomeGatherer`, and `ExplosionVulnerable` prove Timberborn owns real explosion, affected-tile, object-destruction, character, and terrain-physics behavior. Wildfire should not reimplement that as a fake delete path.

Accepted first contract:

- Stored `Explosives` and `Fireworks` remain the stored-goods lane from `TWF-116`.
- Placed dynamite is an armed explosive infrastructure target. Fire exposure can advance an arming threshold and, if the release setting allows it, call a wrapped native `Dynamite.TriggerDelayed(...)` or `Trigger()` path. The same event should enqueue a bounded heat pulse into the Wildfire sim so the field remains visually and mechanically coherent.
- Detonators are trigger devices, not fuel. Fire can disable them or mark them unsafe first; premature arming needs a separate wrapper because automation state and recoverability are risky.
- Tunnels are special terrain-affecting infrastructure. Fire can damage or mark them unstable in the first implementation, but native `Tunnel.Explode()` and terrain mutation stay behind a separate opt-in ticket with live QA and rollback evidence.
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
- `explosive_infrastructure_skipped_no_safe_api`
- `explosive_infrastructure_skipped_setting_disabled`
- `tunnel_destruction_deferred`

Follow-up implementation should split into separate tickets for native dynamite triggering, detonator safety behavior, and tunnel instability or terrain-destruction gating.

`TWF-152` implements the first dynamite lane with native triggering disabled by default. The adapter resolves placed `Dynamite` components from exposed compact deltas, reads native `Dynamite.Depth`, tracks sustained exposure by stable target id, suppresses duplicate cells in one dispatch, and pushes a bounded heat pulse back into Wildfire through queued `FireSimChange` values. `Dynamite.TriggerDelayed(...)` is present only behind `native_dynamite_trigger_enabled`; `Detonate()` remains out of bounds for generic fire deltas.

`TWF-153` implements the first detonator lane as fire safety, not fire spread. Exposed detonators are resolved and deduplicated, then disarmed through a wrapped `Disarm()` path when `detonator_fire_safety_enabled` is true. The lane records recoverability telemetry and intentionally never calls `Arm()`, `Evaluate()`, adjacent dynamite triggers, terrain mutation, or heat-pulse output.

`TWF-154` implements the first tunnel lane as instability/deferred-destruction telemetry. Exposed tunnels are resolved and deduplicated, then marked unstable while `tunnel_terrain_destruction_enabled` remains false by default. The native `Tunnel.Explode()` wrapper is isolated behind that setting and is not part of generic fire deltas, because it can mutate terrain and must be live-proven with save/reload and rebuild evidence before release.

## Scenario Save Generator

The first generated scenario tool is `scripts/generate-wildfire-scenario-save.ts`, run with Bun. It inspects a selected known-good `.timber` archive, parses JSON through structured APIs, writes a generated output folder under the real Wildfire QA generated-scenarios root, refuses unsafe overwrites, and writes a manifest next to the generated archive.

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

I would add a few refinements to make the scenario more useful:

- Put firebreak gaps between asset classes so one row's result does not immediately invalidate the next row.
- Add duplicate structure pads for wood-heavy, mixed-material, and metal-heavy construction cases.
- Add stockpile and warehouse test pads with logs, planks, paper, food, and explosives.
- Add water tanks near one side of the settlement so bucket-brigade behavior can test stored water fallback separately from natural water.
- Add one contaminated ground lane near the badwater channel so tainted ash, toxic smoke, and steam behavior can be proven without contaminating the whole map.
- Keep a clear camera lane down the center so QA screenshots have repeatable framing.

The generator currently mutates entity placement only when matching prototype entities already exist in the template archive. It records exact blockers for terrain-channel carving, crop prototypes, storage inventory contents, and occupied target coordinates in `wildfire-scenario-manifest.json`. Live Timberborn load validation and any schema expansion from those blockers belongs to `TWF-119`.

## Faction Fire Response Ideas

Faction suppression should stay distinct from passive world consequences. It adds player strategy and should consume the same simulation inputs and suppression output channels as water changes, instead of owning fire rules directly.

Ironteeth should get Fire Wardens. This is the capital-intensive response: protective clothing, sprayers, more building/resource cost, fewer beavers required. The gameplay effect is concentrated water application into the simulation, and if the Timberborn API allows it safely, visible water delivery in the game world.

Folktails should get a Fire Bell. This is the labor-intensive response: one staffed bell summons nearby beavers, assigns buckets, and creates a bucket brigade from the nearest natural water source or stored water tanks when no natural source is in range. Each beaver dumps water on one target spot, so the response is powerful only when the community can mobilize enough bodies.

Emberpelts likely respond through direct stamping with tails: fast and effective, but with a higher chance of singed or burned injuries because they are physically entering the fire edge. This should be risky, dramatic, and distinct from both water infrastructure and bucket logistics.

Fans are promising, but later. They should interact with smoke fields first by blocking, redirecting, or thinning smoke. If they affect fire, they should do it through airflow-like field modifiers that can increase heat or push spread direction, which makes them more simulation-sensitive than berms.

Constructible fire berms are a cleaner earlier addition. A berm or firebreak can block or reduce spread across a line of cells, create a tactical construction choice, and fit the existing simulation as a spread-resistance modifier. It should probably be non-burnable or extremely low-fuel.

Other ideas worth ticketing later:

- Fire lookout tower that extends detection or alert range without suppressing fire directly.
- Cistern wagon or mobile tank for districts that cannot reach natural water quickly.
- Firebreak forestry job that clears overgrowth before a controlled burn.
- Ash processor that turns fertile ash into a stockpiled resource.
- Fire-resistant paving or path upgrades for critical corridors.

## Ticket Hygiene

Tickets in this lane should be junior-ready before assignment. Each implementation ticket should include:

- The durable design references it implements.
- The likely source files or services to inspect first.
- The expected safe no-op behavior when a Timberborn API is unavailable.
- The telemetry counters or QA status fields needed for evidence.
- Deterministic tests before live Timberborn validation.
- The smallest acceptable live proof or explicit blocker evidence.

Parent tickets should depend on child tickets when the work is split by asset class, field type, or faction strategy. Child tickets should link back to their parent and should not require the assignee to rediscover the whole design thread.
