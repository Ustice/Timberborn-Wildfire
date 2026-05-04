---
ticket: TWF-078
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-076
  - TWF-077
  - TWF-084
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-078-add-persistent-ash-field-service.md
---

# TWF-078: Add Persistent Ash Field Service

## Goal

Add one Timberborn-side ash field service that stores gameplay ash, fertile ash, spent ash, and contaminated or tainted ash separately from the temporary GPU visual ash channel.

## Why

The design separates visual ash from gameplay ash. The GPU visual field can show temporary residual heat, but plant growth, ash quality, decay, persistence, and future collection need a dedicated Timberborn-side service. Fertile ash and contaminated ash are positive and negative qualities of the same field, so they should stay in one ticket.

## Requirements

- Keep `PackedCell` and the GPU visual ash channel unchanged.
- Add an ash field service that records cells, strength, decay, and quality.
- Represent ash quality as `none`, `fertile`, `spent`, or `tainted`.
- Create fertile ash from accepted non-contaminated burn aftermath sources.
- Create tainted ash from accepted contaminated burn aftermath sources.
- Apply a bounded plant growth-speed bonus for plants that opt into ash fertility.
- Prevent tainted or contaminated ash from granting a growth bonus.
- Persist ash fields across save/load.
- Expose bounded QA/status telemetry for ash cells, quality counts, growth-bonus applications, and decay.
- Add deterministic tests for ash creation, quality classification, growth-speed application, decay, and persistence serialization where possible.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-076` proves crop burn aftermath.
- `TWF-077` proves structure burn aftermath.
- `TWF-084` proves tree burn aftermath.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Keep gameplay ash separate from the temporary GPU visual ash channel.
- Store ash by simulation cell with strength, quality, creation source, decay state, and persistence version.
- `fertile` ash should only come from accepted non-contaminated burn aftermath. `tainted` ash should come from contaminated sources or contaminated affected cells through `TWF-079`.
- Plant-growth bonuses should be bounded and opt-in by plant category; tainted ash must never grant the bonus.
- Expected counters include ash cells by quality, new ash cells, decayed cells, growth bonus applications, tainted bonus skips, persistence saves, and persistence loads.
- Safe no-op cases must include missing plant growth APIs and unresolved contamination data.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for fertile ash and tainted ash creation, or explicit safe unavailable states, plus status/log proof of field state.

## Notes

- This ticket does not implement beaver collection or manual field application of ash.
- Relevant design references: `docs/DESIGN.md` section 20, "Ash And Fertility" and `docs/ARCHITECTURE.md` "Ash Field Service".
