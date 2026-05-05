---
ticket: TWF-130
agent_level: High
role: researcher
requires_qa: false
doc_only: false
dependencies:
   - TWF-116
   - TWF-117
write_scope:
   - docs/DESIGN.md
   - docs/world-consequence-first-pass.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-130-design-tunnel-and-dynamite-destruction.md
---

# TWF-130: Design Tunnel And Dynamite Destruction

## Goal

Define a bounded destruction contract for tunnels, dynamite, double dynamite, triple dynamite, detonators, and related explosive infrastructure.

## Why

Jason accepted that tunnels should eventually cause destruction and that dynamite or detonators are explosive infrastructure cases, but that behavior is too risky to fold into the first infrastructure classification pass or stored-goods pulse.

## Requirements

- Start from `TWF-116` explosive behavior and `TWF-117` infrastructure classification.
- Survey Timberborn runtime objects and blueprint ids for tunnel, dynamite, double dynamite, triple dynamite, and detonator behavior.
- Define which objects are armed explosives, trigger devices, tunnel structures, or safe no-op cases.
- Decide whether destruction is modeled as burn damage, bounded heat/fire pulse, object-state transition, or later terrain-affecting behavior.
- Do not implement terrain deformation in the first accepted contract unless a later explicit ticket approves it.
- Define settings, telemetry, safety gates, and QA evidence required before implementation.
- Create implementation tickets if the accepted contract splits tunnel destruction, dynamite triggering, and terrain mutation into separate lanes.

## Dependencies

- `TWF-116` accepts the stored-goods explosive contract.
- `TWF-117` accepts the infrastructure classification contract.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Implementation Notes

- Research runtime ids, blueprint ids, and existing Timberborn destruction semantics before proposing implementation.
- Keep terrain deformation out of scope unless Jason explicitly accepts a later terrain-mutation ticket.
- Split implementation into smaller tickets if tunnel destruction, dynamite triggering, and detonator behavior need different safety gates.
- Align settings and telemetry with the accepted `TWF-116` explosive behavior where possible.

## Verification

- Run `git diff --check`.
- Run `dotnet test` if code changes are made.
- Run `dotnet build Wildfire.slnx` if code changes are made.

## Notes

- This ticket is a design and safety gate, not implementation permission.
- No terrain deformation should be implemented without a later explicit ticket.
- 2026-05-05 researcher: surveyed installed blueprints and runtime assemblies. `Dynamite`, `DoubleDynamite`, and `TripleDynamite` are placed infrastructure with `DynamiteSpec` depths `1`, `2`, and `3`, all costing `Explosives` and exposing native `Dynamite.Trigger()`, `TriggerDelayed(int)`, `Disarm()`, and `Detonate()` methods. `Detonator` costs `MetalBlock`, `Explosives`, and `Extract`, is constrained to sit over the three dynamite templates, and exposes `Arm()`, `Disarm()`, and `Evaluate()`. `Tunnel` costs `Explosives`, `Extract`, and `Plank`, has `TunnelSpec`, names `Platform.Folktails` as support, and exposes native `Tunnel.Explode()`.
- 2026-05-05 researcher: accepted contract is conservative. Stored `Explosives` and `Fireworks` remain stored-goods behavior; placed dynamite is an explosive infrastructure target with sustained arming threshold, optional wrapped native trigger, and bounded Wildfire heat pulse; detonators are trigger devices that should start with disable or unsafe-mark behavior; tunnel native terrain destruction remains disabled by default and split to a later safety gate.
- 2026-05-05 researcher: created follow-up tickets `TWF-152` native dynamite fire triggering, `TWF-153` detonator fire safety behavior, and `TWF-154` tunnel fire destruction gate.
