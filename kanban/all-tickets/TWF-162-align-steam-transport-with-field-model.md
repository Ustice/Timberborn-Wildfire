---
ticket: TWF-162
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
  - TWF-157
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/steam-simulation-model.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-162-align-steam-transport-with-field-model.md
---

# TWF-162: Align Steam Transport With Field Model

## Goal

Make steam follow the same simulator-owned field authority as smoke, fire, and the new ash direction without adding toxic or contaminated steam.

## Why

Steam already lives in the packed transport field and is rendered by the GPU visual path, but older planning language still allowed water-delta shortcuts and toxic-steam classifications. Steam should be clean suppression vapor produced by simulator wet-hot state, transported and decayed by the simulator, and read by Timberborn/renderers.

## Requirements

- Keep steam in the existing packed transport field.
- Do not add `SteamContamination`, toxic steam, or contaminated steam.
- Keep badwater and contaminated-water suppression semantics separate from steam. They may suppress fire as water-like input, but they must not create a contaminated steam field.
- Review `FireSim.compute` steam transport against smoke transport and add smoke-like field movement only where it improves clean steam readability.
- Preserve steam's distinct behavior: shorter lived, lighter, near-ground-to-rising visual presentation, and distinct from smoke.
- Keep Timberborn adapters from inferring steam from water-delta shortcuts when simulator steam state is available.
- Update deterministic tests or shader harness fixtures for steam creation, transport, decay, and visual-field readback.
- Keep public QA/status counters on clean steam naming, such as `steam_cells`.

## Implementation Notes

- Start from `src/Wildfire.Unity/FireSim.compute`, especially `SteamSourceFromMoistureAndHeat` and `BuildAtmosphericField`.
- Check `src/Wildfire.Unity/WildfireSmoothing.compute` and `src/Wildfire.Unity/WildfireCloud.shader` for presentation readback before changing Timberborn adapter code.
- Keep this scoped to clean steam transport and telemetry. Do not fold final visual tuning from `TWF-070` into this ticket.

## Dependencies

- `TWF-157` keeps ash and transport authority aligned, so this ticket can follow the same buffer ownership language.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run the Unity shader harness if `FireSim.compute` behavior changes.

## Notes

- Source-of-truth note: [docs/steam-simulation-model.md](../../docs/steam-simulation-model.md).
- `TWF-070` owns final visual tuning after this behavior contract is in place.
