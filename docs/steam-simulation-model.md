# Steam Simulation Model

This note records the current steam design direction after the May 2026 ash authority review.

Only settled agreements belong in this note. Open visual tuning questions should stay in `TWF-070` until they are resolved by implementation and QA.

## Core Claim

Steam is a clean, transient simulator-owned transport field produced when heat meets water or wetness.

Steam should work like smoke at the ownership boundary: the simulator creates, moves, decays, and exposes the field; Timberborn adapters and renderers read it. Timberborn must not infer steam from water deltas or create a second steam source of truth.

Steam is not toxic and does not carry contamination. Badwater or contaminated water may suppress fire as water-like input, but Wildfire does not model toxic steam or contaminated steam.

## Current State

The current implementation already stores steam in `AtmosphericFields` alongside smoke, smoke contamination, ash, and ash contamination.

`FireSim.compute` creates steam from wet hot cells through `SteamSourceFromMoistureAndHeat`. The cloud renderer and smoothing pass already consume the packed steam lane for presentation.

The remaining mismatch is mostly behavioral and documentation-level:

- Steam has no explicit source-of-truth note parallel to ash.
- Some older docs and tickets still describe toxic or contaminated steam.
- Steam transport should be reviewed against the smoke transport model so visible steam reads as a field, not as per-cell water-change effects.

## Desired Model

Steam is created by:

- heated water or wetness in simulator cell state;
- queued Timberborn suppression inputs that set or increase the packed water band, then let the simulator create steam if heat remains.

Steam moves and decays through simulator transport state.

Steam presentation should read the simulator field directly. Renderers can use interpolation, smoothing, puffs, opacity, height, and lifetime for presentation only; they should not author steam state.

Steam affects release behavior conservatively:

- visual feedback for water suppression;
- mild respiratory or visibility telemetry if supported by real field samples;
- no injury by default;
- no toxic or contaminated steam classification.

## Implementation Defaults

- Keep steam in the existing packed transport field.
- Do not add `SteamContamination`, `ToxicSteam`, or contaminated-steam state.
- Keep water and wetness in the packed cell or adapter input surfaces; steam itself is the transient transport output.
- Make steam transport intentionally field-based and smoke-like, with its own decay and visual tuning constants where needed.
- Use compact deltas only to wake, bound, or inspect visual regions. Do not map one water-changed cell to one long-lived steam effect.
- Let `TWF-070` own final visual tuning after the simulator-owned source and transport behavior are clear.

## Acceptance Criteria

The next implementation should be considered correct only when:

- steam is produced from simulator-owned wet hot state;
- steam transport and decay are covered by deterministic tests or shader harness fixtures;
- visual presentation reads simulator steam state rather than water-delta shortcuts;
- beaver and QA telemetry distinguish clean steam from smoke, fire, ash, and wet suppression;
- no docs, tickets, counters, or renderer contracts require toxic or contaminated steam.
