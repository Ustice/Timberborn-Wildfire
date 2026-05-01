---
ticket: TWF-005
agent_level: High
role: researcher
requires_qa: false
doc_only: true
dependencies:
   - TWF-001
   - TWF-003
write_scope:
   - kanban/all-tickets/TWF-005-unity-compute-prototype-plan.md
---

# TWF-005: Plan Unity Compute Prototype

## Goal

Prepare the GPU implementation path without outrunning CPU correctness.

## Requirements

- Define the first compute shader buffer layout in terms of the packed cell format.
- Identify CPU/GPU snapshot parity requirements.
- Decide whether the first GPU pass should scan the full grid before active-frontier optimization.
- Recommend a narrow implementation ticket for `Wildfire.Unity`.

## Dependencies

- TWF-001 and TWF-003 should define CPU behavior and snapshots first.

## Role

- Researcher using [../roles/researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check` when the ticket is updated.

## Notes

- Gameplay deltas must remain C#-driven even if visuals are GPU-driven.
