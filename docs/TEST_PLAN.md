# Wildfire Test Plan

## Scope

Validation should prove the shared packed data model, deterministic scenario inputs, shader execution, compact delta readback, and Timberborn adapter behavior.

## Current Automated Coverage

- Packed cell round-trips and field setters.
- Burning-threshold helper behavior.
- Seeded scenario catalog coverage.
- Scenario dimension and seed overrides.
- Seeded sparse layout determinism.
- CLI fixture export shape and deterministic JSON output.
- Shader snapshot harness contract: CLI fixture loading, buffer-grid creation from fixtures, stable accepted-snapshot JSON shape, actionable snapshot diffs, and explicit current execution blocker.
- Unity batchmode shader execution harness: opt-in local test loads `FireSim.compute`, dispatches a seeded `TWF-000` fixture, reads back final packed cells, compact deltas, and visual-field checksum through the existing shader snapshot harness.
- GPU visual field wrapper contract: `wildfire.visual_fields` is allocated as one `float4`-equivalent entry per packed cell, full-grid dispatch receives the visual buffer, shader source writes visual samples from post-step packed cell values, and deterministic tests cover fire, smoke, ash, and visibility derivation.
- Shared material field schema: `WildfireMaterialFieldSchema` and `MaterialFieldSchema.v1.json` define the v1 material classes, packed-cell bands, consequence target kinds, ash qualities, contamination behavior, and resource policies that live import and `.timber` snapshot export must share. C# and Bun tests both read the schema fixture and prove unknown materials fail closed.
- Companion field buffer contract: `ComputeBufferGrid` allocates `wildfire.companion_target_ids` and `wildfire.companion_fields` beside packed cells, uploads default empty companion state when no importer supplies field data, rejects mismatched companion counts, and tests packed companion state for material class, burn capacity, ash, and contamination fields.
- Timberborn map fixture export: `.timber` snapshot export preserves `packedCellValues.values` and now emits `companionFieldValues` with target IDs and packed companion states derived from the shared material field schema. Bun tests cover tree, infrastructure, terrain, water, and empty-cell output.
- Generated world-consequence scenario checkpoints: the scenario manifest now emits field checkpoints for terrain, empty controls, trees, crops, buildings, storage-origin structures, infrastructure, water, and badwater, including expected source material, resolved cell material, packed-cell band, companion-field category, and template identity where known.
- Timberborn world cell importer: the live initializer waits until Timberborn entities are available, composes world source providers, imports terrain from `ITerrainService`, imports live entity-backed trees, buildings, storage, water, and badwater from `EntityRegistry`, builds companion target/state fields from material classes, passes companion fields into the Timberborn compute simulator, and exposes import counts through `status`/`qa-readiness`.
- Runtime fire simulation parameters: `FireSimParameters` carries visual weights, ignition/spread/water/burn/cooling constants, and deterministic fuel burn-down settings through compute dispatch and Timberborn shader binding. Tests assert default dispatch values and prove a non-default preset changes visual-field output through the shared CPU mirror.
- Internal fire tuning presets: the QA-only `qa-fire-preset` command accepts only named presets (`default`, `slow-reactable`, `harsh`, and `conservative`), rejects raw parameter input, exposes the active preset and major knobs in `status`/`qa-readiness`, and feeds the selected preset into the Timberborn compute simulator factory.
- Timberborn cell mapping scaffold: deterministic terrain/building/resource/water source folding into packed cells, named material bands for stockpile resources, vegetation, wood-like buildings, and non-burnable buildings, sorted `SetCell` change emission, field-width clamping, wet-cell overlay behavior, vertical footprint expansion, material priority, and out-of-bounds source rejection.
- Timberborn resource fuel catalog: adapter-owned lookup for shipped `Good.*` ids maps `fuelValue`, `flammability`, `smokeProfile`, `residueQuality`, and `hazardClass` without leaking Timberborn ids into `Wildfire.Core`; tests cover unknown defaults, inert resources, dry burnable goods, food and medicine-like goods, volatile/explosive goods, and stockpile-source mapping.
- Timberborn burn damage foundation: adapter-owned descriptor lookup, resource-catalog-backed damage capacity, stable target/cell ownership, changed-cell-to-single-owner resolution, multi-cell and vertical duplicate suppression, unknown-resource fail-closed behavior, optional delta-consumer sink telemetry, resource-accounting snapshot fields, and state capture/restore.
- Timberborn QA command bridge scaffold: read-only `status` and `help` commands, simulator runtime state when available, searchable command request/result tokens, and explicit no-arbitrary-execution command dispatch.
- Timberborn deploy pipeline scaffold: Bun/TypeScript deploy script, generated Wildfire manifest, managed assembly staging into `~/Documents/Timberborn/Mods/Wildfire/Scripts`, private FireSim and diagnostic AssetBundle staging into `~/Documents/Timberborn/Mods/Wildfire/ComputeShaders`, local build/deploy lock, dry-run/help output, and running-game guard for real deploy/remove.
- Timberborn fixed-cadence dispatch scaffold: adapter initialization from mapped cells through an injected GPU simulator factory, external change registration through `IGpuFireSimulator.RegisterChange`, centralized cadence options, one dispatch per processed game update, compact-delta return/subscription surface, command-bridge status fields, and lifecycle log tokens for attach/init/change/wait/dispatch/readback/failure events.
- Timberborn compute-backed simulator factory: live adapter loads `wildfire_compute_mac` from the deployed AssetBundle, creates Unity `ComputeBuffer` resources, dispatches `ApplyExternalChanges` and `SimulateFullGrid`, reads compact deltas, and initializes `TimberbornFireRuntime` from real terrain sources supplied by `MapSize` and `ITerrainService`.
- Timberborn GPU visual-field surface binding: the live compute simulator binds the `VisualFields` compute buffer once as a Timberborn-facing DI singleton surface with one `float4`-equivalent entry per cell, channel order `fire,smoke,ash,visibility`, a consumer-facing binding view for future renderer/effect/debug-inspector systems, bounded sample inspection for specific cell indices, dispatch-update telemetry, and `qa-readiness`/`status` fields that prove the visual surface is bound without routing gameplay consequences through visual output.
- Timberborn GPU field renderer: compact-delta visual events now feed a primary region-batched renderer that samples the GPU visual-field surface, aggregates fire, smoke, ash, derived steam, visibility, and heat-haze intensities by bounded regions, and presents the result through a single Unity mesh surface instead of one object per burning cell. `status` and `qa-readiness` expose renderer enabled/material/surface state plus visible, updated, dropped, invisible, and failed region counters. `gpu_field_renderer_dropped_regions` means binding or capacity loss; `gpu_field_renderer_invisible_regions` means sampled regions that were culled below the configured visible-intensity threshold.
- Timberborn pooled fire/smoke/ash effect routing: compact delta visual-effect events select bounded visual-field samples through `ITimberbornGpuVisualFieldSurface`, the adapter maintains a capped pool of active fire/smoke/ash presentation anchors instead of one object per simulated cell, visual presentation failures are isolated from gameplay consequences, and `qa-readiness`/`status` fields expose active pooled effects, last-dispatch updated regions, stable last-nonzero updated regions, presentation failures, and native-prefab visibility state.
- Tuned visual-field output: `TWF-041` accepts named fire/smoke/ash/visibility constants in the C# mirror and `FireSim.compute`, two Unity shader checksum snapshots, and live Timberborn pooled-effect evidence for the tuned output.
- Tuned fire game-feel output: `TWF-043` accepts named ignition, spread, burn, heat-loss, flammability-pressure, and water-suppression constants in `FireSim.compute`, Timberborn adapter material bands, and three Unity shader snapshots that assert semantic delta and hot-cell outcomes in addition to visual checksums.
- Release scenario shader snapshots: `TWF-045` accepts seven real Unity compute captures for single ignition, line of fuel, water barrier, vertical fuel column, sparse forest, building cluster, and mixed terrain/fuel/water, with committed exact final packed-cell arrays, per-tick old/new delta records, visual checksums, logs, and normal-test append-counter reset coverage.
- Timberborn debug fire overlay state: the adapter consumes compact deltas, filters them to visual-state changes, stores the latest packed cell only for affected overlay indices, derives fuel/heat/water/burning/spent state from that packed cell, and exposes per-dispatch updated-cell counters separately from the persistent overlay cell count.
- Timberborn player-facing fire alert state: compact delta alert events are aggregated into at most one native quick warning per dispatch, warning text reports new fire cells, burned-out cells, and max heat, and status telemetry exposes the last player alert tick, counts, notification send state, and presentation failures.
- Runtime diagnostics: Unity and Timberborn GPU paths emit concise `wildfire_*` tokens for simulator initialization/disposal, queued change batches, dispatch kernel start/completion with elapsed milliseconds, compact delta readback counts, listener notification counts, and adapter startup/shutdown without logging per-cell changes.
- Release enablement setting: `JasonKleinberg.Wildfire.release.wildfire_enabled` defaults to enabled when missing, accepts stable integer values `1` and `0`, falls back disabled for malformed or out-of-range values, reports invalid settings through `wildfire_release_setting_invalid`, exposes `wildfire_enabled` through status and `qa-readiness`, and deterministically gates QA simulator-change commands plus fixed-cadence dispatch when disabled.

Run:

```bash
dotnet test
```

Run the shader snapshot harness slice:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
```

Run the real Unity compute-shader execution harness locally:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled
```

## Resource Fuel Catalog

`TWF-114` inspected installed Timberborn blueprints under `~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/Blueprints`. The adapter catalog covers the 60 shipped `Good.*` resource ids found there, including materials, food, ingredients, liquids, medicine-like goods, volatile goods, and explosive goods. Unknown ids intentionally map to fuel `1`, flammability `0`, unresolved residue, and `Unknown` hazard so downstream destruction logic can stay conservative until the adapter resolves the name.

Construction blueprint text scans found `GoodId` references for `Dirt`, `Extract`, `Fireworks`, `Log`, and `Water`; all are covered by the catalog. Natural-resource template scans found crop, bush, and tree templates that either map through their yielded goods or through the existing vegetation material band: `Birch`, `BlueberryBush`, `Canola`, `Carrot`, `Cassava`, `Cattail`, `ChestnutTree`, `CoffeeBush`, `Corn`, `Dandelion`, `Eggplant`, `Kohlrabi`, `Mangrove`, `Maple`, `Oak`, `Pine`, `Potato`, `Soybean`, `Spadderdock`, `Sunflower`, and `Wheat`.

Deferred names for downstream tickets:

- `TWF-115` should consume `TimberbornResourceFuelCatalog` for stored-good destruction and preserve unknown ids as searchable unresolved cases instead of escalating them to high hazard.
- `TWF-116` owns explosive pulse behavior for `Explosives`, `Fireworks`, firework variants, and dynamite-like content; this catalog only marks hazard and packed-cell fuel/flammability.
- `TWF-117` owns building and infrastructure classification for explosive-like or trigger-like building ids such as `Dynamite`, `DoubleDynamite`, `TripleDynamite`, and `Detonator`.
- Recipe-only or pseudo ids remain unresolved until a downstream adapter observes them as stored goods: `BadwaterExtracted`, `Biofuel.Carrot`, `Biofuel.Potato`, `Biofuel.Spadderdock`, `Bot.Folktails`, `Bot.IronTeeth`, `BotChassis.Folktails`, `BotChassis.IronTeeth`, `BotHead.Folktails`, `BotHead.IronTeeth`, `BotLimb.Folktails`, `BotLimb.IronTeeth`, `FlowingBadwater`, `FlowingWater`, `SciencePoints`, `SciencePointsNumbercruncher`, `SciencePointsObservatory`, `ScrapMetal.Efficient`, and `Water.Efficient`.
- Firework variant ids remain deferred to `TWF-116`: `CometBlue`, `CometRed`, `CometWhite`, `Fish`, `KamuroBlue`, `KamuroOrange`, `KamuroPink`, `KamuroRed`, `KamuroWhite`, `PalmBlue`, `PalmGold`, `PalmGreen`, `PeonyRedBlue`, `PeonyViolet`, `PeonyYellowGreen`, `Sparks`, and `Willow`.

## Stored Goods Burn Consequences

`TWF-115` binds fuel-loss compact deltas to a Timberborn adapter-owned stored-goods consequence lane. The deterministic sink resolves one storage target per tick, suppresses duplicate cells from the same stockpile, classifies stacks through `TimberbornResourceFuelCatalog`, and only mutates stock through Timberborn `Inventory.Take(GoodAmount)` when a live `Stockpile.Inventory` is available. Unknown resources remain searchable skipped cases, inert goods do not burn, and volatile or explosive goods are counted as hazardous so the `TWF-116` pulse lane can own their special behavior.

