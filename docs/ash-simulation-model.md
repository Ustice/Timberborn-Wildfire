# Ash Simulation Model

This note records the current ash design direction after the May 2026 review. It supersedes older wording that treats visual ash and gameplay ash as separate authoritative systems.

Only settled agreements belong in this note. Open design disagreements should stay out until they are resolved.

## Core Claim

Ash should be one simulated field. "Airborne" and "settled" are rendering or movement states, not separate ash systems.

The simulator should own ash creation, movement, contamination, washing, decay, and externally requested ash mutations. Renderers and Timberborn adapters read or request changes; they do not create independent ash truth.

Ash is intended to become a good, resource, and hazard. The storage and naming model should be strong enough to serve that final role without depending on a later conceptual rewrite.

## Current Lifecycle

Ash lifecycle is simulator-owned end to end:

1. Ash is created in the simulator transport field by burned material, smoke fallout, or a queued external ash mutation.
2. Ash amount and contamination travel together in the transport field.
3. Renderers read the transport field and present the current ash level; they do not project or store ash.
4. Timberborn gameplay adapters read a derived snapshot of the simulator field.
5. Beaver harvest, fertile-ash application, decay, and washout queue bounded simulator ash mutations.
6. The next simulator dispatch applies those mutations and emits the updated field.
7. Persistence, status, QA, and save/reload evidence report simulator ash state, not an adapter-owned ash store.

`TimberbornAshFieldService` is only a Timberborn-side read model over simulator ash readback. Its entries are derived from `AtmosphericFields`/`TransportFields`, and code must not seed them from burn source events or treat them as an independent ash ledger.

## Current Mismatch

The current code has several ash-like authorities:

- `AtmosphericFields` carries packed steam, smoke, smoke contamination, ash, and ash contamination.
- `CompanionFields` carries material metadata plus packed ash strength and ash quality bits.
- `UpdateCompanionAsh` projects atmospheric ash down to landing surfaces and mutates companion ash bits.
- Older `TimberbornAshFieldService` wording and tests treated service entries as persistent gameplay ash storage.
- The ash overlay can read atmospheric ash for immediate ground projection.

This is confusing because names and responsibilities do not match the desired model. It also makes it easy for visuals, gameplay, and persistence to disagree.

## Desired Model

Ash is simulation state. Visual ash is just presentation of that state.

Ash is created by:

- burning plants, crops, trees, and other ash-producing material;
- smoke dissipating into ash through deterministic stochastic production;
- explicit queued external effects that represent Timberborn-world actions.

Ash contamination comes from the source of the ash:

- ash from a burning plant uses the relevant ground or source contamination at creation time;
- ash from smoke fallout inherits contamination from that smoke;
- contamination travels with ash, not as a separate independent fluid.

Fertile ash is not a separate simulated ash kind. In simulator state, fertile ash means ash with no contamination. `FertileAsh` is the Timberborn good produced from harvesting uncontaminated ash.

Ash amount is resource amount. The agreed ash amount scale is 0-3, matching the four visual ash levels. Beavers can harvest 0-3 units from a cell. `1 FertileAsh` good equals 1 uncontaminated ash unit harvested from the field.

Ash contamination uses a 0-7 scale. Contamination strength affects hazard strength, soil poisoning, beaver exposure, visual tint, and cloud mixing.

Tainted ash is not permanent by default. It should fade over time, and water contact can wash it away. Washed tainted ash should slightly taint the affected water through a bounded adapter mutation when a safe Timberborn API exists; otherwise the washout should still be visible in ash state and report safe-unavailable water-taint telemetry.

Timberborn can affect ash through adapter services, but those services must queue bounded ash changes for the next simulator cycle. They must not directly mutate simulator buffers or maintain a competing ash state.

Queued updates should be applied in this order:

1. External mutations.
2. Simulation update.
3. Delta emission or readback.

## Naming Direction

The current names hide intent:

- `AtmosphericFields` sounds like an air-only visual buffer, but it currently carries smoke, steam, ash, and contamination transport state.
- `CompanionFields` sounds vague, but it mostly stores per-cell material/source metadata and some dynamic ash bits.

Recommended renames for the next implementation pass:

