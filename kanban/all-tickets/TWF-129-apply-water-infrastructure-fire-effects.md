---
ticket: TWF-129
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-075
   - TWF-114
   - TWF-117
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-129-apply-water-infrastructure-fire-effects.md
---

# TWF-129: Apply Water Infrastructure Fire Effects

## Goal

Define and apply conservative fire effects for dams, levees, floodgates, valves, sluices, and other water-passing infrastructure.

## Why

Jason accepted that water-passing infrastructure should likely be difficult to burn. These objects interact with water simulation and map safety, so they need their own conservative ticket instead of being grouped with ordinary burnable infrastructure.

## Requirements

- Use `TWF-117` classification and `TWF-075` burn damage ownership.
- Treat water-passing infrastructure as difficult to burn unless material evidence says otherwise.
- Apply burn capacity only from burnable construction resources; water, dirt, metal, and other inert resources must not fuel fire.
- Identify safe Timberborn APIs before changing water passage, gate state, blockage, or presentation.
- Prefer safe no-op plus explicit telemetry over risky water-simulation mutation.
- Add bounded telemetry for considered water infrastructure, burnable material value, damaged targets, water-state mutation attempts, skipped no-safe-api cases, and repair eligibility.
- Add deterministic tests for inert-resource no-op behavior, partially burnable mixed resources, duplicate suppression, difficult-to-burn thresholds, and safe unavailable APIs.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-075` provides burn damage state and target ownership.
- `TWF-114` provides construction-resource fuel classification.
- `TWF-117` accepts the infrastructure classification contract.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Start with descriptor and capacity generation before any water-passage mutation.
- Treat water-simulation and water-control APIs as unsafe until a deterministic wrapper and live proof show recoverable behavior.
- Make water-passing infrastructure difficult to burn by default, then tune from evidence rather than intuition.
- Prefer explicit safe no-op telemetry over mutating water state when the API is unclear.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence for one water infrastructure target being safely handled, damaged, or explicitly skipped because no safe API exists.

## Notes

- No terrain deformation belongs in this ticket.
- Keep water infrastructure separate from path, power, tunnel, and dynamite behavior.
