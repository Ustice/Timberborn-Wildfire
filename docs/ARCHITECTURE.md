# Wildfire Architecture

Wildfire separates simulation authority from host integration. There is one authoritative GPU simulation path.

## Ownership

- `Wildfire.Core` owns packed cells, grid helpers, deterministic fixture hashing, change records, delta records, and GPU simulator contracts.
- `Wildfire.Cli` owns seeded scenario preview and fixture input inspection.
- `Wildfire.Unity` owns compute buffers, HLSL rule translation, shader dispatch, compact delta readback, and GPU visual fields.
- `Wildfire.Timberborn` owns terrain/building/resource/water adapters, event registration, overlay updates, pooled effects, alerts, burn damage, ash fields, beaver exposure, contamination interaction, persistence, and gameplay consequences.

## Boundary Rules

- Hosts call `RegisterChange`; they do not mutate cells directly.
- Listeners may register changes, but those changes apply on the next tick.
- `Wildfire.Core` does not reference Unity or Timberborn.
- GPU visuals may be driven by simulation buffers, but gameplay changes flow through C# deltas.
- Fire spread rules live in compute shaders, not in a second C# execution path.
- Fire does not reduce contamination. Contamination-aware effects are Timberborn-side consequences of simulator fields and deltas.
- Persistent gameplay ash is separate from the temporary GPU visual ash channel.
- Fire and smoke visuals are field-based presentation, not one effect object per tile.

## Timberborn Compatibility Boundary

`Wildfire.Timberborn` owns release-facing compatibility probes for Timberborn APIs, Unity capabilities, and adapter-owned asset paths. The probe layer is evidence-only: it checks service/member availability, compute support, private compute/diagnostic bundles, native effect prefab availability, and quick-notification access, then logs whether each release-facing feature is compatible, degraded, or failed. It does not own fire rules, mutate the grid, or install a fake simulator fallback.

Required terrain and compute failures block the real compute-backed runtime path and surface as `compatibility_probe_status=failed`; the runtime logs `wildfire_timberborn_runtime_initialization_blocked`, rejects initialization, and `qa-readiness` reports `loaded_game_ready=false` even if other status fields are present. Optional feature failures, including building-burnout consequence APIs, native visual-effect prefabs, quick notifications, and diagnostic bundles, surface as `compatibility_probe_status=degraded` with `compatibility_probe_degraded_features=<tokens>`, so QA can distinguish a playable-but-degraded adapter from a release blocker. Reflection belongs behind this probe boundary when Timberborn has no stable public surface for a version-sensitive member; gameplay code should consume public services directly after the probe evidence is logged.

## Data Flow

```text
Host map/events
  -> FireSimChange queue
  -> GPU fire sim dispatch
  -> packed cells, visual fields, and CellDelta list
  -> host listeners
  -> overlays, effects, damage, ash fields, beaver exposure, alerts
```

## First Implementation State

The repository currently has a core scaffold with:

- `PackedCell`
- `FireGrid`
- `FireRandom`
- `IGpuFireSimulator`
- `GpuFireStepResult`
- `CellDelta`
- `FireSimChange`
- `IFireSimListener`

Unity has compute-facing scaffolds, and Timberborn has adapter-facing names so compute and host integration can grow without moving the core boundary.

## Unity Compute Buffer Scaffold

`Wildfire.Unity` owns `ComputeBufferGrid`, the first GPU-side allocation boundary. The grid records width, height, depth, and checked cell count, then allocates named buffers for current cells, next cells, queued changes, delta output, generation state, and visual fields.

Initial fixture-style `ushort` packed cells are uploaded to current and next cell buffers as `uint` values. The packed cell payload remains in the lower 16 bits, leaving the upper bits available for future GPU-side bookkeeping without changing the core `PackedCell` contract.

The plain solution build does not reference UnityEngine APIs yet. Buffer allocation therefore flows through `IComputeBufferAllocator` and `IComputeBufferHandle`, so Unity can later provide real compute-buffer handles while tests use deterministic recording handles. Fire-spread rules are still owned by future compute shaders, not by the C# scaffold.

## Timberborn Cell Mapping Scaffold

`Wildfire.Timberborn` owns the first adapter-side mapping boundary through `TimberbornFireCellMapper` and narrow terrain, building, resource, and water source adapters. The mapper accepts Timberborn-facing observations, groups them by `FireGrid` index, and produces either initial packed cells or sorted `FireSimChange` records with `SetCell` populated.

