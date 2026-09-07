# Wildfire Design

Wildfire models fire, heat, smoke, steam, and ash on a discrete grid and connects their effects to Timberborn. The reusable simulation owns fire rules; the game adapter supplies world observations and applies gameplay consequences.

This document describes the implemented data model and durable design choices. [Architecture](ARCHITECTURE.md) maps them to execution paths. Exact equations and tuning live in [FireSim.compute](../src/Wildfire.Unity/FireSim.compute) and [FireSimParameters](../src/Wildfire.Core/FireSimParameters.cs); copied pseudocode must not become a competing specification.

## First release target

The accepted completion target is a polished macOS release with Folktails and Ironteeth firefighting, prevention, natural ignition, recovery, and reliable saves. Fans, extensive overgrowth, Emberpelt response, and Windows support are later expansions. This target describes work in development, not implemented behavior.

The player prepares a settlement, recognizes an incident, directs a response, and recovers afterward. Folktails trade mobilized labor and interrupted production for flexible response; Ironteeth trade equipment and prepared water capacity for greater response per worker. Both need reachable working positions, finite water supply, understandable coverage and failure reasons, and safe retreat. Detailed staffing, containment commands, water accounting, and equipment behavior must be proven through a complete responder prototype before their contracts are fixed.

Completion requires evidence from the packaged mod in the target game version:

- Both factions can prepare for, detect, contain, and recover from a fire through normal player controls. Prepared and unprepared settlements produce understandable differences.
- Natural ignition is explainable, conservatively bounded at settlement scale, and paced against measured detection, mobilization, travel, and suppression times.
- Responders obtain and spend water consistently, handle unreachable or extinguished targets, retreat from danger, and resume normal work.
- Save/reload during response and aftermath preserves accepted durable state without duplicating water, goods, ash, or consequences. Disable/re-enable behavior is recoverable and documented.
- Visuals and alerts explain fire, smoke, danger, response, and aftermath at ordinary play scales. Performance is measured on representative populated maps, including quiet and aftermath states.
- A clean macOS installation of the release package passes the gameplay and persistence checks; player documentation, settings, diagnostics, attribution, and Workshop metadata match the verified artifact.

Publishing the public Workshop item is a separate approval from preparing and verifying the release.

## Simulation model

The simulator uses full-grid compute dispatch with double-buffered cell and transport state. It uses deterministic hash inputs keyed by cell, tick, and seed for stochastic decisions. Reproducibility claims require the same fixture, parameters, shader, and execution environment; deterministic C# fixture tests alone do not prove GPU results.

A grid cell is a compact state record, not a game entity. One Timberborn entity can occupy several cells. The adapter maps those cells back to their owner when applying damage or other consequences.

The CLI previews seeded initial conditions and exports fixtures. It does not execute a second C# fire-spread simulation. Full fluid dynamics and one game object per visual cell are outside the design. Active-frontier dispatch is a possible optimization, not the current algorithm.

## Packed cell format

[PackedCell](../src/Wildfire.Core/PackedCell.cs) defines the 16-bit payload, stored in the lower half of a GPU `uint`.

| Bits | Field | Range | Meaning |
| --- | --- | --- | --- |
| 0–3 | Fuel | 0–15 | Remaining burnable material |
| 4–7 | Heat | 0–15 | Thermal energy band |
| 8–9 | Flammability | 0–3 | Ignition susceptibility |
| 10–11 | Water | 0–3 | Wetness or suppression band |
| 12 | Terrain | 0–1 | Solid material occupancy |
| 13–15 | BurningLevel | 0–7 | Stored burning intensity |

`BurningLevel` is stored explicitly. The old `heatLoss` packed field no longer exists. Cooling is governed by simulation logic and parameters. Packing masks values to field width; callers requiring saturation must clamp before packing.

## Transport and material fields

[WildfireTransportFieldState](../src/Wildfire.Core/WildfireTransportFieldState.cs) carries dynamic transport separately from the cell:

| Bits | Field | Range |
| --- | --- | --- |
| 0–2 | Steam | 0–7 |
| 3–5 | Smoke | 0–7 |
| 6–8 | Smoke contamination | 0–7 |
| 9–10 | Ash | 0–3 |
| 12–14 | Ash contamination | 0–7 |
| 15 | Source this tick | Boolean |

Bit 11 is unused. Unpacking clears smoke or ash contamination when the corresponding amount is zero. Steam has no contamination lane.

[WildfireMaterialFieldState](../src/Wildfire.Core/WildfireMaterialFieldState.cs) contains material class, burn capacity, burn history, ash metadata, contamination behavior, and soil contamination. A separate target id associates imported material with a host consequence target. The [versioned material schema](../src/Wildfire.Core/MaterialFieldSchema.v1.json) and resource catalog supply import defaults. Unknown material uses the explicit unknown profile; it does not acquire invented fuel or damage capacity.

Some shader bindings retain `AtmosphericFields` and `CompanionFields` names for transport and material data. Material ash metadata is not a second authoritative collectible-ash inventory.

## Host interaction

[FireSimContracts](../src/Wildfire.Core/FireSimContracts.cs) defines queued `FireSimChange` inputs and `CellDelta` outputs. Changes can replace a cell or modify selected cell and transport fields. The shared Core coordinator applies queued inputs, simulates, reads results, swaps buffers, and notifies listeners. Changes registered by listeners wait for a subsequent tick.

`AddWater` adds wetness bands (0..3), saturating the cell at 3; null and zero are no-ops. Within one command, `SetCell` runs first, additions next, and explicit field overrides last: `SetWater` wins over `AddWater`. Separate commands run in registration order, so multiple responders can add wetness without overwriting one another. Ambient observations may still use `SetWater`. This input neither creates native water volume nor accounts for carried goods; Timberborn owns inventory consumption and conversion to bands. The normal shader rules determine cooling, evaporation, and fire response.

The GPU output reserves capacity for both external-change and simulation records. A cell can appear more than once in a tick. A `CellDelta` contains an index, old/new packed cell values, and the originating material target and local-slot ids. These distinguish separate material parts even when they belong to the same native entity. Owned consequences must resolve that retained identity rather than whichever entity currently occupies the cell. It is not a complete transport snapshot. Consumers needing ash, smoke, or steam read the corresponding simulation fields. Renderers may smooth or aggregate these fields for presentation without authoring gameplay state.

## Gameplay ownership

Timberborn owns world import, entity identities, inventory operations, construction and vegetation consequences, beaver exposure, alerts, settings, and save integration. Those services consume simulation results and queue further inputs. Damage to a multi-cell structure must be reconciled at the entity level rather than multiplied by footprint size.

Ash quantity and contamination belong to simulator transport. The adapter maintains a derived read model for collection, application, status, and fertility. Uncontaminated ash can become the `FertileAsh` good; contaminated ash is hazardous. Steam is clean and transient. Fire and suppression do not cleanse badwater or soil contamination. See [ash decisions](ash-simulation-model.md) and [steam decisions](steam-simulation-model.md) for the detailed rationale and intended behavior.

The repository contains consequence implementations and live QA tools, but their presence does not establish game-version compatibility, visual quality, or release readiness. Use the [validation runbook](TEST_PLAN.md) for the relevant proof.

## Design records and future work

[GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues) owns active work and acceptance criteria. Field-model and faction-response proposals remain proposals until implementation and evidence establish their behavior. The [historical design](history/2026-09-04/DESIGN.md) preserves previous equations, phased plans, and gameplay rationale; it is not current execution guidance.