Automated coverage should prove partial-stack destruction, duplicate target suppression, non-burnable and unknown resource handling, hazardous-good counting, and safe no-op behavior when no inventory API is available. Live QA must capture a storage fire where status or `qa-readiness` reports `last_delta_consumer_stored_good_burn_matched_storage_cells=<nonzero>` and `last_delta_consumer_stored_good_burn_destroyed_items=<nonzero>`, or a precise safe-unavailable result through `last_delta_consumer_stored_good_burn_skipped_no_inventory_api=<nonzero>`. Required log tokens are `wildfire_timberborn_delta_consequence_sink_bound lane=stored_goods_burn`, `wildfire_timberborn_stored_goods_burn_applied`, and the matching `wildfire_timberborn_delta_consumer_completed ... stored_good_burn_*` fields.

## Explosive Infrastructure

`TWF-130` accepts a separate explosive-infrastructure contract for placed `Dynamite`, `DoubleDynamite`, `TripleDynamite`, `Detonator`, and `Tunnel` targets. Automated tests for implementation tickets should start with descriptor classification, sustained-heat arming thresholds, duplicate target suppression, setting gates, bounded heat-pulse output, and safe unavailable wrappers before any live native explosion is attempted.

`TWF-152` adds the first dynamite implementation lane. Compact fire deltas are converted into explosive-infrastructure exposure decisions, resolved through a Timberborn adapter target API, deduplicated by target stable id, and tracked against `explosive_infrastructure_armed_threshold_ticks`. Once armed, the sink enqueues a bounded `FireSimChange` heat pulse through the simulator external-change path. Native `Dynamite.TriggerDelayed(...)` is wrapped, but `native_dynamite_trigger_enabled` defaults to disabled; the default behavior is pulse-only plus skipped-native telemetry. Building against native `Dynamite` also requires explicit `Timberborn.Explosions.dll` and `Timberborn.TickSystem.dll` references.

Automated coverage for `TWF-152` must prove the disabled setting gate, sustained threshold progression, duplicate target suppression, bounded 3D pulse cells, native-wrapper enabled and unavailable paths, release-setting defaults, and `last_delta_consumer_explosive_infrastructure_*` QA/status tokens.

Live QA must prove each native wrapper independently. Dynamite triggering evidence must show the target id and depth, the arming threshold, the selected native call (`TriggerDelayed` or `Trigger`), bounded Wildfire heat-pulse cells, and final status counters. Detonator evidence must show disable or arming behavior without corrupting automation state. Tunnel terrain destruction must stay disabled until a later ticket captures native `Tunnel.Explode()` proof, terrain/object impact evidence, save/reload behavior, and a player-recoverable rollback or rebuild path.

`TWF-153` adds the first detonator fire-safety lane. Compact fire deltas are converted into detonator safety decisions, deduplicated by stable target id, and handled as trigger-device safety consequences rather than fuel or heat-pulse sources. The current accepted behavior is `Disarm()` only, never `Arm()` or `Evaluate()`, behind `detonator_fire_safety_enabled`. Timberborn's `Detonator` type is present in `Timberborn.AutomationBuildings.dll` but is not publicly accessible to the mod assembly, so the live adapter uses a reflection wrapper around `GetObjectsWithComponentAt<Detonator>` and `Disarm()` while deterministic tests stay on the typed adapter interface.

Automated coverage for `TWF-153` must prove the disabled setting gate, duplicate target suppression, disabled behavior, unavailable wrapper telemetry, zero armed targets, recoverability counters, and `last_delta_consumer_detonator_fire_safety_*` QA/status tokens.

`TWF-154` adds the first tunnel fire lane. Compact fire deltas are converted into tunnel fire decisions, deduplicated by stable target id, and handled as terrain-affecting infrastructure. The default behavior marks targets unstable and reports deferred destruction through `tunnel_fire_*` telemetry. Native `Tunnel.Explode()` is wrapped but only runs when `tunnel_terrain_destruction_enabled` is explicitly true; it remains disabled by default because terrain mutation needs live save/reload and rebuild evidence. The live adapter resolves `Tunnel` through a reflection wrapper around `Timberborn.Explosions.Tunnel`, because the type is not public to the mod assembly.

Automated coverage for `TWF-154` must prove the behavior setting gate, duplicate target suppression, default deferred destruction, native wrapper gating, unavailable wrapper telemetry, no generic terrain mutation, recoverability counters, and `last_delta_consumer_tunnel_fire_*` QA/status tokens.

## Burn Damage Foundation

`TWF-075` keeps burn-damage state deterministic and Timberborn-local. Automated coverage should prove that static descriptors do not store per-instance damage, target registration owns the stable entity/cell mapping, and downstream consequence tickets can consume bounded state without adding host-owned spread rules.

`TWF-077` adds the first structure burn-damage rollback lane. It consumes compact deltas as construction-value loss, resolves building-like targets separately from infrastructure lanes, deduplicates multi-cell structures by stable target id, closes pausable structures while fire or dangerous heat is present, blocks repair until danger falls below the accepted heat threshold, and reports rollback stages without directly destroying Timberborn entities. Live QA can accept a safely closed/repair-gated structure or an explicit `structure_burn_damage_rollback_skipped_no_safe_api` result; unfinished/construction visual rollback remains telemetry-only until a safe native presentation wrapper is proven.

`TWF-127` adds the first path-infrastructure consequence lane. It consumes compact deltas as burn-damage units, resolves path-like targets, deduplicates by stable target id, calculates damage capacity from construction resources through `TimberbornBurnDamageCapacityCalculator`, treats zero-cost paths as non-burnable safe no-ops, and reports safe-unavailable passability mutation instead of blocking Timberborn paths. Live QA can accept either a safe damaged/repair-eligible target or an explicit `path_infrastructure_skipped_no_safe_api` result; path blocking must remain zero until a recoverable native pathing wrapper is proven.

`TWF-128` adds the first power-infrastructure consequence lane. It consumes compact deltas as burn-damage units, resolves power-like targets, deduplicates by stable target id, calculates construction-resource burn capacity through `TimberbornBurnDamageCapacityCalculator`, treats metal-only infrastructure as safe no-op, and reports safe-unavailable network mutation instead of faking a power outage. Live QA can accept a safely damaged/repair-eligible power target or an explicit `power_infrastructure_skipped_no_safe_api` result; disconnect counters must stay zero until a recoverable Timberborn power-network wrapper is proven.

`TWF-129` adds the first water-infrastructure consequence lane. It consumes compact deltas as burn-damage units, resolves dam/levee/floodgate/valve/sluice-like targets, deduplicates by stable target id, calculates construction-resource burn capacity through `TimberbornBurnDamageCapacityCalculator`, treats water/dirt/metal-only infrastructure as inert safe no-op, and applies an explicit difficult-to-burn resistance before any damage is reported. Live QA can accept a safely damaged/repair-eligible water target or an explicit `water_infrastructure_skipped_no_safe_api` result; water-state mutation counters must stay zero until a recoverable Timberborn water-passage wrapper is proven.

Run:

```bash
dotnet test --filter FullyQualifiedName~TimberbornBurnDamageStateTests
```

Required deterministic evidence:

- Descriptor lookup returns known static descriptors and safe unknown descriptors.
- Damage capacity uses yielded goods and construction investment through `TimberbornResourceFuelCatalog`.
- Unknown resource ids contribute no capacity and remain searchable in state/telemetry.
- Resource-accounting fields, including `FuelValue`, `Flammability`, and `AccountedResourceIds`, survive into exposed state snapshots.
- Changed simulation cells resolve to one owning target when footprints overlap.
- Multi-cell and vertical footprints suppress duplicate damage within one dispatch.
- Damage is bounded by target capacity and state can be captured/restored.
- The optional delta-consumer burn-damage sink reports telemetry while remaining separate from crop, tree, structure, storage, explosive, ash, beaver, and UI consequences.

## Beaver Field Effects

`TWF-071` accepts a conservative beaver-facing field contract. Fire, heat, smoke, toxic smoke, steam, toxic steam, ash aftermath, and wet suppression are field inputs from the simulator or Timberborn-side consequence fields. The release ladder is exposure telemetry, avoidance or work interruption, reversible debuffs, incapacitation, then death. Automatic death, forced incapacitation, native contamination coupling, ash collection behavior, firefighting panic, faction-specific response behavior, and arbitrary path graph mutation are deferred until separate tickets prove safe APIs and recoverability.

Automated coverage for downstream implementation tickets must prove:

- Exposure samples come from real field values or deterministic field fixtures, not hard-coded beaver triggers.
- Field classes remain distinguishable in telemetry: fire, heat, smoke, toxic smoke, steam, toxic steam, ash, and wet suppression.
- Respiratory progression moves through coughing, choking-candidate, and death-candidate counters without applying unsafe native effects by default.
- Burn progression moves through singed, burned, and death-candidate counters without applying unsafe native effects by default.
- Work interruption and avoidance are reported separately from injury or incapacitation.
- Hysteresis prevents a beaver from flickering between exposed and recovered states across adjacent ticks.
- Safe no-op counters identify missing pathing, status, incapacitation, contamination, and death APIs.
- Player feedback aggregates beaver danger instead of emitting one alert per beaver per tick.

Live QA for the first accepted beaver behavior must use a real fire or suppression event, then capture `status` or `qa-readiness` fields showing nonzero exposure telemetry for at least one field class. If an implementation applies a native debuff, the evidence must also show the matching safe wrapper result and recovery path.

## Release Log Noise Policy

`TWF-108` classifies release logs into errors, warnings, diagnostics, QA-only tokens, and too-noisy consequence chatter. Release errors include failed, blocked, invalid, and failure tokens. Release warnings include skipped, missing, unavailable, and disabled tokens. Release diagnostics include lifecycle, configuration, dispatch, binding, registration, and compatibility summaries. QA-only tokens include `wildfire_command_*` and QA stimulus/proof tokens. Per-dispatch consequence summaries are too noisy when they have no matched target or actionable outcome.

Release logs should preserve:

- Startup, compatibility, asset, and initialization diagnostics.
- Dispatch failures, blocked initialization, invalid settings, and presentation failures.
- Bounded dispatch-completed summaries, because they prove cadence and delta counts.
- Sink-bound tokens, because they prove feature wiring once per runtime configuration.
- QA command request/result tokens, but only for QA sessions and support captures.
- Consequence summaries only when a target matched or an action, safe limitation, repair state, destruction, hazardous result, or mutation attempt happened.

Release logs should avoid:

- One line per cell, per beaver, per stored stack, or per visual sample.
- Empty consequence summary logs from every dispatch.
- Repeated unavailable-path chatter after status already exposes the current unavailable field.
- Debug-only surface inspection dumps unless a QA command requested them.

Automated coverage must prove `TimberbornReleaseLogNoisePolicy` classification and at least one quiet consequence dispatch that still returns status counters without writing an info log. Live QA and support captures should prefer `status` or `qa-readiness` for detailed counters, and use `Player.log` for lifecycle, failure, compatibility, and nonzero consequence events.

## Release Simulation Decision Validation

`TWF-044` closes the release-blocking simulation design questions without adding runtime mechanics. Validation for the initial release should prove the conservative path, not speculative variants:

- Cadence: live Timberborn QA should continue to use `qa-readiness --require-advanced-tick` and inspect `cadence_interval_ms=1000`, advancing `tick_count`, and `wildfire_timberborn_dispatch_completed` tokens. If `TWF-048` exposes cadence as a release setting, it needs separate setting-boundary tests and one live run per accepted preset.
- Neighbor model: release shader snapshots should assume 6-neighbor spread only. `TWF-045` should include enough scenarios, such as single ignition, line of fuel, vertical fuel column, and water barrier, to catch accidental diagonal or wind-like spread.
- Wind: no release validation is required beyond confirming there is no wind input, host-owned wind modifier, or wind setting in the public release surface.
- Ash: snapshot and visual validation should treat ash as derived visual output. Passing evidence may show ash decays with heat; persistent ash must not be required for release screenshots or gameplay consequences.
- Vertical building mapping: deterministic tests should keep covering multi-cell and vertical `TimberbornCellFootprint` expansion, sorted cell mapping, and out-of-bounds rejection. Live validation should use mapped building/consequence evidence rather than adding Timberborn-owned fire rules.
- Water: water validation should continue to use `qa-water-suppression-stimulus` followed by `qa-readiness --require-advanced-tick --require-water-changed`, plus `Player.log` proof that the queued `SetWater=3` change produced a GPU delta and a water-change consumer count.
- Heat loss: scenario snapshots and mapping tests should pin material-driven heat-loss bands for terrain, vegetation, stockpile resources, wood-like buildings, and non-burnable buildings. Weather, biome, or season-driven heat-loss changes are out of release scope unless a later adapter ticket adds explicit tests.
- Dispatch strategy: full-grid dispatch is accepted for the first release. `TWF-051` reviewed `TWF-034` profiling and `TWF-046` live-loop evidence and keeps active-frontier optimization deferred. Release validation should watch dispatch/readback timing for regressions, but it must not require active-frontier buffers.

## CLI Fixture Export