- Rename `AtmosphericFields` conceptually to `TransportFields`.
- Rename `CompanionFields` conceptually to `MaterialFields`.
- Rename renderer-facing ash terms from `visual ash` to `ash presentation`, `ash overlay`, or `ash render input`.

`TransportFields` is the best current candidate because it describes what the buffer does without claiming every value is airborne. It can carry smoke, steam, ash, and their contamination through current/next double-buffered simulation.

`MaterialFields` is the best current candidate for the companion buffer because it names the mostly static import facts: material class, burn capacity, source ash quality, contamination behavior, soil contamination, and target identity. If any dynamic ash bits remain there, they should be explicitly transitional or derived.

`PackedCells` is also a possible rename target because it represents current and next fire-simulation cell state held in a compact memory layout, not only a static packed value.

## Settled Implementation Decisions

Ash should remain in the packed `TransportFields` data. A dedicated `AshFields` buffer is not part of the agreed design.

The current transport `source` flag marks whether the cell produced steam, smoke, or ash during the tick. It can remain useful for presentation, such as showing smoke rising from the fire or vegetation source.

The simulator should emit ash deltas. Deltas make CPU-side persistence, collection, status, and adapter updates easier to manage without making CPU state authoritative.

Source attribution is not required: when ash changes, its contamination changes with it.

When ash mixes, contamination uses maxing rather than dilution for the first implementation. This is deterministic, hazard-conservative, and easier to reason about than weighted contamination blending.

When water removes ash, ash amount decreases and presentation should recede from the cell. Fertile ash washout should not create water contamination. Tainted ash washout may move a small bounded contamination amount into water, but it must not cleanse soil or affected entities.

## Visual Transition Model

Ash presentation should read simulator state and render transitions from that state.

- If the previous update had no ash in a cell with a floor and the current update has ash, render ash drifting down like snow and landing.
- If the previous update had ash and the current update still has ash, render it as settled.
- If ash level decreases through collection, decay, or washout, render ash receding rather than popping off abruptly.
- Render tainted ash with a contamination-tinted presentation distinct from uncontaminated ash.
- Renderers should use interpolation, lerp, and smoothstep for presentation only. They should not invent ash, project ash, or author simulation state.

The falling presentation is not stored as simulation state. It is inferred from an ash delta or previous/current presentation inputs. The next simulator state has the ash on the ground.

Ash falls one cell per tick. Wind can affect falling ash through deterministic stochastic movement.

## Implementation Defaults

Prefer the smallest memory-respecting architecture that removes the split authority:

- Keep ash in the existing packed transport buffer unless a dedicated `AshFields` buffer is justified.
- Retire `CompanionFields.AshStrength` as dynamic ash state once transport ash becomes authoritative.
- Use burn-created vegetation ash as the burn residue, visually and mechanically.
- Use deterministic stochastic production when smoke decays into ash. Smoke presence alone must not create ash.
- Process same-tick ash changes in order. External removal happens before simulation addition, so sim-produced ash can reappear after a removal in the same tick.
- Remove projection from both `UpdateCompanionAsh` and the ash overlay.
- Drive ash visuals from simulated ash values with lerp and smoothstep, just as smoke and fire visuals do.
- Add a queued ash-change service for Timberborn adapters, parallel to existing queued fire-cell changes.
- Do not reintroduce `ApplySources`-style adapter seeding. Burn consequences may report ash-source telemetry, but simulator transport state decides whether ash exists.

## Non-Goals

- Do not store ash in `PackedCell`.
- Do not create one Unity or Timberborn entity per ash cell.
- Do not let Timberborn services directly mutate simulator buffers.
- Do not treat renderer projection as simulation.
- Do not keep `TimberbornAshFieldService` as a competing source of truth once simulator ash is authoritative.

## Acceptance Criteria

The next implementation should be considered correct only when:

- ash creation from burning vegetation and smoke fallout is represented in simulator-owned state;
- contamination follows the ash source rules above;
- renderer output reads ash state without inventing or projecting ash;
- Timberborn ash-affecting actions queue simulator changes;
- persistence and status report simulator ash, not a separate ash field;
- uncontaminated harvested ash maps to `FertileAsh` goods at 1 good per 1 ash unit;
- deterministic tests cover burn-created ash, smoke-created ash, contaminated ash, queued ash mutation, visual read behavior, and save/load state.
