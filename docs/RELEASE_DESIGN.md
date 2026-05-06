# Wildfire Release Design

This document describes the release-facing Wildfire system on this branch compared with `main`. It intentionally leaves out ticket workflow, QA command scaffolding, snapshot harnesses, generated fixtures, and other development-only surfaces.

## Release Scope

The release build turns the previous proof-oriented fire simulation into a Timberborn-integrated wildfire loop:

- Timberborn world cells are imported from terrain, soil moisture, natural resources, buildings, storage, infrastructure, water, and badwater.
- A GPU fire simulation tracks fuel, heat, flammability, water, terrain, heat loss, atmospheric fields, and companion material metadata.
- Timberborn global wind affects heat spread, smoke drift, and visible particle drift.
- Fire, smoke, toxic smoke, and steam render as pooled procedural particle effects.
- Fire deltas can drive player alerts, debug-only field overlays, beaver exposure telemetry, structure damage, stored-good burning, explosive infrastructure behavior, tunnel behavior, and infrastructure damage.
- Release settings provide stable feature gates and fail-closed defaults for risky effects.

## Field Model

The simulation still uses a packed cell grid for the core fire state:

| Field | Range | Purpose |
| --- | ---: | --- |
| fuel | 0-15 | Remaining burnable material in the cell |
| heat | 0-15 | Current fire heat |
| flammability | 0-3 | Material ignition assistance |
| water | 0-3 | Moisture or water suppression |
| terrain | 0-1 | Whether the cell can participate as burnable terrain |
| heat loss | 0-7 | Cooling resistance |

The companion field stores material identity and consequence metadata:

| Field | Purpose |
| --- | --- |
| material class | Empty, terrain, vegetation, crop, tree, building, storage, infrastructure, water, badwater, or unknown |
| burn capacity | Maximum material damage capacity |
| burn history | Accumulated burn state |
| ash strength | Future aftermath intensity |
| ash quality | none, fertile, spent, or tainted |
| contamination behavior | none, taint if source contaminated, tainted source, suppresses without cleaning, or fail closed |

The atmospheric field stores lingering effects separately from the core cell state:

| Field | Range | Purpose |
| --- | ---: | --- |
| steam | 0-7 | Moisture heated into white smoke-like steam |
| smoke | 0-7 | Lingering smoke intensity |
| smoke contamination | 0-7 | Toxic fraction for smoke |
| ash | 0-7 | Lingering ash intensity |
| ash contamination | 0-7 | Toxic fraction for ash |
| source | 0-1 | Whether this tick generated a local atmospheric source |

## Material Profiles

The default material schema maps Timberborn surfaces into simulation-ready profiles:

| Material | Fuel | Flammability | Heat loss | Water | Burn capacity | Consequence target | Ash |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| terrain | 0 | 0 | 6 | 0 | 0 | none | none |
| vegetation | 10 | 3 | 1 | 0 | 10 | tree | fertile |
| crop | 4 | 2 | 2 | 0 | 4 | crop | fertile |
| tree | 12 | 2 | 1 | 0 | 12 | tree | fertile |
| building | 15 | 1 | 3 | 0 | 15 | structure | fertile |
| storage | 8 | 2 | 3 | 0 | 8 | storage | fertile |
| infrastructure | 0 | 0 | 5 | 0 | 0 | infrastructure | fertile |
| water | 0 | 0 | 7 | 3 | 0 | water | none |
| badwater | 0 | 0 | 7 | 3 | 0 | water | tainted |
| unknown | 0 | 0 | 7 | 0 | 0 | none | none |

Crops, trees, buildings, storage, and infrastructure use the resource catalog where available. Unknown or unsafe resource paths fail closed instead of inventing burnable state.

## Fire Simulation

Heat spreads through a Euclidean neighborhood up to two cells away. Each neighbor contributes:

```text
distanceWeight = 1 / max(1, euclideanDistance)
neighborHeat = windWeightedNeighborHeat(neighbor.heat, directionToCurrentCell)
```

The next heat before burning is:

```text
heat = round((oldHeat + sum(neighborHeat * distanceWeight)) / (1 + sum(distanceWeight)))
heat = min(15, heat)
```

Wind weighting uses Timberborn global wind:

```text
effectiveWindStrength = saturate(windStrength * 0.5)
```

When the neighbor is upwind of the target, heat is boosted by up to the effective wind strength. When it is downwind, heat is reduced by the same factor. Crosswind neighbors use their raw heat.

Water acts as anti-fuel before ignition:

```text
lockedFuel = min(fuel, water * FireWaterFuelLock)
effectiveFuel = fuel - lockedFuel
canBurn = terrain == 1 and effectiveFuel > 0
```

Water also evaporates when the cell is hot enough:

```text
if water > 0 and heat > FireWaterEvaporationHeat:
    water -= 1
```

Ignition remains stochastic:

```text
ignitionThreshold = FireIgnitionBaseHeat - flammability
burnPressure = heat
burnChance = min(15, burnPressure)
burnsThisTick = canBurn and heat >= ignitionThreshold and random4bit < burnChance
```

When a cell burns, fuel burn-down is a second stochastic roll:

```text
fuelBurnDownChance = min(15, ceil(burnChance * FireFuelBurnDownPressureNumerator / FireFuelBurnDownPressureDenominator))
fuel -= random4bit < fuelBurnDownChance ? 1 : 0
```

Burning also regenerates heat from the remaining effective fuel:

```text
fuelHeat = ceil(effectiveFuel * FireFuelHeatWeight / 15)
heat = min(15, heat + flammability + fuelHeat)
```

Cooling happens after burning:

```text
cooling = heatLoss / FireHeatLossCoolingDivisor
heat = max(0, heat - cooling)
```

The currently tuned live preset uses these nonzero levers:

| Lever | Value |
| --- | ---: |
| FireIgnitionBaseHeat | 5 |
| FireWaterFuelLock | 5 |
| FireWaterEvaporationHeat | 4 |
| FireFuelHeatWeight | 6 |
| FireHeatLossCoolingDivisor | 16 |
| FireFuelBurnDownPressureNumerator | 2 |
| FireFuelBurnDownPressureDenominator | 1 |
| FireFuelBurnDownRollSeed | 0x9E3779B9 |

## Atmospheric Simulation

Atmospheric state is simulated as a field, not as an on/off visual flag. It is rebuilt from the previous atmospheric field, the new packed cell state, companion material metadata, vertical transfer, and wind-aware neighbor transfer.

Steam comes from heated moisture:

```text
steamSource = water > 0 and heat > 0 ? clamp(ceil(heat * 7 / 15), 1, 7) : 0
```

Smoke comes from hot fuel, even before full ignition:

```text
smokeSource = terrain == 1 and fuel > 0 and heat > 0 ? min(5, 1 + heat / 3) : 0
```

Ash comes from fuel drop and heavy smoke dissipation:

```text
fuelDrop = max(0, oldFuel - newFuel)
ashSource = min(7, fuelDrop + (oldSmoke >= 4 ? 1 : 0))
```

Atmospheric decay and transfer:

| Field | Local decay | Vertical transfer | Horizontal transfer |
| --- | ---: | --- | --- |
| steam | 2 per tick | rises from below with decay 1 | none |
| smoke | 1 per tick | rises from below with decay 1 | spreads from neighbors with wind penalty |
| ash | 0 per tick | none | none |

Wind changes smoke transfer by changing the neighbor penalty:

| Wind alignment | Penalty |
| --- | ---: |
| with wind and strong enough | 0 |
| against wind and strong enough | 2 |
| neutral or weak wind | 1 |

Toxic smoke and toxic ash are represented as contamination concentration on smoke and ash, not as fully separate transport fields. Tainted source materials and fail-closed contamination behavior emit maximum contamination.

## Visual Design

The release visual path uses pooled procedural particle systems driven by the visual and atmospheric fields. Effects are keyed by cell and effect kind, which lets fire, steam, smoke, and toxic smoke coexist without replacing each other.

Particle systems update emission for existing fire slots instead of resetting particle transforms. When a field drops below visibility, fire, smoke, steam, and toxic smoke stop emitting and existing particles finish their animation before release.

The visual field exposes fire, smoke, ash, and visibility. Steam and contamination are read from the atmospheric buffer.

### Fire

Fire particles are circular billboards emitted from the bottom footprint of the cell. Their emission rate and size scale with fire intensity.

| Property | Value |
| --- | --- |
| emission rate | 6-32 |
| lifetime | 0.55-1.05 seconds |
| size | 0.08-0.48 |
| upward velocity | 1.85-5.35 |
| lateral drift | wind plus per-cell jitter, then per-particle spread |
| color | yellow/orange to red, fading out |

### Smoke

Smoke is light gray, more opaque than the earlier draft, rises slowly, drifts with wind, and persists long enough to feel like a field rather than a one-tick event.