Use the CLI fixture exporter when shader tests need deterministic packed-cell inputs without launching Timberborn:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=mixed-terrain --seed=42 --width=32 --height=18 --depth=3 --layer=0 --export-fixture=artifacts/mixed-terrain.fixture.json
```

The JSON fixture contains:

- `formatVersion`.
- Scenario name and seed.
- Grid `width`, `height`, and `depth`.
- Selected layer `index`, flat `offset`, and `cellCount`.
- Packed cell value metadata with `valueType: "uint16"` and index order `x + y * width + z * width * height`.
- Full-grid packed cell `values` in flat index order.

Fixture files are deterministic for the same scenario, seed, dimensions, and layer. Shader harnesses should load the JSON, upload the `values` array as the initial packed grid, and use the selected layer metadata only as the preview or snapshot slice.

## World Consequence Scenario Saves

Use the TWF-118 generator to prepare copied Timberborn save archives for live world-consequence validation without writing directly into user saves:

```bash
bun scripts/generate-wildfire-scenario-save.ts --template "$HOME/Documents/Timberborn/ExperimentalSaves/Wildfire testing/Wildfire testing.timber" --dry-run
bun scripts/generate-wildfire-scenario-save.ts --template /path/to/template-copy.timber --output-dir "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-118-check"
```

The adjacent `wildfire-scenario-manifest.json` is the validation contract for `TWF-119`. QA should inspect `template.entries`, `template.mapSize`, generated entity counts, and `result.schemaBlockers` before attempting a live load. Passing TWF-118 only proves archive inspection, structured JSON handling, overwrite protection, generated output writing, and manifest evidence; live Timberborn loading belongs to TWF-119.

`TWF-119` live QA on 2026-05-03 used evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T152225Z` and fixed artifact `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-131-generated-metadata-fix-20260503T1423Z/wildfire-world-consequence-scenario.timber`. Under active `caffeinate -disu`, Timberborn reached startup dialogs, main menu, Load Game UI, and loaded the exact generated save. The exact save then raised Timberborn Loading issues and deleted the generated manifest objects as invalid locations: badwater sources, water sources, Birch/Oak/Pine trees, Path, Small Tank, Large Pile, and Medium Warehouse. After Continue playing and unpause, `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=20 --require-advanced-tick` passed with `loaded_game_ready=true`, `width=128`, `height=128`, `depth=23`, and `tick_count=4`, but the scenario-content gate failed because the generated checkpoints were removed by Timberborn before validation.

`TWF-132` changes the generator contract after that failure: while terrain/channel/support mutation is still unresolved, the generator no longer clones planned checkpoints into unvalidated coordinates. Instead, it emits survivor-expected checkpoints at existing template-supported BlockObject coordinates when enough matching template entities already exist, and records any shortage as `result.blockedPlacements`. The accepted worker artifact is `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-132-template-supported-checkpoints-20260503T154213Z`. Its manifest uses generator version `TWF-132.0`, reports 24 generated survivor-expected checkpoints, records six blocked placements, and preserves the Timberborn `save_metadata.json.Timestamp` format. Archive inspection showed the generated archive still has `2246` world entities because the survivor checkpoints refer to existing template-supported coordinates rather than injected clones. Generated checkpoints cover two badwater sources, four water sources, 12 tree checkpoints, one warehouse, one pile, one tank, and three path tiles; the remaining blockers are two missing additional badwater-source checkpoints and four missing carrot crop checkpoints.

`TWF-119` narrowed live QA on 2026-05-03 used evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/TWF-119-qa-20260503T154926Z` and the reviewed `TWF-132` artifact. Under active `caffeinate -disu` PID `94422`, the artifact checksum matched the installed save at `~/Documents/Timberborn/ExperimentalSaves/Wildfire generated QA/Wildfire world consequence scenario TWF-119.timber`; static archive inspection matched all 24 manifest-declared survivor checkpoints before load. Load Game selection proof is `05-load-dialog-opened.png`, and `Player.log` confirms `Opening file: .../Wildfire world consequence scenario TWF-119.timber`. Timberborn loaded directly into the save with no Loading issues dialog, and the post-load log scan found no checkpoint deletion or invalid-location tokens. After unpause, `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=20 --require-advanced-tick` passed with `loaded_game_ready=true`, `simulator_integrated=true`, `width=128`, `height=128`, `depth=23`, and `tick_count=5`; `status` also passed. Treat this as a pass for the narrowed load-survival and manifest-checkpoint gate only. The original 50 by 50 layout, crop pads, full badwater source count, water/badwater flow layout, and storage inventory remain outside this accepted rerun because the manifest still records those blockers.

`TWF-133` extends the generator toward the full-layout contract by accepting harvestable crop fallbacks (`Carrot`, `Potato`, `Wheat`, `Sunflower`), modern Folktails path templates (`Path.Folktails`), and manifest evidence for water span, badwater span, and static storage-good references. The generated artifact `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-133-full-layout-20260504T174048` uses the known-valid `Home (20).timber` template and statically matches all manifest-declared checkpoints in the archive: three badwater sources, four water sources, 12 tree checkpoints, four crop checkpoints, one warehouse, one pile, one tank, and three path tiles. The manifest now records storage goods `Carrot`, `Log`, and `Water`. This is not yet a full acceptance pass: the template has only three available badwater sources for the planned four, flow direction remains a live-QA check, and the generated `256x256x23` save is too large for the current live QA harness. The Timberborn adapter now skips Wildfire simulator initialization above `500,000` live cells, and `scripts/load-latest-save-and-unpause.ts` preflights the newest `.timber` save before clicking `Continue` so oversized generated scenarios fail fast instead of locking Timberborn. Use `--skip-latest-save-preflight` only for intentional manual stress runs.

Overwrite safety is part of the generator contract. The tool only writes under the real `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios` tree, validates `--name` as a lowercase slug, rejects `~/Documents/Timberborn` save roots, refuses symlinked output ancestors, and accepts `--overwrite` only when the existing output folder already contains a `wildfire-scenario-manifest.json` marker from `wildfire-scenario-save-generator`.

## Shader Snapshot Coverage

Current `TWF-002` coverage proves the wrapper dispatch contract in .NET tests only. `TWF-006` adds the fixture-driven snapshot harness shape in `Wildfire.Unity`: it reads CLI fixture JSON, creates a `ComputeBufferGrid` from the fixture cells, defines the accepted snapshot JSON shape, compares final packed grids, per-tick compact deltas, and optional visual checksums, and exposes an `IShaderSnapshotExecutor` boundary.

The harness command validates shape, comparison, blocker handling, and Unity executor failure surfacing:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
```

`TWF-018` adds `UnityBatchmodeShaderSnapshotExecutor`, which launches `src/Wildfire.Unity/UnityBatchmodeProject` in Unity batchmode. The Unity Editor runner copies the repository `FireSim.compute` into the temporary project asset area, imports it as a real `ComputeShader`, dispatches `SimulateFullGrid` for a seeded `TWF-000` fixture, reads the append-buffer delta counter and records, reads final packed cells, reads visual fields, and writes snapshot JSON back through the existing `ShaderSnapshotHarness`.

The real shader execution test is opt-in because it requires a local Unity Editor installation, licensing, and compute-shader capable graphics access. CI should keep running the normal .NET harness tests unless the runner image explicitly provides Unity and a graphics device. Use `WILDFIRE_UNITY_EXECUTABLE` when Unity is not installed at the default macOS Hub path.

`TWF-004` adds .NET coverage for the compact delta readback wrapper: `wildfire.deltas` is allocated through the append-buffer abstraction, its append counter is reset before dispatch, the append counter is read after dispatch, compact `CellDelta` records are decoded, and subscribed listeners are notified from the readback result. Those wrapper tests remain contract-only; use the TWF-018 Unity batchmode harness for HLSL compile/runtime proof.

`TWF-005` adds .NET coverage for the visual-field data path only: the visual field is a `float4`-equivalent buffer handle, dispatch records carry it to the compute boundary, and shader source writes the visual sample from packed cell output. The TWF-018 Unity batchmode harness proves shader visual-field readback via checksum, but rendered pixels, GPU texture binding, and material sampling still need later visual validation.

`TWF-045` adds accepted shader snapshot fixtures for the release behavior scenarios:

- Single ignition point.
- Line of fuel.
- Water barrier.
- Vertical fuel column.
- Sparse forest.
- Building cluster.
- Mixed terrain/fuel/water.

For each accepted snapshot, record:

- Scenario name.
- Seed.
- Grid dimensions.
- Tick count.
- Final packed cell grid or semantic final-cell summary.
- Per-tick compact delta counts.
- Per-tick compact delta records for changed cells only, with old and new packed values.
- Evidence that the append-buffer counter is reset before each dispatch/readback cycle.
- Visual field checksum or image artifact when useful.

Update snapshots intentionally only after reviewing the diff scenario by scenario. Regenerate the CLI fixture, run the shader snapshot command, inspect final packed-cell differences, semantic summaries, and per-tick delta differences, and commit or record the changed accepted snapshot JSON with the rule or shader change that justifies it. Avoid broad visual-only approval for behavior changes.

`TWF-041` accepted visual-output tuning constants are now the default `FireSimParameters` values mirrored by `FireVisualField` and bound into `FireSim.compute`:

- Fire: base `0.45`, heat weight `0.55`.
- Smoke: base `0.12`, fuel weight `0.52`, heat weight `0.24`.
- Ash: base `0.18`, inverse-fuel weight `0.5`, heat weight `0.32`.
- Visibility: heat weight `0.55`, smoke weight `0.9`, ash weight `0.8`, with raw fire intensity still allowed to dominate visibility.

Interpretation:

- Fire remains the strongest channel for hot burning cells.
- Heavy-fuel cells just at ignition can read as smoke-dominant before peak fire.
- Ash is stronger on low-fuel residual-heat terrain, but it is still temporary. `PackedCell` has no burn-history field, so ash decays with heat; persistent ash requires an explicit future storage/design decision.
- Visibility no longer lets heat alone dominate every visual sample; it weights residual heat below active fire and smoke so the pooled presentation lane stays less noisy.

Accepted shader snapshot evidence for this tuning pass lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/`:

- `single-ignition`, seed `21`, grid `5x5x1`, ticks `2`: fixture `single-ignition-seed21-5x5x1.fixture.json`, capture `single-ignition-seed21-5x5x1-tick2.capture.json`, checksum `visual-fnv1a32:8710B4BB`.
- `line-of-fuel`, seed `42`, grid `12x5x1`, ticks `4`: fixture `line-of-fuel-seed42-12x5x1.fixture.json`, capture `line-of-fuel-seed42-12x5x1-tick4.capture.json`, checksum `visual-fnv1a32:BFDB9857`.
- Unity log evidence: `single-ignition-unity.log` and `line-of-fuel-unity.log`, both with `phase=compile`, `phase=buffer`, `phase=dispatch`, and `phase=readback` `status=ok` tokens.

Regenerate the accepted TWF-041 snapshots with:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-seed21-5x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-041/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-seed21-5x5x1.fixture.json" --shader ~/repos/wildfire-TWF-041/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-seed21-5x5x1-tick2.capture.json" --ticks 2
dotnet run --project src/Wildfire.Cli -- --scenario=line-of-fuel --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-seed42-12x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-041/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-seed42-12x5x1.fixture.json" --shader ~/repos/wildfire-TWF-041/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-seed42-12x5x1-tick4.capture.json" --ticks 4
```

`TWF-043` accepted fire game-feel tuning keeps the `TWF-044` release decisions intact: the shader still reads the six cardinal 3D neighbors only, there is no wind input, and Timberborn supplies material and water bands without owning fire rules.

Accepted `FireSim.compute` game-feel constants:

- Ignition: `FIRE_IGNITION_BASE_HEAT=11`, `FIRE_WATER_IGNITION_PENALTY=2`.
- Spread: `FIRE_RETAINED_HEAT_WEIGHT=2`, `FIRE_SPREAD_HEAT_WEIGHT=1`, `FIRE_BURNING_NEIGHBOR_HEAT_BONUS=3`, `FIRE_BURNING_NEIGHBOR_DIRECT_HEAT=1`.
- Water suppression: `FIRE_WATER_SUPPRESSION_HEAT=2`, `FIRE_WATER_EVAPORATION_HEAT=10`.
- Burn pressure: `FIRE_FLAMMABILITY_BURN_PRESSURE=2`, `FIRE_WATER_BURN_PRESSURE_PENALTY=3`, `FIRE_BURN_HEAT_BASE=1`.

Accepted Timberborn adapter material bands:

- Wood-like buildings: fuel `15`, flammability `1`, heat loss `3`.
- Stockpile resources: fuel `8`, flammability `2`, heat loss `3`.
- Vegetation: fuel `10`, flammability `3`, heat loss `1`.
- Non-burnable buildings and solid terrain keep their existing non-fuel and high-heat-loss behavior.

Interpretation:

- Single ignition now radiates to adjacent cells in broad grass-like fuel instead of having neighbor heat disappear into integer averaging.
- Line-of-fuel remains bounded and legible: the ignition advances along the fuel line, then settles as available heat and stochastic burn rolls decline.
- The water barrier remains an effective suppression case: with `SetWater=3`, the accepted snapshot leaves only one hot cell after four ticks, proving water raises ignition difficulty and suppresses burn pressure without adding host-owned fire rules.
- Wood-like buildings burn longer and less explosively because they carry more fuel, lower flammability, and lower heat loss than the old band; vegetation remains the fast-catching material.

