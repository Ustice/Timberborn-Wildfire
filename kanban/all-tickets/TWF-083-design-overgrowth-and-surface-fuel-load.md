---
ticket: TWF-083
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies:
  - TWF-069
write_scope:
  - docs/DESIGN.md
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-083-design-overgrowth-and-surface-fuel-load.md
---

# TWF-083: Design Overgrowth And Surface Fuel Load

## Goal

Define a later fuel-load mechanic for underbrush, grass, overgrown trees, or overgrown irrigated fields.

## Why

The design notes that underbrush, grass, and overgrowth could become a future hazard: mature trees might burn relatively slowly while overgrown tiles burn fast. This should be designed intentionally instead of being smuggled into first-release material tuning.

## Requirements

- Decide whether overgrowth is a new field, an entity state, a plant modifier, a terrain modifier, or a Timberborn adapter input.
- Preserve the simulator boundary: Timberborn may map overgrowth to fuel or flammability, but it must not own fire-spread rules.
- Define how overgrowth appears, grows, decays, is removed, and affects controlled burns.
- Define how irrigated fields, trees, and surface fuel differ.
- Decide what evidence would be needed before promoting the mechanic out of deferred scope.
- Update `docs/DESIGN.md`, `docs/ARCHITECTURE.md`, and `docs/TEST_PLAN.md` with the accepted future contract.

## Dependencies

- `TWF-069` provides fire behavior recording evidence that can inform fuel-load design.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- No runtime validation is required for this design ticket.

## Notes

- This is deferred future work.
- 2026-05-19 release scope correction: the broad overgrowth system remains deferred, but `TWF-165` promotes a focused release slice for rare deterministic ignition from fire-using buildings and dry dead vegetation during drought.
- Relevant design reference: `docs/DESIGN.md` section 20, "Ash And Fertility".
