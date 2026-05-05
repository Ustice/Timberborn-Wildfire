# World Consequence First Pass

This document captures the first implementation-facing pass for fire consequences beyond the fire field itself. `docs/DESIGN.md` remains the durable design source of truth; this page is the working bridge from those decisions into tickets that a junior developer or QA agent can execute.

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