Accepted shader snapshot evidence for this tuning pass lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/`:

- `single-ignition`, seed `21`, grid `5x5x1`, ticks `2`: fixture `single-ignition-seed21-5x5x1.fixture.json`, capture `single-ignition-seed21-5x5x1-tick2.capture.json`, checksum `visual-fnv1a32:50C4978E`, per-tick deltas `[5, 5]`, final hot cells `5`.
- `line-of-fuel`, seed `42`, grid `12x5x1`, ticks `4`: fixture `line-of-fuel-seed42-12x5x1.fixture.json`, capture `line-of-fuel-seed42-12x5x1-tick4.capture.json`, checksum `visual-fnv1a32:120F70AE`, per-tick deltas `[5, 5, 5, 2]`, final hot cells `5`.
- `water-barrier`, seed `42`, grid `12x5x1`, ticks `4`: fixture `water-barrier-seed42-12x5x1.fixture.json`, capture `water-barrier-seed42-12x5x1-tick4.capture.json`, checksum `visual-fnv1a32:40818F57`, per-tick deltas `[5, 5, 5, 5]`, final hot cells `1`.
- Unity log evidence: `single-ignition-unity.log`, `line-of-fuel-unity.log`, and `water-barrier-unity.log`, all with `phase=compile`, `phase=buffer`, `phase=dispatch`, and `phase=readback` `status=ok` tokens.
- Screenshot evidence: no live Timberborn screenshot was captured in the worker pass. QA must capture live screenshots after deploying these constants, and should attach the copied `Player.log` plus command evidence from the live sequence below before marking the ticket accepted.

Regenerate the accepted TWF-043 snapshots with:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-043/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1.fixture.json" --shader ~/repos/wildfire-TWF-043/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1-tick2.capture.json" --ticks 2
dotnet run --project src/Wildfire.Cli -- --scenario=line-of-fuel --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-043/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1.fixture.json" --shader ~/repos/wildfire-TWF-043/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1-tick4.capture.json" --ticks 4
dotnet run --project src/Wildfire.Cli -- --scenario=water-barrier --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-043/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1.fixture.json" --shader ~/repos/wildfire-TWF-043/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1-tick4.capture.json" --ticks 4
```

Run the accepted TWF-043 shader harness assertions with:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled
```

Live Timberborn QA for this tuning pass should deploy the mod, load and unpause a save, run `qa-delta-stimulus` or `qa-building-burnout-stimulus` for visible fire, run `qa-water-suppression-stimulus` plus `qa-readiness --require-advanced-tick --require-water-changed` for suppression proof, capture screenshots of the visible loop, and copy `Player.log` tokens showing the command request/result, queued GPU changes, compute dispatch/readback, visual/presentation update, and water-change consumer count.

## TWF-088 Spread Pace Evidence

`TWF-088` tunes only spread pace by changing `FIRE_BURNING_NEIGHBOR_HEAT_BONUS` from `3` to `5` in `FireSim.compute`. The shader still reads only the six cardinal 3D neighbors; ignition threshold, burn pressure, fuel duration, water suppression, structure behavior, burnout cooling, visual scale, prefab choice, and player alerts are unchanged.

Accepted deterministic shader evidence for this tuning pass lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-088-spread-pace/`:

- `single-ignition`, seed `21`, grid `5x5x1`, ticks `2`: per-tick deltas `[5, 5]`, final hot cells `5`, burning cells `0`, max heat `7`, fuel total `175`, checksum `visual-fnv1a32:F37C248E`.
- `line-of-fuel`, seed `42`, grid `12x5x1`, ticks `4`: per-tick deltas `[5, 5, 5, 5]`, final hot cells `5`, burning cells `2`, max heat `12`, fuel total `103`, checksum `visual-fnv1a32:5F54D28E`.
- `sparse-forest`, seed `73`, grid `16x10x1`, ticks `3`: per-tick deltas `[5, 5, 5]`, final hot cells `5`, burning cells `1`, max heat `12`, fuel total `978`, checksum `visual-fnv1a32:82C9CDCA`.
- `building-cluster`, seed `91`, grid `14x10x1`, ticks `3`: per-tick deltas `[5, 5, 5]`, final hot cells `1`, burning cells `0`, max heat `2`, fuel total `1179`, checksum `visual-fnv1a32:5D5FCA57`.
- `water-barrier`, seed `42`, grid `12x5x1`, ticks `4`: per-tick deltas `[5, 5, 5, 5]`, final hot cells `1`, burning cells `0`, max heat `3`, water cells `5`, fuel total `385`, checksum `visual-fnv1a32:5947E999`.

Interpretation:

- Dry contiguous fuel has a livelier edge: `line-of-fuel` now keeps changing through tick `4` and ends with two burning cells instead of one.
- Broad single ignition and sparse forest remain bounded at the accepted snapshot tick counts rather than becoming immediate runaway fires.
- The building-cluster scenario records persistent neighbor heat through every tick, but does not change structure behavior or fuel duration in this ticket.
- The water barrier remains a barrier at the accepted tick count: one hot cell, zero burning cells, and all five water cells still present.

Accepted live Timberborn QA evidence for the low-resolution spread recording lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-088-live-20260503T161336Z/`. The rerun deployed `~/repos/wildfire-TWF-088` at commit `8eff5cf6adf85cf8729ab19c1abdb592a7f549e3`, launched and loaded the latest save, unpaused successfully, and proved command responsiveness with `qa-readiness --require-advanced-tick` at `tick_count=13`. During `scripts/record-timberborn-qa.ts --mode low --duration=20`, `qa-delta-stimulus` queued center cell `188480` at `x=64 y=64 z=11`, and follow-up `qa-readiness --require-advanced-tick --require-nonzero-delta` passed at `tick_count=33` with `last_delta_count=1`, `last_delta_consumer_changed_cells=1`, `last_delta_consumer_gameplay_consequences=1`, `updated_visual_regions=1`, `player_fire_alert_notification_sent=true`, and `pooled_fire_effects_native_prefab=CampfireFire`.

Live artifact highlights:

- Deploy transcript: `twf-088-live-20260503T161336Z/deploy-transcript.txt`.
- Load/unpause transcript and screenshots: `twf-088-live-20260503T161336Z/latest-save-startup/`.
- Command transcripts: `qa-readiness-before-stimulus.txt`, `spread-stimulus-and-readiness-transcript.txt`, and `final-status-transcript.txt`.
- Recording metadata and movie: `screen-recordings/2026-05-03T16-15-07-544Z-low/recording-metadata.json` and `recording.mov`.
- Copied `Player.log` and token excerpts: `Player.log`, `player-log-spread-tokens.txt`, and `player-log-stimulus-focused-tokens.txt`.
- Final state: `caffeinate -disu` PID `94422` remained active, Timberborn PID `50103` remained running, and no shared QA lock file was present in either lock root.

## TWF-089 Fuel Burn-Duration Evidence

TWF-089 deterministic shader evidence lives in the ticket worktree and remains the accepted proof for low, medium, and high burn-duration constants until the live bridge exposes selectable fuel-duration targets. The reviewed implementation in `~/repos/wildfire-TWF-089` at commit `082077d2b99819c4b448b0ba9fe758ed81f4f412` accepts depletion ticks of low fuel `7`, medium fuel `15`, and high fuel `27` in `tests/Wildfire.Core.Tests/ShaderSnapshots/twf-089/`.

Live QA on 2026-05-03 deployed `~/repos/wildfire-TWF-089`, loaded and unpaused a command-responsive save, and preserved evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-20260503T162018Z/`. The run proved deploy/readiness and captured a normal-angle 35 second recording at `recordings/2026-05-03T16-22-04-247Z-high/recording.mov`; `qa-readiness --require-advanced-tick` passed at `tick_count=18`, and `qa-delta-stimulus` queued the fixed center cell `188480` at `x=64 y=64 z=11` with `set_cell=13311`. Follow-up status showed the visible/readable medium fixed-stimulus path at tick `39`: `last_delta_consumer_started_burning=1`, `active_pooled_fire_effects=1`, native prefab `CampfireFire`, `player_fire_alert_notification_sent=true`, and max heat `15`.

Do not treat that live run as accepting the low/medium/high burn-duration gate. That run used only the old fixed `qa-delta-stimulus` path, which did not allow QA to select low, medium, and high fuel inputs or read durable per-target burn start/depletion ticks. The sampled status window from tick `38` through `77` never reported `last_delta_consumer_fuel_depleted>0`.

Live QA retry preflight on 2026-05-03 stopped before low/medium/high sampling because no verified deploy source contained both sides of the gate. Evidence lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-089-live-retry-preflight-20260503T163733Z/`. `main` contained the TWF-134 command bridge fields but not the TWF-089 `FireSim.compute` fuel-burn-down tuning; `~/repos/wildfire-TWF-089` contained the accepted shader tuning at `082077d2b99819c4b448b0ba9fe758ed81f4f412` but not `qa-burn-duration-stimulus` or `burn_duration_proof_*`. The loaded Timberborn save was command-responsive under `caffeinate -disu` PID `94422`, but deployed `help` still listed only `help,qa-building-burnout-stimulus,qa-delta-stimulus,qa-readiness,qa-water-suppression-stimulus,status`, and `qa-readiness` had no durable burn-duration proof fields. Do not rerun this live gate until the deploy source is a single reconciled tree containing both the TWF-089 shader tuning and the TWF-134 proof command.

`TWF-135` reconciles that split deploy source by importing the reviewed `TWF-089` shader tuning and deterministic shader artifacts into the checkout that already has the reviewed `TWF-134` command bridge. After `TWF-135` review passes, use this reconciled tree for the low, medium, and high live proof retry.

The live proof retry should use the QA-only command `qa-burn-duration-stimulus <target>`, where `<target>` is exactly `low`, `medium`, or `high`. The command queues heat on one imported burnable target through the existing safe simulator change path and does not accept arbitrary coordinates or packed-cell values. The target bands are low fuel `1..4`, medium fuel `5..10`, and high fuel `11..15`; each command result reports `target_material`, `companion_target_id`, `initial_cell`, `target_index`, `target_x`, `target_y`, `target_z`, `initial_fuel`, `set_heat`, and `timeout_ticks`.

After recording, QA should sample `status` or `qa-readiness` and preserve the durable `burn_duration_proof_*` fields:

- `burn_duration_proof_target`
- `burn_duration_proof_target_index`
- `burn_duration_proof_target_x`
- `burn_duration_proof_target_y`
- `burn_duration_proof_target_z`
- `burn_duration_proof_initial_fuel`
- `burn_duration_proof_queued_tick`
- `burn_duration_proof_burn_start_tick`
- `burn_duration_proof_depletion_tick`
- `burn_duration_proof_elapsed_burn_ticks`
- `burn_duration_proof_timeout_ticks`
- `burn_duration_proof_timed_out`
- `burn_duration_proof_status`

A passing live burn-duration proof needs one recorded run per target or an equivalent bounded sequence, copied `Player.log`, command output for the stimulus, and final `status` or `qa-readiness` output showing `burn_duration_proof_status=depleted` with non-placeholder burn start, depletion, and elapsed tick fields. If depletion is not observed by the timeout window, the status surface reports `burn_duration_proof_status=no_depletion_timeout` and `burn_duration_proof_timed_out=true`, which is evidence but not acceptance for `TWF-089`.

## Release Shader Snapshot Evidence

`TWF-045` accepts the release shader snapshot set after the `TWF-043` game-feel tuning and `TWF-044` conservative release decisions. Exact accepted capture JSONs are committed under `tests/Wildfire.Core.Tests/ShaderSnapshots/release/`; those files contain the durable `finalPackedCells` arrays and every per-tick delta record with `cellIndex`, `oldCell`, and `newCell`. Local fixture, capture, and Unity log mirrors live under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/`.

