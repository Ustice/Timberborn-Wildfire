# Ash Simulation Model

Ash amount and contamination belong to simulator transport state. Timberborn gameplay and rendering consume that state and queue changes back to the simulator.

## Decisions

- Ash amount uses 0–3 units per cell. Contamination uses 0–7 and travels with ash.
- Uncontaminated ash is fertile for gameplay. `FertileAsh` is the collected Timberborn good, not a second simulated ash species; one harvested unit corresponds to one good.
- Rendered airborne or settled ash is a presentation of the field. A renderer does not own deposition or an independent ash ledger.
- External collection, application, washing, and decay requests use the queued change path. They do not write native buffers directly.
- Fire and washing do not cleanse contamination. Water-taint effects belong at the Timberborn API boundary and require evidence for that native operation.

## Implementation anchors

[WildfireTransportFieldState](../src/Wildfire.Core/WildfireTransportFieldState.cs) defines the packed ash lanes. [FireSim.compute](../src/Wildfire.Unity/FireSim.compute) implements ash production and transport. [TimberbornAshFieldService](../src/Wildfire.Timberborn/Ash/TimberbornAshFieldService.cs) maintains the derived read model used by gameplay consumers.

The shader still has material ash metadata and historical `Atmospheric`/`Companion` names. Those names do not change the authority boundary. `CellDelta` alone cannot represent transport-only changes, so adapter synchronization must observe transport state explicitly.

The [May design discussion preserved in the September archive](history/2026-09-04/ash-simulation-model.md) includes earlier mismatches, naming alternatives, and intended follow-ups. Consult source and current issue evidence before treating any of those follow-ups as implemented. [DESIGN.md](DESIGN.md) owns the current field layout.
