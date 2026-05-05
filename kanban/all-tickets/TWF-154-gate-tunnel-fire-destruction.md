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