| Scenario               |  Seed | Grid      | Ticks | Per-tick deltas | Final semantic summary                                                  | Visual checksum           | Accepted files                                                                                                                             |
| ---------------------- | ----: | --------- | ----: | --------------- | ----------------------------------------------------------------------- | ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `single-ignition`      |  `21` | `5x5x1`   |   `2` | `[5, 5]`        | hot `5`, burning `0`, max heat `7`, water cells `0`, fuel total `175`   | `visual-fnv1a32:50C4978E` | `single-ignition-seed21-5x5x1.fixture.json`, `single-ignition-seed21-5x5x1-tick2.capture.json`, `single-ignition-unity.log`                |
| `line-of-fuel`         |  `42` | `12x5x1`  |   `4` | `[5, 5, 5, 2]`  | hot `5`, burning `1`, max heat `12`, water cells `0`, fuel total `104`  | `visual-fnv1a32:120F70AE` | `line-of-fuel-seed42-12x5x1.fixture.json`, `line-of-fuel-seed42-12x5x1-tick4.capture.json`, `line-of-fuel-unity.log`                       |
| `water-barrier`        |  `42` | `12x5x1`  |   `4` | `[5, 5, 5, 5]`  | hot `1`, burning `0`, max heat `2`, water cells `5`, fuel total `385`   | `visual-fnv1a32:40818F57` | `water-barrier-seed42-12x5x1.fixture.json`, `water-barrier-seed42-12x5x1-tick4.capture.json`, `water-barrier-unity.log`                    |
| `vertical-fuel-column` |  `17` | `5x5x4`   |   `4` | `[6, 6, 2, 1]`  | hot `6`, burning `1`, max heat `11`, water cells `0`, fuel total `44`   | `visual-fnv1a32:5F05530F` | `vertical-fuel-column-seed17-5x5x4.fixture.json`, `vertical-fuel-column-seed17-5x5x4-tick4.capture.json`, `vertical-fuel-column-unity.log` |
| `sparse-forest`        |  `73` | `16x10x1` |   `3` | `[5, 5, 5]`     | hot `5`, burning `1`, max heat `12`, water cells `0`, fuel total `978`  | `visual-fnv1a32:E4355BFA` | `sparse-forest-seed73-16x10x1.fixture.json`, `sparse-forest-seed73-16x10x1-tick3.capture.json`, `sparse-forest-tick3-unity.log`            |
| `building-cluster`     |  `91` | `14x10x1` |   `3` | `[5, 1, 5]`     | hot `1`, burning `0`, max heat `1`, water cells `0`, fuel total `1179`  | `visual-fnv1a32:D12ED5D7` | `building-cluster-seed91-14x10x1.fixture.json`, `building-cluster-seed91-14x10x1-tick3.capture.json`, `building-cluster-tick3-unity.log`   |
| `mixed-terrain`        | `123` | `16x10x3` |   `3` | `[6, 6, 6]`     | hot `5`, burning `0`, max heat `4`, water cells `10`, fuel total `3286` | `visual-fnv1a32:67BFDEEA` | `mixed-terrain-seed123-16x10x3.fixture.json`, `mixed-terrain-seed123-16x10x3-tick3.capture.json`, `mixed-terrain-tick3-unity.log`          |

Each Unity log has `phase=compile`, `phase=buffer`, per-tick `phase=dispatch`, and per-tick `phase=readback` `status=ok` tokens. The opt-in test `UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled` regenerates each scenario through real Unity compute execution and compares the full capture against the committed JSON with `ShaderSnapshotComparison`, so a moved heat/fuel/water value or changed old/new delta record fails even if aggregate totals remain unchanged. Per-tick GPU append order is not part of the production contract; comparison sorts expected and actual delta records by `cellIndex`, `oldCell`, and `newCell` before comparing the record set. The Unity batchmode runner resets the append-buffer counter with `deltas.SetCounterValue(0)` before every tick dispatch, reads it with `ComputeBuffer.CopyCount`, and the non-Unity wrapper test `TickResetsAppendCounterBeforeEveryFullGridDispatch` keeps the repeated-tick reset contract covered in normal `dotnet test`.

Regenerate one accepted snapshot with:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=<scenario> --seed=<seed> --width=<width> --height=<height> --depth=<depth> --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<fixture>.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-045/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<scenario>-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<fixture>.fixture.json" --shader ~/repos/wildfire-TWF-045/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<capture>.capture.json" --ticks <ticks>
```

Run the accepted release snapshot assertions with:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled
```

## Coherent Live Gameplay Loop Evidence

`TWF-046` accepts the first coherent live Timberborn gameplay loop after the `TWF-043` game-feel tuning and `TWF-045` release snapshot set. Live QA on 2026-05-02 attached to an already-running loaded save at `4de4642e7fd84d5033cf4b0a694db5b74b03238b`, used only the guarded startup and allowlisted stimulus paths, and preserved evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-046-live-20260502T232641Z/`.

Run shape:

```bash
bun scripts/load-latest-save-and-unpause.ts --attach --wait=180 --artifacts-dir "$ARTIFACT/latest-save-attach" --lock-timeout=60
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=10 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick --require-nonzero-delta
printf 'qa-building-burnout-stimulus\n' > "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt"
bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=10 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick --require-water-changed
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=10 --require-advanced-tick
```

Accepted evidence:

- Guarded loaded-save attach reached `screen=loaded-save`, detected the save was already unpaused, and observed `tick_count` advance from `1707` to `1709`.
- Baseline `qa-readiness` reported `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, `tick_count=1722`, and `queued_changes=0`.
- `qa-delta-stimulus` queued the fixed center cell `target_index=188480`, `target_x=64`, `target_y=64`, `target_z=11`, `set_cell=13311`.
- The next dispatch at tick `1734` uploaded the queued change, read back `delta_count=2`, updated the visual field, emitted `active_pooled_effects=1`, sent `Wildfire alert: 1 new fire. Max heat 15.`, and recorded `changed_cells=2`, `started_burning=1`, `visual_effect_events=2`, `gameplay_consequences=1`, and `alerts=1`.
- Follow-up spread/resolution ticks kept advancing: tick `1735` reported `last_delta_count=1`, `active_pooled_fire_effects=1`, and alert counters for tick `1734`; tick `1736` reported `stopped_burning=1` and `gameplay_consequences=1`; by tick `1739`, heat settled to `0`.
- Visible screenshots show the native Timberborn quick warning and loaded-save state: `fire-stimulus-visible-alert.png`, `building-burnout-consequence-alert.png`, `water-suppression-resolution.png`, and `final-stability-screen.png`.
- `qa-water-suppression-stimulus` queued `SetWater=3` for the same fixed center target, and follow-up `qa-readiness --require-water-changed` reported `last_positive_water_changed_tick=1851`, `last_positive_water_changed_count=1`, `queued_changes=0`, and stable zero-delta state at `tick_count=1852`.
- Delayed stability at `tick_count=1908` reported `queued_changes=0`, `last_delta_count=0`, `visual_field_surface_bound=true`, `pooled_fire_effects_visible_enabled=true`, `player_fire_alert_presentation_failures=0`, `pooled_fire_effect_presentation_failures=0`, and `message=loaded_game_ready`.
- Copied log evidence includes `Player.log`, the baseline-bounded `Player-run-window.log`, `Player-run-window-wildfire-events.txt`, and `twf-046-live-loop-summary.txt`. The strict run-window failure scan in `Player-run-window-failures.txt` has `0` lines.
- Final QA lock state: no lock files under `~/Library/Application Support/Timberborn/WildfireQA/locks` or `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/locks`. Timberborn remained running because QA attached to a pre-existing process.

Follow-up note: `TWF-064` re-read the preserved building-burnout evidence and found that the first direct `qa-building-burnout-stimulus` did apply one pause consequence at dispatch tick `1768`, but the later `qa-readiness` proof point was sampled at tick `1770` after volatile last-dispatch fields had returned to `0`. Building-burnout proof should therefore use durable `last_positive_building_burnout_applied_*` status fields plus the original nonzero `Player.log` consumer token, not only the latest `last_delta_consumer_building_burnout_*` values.

## Enabled Save Reload Evidence

`TWF-093` accepts the baseline enabled-mod save/reload path. Live QA on 2026-05-03 triggered fire activity, saved `Wildfire testing (7)` at `Cycle 23, day 14`, reloaded `/Users/jasonkleinberg/Documents/Timberborn/ExperimentalSaves/Wildfire testing/Wildfire testing (7).timber` with Wildfire still enabled, and preserved evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-093-enabled-save-reload-20260503T040447Z`.

Accepted evidence:

- Pre-save `qa-delta-stimulus` and readiness output reported `last_delta_count=2`, `started_burning=1`, `visual_effect_events=2`, `gameplay_consequences=1`, and `alerts=1`.
- `Player.log` recorded the save as `Saving game to Wildfire testing - Wildfire testing (7) at 2026-05-03 00:06:57Z` followed by `Saved game in 0.39s`.
- Post-reload attach reached the loaded save, unpaused, and recovered command/status output with `loaded_game_ready=true`, `simulator_integrated=true`, `visual_field_surface_bound=true`, dimensions `128x128x23`, and fresh dispatch ticks.
- Post-reload stimulus proved the visual, alert, and command paths again with `last_delta_count=2`, `visual_effect_events=2`, `alerts=1`, `active_pooled_fire_effects=1`, `notification_sent=true`, and `pooled_fire_effect_presentation_failures=0`.
- `Player-run-window-critical-scan.txt` had `0` lines. A transient first post-reload nonzero-delta check missed the narrow `last_delta_count` window, and rerun evidence in `18-post-reload-second-after-delta-readiness.txt` passed the same gate.

Use this evidence as the enabled-save baseline before disabled-mod recovery (`TWF-094`) and re-enable rebuild validation (`TWF-095`).

## Disabled Mod Recovery Evidence

`TWF-094` accepts the disabled-mod recovery path for the current local player-facing workflow. Live QA on 2026-05-03 used Timberborn's main-menu `Mods` dialog to disable `Wildfire v0.1.0.0`, relaunched with Wildfire still unchecked, loaded `Wildfire testing (7)`, accepted Timberborn's missing-mod warning, and preserved evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-094-disabled-mod-recovery-20260503T041547Z`.

Accepted evidence:

- Pre-disable `qa-readiness` proved the save was loaded with Wildfire enabled and the simulator integrated.
- The disable path used Timberborn UI state, not deploy-folder cleanup: `13-main-menu-mods-dialog-before-disable.png`, `14-main-menu-mods-dialog-wildfire-disabled.png`, `15-after-mods-ok.png`, and `16-main-menu-after-disable-ok.png`.
- After relaunch, Wildfire remained unchecked in the Mods dialog, and `Player.log` active-mod output excluded Wildfire.
- Loading `Wildfire testing (7)` with Wildfire disabled produced Timberborn's missing-mod warning: `You are trying to load a game without mods that it was saved with.` Choosing `Yes` loaded the save into gameplay.
- `qa-readiness` timed out waiting for `command-outbox.txt`, which is expected with Wildfire disabled because the Wildfire runtime and QA bridge are absent.
- Copied disabled-load logs had no critical exception, error, or crash scan hits.

This is recoverable disabled-load evidence, not re-enable evidence. `TWF-095` should start from the preserved disabled/missing loaded state when possible.

## Reenable Runtime Rebuild Evidence

`TWF-095` accepts the re-enable runtime rebuild path after disabled-mod recovery. QA artifacts on 2026-05-03 show Wildfire re-enabled through Timberborn's Mods dialog, Timberborn restarted, and `Wildfire testing (7)` loaded again with runtime state rebuilt. Evidence lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-095-reenable-runtime-rebuild-20260503T050000Z`.

Accepted evidence:

- The run started from the preserved `TWF-094` disabled/missing loaded state and re-enabled `Wildfire v0.1.0.0` through screenshots `06-main-menu-mods-before-reenable.png`, `07-main-menu-mods-wildfire-reenabled.png`, and `08-after-mods-ok-reenabled.png`.
- After restart, `Player-after-explicit-wildfire-testing-7-load.log` listed `Wildfire (v0.1.0.0)`, loaded compute and diagnostic assets, bound the visual-field surface, initialized the simulator at `128x128x23`, and completed runtime initialization.
- A transient malformed command race appears in `23-qa-readiness-after-reenable-explicit-save.txt` and `24-status-after-reenable-explicit-save.txt` as `Unknown_command_'qa-read...'`; recovery command `25-status-after-command-race-recovery.txt` passed with `runtime_loaded=true`, `loaded_game_ready=true`, `simulator_integrated=true`, dimensions `128x128x23`, and `visual_field_surface_bound=true`.
- After unpause, `27-qa-readiness-after-unpause-advanced-tick.txt` passed with `tick_count=4`, `queued_changes=0`, `visual_field_surface_cells=376832`, and `message=loaded_game_ready`.
- Passive log scan found only existing non-Wildfire Unity `gpath.c:115` assertions; no stale simulator crash or Wildfire runtime failure was present in the accepted re-enable evidence.

Use this as the child evidence for parent lifecycle gate `TWF-047`.

## Save Lifecycle Parent Acceptance

`TWF-047` accepts the save lifecycle gate from the three child runs above. The coherent story uses `Wildfire testing (7)` across enabled save/reload, local player-facing Mods-dialog disable, missing-mod warning recovery, Mods-dialog re-enable, restart, explicit reload, runtime rebuild, and post-unpause readiness. No save lifecycle defect was exposed, so the accepted parent gate requires no production-code change.

## Timberborn Validation

Live Timberborn validation should start only after the GPU simulator and adapter path can:

- Upload terrain/building/water cells.
- Register external heat and water changes.
- Dispatch on a fixed cadence.
- Read compact deltas.
- Update overlays or effects from changed cells.
- Apply gameplay consequences from deltas.

Current `TWF-007` coverage proves the mapper contract in .NET tests only. It does not prove live Timberborn API binding, map-service discovery, terrain-height queries, building footprint extraction, vegetation/resource component lookup, water-depth sampling, mod loading, or in-game dispatch because the repository still has no Timberborn mod project reference or live-game harness wired to this adapter scaffold.

