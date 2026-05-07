---
ticket: TWF-154
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
   - kanban/all-tickets/TWF-154-gate-tunnel-fire-destruction.md
---

# TWF-154: Gate Tunnel Fire Destruction

## Goal

Handle fire-damaged `Tunnel` infrastructure conservatively and keep native `Tunnel.Explode()` terrain mutation behind an explicit safety gate.

## Why

`TWF-130` found native tunnel explosion behavior and a `TunnelSupportTemplateName`, but terrain mutation is too risky to fold into ordinary burn damage. Fire can mark tunnels unstable first; actual terrain-affecting destruction needs live proof, setting gates, and save/reload evidence.

## Requirements

- Resolve placed tunnel targets from compact fire deltas.
- Treat tunnels as terrain-affecting infrastructure, not ordinary burnable structures.
- Add settings for tunnel fire behavior and tunnel terrain destruction.
- Default terrain destruction disabled.
- When disabled, mark tunnel instability or damage through safe telemetry/presentation only.
- When enabled, call a wrapped native `Tunnel.Explode()` path only after live proof accepts it.
- Preserve recoverability through rebuild, repair, or explicit safe no-op behavior.
- Suppress duplicate cells for the same tunnel target in one dispatch.
- Expose telemetry for considered, unstable, native-explode attempted, native-explode applied, destruction deferred, skipped setting disabled, skipped no-safe-api, and duplicate suppressed.
- Add deterministic tests for settings gates, duplicate suppression, deferred destruction, safe unavailable APIs, and no generic terrain mutation.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Implementation Notes

- Start with instability state and deferred-destruction telemetry before any native terrain mutation.
- Wrap native `Tunnel.Explode()` separately from the generic burn-damage path.
- Keep terrain destruction disabled by default and require explicit live proof before integration.
- Verify save/reload and rebuild or repair recovery before treating native destruction as acceptable.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one tunnel being marked unstable or explicitly deferred; native terrain destruction requires separate accepted evidence before integration.

## Notes

- Do not terrain-mutate through generic fire deltas.
- Do not call `Tunnel.Explode()` unless the setting is enabled and QA has accepted the wrapper.
- Keep placed dynamite and detonators in their own tickets.
- 2026-05-05 worker: implemented conservative tunnel fire lane. Compact fire deltas now resolve tunnel targets through an adapter API, suppress duplicate cells by stable target id, mark tunnels unstable, defer terrain destruction by default, and expose `tunnel_fire_*` summary plus QA status fields. The native `Tunnel.Explode()` wrapper exists only behind `tunnel_terrain_destruction_enabled`, which defaults disabled.
- 2026-05-05 worker learning: Timberborn's `Tunnel` runtime type is present in `Timberborn.Explosions.dll` but is not publicly accessible to the mod assembly. The live adapter uses reflection to resolve `GetObjectsWithComponentAt<Tunnel>`, read `BottomLevel`, and call public `Explode()` only when the explicit terrain-destruction setting is enabled.
- 2026-05-06 review failed: reflected tunnel lookup and native `Explode()` calls are not contained, so default behavior and any future terrain-destruction proof can throw through dispatch instead of reporting safe-unavailable/deferred telemetry. Moved back to `03-in-progress`; require containment tests and fresh review before live QA.
- 2026-05-06 worker safety fix: contained tunnel resolver, `BottomLevel`, and `Explode()` reflection failures as safe-unavailable/recoverability-unknown telemetry, kept terrain destruction default-disabled, and added resolver type, explode method, and explode invocation failure regression tests.
- 2026-05-06 live QA failed after deploy/load/readiness succeeded under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-152-153-154-live-20260506T143435Z/`. The loaded save had no tunnel target; other saves contain `Tunnel.*`, but the command bridge has no tunnel or coordinate-targeted conservative stimulus. Moved back to `03-in-progress`; add a stable tunnel fixture or safe targetable QA surface before rerunning the deferred-destruction gate with `tunnel_terrain_destruction_enabled=false`.
- 2026-05-06 review failed the direct targeting unblock: `qa-delta-stimulus tunnel` can treat a synthetic `tunnel-unavailable:<cell>` safe-unavailable target as a real placed direct target and queue simulator heat/fuel. Keep in `03-in-progress`; direct QA targeting must fail cleanly without queueing when only unavailable pseudo-targets exist.
- 2026-05-06 QA-targeting unblock worker: added `qa-delta-stimulus tunnel`, which scans placed infrastructure through the existing tunnel target API and queues only conservative Wildfire heat/fuel changes at the resolved target cell. Command queueing never calls `Tunnel.Explode()`; deferred destruction remains the default consequence while `tunnel_terrain_destruction_enabled=false`.
- 2026-05-06 worker direct-selector fix: `qa-delta-stimulus tunnel` now rejects `tunnel-unavailable:<cell>` pseudo-targets as no placed target and exits without queueing simulator changes. Regression coverage: `QueueQaDeltaStimulusDirectConsequenceSelectorsRejectUnavailablePseudoTargets`.
- 2026-05-06 live QA passed on `QA Tunnels and Booms` under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-153-154-qa-20260506T155606Z/`. `qa-delta-stimulus tunnel` selected a real tunnel target at `x=39 y=2 z=4`; follow-up readiness showed `tunnel_fire_matched_target_cells=1`, `tunnel_fire_destruction_deferred=1`, `native_explode_attempted=0`, `native_explode_applied=0`, and `tunnel_terrain_destruction_enabled=false`. `tunnel_fire_unstable_targets=0`, but the conservative deferred-destruction gate passed. Move to `05-integration`.
- 2026-05-06 integration review passed with no blocking findings. Acceptance is for the conservative tunnel deferred-destruction gate only: `tunnel_terrain_destruction_enabled=false`, `native_explode_attempted=0`, `native_explode_applied=0`, and explicit deferred-destruction telemetry. `Tunnel.Explode()` terrain mutation remains disabled and unaccepted, and the accepted evidence did not prove marked-unstable presentation because `tunnel_fire_unstable_targets=0`. Current checkout verification passed `git diff --check`, `dotnet test` with `363` passed, `dotnet build Wildfire.slnx`, and `bun run kanban:audit -- --strict`. Move to `06-done` without bundling unrelated dirty-checkout work into this ticket's acceptance.
