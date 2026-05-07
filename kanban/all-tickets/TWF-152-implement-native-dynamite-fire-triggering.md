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
- 2026-05-05 worker: implemented deterministic explosive-infrastructure consequence path. Compact fire deltas now resolve placed native `Dynamite` targets through an adapter API, read target depth, suppress duplicate cells by stable target id, track sustained exposure against release settings, enqueue bounded heat pulses as `FireSimChange` values, and expose `explosive_infrastructure_*` summary plus QA status fields. Native `Dynamite.TriggerDelayed(...)` is wrapped but remains disabled by default through `native_dynamite_trigger_enabled`; disabled native triggering currently produces the pulse-only path plus skipped-native telemetry.
- 2026-05-05 worker learning: referencing Timberborn native `Dynamite` requires both `Timberborn.Explosions.dll` and `Timberborn.TickSystem.dll`; `Dynamite` derives through Timberborn's tickable component stack.
- 2026-05-06 review failed: the native `TriggerDelayed(...)` wrapper can throw through the dispatch path instead of becoming bounded safe-unavailable/failure telemetry, and review requested focused coverage that native-disabled pulse-only behavior never calls the native trigger. Moved back to `03-in-progress`; require fresh review before live QA.
- 2026-05-06 worker safety fix: contained explosive target resolver and native `TriggerDelayed(...)` wrapper failures as `skipped_no_safe_api` telemetry with warning tokens, kept heat-pulse output bounded, and added regression coverage proving native-disabled pulse-only behavior does not call the native trigger wrapper.
- 2026-05-06 live QA failed after deploy/load/readiness succeeded under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-152-153-154-live-20260506T143435Z/`. The loaded save had placed `Dynamite.IronTeeth` / `TripleDynamite.IronTeeth` and `native_dynamite_trigger_enabled=false`, but the available conservative infrastructure stimulus selected `Path(Clone)` instead of a dynamite target. Moved back to `03-in-progress`; add a safe targetable dynamite QA surface or stable fixture before rerunning the required pulse-only/skipped-native gate.
- 2026-05-06 QA-targeting unblock worker: added `qa-delta-stimulus dynamite`, which scans placed infrastructure through the existing explosive target API and queues only conservative Wildfire heat/fuel changes at the resolved target cell. Command output reports `direct_target_kind=dynamite`, `direct_target_stable_id`, and scanned cells; native dynamite triggering remains governed by `native_dynamite_trigger_enabled=false` by default and is not called during command queueing.
- 2026-05-06 live QA failed under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-152-dynamite-direct-qa-20260506T145932Z/`. Direct targeting selected real `direct_target_kind=dynamite` with `direct_target_stable_id=dynamite:1252950368` and queued bounded changes, but repeated matched exposures logged `matched_target_cells=1` with `armed_targets=0`, `triggered_targets=0`, `heat_pulse_cells=0`, and `skipped_no_safe_api=0`. Moved back to `03-in-progress`; fix sustained arming/trigger telemetry so the native-disabled conservative path emits pulse-only/skipped-native evidence.
- 2026-05-06 worker sustained-arming fix: keyed sustained arming by resolved explosive exposure identity (`kind`, fire cell, depth) instead of the native adapter stable id, which may be object-hash volatile across live Timberborn resolution calls; same-dispatch duplicate suppression still reports the target stable-id path, and arming counts each exposure identity at most once per dispatch. Added regression coverage for repeated matched exposure with changing native ids proving threshold `2` reaches the native-disabled pulse-only path with `skipped_no_safe_api=1` and zero native trigger calls.
- 2026-05-06 live QA passed under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-152-live-rerun-20260506T151926Z/`. `qa-delta-stimulus dynamite` selected real `direct_target_kind=dynamite` with `direct_target_stable_id=dynamite:-737879604`, native triggering stayed disabled, and the accepted `Player.log` gate showed `armed_targets=1`, `triggered_targets=1`, `native_triggered_targets=0`, `heat_pulse_cells=7`, `skipped_no_safe_api=1`, and `last_triggered_depth=1`. Move to `05-integration`.
- 2026-05-06 integration review passed with no blocking findings. Acceptance is for the conservative default-disabled native dynamite gate only: bounded Wildfire heat pulse plus safe-unavailable/skipped-native telemetry with `native_dynamite_trigger_enabled=false`. Native triggering itself remains disabled and unaccepted for release behavior. Current checkout verification passed `git diff --check`, `dotnet test` with `363` passed, `dotnet build Wildfire.slnx`, and `bun run kanban:audit -- --strict`. Move to `06-done` without bundling unrelated dirty-checkout work into this ticket's acceptance.