Current `TWF-012` and `TWF-019` coverage adds an in-process command bridge plus a narrow Timberborn game-context file binding. QA can invoke read-only `status`, `qa-readiness`, or `help` from a loaded game by writing one command to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt`, or by running:

```bash
bun scripts/invoke-timberborn-command.ts status
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
```

The Timberborn adapter polls that inbox from `TimberbornQaCommandFileBridge`, forwards the command to `TimberbornQaCommandBridge`, deletes the inbox, and writes the latest result to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-outbox.txt`. The script and bridge expose only known allowlisted commands. Unknown manual inbox commands are rejected by the bridge and logged as failures rather than executed.

Current `TWF-008` coverage adds a Timberborn game-context runtime singleton for fixed-cadence dispatch. `TimberbornFireRuntime` is the command-bridge state provider, so `status` and `qa-readiness` report `bridge_alive=true`, `runtime_loaded`, `loaded_game_ready`, `simulator_integrated`, dimensions, `tick_count`, `queued_changes`, and `last_delta_count` after a simulator is attached. When no simulator factory has been attached by the live host yet, simulator fields intentionally return `placeholder` and `loaded_game_ready=false`.

`qa-readiness` is intentionally a loaded-game readiness probe, not a UI automation command. It does not navigate menus, click Timberborn UI, load saves, delete saves, invoke arbitrary `VisualElement` callbacks, mutate the Wildfire grid, or trigger debug/destructive actions. Treat `success=true` as "the command was handled safely"; treat `loaded_game_ready=true` plus numeric dimensions and tick fields as the loaded-game readiness signal. For live QA that needs to prove fixed-cadence dispatch is advancing, unpause the loaded save first and add `--require-advanced-tick`; the command script then fails unless the result includes `tick_count` greater than `0`.

`TWF-097` adds the release safety switch `JasonKleinberg.Wildfire.release.wildfire_enabled`. Missing settings should report `wildfire_enabled=true` to preserve default enabled behavior. A stored value of `0` should report `wildfire_enabled=false`, `loaded_game_ready=false`, and `message=wildfire_disabled` from `qa-readiness` while still allowing `status`, `qa-readiness`, and `help` to return command/status output. Live disabled-state QA should load a save, capture `status` or `qa-readiness` output with `wildfire_enabled=false`, attempt `qa-delta-stimulus` and confirm it fails with `message=wildfire_disabled` without increasing `queued_changes`, then wait longer than one cadence interval and confirm no new `wildfire_timberborn_dispatch_completed` token appears after the disabled-state status token. The searchable disabled dispatch token is `wildfire_timberborn_dispatch_skipped_disabled`. Re-enable lifecycle evidence remains covered by `TWF-095`; do not treat this setting ticket as full save/re-enable lifecycle proof.

`TWF-049` adds startup compatibility probes before the runtime enters normal loaded-save evidence collection. QA should capture `Player.log` `wildfire_timberborn_compatibility_probe_summary ... status=<compatible|degraded|failed> ... required_passed=<n>/<n> ... optional_passed=<n>/<n> ... degraded_features=<tokens>` and at least one `wildfire_timberborn_compatibility_probe_result` token for each release-facing lane: `terrain`, `building_burnout`, `compute`, `diagnostic_assets`, `visual_effects`, and `player_alerts`. A release-compatible live run should show `compatibility_probe_status=compatible` or an intentionally accepted `compatibility_probe_status=degraded` in a follow-up `qa-readiness` or `status` result; `compatibility_probe_status=failed` is a blocker because the required compute-backed runtime path or required Timberborn terrain surface is unavailable. Required failures must also produce `wildfire_timberborn_runtime_initialization_blocked` or `wildfire_timberborn_runtime_initialize_rejected`, and `qa-readiness` must report `loaded_game_ready=false`.

The building-burnout probes are optional compatibility probes. Missing or changed `IBlockService` or `PausableBuilding` surfaces should degrade the `building_burnout` lane and become follow-up evidence for `TWF-064`, but they should not block healthy terrain mapping or compute dispatch by themselves. The compute bundle probe is intentionally stronger than `File.Exists`: it checks that the selected private bundle exists, is non-empty, and starts with a Unity AssetBundle header such as `UnityFS`. This catches missing, empty, or plainly wrong content in `TWF-049`; full AssetBundle loading, FireSim asset lookup, and kernel validation remain the runtime load path and should be hardened further by `TWF-050`. Optional degradation, such as missing native visual-effect prefabs or the diagnostic bundle, must be called out in the QA notes with the exact `compatibility_probe_degraded_features` token.

`TWF-031` adds one non-read-only QA stimulus command: `qa-delta-stimulus [selector]`. The optional selector is allowlisted to `burnable`, `tree`, `vegetation`, `crop`, `storage`, or `building`; it does not accept user-supplied coordinates or packed-cell values. It requires an initialized `TimberbornFireRuntime`, chooses a real imported burnable field target matching the selector, and queues one `SetHeat=15` external change through `IGpuFireSimulator.RegisterChange`; it does not mutate Timberborn terrain, buildings, saves, UI state, or simulator buffers directly. The command result message includes `target_selector`, `target_material`, `companion_target_id`, `initial_cell`, `target_index`, `target_x`, `target_y`, `target_z`, `set_heat`, and `queued_heat_changes`; the same result token also reports current `queued_changes` and `tick_count` so QA can tie the request to the next simulator dispatch. If no imported burnable target exists, the command fails explicitly instead of falling back to a synthetic cell.

Use the stimulus only after the guarded startup utility has loaded and unpaused a save:

```bash
bun scripts/load-latest-save-and-unpause.ts --launch
bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-nonzero-delta
```

The first command queues the bounded heat change against the imported target. The follow-up `qa-readiness` or `status` command is the proof point after at least one subsequent fixed-cadence tick; `--require-nonzero-delta` fails unless the result reports `last_delta_count` greater than `0`. Live QA evidence should include both command outputs plus `Player.log` tokens for `wildfire_command_request command=qa-delta-stimulus`, `wildfire_timberborn_qa_delta_stimulus_queued`, `wildfire_timberborn_changes_registered source=qa_delta_stimulus`, and the subsequent `wildfire_timberborn_dispatch_completed ... delta_count=<nonzero>` line.

`TWF-038` adds one QA-only water suppression command: `qa-water-suppression-stimulus [selector]`. The optional selector is allowlisted to `burnable`, `tree`, `vegetation`, `crop`, `storage`, or `building`; it does not accept user-supplied coordinates or packed-cell values. It requires an initialized `TimberbornFireRuntime`, chooses a real imported burnable field target matching the selector with water below `3`, and queues exactly one `SetWater=3` external change through `IGpuFireSimulator.RegisterChange`; it does not mutate Timberborn water, terrain, buildings, saves, UI state, simulator buffers, or arbitrary coordinates directly. The command bridge rejects broad coordinate mutation attempts such as `qa-water-suppression-stimulus x=1 y=2` before state is queried. The command result message includes `target_selector`, `target_material`, `companion_target_id`, `initial_cell`, `target_index`, `target_x`, `target_y`, `target_z`, `set_water`, and `queued_water_changes`; the same result token reports current `queued_changes`, `tick_count`, `last_delta_count`, `last_delta_consumer_water_changed`, `last_positive_water_changed_tick`, and `last_positive_water_changed_count` so QA can tie the accepted target to the next simulator dispatch and prove the consumer saw a water-field change even if later zero-delta ticks have overwritten the last-dispatch fields. If no imported burnable target is eligible, the command fails explicitly instead of falling back to a synthetic cell.

Use the suppression stimulus only after the guarded startup utility has loaded and unpaused a save:

```bash
bun scripts/load-latest-save-and-unpause.ts --launch
bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed
```

The first command queues the bounded water change and should report `queued_changes=1` without advancing the target itself. The follow-up `qa-readiness` or `status` command is the proof point after at least one subsequent fixed-cadence tick; passing evidence needs numeric `tick_count` advancement and `last_positive_water_changed_count` greater than `0`, with `last_positive_water_changed_tick` naming the dispatch that produced it. `last_delta_count` alone is not sufficient for this ticket because non-water external changes can append deltas, and `last_delta_consumer_water_changed` may return to `0` after later settled ticks. Live QA evidence should include both command outputs plus `Player.log` tokens for `wildfire_command_request command=qa-water-suppression-stimulus`, `wildfire_timberborn_qa_water_suppression_queued`, `wildfire_timberborn_changes_registered source=qa_water_suppression`, the subsequent `wildfire_timberborn_dispatch_completed ... delta_count=<nonzero>` line, and `wildfire_timberborn_delta_consumer_completed ... water_changed=<nonzero>`. If no eligible imported target exists, reload or use a different save before marking the ticket failed; do not add ad hoc coordinates to the command.

`TWF-033` binds the first Timberborn-facing consequence to the existing delta-consumer sink surface. The live runtime records debug visual state only for changed cells delivered by compact deltas; it does not mutate Timberborn terrain, saves, buildings, resources, UI, or simulator buffers. Passing live evidence requires the `qa-delta-stimulus` sequence above, `Player.log` proof of the subsequent non-zero dispatch and consumer pass, and a follow-up `qa-readiness` or `status` result showing `last_delta_consumer_debug_visual_cells` greater than `0`. The follow-up command may report `last_delta_count=0` if later simulator ticks have already consumed and settled the stimulus. `Player.log` should include `wildfire_timberborn_delta_consequence_sink_bound lane=debug_visual_state`, `wildfire_timberborn_dispatch_completed ... delta_count=<nonzero>`, and `wildfire_timberborn_delta_consumer_completed ... changed_cells=<nonzero> ... debug_visual_cells=<nonzero>`.

`TWF-037` tightens the same safe debug lane into the current inspection overlay. The overlay state remains adapter-local and rule-free: each entry is keyed by compact-delta cell index, stores the latest packed cell value, and derives visible inspection fields from `PackedCell` helpers instead of duplicating fire rules in Timberborn. Live QA should prove that updates are bounded to changed cells by capturing `Player.log` `wildfire_timberborn_delta_consumer_completed ... debug_visual_updated_cells=<nonzero> ... debug_visual_cells=<count>` after `qa-delta-stimulus`, then a `qa-readiness` or `status` result containing both `last_delta_consumer_debug_visual_updated_cells` and `last_delta_consumer_debug_visual_cells`. Screenshots are useful only if a later Timberborn UI panel or rendered overlay consumes this state; for this ticket, command counters plus the dispatch/consumer log pair are the required live evidence.

`TWF-039` binds the existing GPU visual-field buffer to a Timberborn-facing surface instead of creating one Timberborn entity per simulated cell. `WildfireConfigurator` exposes `ITimberbornGpuVisualFieldSurface` as a game singleton, and `TimberbornComputeFireSimulatorFactory` receives that same singleton before binding `VisualFields`, so future renderer/effect/debug-inspector systems can resolve the same live surface. The surface is adapter-local and visual-only: `TryGetBinding` exposes the bound `VisualFields` buffer handle with dimensions, cell count, 16-byte stride, and the channel order `fire,smoke,ash,visibility`; `TryGetComputeBuffer` gives Timberborn renderer/effect code the typed Unity buffer when the binding is live; `InspectCells` allows bounded readback of up to 256 explicit cell samples for renderer/effect development or debug inspection. Gameplay consequences continue to flow through compact C# deltas. Live QA should capture `Player.log` tokens for `wildfire_timberborn_gpu_visual_field_surface_bound ... channels=fire,smoke,ash,visibility` and `wildfire_timberborn_gpu_visual_field_surface_updated tick=<tick>`, plus a follow-up `qa-readiness` or `status` result showing `visual_field_surface_bound=true`, `visual_field_surface_cells=<map cell count>`, and `visual_field_surface_updated_tick` at or after the latest observed dispatch. Unit coverage proves the bounded inspection API with a fakeable reader seam and proves a factory/consumer reference can observe the same bind/update/unbind lifecycle because constructing Unity `ComputeBuffer` resources is only safe in the Unity/Timberborn runtime. A screenshot or visual artifact is still required before product acceptance once a material/effect renderer consumes the bound buffer; do not treat the status token alone as rendered-pixel proof.

