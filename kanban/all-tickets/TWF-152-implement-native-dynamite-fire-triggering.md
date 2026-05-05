---
ticket: TWF-152
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-130
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-152-implement-native-dynamite-fire-triggering.md
---

# TWF-152: Implement Native Dynamite Fire Triggering

## Goal

Make placed `Dynamite`, `DoubleDynamite`, and `TripleDynamite` respond to sustained fire exposure through a safe native wrapper and bounded Wildfire heat pulse.

## Why

`TWF-130` found real Timberborn `Dynamite` APIs: `Trigger()`, `TriggerDelayed(int)`, `Disarm()`, `Detonate()`, depth-specific blueprints, and native terrain/object behavior. Wildfire should use a wrapped native path only when settings and QA gates prove it is recoverable, while still mirroring danger into the fire field.

## Requirements

- Resolve placed dynamite infrastructure targets from compact fire deltas without arbitrary coordinate mutation.
- Distinguish depth `1`, `2`, and `3` dynamite targets from `DynamiteSpec`.
- Track sustained arming threshold ticks before triggering.
- Add settings for explosive infrastructure enabled, native dynamite triggering enabled, armed threshold ticks, pulse heat, and pulse radius.
- Default native triggering disabled until live proof accepts it.
- When native triggering is disabled, enqueue only the bounded Wildfire heat pulse and report skipped native triggering.
- When enabled, call a wrapped native `TriggerDelayed(...)` or `Trigger()` API only once per resolved target.
- Suppress duplicate cells for the same dynamite target in one dispatch.
- Expose bounded telemetry for considered, armed, triggered, native-triggered, heat-pulse cells, skipped setting disabled, skipped no-safe-api, duplicate suppressed, and depth.
- Add deterministic tests for settings gates, threshold progression, duplicate suppression, bounded pulse cells, and safe unavailable wrappers.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Implementation Notes

- Start with a deterministic target resolver and wrapper interface around native `Dynamite` calls.
- Keep native triggering behind settings and expose skipped telemetry by default.
- Use the simulator external-change lane for bounded heat pulse output; do not mutate packed cells directly.
- Treat `Detonate()` as unsafe for this ticket unless a later review explicitly accepts it.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one placed dynamite target with native triggering disabled and, after explicit acceptance, one native-triggered target with recoverability evidence.

## Notes

- Do not call `Detonate()` directly from generic fire deltas.
- Do not mutate terrain outside the named native dynamite wrapper.
- Keep detonators and tunnels out of this ticket.