The mapping is deterministic and intentionally rule-free:

- Empty cells pack as no terrain, no fuel, no water, and maximum heat loss.
- Terrain contributes solid material with no fuel, no flammability, and high heat loss.
- Resource adapters expose deterministic stockpile and vegetation fuel bands before packing.
- Building adapters expose deterministic wood-like and non-burnable material bands.
- Buildings have material priority over resources and terrain for the same cell; non-burnable buildings still occupy terrain cells but pack no fuel or flammability.
- Water and terrain wetness contribute only to the packed water field, clamp to the water field width, and do not overwrite material.
- Multi-cell and vertical occupancy expands through `TimberbornCellFootprint`, emitting one source per covered `x`, `y`, and `z` cell before the mapper sorts by `FireGrid` index.
- Inputs are clamped to the packed-cell field widths before packing.

Timberborn systems should use this layer to translate game state, then register changes through `IGpuFireSimulator.RegisterChange`. The Timberborn project still does not own fire-spread rules or mutate Unity/GPU buffers directly.

## Shared Material Field Schema

`Wildfire.Core` owns the versioned material field contract used by both live Timberborn import and offline `.timber` snapshot export. The current v1 schema lives in code as `WildfireMaterialFieldSchema` and as the shared fixture `src/Wildfire.Core/MaterialFieldSchema.v1.json`.

The schema classifies observed map inputs into material classes before they become packed simulation cells or companion fields:

- Empty.
- Terrain.
- Vegetation.
- Crop.
- Tree.
- Building.
- Storage.
- Infrastructure.
- Water.
- Badwater.
- Unknown.

Each profile defines the packed fuel, flammability, heat loss, terrain bit, water band, burn capacity, consequence target kind, ash quality, contamination behavior, and resource policy. Packed bands remain small and deterministic; resource-specific detail still flows through adapter catalogs such as `TimberbornResourceFuelCatalog`.

Unknown materials fail closed. They do not invent fuel, burn capacity, consequence targets, or clean aftermath. This is deliberate: importer gaps should become explicit telemetry and ticket blockers instead of another layer of fake fuel.

## Timberborn Consequence Services

Timberborn consequence services consume compact deltas and visual or exposure samples after a simulator tick. They may register follow-up `FireSimChange` records, but those changes apply on the next tick.

### Field Visual Presentation Service

The field visual presentation service owns fire, smoke, steam, and temporary visual ash rendering from the GPU visual field. It should use compact deltas to bound the regions that need updates, then sample neighboring field intensity to produce larger coherent effects.

Responsibilities:

- Cluster, blur, threshold, or otherwise aggregate adjacent fire and smoke intensity into convincing regions.
- Maintain bounded pooled anchors, meshes, particles, or material-driven volumes rather than one effect object per cell.
- Scale visual presentation by sampled intensity, spread, field shape, and region size.
- Keep visual presentation failures isolated from gameplay consequences.
- Keep persistent gameplay ash in the ash field service, not in the temporary visual ash channel.

### Burn Damage Service

The burn damage service owns persisted burn state for live Timberborn entities. Static burn descriptors can be attached to spec-backed objects such as buildings, harvestables, and cuttables, but damage is per instance.

The foundation boundary is:

- Static `TimberbornBurnDamageDescriptor` records describe target kind, material kind, yielded resources, and construction-resource investment for a spec-backed Timberborn object.
- `TimberbornBurnDamageService.RegisterTargets` binds stable target identities to owned simulation cells after the adapter resolves a Timberborn entity footprint. Multi-cell and vertical targets are rolled up to one target key.
- The service resolves changed simulation cells from `TimberbornFireCellDeltaDecision` back to a single owner, suppresses duplicate hits from the same target footprint during one dispatch, and applies bounded per-instance damage.
- Damage capacity is derived from known resource yield or construction investment through `TimberbornResourceFuelCatalog`. Unknown resource ids are recorded as missing and contribute no capacity so downstream consequences fail closed.
- State snapshots capture target key, spec id, capacity, resource-accounting fields (`FuelValue`, `Flammability`, and `AccountedResourceIds`), damage taken, last damaged tick, owned cell indices, and unresolved resources.
- Telemetry is emitted through `wildfire_timberborn_burn_damage_targets_registered` and `wildfire_timberborn_burn_damage_applied`, while the existing delta-consumer summary exposes optional burn-damage sink counts without making Timberborn own fire-spread rules.