`TWF-040` consumes that surface from the presentation lane. The pooled effect sink is bound as the Timberborn visual-effect sink, uses compact delta events only to choose candidate visual regions, reads at most the configured number of changed-cell visual samples per dispatch, and keeps active effect anchors capped by `MaxActiveEffects` instead of creating one Timberborn object per simulated cell. Visual presentation exceptions are caught and logged as `wildfire_timberborn_visual_effect_sink_failed` or `wildfire_timberborn_pooled_fire_effects_failed`; compact-delta gameplay, building, alert, and dispatch telemetry must continue after those presentation failures. The live presenter searches loaded Timberborn/Unity resources for native-looking fire or smoke prefabs such as `CampfireFire`, `Sparks_Trail`, `SmelterSmoke`, and `SteamEngineSmoke`. If no native prefab is resolved, visible effects are disabled honestly instead of creating invisible fallback anchors; QA should treat `pooled_fire_effects_visible_enabled=false` or `pooled_fire_effects_native_prefab_resolved=false` as a visible-effects blocker, not a screenshot pass. `status` and `qa-readiness` should include `active_pooled_fire_effects`, `updated_visual_regions`, `last_nonzero_updated_visual_regions`, `last_nonzero_updated_visual_regions_tick`, `max_pooled_fire_effects`, `max_updated_visual_regions`, `pooled_fire_effect_presentation_failures`, `pooled_fire_effects_visible_enabled`, `pooled_fire_effects_native_prefab_resolved`, and `pooled_fire_effects_native_prefab`. Passing live QA should capture a screenshot or visual artifact after `qa-delta-stimulus`, copy `Player.log`, and include log tokens for `wildfire_timberborn_delta_consequence_sink_bound lane=pooled_fire_smoke_ash_effects`, either `wildfire_timberborn_pooled_fire_effect_native_prefab_resolved ... prefab=<name>` or the explicit unavailable token, `wildfire_timberborn_pooled_fire_effects_updated ... active_pooled_effects=<count> ... last_nonzero_updated_visual_regions=<count>`, the TWF-039 surface bind/update tokens, and the final QA lock state. Screenshot approval remains a QA step because unit tests prove selection, routing, pooling limits, exception isolation, native-resolution telemetry, and counters, not rendered-pixel quality.

`TWF-066` live QA on 2026-05-03 proved the command-responsive and native-prefab portions of the fire-effect gate but did not accept the visual readability gate. Evidence from `~/repos/wildfire-TWF-066` branch `codex/TWF-066-visible-fire-effect` commit `199047d8b7ac854d102c708854506a1bc1b6e62e` lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-066-live-rapid-20260503T153723Z`, `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-066-recording-20260503T153735Z/2026-05-03T15-37-36-337Z-high`, and `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-066-recording-command-20260503T153750Z`. Rapid `status` polling after `qa-delta-stimulus` reported `active_pooled_fire_effects=1`, `pooled_fire_effects_visible_enabled=true`, `pooled_fire_effects_native_prefab_resolved=true`, `pooled_fire_effects_native_prefab=CampfireFire`, and `pooled_fire_effect_presentation_failures=0`, with matching `wildfire_timberborn_pooled_fire_effects_updated` tokens in `Player.log`. The captured normal-camera screenshots still showed only a tiny fire spark, not a legible fire effect, so this run is blocker evidence rather than accepted fire-effect tuning evidence.

`TWF-066` Fire-only readability follow-up live QA on 2026-05-03 also remains blocker evidence, not acceptance. Evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-066-live-readable-20260503T160350Z` proves deployment, command-responsive loaded-save startup, high-resolution recording, copied `Player.log`, and no presentation failures. `Player.log` reported `active_pooled_effects=1`, `visible_effects_enabled=true`, `native_effect_prefab_resolved=true`, `native_effect_prefab=CampfireFire`, and `presentation_failures=0` during active fire ticks `35`, `36`, and `73`, but extracted normal-camera frames `recording-second-frame-5s.png` and `recording-second-frame-6s.png` still show only a small orange flicker in the trees. Future TWF-066 acceptance must produce high-resolution recording and screenshots where the fire effect itself is plainly legible at normal gameplay camera angles, not merely active in counters or visible as alert text.

`TWF-036` binds the first building burnout consequence to the same compact-delta consumer pass. The Timberborn adapter considers only changed cells delivered by compact deltas, checks the live `IBlockService` for pausable buildings at each changed fire-grid coordinate, and calls `PausableBuilding.Pause()` only when a matched building cell reaches fuel depletion. This is intentionally bounded and reversible: it does not destroy buildings, mutate the fire grid, or write simulator state from Timberborn. The QA-only `qa-building-burnout-stimulus` bridge command has no user-supplied coordinates. It scans the current fire grid for the first unpaused pausable building cell, then queues exactly two ordered field changes through `IGpuFireSimulator.RegisterChange` for that one cell: `SetHeat=15` followed by `SetFuel=0`. If every scanned pausable building is already paused, the command reports no usable target instead of queueing a stimulus that cannot increment `building_burnout_applied_consequences`. This keeps the target discoverable without arbitrary coordinate mutation and lets the next dispatch emit a fuel-depleted compact delta on a known pausable building cell. Invoke it with `bun scripts/invoke-timberborn-command.ts qa-building-burnout-stimulus --wait=6`, then capture the command result message fields `target_index`, `target_x`, `target_y`, `target_z`, `scanned_cells`, `set_heat`, `set_fuel`, and `queued_field_changes=2`. Live QA should then capture `Player.log` `wildfire_timberborn_delta_consequence_sink_bound lane=building_burnout_pause`, `wildfire_timberborn_qa_building_burnout_stimulus_queued`, `wildfire_timberborn_changes_registered source=qa_building_burnout_heat`, `wildfire_timberborn_changes_registered source=qa_building_burnout_stimulus`, and `wildfire_timberborn_delta_consumer_completed ... building_burnout_considered_deltas=<nonzero> ... building_burnout_matched_cells=<nonzero> ... building_burnout_applied_consequences=<nonzero>`. The follow-up `qa-readiness` or `status` result must include `last_positive_building_burnout_applied_tick` and `last_positive_building_burnout_applied_count`, with the count above zero for pass evidence; the volatile `last_delta_consumer_building_burnout_*` fields may return to zero after later settled dispatches and should be used only to correlate the most recent dispatch when the command is read immediately.

`TWF-042` adds the first player-facing alert loop. The alert sink is still compact-delta driven: it consumes only `TimberbornFireAlertEvent` values derived from changed simulator cells, aggregates them per dispatch, and sends at most one native Timberborn quick warning with new-fire count, burned-out-cell count, and max heat. It does not add command UI, arbitrary coordinates, or core gameplay rules. Live QA should use an existing stimulus that causes alert deltas, such as `qa-delta-stimulus` for new fire cells or `qa-building-burnout-stimulus` for fuel depletion, then capture a screenshot showing the quick warning in the native Timberborn notification area. Capture `Player.log` tokens for `wildfire_timberborn_delta_consequence_sink_bound lane=player_fire_alert`, `wildfire_timberborn_delta_consumer_completed ... alerts=<nonzero>`, and `wildfire_timberborn_player_fire_alert_updated ... notification_sent=true ... fire_started=<count> ... fuel_spent=<count> ... max_heat=<heat>`. A follow-up `qa-readiness` or `status` result must include `last_delta_consumer_alerts=<nonzero>`, `last_player_fire_alert_tick`, `last_player_fire_alert_started_fires`, `last_player_fire_alert_fuel_spent`, `last_player_fire_alert_max_heat`, `player_fire_alert_notifications`, `player_fire_alert_notification_sent=true`, and `player_fire_alert_presentation_failures=0`. Screenshot approval remains a QA step because unit tests prove aggregation, notification routing, failure isolation, and status telemetry, not the rendered on-screen placement.

Current `TWF-021` coverage adds the live compute-backed attachment path. `TimberbornFireRuntimeInitializer` builds the initial `FireGrid` from `MapSize.TerrainSize`, converts terrain cells from `ITerrainService.GetAllHeightsInCell(...)` through `TimberbornTerrainAdapter`, and initializes the runtime through `ITimberbornFireSimulatorFactory`. The factory manually loads `ComputeShaders/wildfire_compute_mac`, creates a real Unity `ComputeShader` simulator, and leaves fire-spread behavior in `FireSim.compute`.

Current `TWF-023` coverage resolves the previous live blocker. The Wildfire batchmode builder already matched the official `~/repos/timberborn-modding/Assets/Tools/Editor/Scripts/ModBuilding/AssetBundleBuilder.cs` call shape: explicit `AssetBundleBuild[]`, `BuildAssetBundleOptions.None`, and `BuildTarget.StandaloneOSX`. The missing compatibility requirement was the Unity built-in package `com.unity.modules.assetbundle` in the minimal batchmode project's `Packages/manifest.json`. Without that package, Unity emitted `'AssetBundle' is not supported because the module AssetBundle is disabled in the build.` and Timberborn rejected the generated bundle. With the package present, the warning is gone and Timberborn loads the bundle.

The deploy script now builds and stages two private bundles: `wildfire_compute_mac` containing `Assets/WildfireGenerated/FireSim.compute`, and `wildfire_diagnostic_mac` containing `Assets/WildfireGenerated/Diagnostic.txt`. The Timberborn loader probes the diagnostic text bundle before loading the compute bundle, so live logs separate "all Wildfire bundles fail" from "ComputeShader bundles fail" without replacing or faking simulator behavior.

Live evidence from 2026-05-01 after `bun scripts/deploy-timberborn-mod.ts --apply --clean`, restarting Timberborn, and continuing the Wildfire save:

- `Player.log` showed `wildfire_timberborn_diagnostic_asset_loaded bundle=wildfire_diagnostic_mac asset=assets/wildfiregenerated/diagnostic.txt text_length=33`.
- `Player.log` showed `wildfire_timberborn_compute_asset_loaded bundle=wildfire_compute_mac asset=assets/wildfiregenerated/firesim.compute`.
- `Player.log` showed `wildfire_timberborn_gpu_simulator_created width=128 height=128 depth=23 cell_count=376832` and `wildfire_timberborn_runtime_simulator_initialized width=128 height=128 depth=23`.
- After unpausing, `Player.log` showed `wildfire_timberborn_gpu_dispatch_kernel_started kernel=SimulateFullGrid tick=20 groups=16x16x6`, `wildfire_timberborn_gpu_readback_completed tick=20 delta_count=0`, and `wildfire_timberborn_dispatch_completed tick=20 delta_count=0`.
- `bun scripts/invoke-timberborn-command.ts status --wait=6` returned `wildfire_command_result command=status success=true status=success simulator_integrated=true width=128 height=128 depth=23 tick_count=20 queued_changes=0 last_delta_count=0 message=ok`.
- For `TWF-029`, `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` returned `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=178 queued_changes=0 last_delta_count=0 message=loaded_game_ready` after the loaded save was unpaused.

Search `~/Library/Logs/Mechanistry/Timberborn/Player.log` for `wildfire_command_bridge_ready`, `wildfire_command_request`, `wildfire_command_result`, `wildfire_timberborn_adapter_started`, `wildfire_timberborn_runtime_ready`, `wildfire_timberborn_runtime_initialize_started`, `wildfire_timberborn_diagnostic_asset_loaded`, `wildfire_timberborn_compute_asset_loaded`, `wildfire_timberborn_gpu_factory_created`, `wildfire_timberborn_gpu_simulator_initialized`, `wildfire_timberborn_runtime_simulator_initialized`, `wildfire_timberborn_cadence_configured`, `wildfire_timberborn_gpu_queued_changes`, `wildfire_timberborn_gpu_dispatch_kernel_started`, `wildfire_timberborn_gpu_dispatch_kernel_completed`, `wildfire_timberborn_gpu_readback_counter`, `wildfire_timberborn_gpu_readback_completed`, `wildfire_timberborn_gpu_listeners_notified`, `wildfire_timberborn_dispatch_started`, `wildfire_timberborn_dispatch_completed`, `wildfire_timberborn_adapter_stopping`, and `wildfire_timberborn_adapter_stopped`.

Do not satisfy this stage by attaching a dispatch-only or C# no-op simulator. Live completion requires `wildfire_command_result ... simulator_integrated=true` with numeric dimensions, `tick_count`, `queued_changes`, and `last_delta_count`, plus Player.log evidence that the AssetBundle loaded and the real compute dispatch/readback path ran.

## Timberborn Startup Log Harness

Use the startup log harness when QA needs repeatable evidence that the deployed Wildfire mod loaded in Timberborn:

```bash
bun scripts/check-timberborn-startup.ts --attach --wait=30
```

Use `--launch` instead of `--attach` when Timberborn should be opened by bundle id:

```bash
bun scripts/check-timberborn-startup.ts --launch --wait=120
```

The harness serializes with deploy work through the shared QA lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, validates the documented `1920x1080` display resolution by default, captures a `Player.log` baseline before attach or launch work, activates `com.mechanistry.timberborn`, waits for required current-window `Player.log` tokens, and writes evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/<timestamp>/`.

Default required startup tokens are:

- `wildfire_command_bridge_ready`.
- `wildfire_timberborn_runtime_ready`.
- `wildfire_timberborn_diagnostic_asset_loaded`.
- `wildfire_timberborn_compute_asset_loaded`.
- `wildfire_timberborn_gpu_factory_created`.
- `wildfire_timberborn_runtime_simulator_initialized`.

Use `--require-command-status` only when a save is already loaded and the command bridge is expected to answer a read-only `status` request with `success=true` and `simulator_integrated=true`. Failure tokens after the `Player.log` baseline fail the run even when all success tokens are present. The startup harness does not click through the startup Mods dialog, load saves, unpause the simulation, or replace the live gameplay validation owned by later QA tickets. Screenshots are captured only on failure by default, or when explicitly requested with `--screenshot=always`.

## Timberborn Screen Recording QA

Use the screen recording utility when visual tuning or behavior tuning needs time-based evidence instead of still screenshots. The utility wraps macOS `screencapture -v`, stays outside Timberborn UI automation, and writes a timestamped evidence directory under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/screen-recordings/` by default.