| Property | Value |
| --- | --- |
| emission rate | 1-4.5 |
| lifetime | 4.8 seconds |
| size | 0.45-1.55 |
| upward velocity | 0.35-1.35 |
| color | warm gray fading out |

### Toxic Smoke

Toxic smoke is smoke multiplied by smoke contamination. It uses the same smoke-like motion, but with a dark ruby color ramp.

| Property | Value |
| --- | --- |
| emission rate | 0.75-3.5 |
| lifetime | 5.4 seconds |
| size | 0.42-1.38 |
| upward velocity | 0.3-1.2 |
| color | dark ruby fading out |

### Steam

Steam is generated by heated moisture, starts at the floor of the cell, and looks like white smoke.

| Property | Value |
| --- | --- |
| emission rate | 3-13.5 |
| lifetime | 4.8 seconds |
| size | 0.45-1.55 |
| upward velocity | 0.35-1.35 |
| color | white-gray fading out |

Ash visual particles are currently disabled to preserve particle budget, though the ash fields remain available for simulation, contamination, and future consequences.

The broad GPU field renderer is debug-only by default. Its terrain-hugging overlay remains disabled in release presentation.

## Timberborn Integration

Runtime initialization waits for Timberborn entities before importing the world. If entity data does not arrive after a fallback window, terrain-only import can initialize the system rather than blocking the save forever.

The live importer combines:

- terrain and soil moisture
- natural resources and crops
- buildings
- stockpiles and stored goods
- infrastructure
- water and badwater

The runtime skips auto-dispatch on oversized maps, binds the visual field surface when a compute simulator is available, and exposes counters for readiness and live diagnostics.

Timberborn wind is read through `WindService`, normalized, and passed to both the compute shader and particle presentation.

## Player Alerts

The player alert sink aggregates started fires and burned-out cells per dispatch. It sends a Timberborn warning notification with the count and max heat.

The latest fire-start cell is retained as a focus target. A temporary click area over the notification can focus the camera on that cell through Timberborn camera services.

## Gameplay Consequences

Delta consumers fan out cell changes into release-facing consequences:

| Surface | Release behavior |
| --- | --- |
| structures | fire damage accumulates through a burn damage state, with rollback/repair-aware presentation where safe APIs exist |
| stored goods | burnable stockpile contents can be destroyed based on resource classification |
| explosive infrastructure | heated explosive targets can arm, trigger, and enqueue heat pulses |
| detonators | fire safety behavior can disable or protect detonator-related targets |
| tunnels | tunnel fire behavior is tracked, while terrain destruction is gated off by default |
| paths | burnable path infrastructure can take damage |
| power infrastructure | burnable power targets can take damage and be disabled or disconnected |
| water infrastructure | inert or difficult-to-burn water targets resist damage; burnable material can still be damaged |
| beavers | respiratory, burn, toxic, and aftermath exposure counters are produced for future behavior integration |

Consequences deduplicate multi-cell structures by target before applying damage, favoring hotter or more damaged deltas first.

## Release Settings

Release settings are stored under the `JasonKleinberg.Wildfire.release.` prefix. Missing settings use safe defaults; invalid settings default to a fail-closed value where the feature could be risky.

Nonzero defaults:

| Setting | Default |
| --- | --- |
| settings schema version | 1 |
| wildfire enabled | true |
| explosive infrastructure enabled | true |
| explosive infrastructure armed threshold ticks | 2 |
| explosive infrastructure pulse heat | 15 |
| explosive infrastructure pulse radius | 1 |
| detonator fire safety enabled | true |
| tunnel fire behavior enabled | true |

Feature gates with false defaults are intentionally omitted here because they are not active release behavior.

## Safety And Failure Modes

The release path favors degradation over hard failure:

- Compatibility probes classify required and optional features before live readiness is reported.
- Asset bundle or compute shader failure degrades runtime features instead of mutating Timberborn state unsafely.
- Unknown material/resource classifications fail closed.
- Debug overlays are disabled unless explicitly enabled.
- Release log noise policy suppresses empty consequence summaries but keeps positive or failure summaries visible.
- Oversized maps skip auto-dispatch rather than locking the game.

## Verification Evidence

The branch includes deterministic tests, shader snapshots, and live QA utilities, but those are scaffolding and are not part of this release design. The current live validation after the latest visual tuning showed:

| Counter | Value |
| --- | ---: |
| started burning | 25 |
| active pooled fire effects | 343 |
| visual effect failures | 0 |
| active preset | high-threshold-high-bonus |
| fuel burn-down | 2/1 |

