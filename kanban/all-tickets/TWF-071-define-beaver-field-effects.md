---
ticket: TWF-071
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies:
  - TWF-046
write_scope:
  - docs/DESIGN.md
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-071-define-beaver-field-effects.md
---

# TWF-071: Define Beaver Field Effects

## Goal

Define what fire, smoke, ash, steam, heat, and water-suppression fields mean for beavers before implementing behavior changes.

## Why

Wildfire currently defines fire simulation, visual fields, alerts, and building consequences, but it does not define how beavers should perceive or react to those fields. Release needs a clear behavior contract so beavers do not ignore dangerous cells, and so implementation does not smuggle fire rules into Timberborn adapter code.

`docs/DESIGN.md` section 20 now accepts two beaver danger progressions: respiratory exposure (`coughing` to `choking` to `death`) and heat/flame exposure (`singed` to `burned` to `death`). This ticket should turn that accepted direction into an implementable release contract.

## Requirements

- Define beaver-facing effects for active fire, heat, smoke, toxic smoke, clean steam, ash or aftermath, and wet suppression fields.
- Decide which effects are release scope and which are deferred.
- Define the respiratory progression: coughing slowdown, choking incapacitation or sleep-like behavior, and death after sustained severe exposure.
- Define the burn progression: singed injury, burned work-preventing severe injury, and death after sustained direct heat or flame exposure.
- Define behavior expectations such as avoidance, path cost, interruption, panic, work cancellation, injury, incapacitation, death, or safe no-op.
- Keep initial release conservative and reversible unless Timberborn APIs make deeper behavior safe.
- Preserve host boundaries: the GPU owns fire fields, Timberborn reads field exposure and applies beaver-facing consequences.
- Document what must be observable in logs, status, screenshots, or recordings.
- Update `docs/ARCHITECTURE.md` only if a new adapter boundary is accepted.
- Update `docs/TEST_PLAN.md` with validation expectations for accepted beaver behavior.

## Dependencies

- `TWF-046` proves the coherent live fire loop that beaver behavior should respond to.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- No runtime validation is required for this design ticket.

## Notes

- Prefer visible, understandable beaver behavior over hidden stat changes for the first implementation.
- If Timberborn exposes no safe API for a desired beaver consequence, defer that consequence explicitly instead of forcing it through reflection.
- Relevant design reference: `docs/DESIGN.md` section 20, "Beaver Field Effects".
- 2026-05-05 researcher: expanded the accepted beaver field contract. Release scope is exposure telemetry, work interruption or avoidance, safe no-op counters, aggregated player feedback, and reversible coughing/singed/burned effects only when a native API is proven safe. Choking, death, native contamination coupling, ash collection, faction response, panic, and arbitrary path graph mutation are deferred gates. Downstream implementation must consume real field samples or deterministic field fixtures rather than hard-coded beaver triggers.
- 2026-05-19 design correction: steam is clean suppression vapor only. There is no toxic steam or contaminated steam beaver class.