High-resolution mode records the full selected display and is intended for fire, smoke, ash, steam, and alert readability:

```bash
bun scripts/record-timberborn-qa.ts --mode high --duration=10 --save-name <save-name> --command "qa-delta-stimulus then qa-readiness --require-nonzero-delta"
```

Low-resolution mode records a centered `1280x720` rectangle by default and is intended for faster comparison of spread, suppression, burnout, and pacing:

```bash
bun scripts/record-timberborn-qa.ts --mode low --duration=20 --scenario-name <scenario-name> --command "qa-water-suppression-stimulus then qa-readiness --require-water-changed"
```

Use dry-run mode before live capture to confirm the source, duration, output path, Timberborn PID, and exact `screencapture` command:

```bash
bun scripts/record-timberborn-qa.ts --dry-run --mode high --duration=6 --save-name <save-name>
bun scripts/record-timberborn-qa.ts --dry-run --mode low --duration=6 --scenario-name <scenario-name>
```

Each live capture writes:

- `recording.mov`: the macOS screen recording.
- `recording-metadata.json`: mode, duration, display or rectangle/window bounds, frame-rate note, save/scenario name, Timberborn PID, command sequence, output path, file size, command bridge inbox/outbox paths, copied Player.log paths, bounded log-tail path, and final QA lock-state path.
- `recording-plan.txt`: human-readable plan and replay command.
- `command-sequence.txt`: reviewer-facing command or action sequence supplied through repeated `--command` flags or `--commands-file`.
- `Player.log`: a best-effort copy of the current Timberborn log when available.
- `Player-run-window-tail.log`: a best-effort bounded tail of the current Timberborn log for quick review.
- `command-outbox.txt`: a best-effort copy of the current command bridge outbox when available.
- `final-qa-lock-state.txt`: the final state of the known Wildfire QA lock roots.

The companion files are intentionally best-effort because the recording tool does not own the command bridge sequence. QA must still attach the command output that created the visible state, and should replace or supplement `Player-run-window-tail.log` with a tighter bounded run-window excerpt when a long-running `Player.log` makes the tail ambiguous.

Pass `--rect x,y,w,h` for an explicit crop, `--source window` to resolve the current Timberborn window bounds, `--display <number>` for a non-default display, and `--include-cursor` or `--show-clicks` only when the pointer is relevant to the evidence. `--dry-run --source window` and `--no-activate --source window` resolve bounds through System Events without activating Timberborn; if bounds cannot be resolved without activation, dry-run reports `window_bounds_status=unresolved_without_activation` and live no-activate capture fails before recording. The tool can record default input audio with `--audio`, but release QA should prefer silent clips unless narration is intentionally part of the evidence.

Display metadata records the `system_profiler` display order and resolution for the selected display when available. macOS does not expose global multi-display origins through that source, so explicit `--rect` remains the reviewable source of truth for multi-display crops.

Live acceptance for recording-dependent tuning requires at least one short high-resolution clip and one short low-resolution clip, the command output that created the visible state, copied `Player.log` or bounded log excerpts for the same run window, and the final shared QA lock state. The recording tool does not run `qa-*` commands by itself; run the guarded command bridge utilities before or during capture and put the exact sequence in metadata.

`TWF-147` live QA on 2026-05-05 used evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-147-live-20260505T030230Z/` against the loaded 50x50 Diorama save. The attach utility confirmed the save was loaded and already unpaused, with tick count advancing from `184` to `186`. The high-resolution clip `recording-high-fuel/2026-05-05T03-03-57-990Z-high/recording.mov` and extracted frame `twf-147-recording-frame-10.5s.png` capture the live GPU renderer run after `qa-fire-preset slow-reactable` and `qa-burn-duration-stimulus high`.

The accepted proof point is the `Player.log` token `wildfire_timberborn_gpu_field_renderer_updated tick=260 visible_regions=1 updated_regions=1 ... material_failures=0` plus follow-up `qa-readiness --require-nonzero-delta` reporting `last_delta_count=4`, `visual_field_surface_bound=true`, `visual_field_surface_cells=57500`, `gpu_field_renderer_enabled=true`, `gpu_field_renderer_material_ready=true`, `gpu_field_renderer_surface_bound=true`, `gpu_field_renderer_last_nonzero_updated_regions=1`, and `gpu_field_renderer_last_nonzero_updated_regions_tick=260`. Treat this as renderer-pipeline acceptance, not final visual readability tuning; fire, smoke, ash, steam, and behavior tuning remain separate recording-dependent tickets.

## Release Settings Framework

Wildfire release settings are owned by `src/Wildfire.Timberborn/WildfireReleaseSettings.cs`. The settings owner is Timberborn-facing and adapter-local: it reads player preferences from Timberborn settings, never from save data, and does not introduce Timberborn dependencies into `Wildfire.Core`.

The stable key shape is:

| Key                                                       | Type  | Default | Invalid Value Behavior                                      |
| --------------------------------------------------------- | ----- | ------- | ----------------------------------------------------------- |
| `JasonKleinberg.Wildfire.release.settings_schema_version` | `int` | `1`     | Fall back to `1` and log `wildfire_release_setting_invalid` |
| `JasonKleinberg.Wildfire.release.wildfire_enabled`        | `int` | `1`     | Fall back to disabled (`0`) and log `wildfire_release_setting_invalid` |

The key prefix for later child settings is `JasonKleinberg.Wildfire.release.`. Missing keys use defaults without warning. Present but malformed or unsupported values fall back to conservative defaults and surface through `wildfire_release_settings ... invalid_values=<n>` plus one warning token per invalid key.

The current native API decision is deliberately narrow. Installed Timberborn exposes `Timberborn.SettingsSystem.ISettings` with safe typed getters, so the framework reads through that first-party backend. Declared integer keys, including the schema key and `JasonKleinberg.Wildfire.release.wildfire_enabled`, use `GetSafeInt` so Timberborn `PlayerPrefs` integer storage is authoritative. The richer community-style `ModSettingsOwner` / `ModSetting<T>` UI types were not present in the installed Timberborn managed assemblies or local mod DLLs during `TWF-096`; child tickets that need an in-game settings UI should either add that dependency explicitly or bind a native Timberborn UI surface without duplicating settings storage.

Deterministic coverage lives in `tests/Wildfire.Core.Tests/WildfireReleaseSettingsTests.cs` and must cover missing defaults, malformed values, unsupported schema values, and log severity for invalid values. Live Timberborn QA is not required for this framework ticket; future child settings can add live proof for their user-facing controls.

## Timberborn QA Utilities

Use the local [Timberborn QA Utility skill](../.codex/skills/timberborn-qa-utility/SKILL.md) when building Bun/TypeScript scripts or guarded `cliclick`-style automation for live Timberborn QA.

UI automation must take coordinate targets from [timberborn-menu-coordinate-guide.md](timberborn-menu-coordinate-guide.md), verify the target app and expected screen before acting, and fail loudly rather than clicking through an unknown Timberborn state.

Use the latest-save startup utility when live QA needs to get from closed Timberborn, startup dialogs, or the standalone main menu into the latest loaded save and start simulation dispatch:

```bash
bun scripts/load-latest-save-and-unpause.ts --launch --wait=240
```

The default `--launch` route is the guarded signal-driven cold-start path. It launches by bundle id, retries macOS activation while Timberborn finishes connecting to AppleEvents, samples screenshot frames while Timberborn loads, presses `Enter` only after the startup Mods or Experimental Mode gates are positively identified, retries those gates while they remain visible, clicks only the documented `main.continue` coordinate, then waits for top-HUD loaded-save classification before unpause/status proof. The frame evidence is saved as PNG samples plus `fast-frame-samples.csv` because true video recording from Bun is brittle across macOS screen-recording permissions and display-capture failures.

Use `--attach` when Timberborn is already running:

```bash
bun scripts/load-latest-save-and-unpause.ts --attach --wait=120
```

The `--attach` route stays classifier-driven for already-running sessions. If `--launch` is used while Timberborn is already running, the utility also uses the classifier path instead of sending fast startup inputs into an unknown live state.

The utility serializes with deploy/startup work through the shared QA lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, validates the documented `1920x1080` display resolution by default, activates `com.mechanistry.timberborn`, captures screenshots for each identified transition under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/<timestamp>/`, and preserves classifier screenshots as fallback/debug aids.

The narrow allowed path is startup confirmation by `Enter`, main-menu `Continue`, loaded-save HUD, and `hud.speed1` to unpause or set normal speed. The classifier attach path may click documented startup Mods `OK` or Experimental Mode Information `Start!` only after positive screen identification. The utility does not navigate arbitrary menus, select saves, delete saves, save the game, open debug panels, exit Timberborn, or invoke destructive actions.

Use screenshot classification mode to debug captured UI evidence without acquiring the shared QA lock, launching Timberborn, or clicking:

```bash
bun scripts/load-latest-save-and-unpause.ts --classify-screenshot ~/Library/Application\ Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/<timestamp>/<screen>.png
```

The classifier reports the visible Timberborn screen separately from blocking overlays, for example `screen=startup-mods blocking_overlay=mac-system-alert`. Live automation fails before clicking any Timberborn coordinate when a real macOS system alert overlay is detected. Clear system alerts manually before retrying the live startup path.

By default, the utility requests read-only `status` after unpause and requires `wildfire_command_result command=status success=true status=success simulator_integrated=true` plus a numeric `tick_count` greater than `0`. Use `--skip-post-status` only when validating UI flow without a deployed Wildfire command bridge. After a passing live run, inspect the copied `Player.log` in the artifact directory for `wildfire_timberborn_dispatch_completed`.

## Timberborn Deploy Pipeline

Use the deploy script to build and stage the Timberborn adapter without making Timberborn own simulation rules:

```bash
bun scripts/deploy-timberborn-mod.ts
```

The default mode is a dry-run. It acquires the shared build/deploy lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, runs `dotnet build Wildfire.slnx --configuration Debug`, and prints `wildfire-deploy` lines for the mod id, version, target directory, manifest, and each planned assembly copy.

Expected deployed folder shape:

```text
~/Documents/Timberborn/Mods/Wildfire/
  manifest.json
  Scripts/
    Wildfire.Timberborn.dll
    Wildfire.Core.dll
    Wildfire.Timberborn.pdb
    Wildfire.Core.pdb
  ComputeShaders/
    wildfire_compute_mac
    wildfire_compute_mac.manifest
    wildfire_diagnostic_mac
    wildfire_diagnostic_mac.manifest
```

`Scripts/` contains the managed assemblies, following the official Timberborn mod builder's code-output convention. `ComputeShaders/` contains the Unity-built compute shader bundle generated from `src/Wildfire.Unity/FireSim.compute` and a text-only diagnostic bundle generated by `DiagnosticTextAssetBundleBuilder`; both are intentionally outside Timberborn's built-in `AssetBundles/` auto-load folder so the adapter can load exact private paths after startup. The script only stages known build artifacts from `src/Wildfire.Timberborn/bin/<Configuration>/netstandard2.1/` and known bundles from the Unity batchmode project; it does not copy `docs/`, `kanban/`, `.git/`, or other internal repository content into the deployed mod.

The deploy script must validate generated bundle manifests before copying them. A valid compute bundle manifest includes `Assets/WildfireGenerated/FireSim.compute`; a valid diagnostic bundle manifest includes `Assets/WildfireGenerated/Diagnostic.txt`. If either manifest describes another mod's assets or a stale wrong bundle, treat the deploy as invalid and rebuild before running live QA.

Run the real deploy only when Timberborn is closed or QA explicitly approves writing while the game is open:

```bash
bun scripts/deploy-timberborn-mod.ts --apply
```

Optional command flags:

- `--configuration=Release` builds and stages Release artifacts.
- `--skip-build` reuses existing build output.
- `--skip-asset-bundle` reuses existing `wildfire_compute_mac` and `wildfire_diagnostic_mac` bundles instead of running Unity batchmode.
- `--unity-executable=/path/to/Unity` selects a Unity Editor when `WILDFIRE_UNITY_EXECUTABLE` is not set.
- `--mods-dir=/path/to/Mods` targets a non-default Timberborn Mods directory.
- `--dry-run --remove` prints the cleanup action for the deployed Wildfire folder without deleting it.

Cleanup is intentionally manual for live QA safety. Close Timberborn first, then inspect the dry-run remove output. Only remove `~/Documents/Timberborn/Mods/Wildfire` when QA no longer needs the deployed mod evidence.

Player.log proof remains a live-QA step after a real deploy. Capture `~/Library/Logs/Mechanistry/Timberborn/Player.log` evidence that Timberborn discovered the Wildfire folder or loaded `Wildfire.Timberborn.dll`; if Timberborn cannot load the assembly yet, record the exact loader error and keep the fix in `Wildfire.Timberborn` or the deploy script rather than moving fire rules into `Wildfire.Core`.