Responsibilities:

- Resolve changed simulation cells back to owning buildings, plants, crops, trees, stockpiles, or resources.
- Roll up multi-cell and vertical footprints into one damage target without duplicate damage.
- Track damage capacity from resource yield or construction-resource investment.
- Apply resource-specific fuel and flammability tuning.
- Leave crop death, tree death, structure rollback, inventory destruction, explosive behavior, ash, fertility, beaver effects, and player feedback to downstream consumers of this state.
- Persist burn damage state across save/load once downstream tickets bind real Timberborn entity persistence.

### Stored Goods Burn Service

The stored goods burn service owns inventory-content loss caused by fire. It is separate from the burn damage service because storage contents are goods, not construction state.

Responsibilities:

- Resolve stockpiles, warehouses, tanks, and other storage entities from changed simulation cells.
- Use the shared resource fuel and flammability catalog for burnable goods.
- Destroy stored items only through safe Timberborn inventory APIs.
- Keep inventory loss separate from structural damage to the storage building.
- Route explosives or volatile goods through a bounded hazardous-good behavior before broader blast mechanics are considered.
- Expose counters for item loss by resource, skipped non-burnable goods, hazardous goods, and unsafe API paths.

### Ash Field Service

The ash field service owns persistent gameplay ash. It does not replace the GPU visual-field ash channel, which remains derived visual output.

Responsibilities:

- Record ash field cells, strength, decay, and quality.
- Represent ash quality as `none`, `fertile`, `spent`, or `tainted`.
- Apply fertile ash growth-speed bonuses to plants that opt into ash fertility.
- Prevent contaminated soil or contaminated burn sources from producing a fertility bonus unless a future decontamination mechanic explicitly allows it.
- Preserve ash fields across save/load.
- Expose future collection hooks so beavers can gather fertile ash and place it in fields.

### Beaver Exposure Service

The beaver exposure service translates fire, heat, smoke, steam, toxic steam, ash, and contamination-adjacent fields into beaver-facing effects. It should be built as an evidence ladder: exposure telemetry first, then debuffs, then incapacitation, then death.

Respiratory progression:

- Coughing: slowdown and work inefficiency.
- Choking: sleep-like or incapacitated state if Timberborn exposes a safe API.
- Death: sustained severe exposure after avoidance and status behavior are proven.

Burn progression:

- Singed: injury-style debuff.
- Burned: contamination-like severe injury that prevents work until healed.
- Death: sustained direct heat or flame exposure after safer states are proven.

The service should cancel or interrupt unsafe work, increase path costs or avoidance where Timberborn supports it, and aggregate danger telemetry for player feedback. It should reuse native injury, sleep, contamination, treatment, and death APIs only after live tests prove the paths safe.

### Active Suppression Services

Future faction fire-response services should translate staffed buildings, beaver actions, and player-built defenses into simulation inputs. They must not own fire rules or mutate the simulation grid directly.

Responsibilities:

- Convert Fire Warden sprayer output, bucket dumps, tail-stamping, fans, or berms into explicit water, suppression, airflow, or spread-resistance changes.
- Register those changes for the next simulator tick through the existing change path.
- Keep faction-specific labor, cost, equipment, and injury behavior in Timberborn adapter services.
- Reuse beaver exposure and player feedback services for injury risk, alerts, and telemetry.
- Keep future suppression visuals field-based where they overlap fire, smoke, steam, or ash fields.

### Contamination Interaction

Contamination stays a Timberborn-owned environmental and status concern. The simulator can consume contaminated fuel through ordinary packed fuel, water, heat, and flammability inputs, but it does not cleanse contamination or store contamination state in `PackedCell`.

Timberborn consequence services should apply these rules:

- Contaminated burnable material can burn and can produce toxic smoke, toxic steam, or tainted ash.
- Contaminated water or badwater can suppress fire but does not become clean water.
- Contaminated soil remains contaminated after fire.
- Ash on contaminated soil is tainted rather than fertile.
- Contaminated beaver exposure can combine respiratory or burn progressions with native badwater contamination if the API path is proven safe.

### Player Feedback Aggregation

Feedback systems should aggregate world consequences instead of emitting one alert per affected cell or entity. The alert and status lane should separately report active fire, building damage, plant or resource loss, beaver danger, and ash aftermath when those states become gameplay-relevant.
