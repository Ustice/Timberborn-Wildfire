---
ticket: TWF-141
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-146
  - TWF-139
  - TWF-140
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-141-gate-importer-parity.md
---

# TWF-141: Gate Importer Parity

## Goal

Prove that live Timberborn import and `.timber` snapshot export classify the generated QA map consistently enough for tuning and consequences.

## Requirements

- Use the generated QA scenario from `TWF-133` or the closest accepted generated scenario.
- Export the `.timber` snapshot to JSON and binary.
- Load the same scenario in Timberborn and capture live importer status.
- Compare checkpoint counts and representative cells for terrain, trees, crops, buildings, storage, infrastructure, water, and badwater.
- Record every mismatch as a blocker with exact object/template/resource evidence.
- Do not accept downstream tuning or consequences from fixed-cell stimuli if this gate fails.

## Dependencies

- `TWF-146` provides generated scenario checkpoints and manifests.
- `TWF-139` provides live import.
- `TWF-140` provides snapshot export.

## Role

- QA.
- Follow [../roles/qa.md](../roles/qa.md).

## Implementation Notes

- Start from the generated scenario manifest and choose a small representative checkpoint set before running live QA.
- Compare counts and cell identities first, then inspect packed values and companion fields for mismatches.
- Treat a missing live API as a blocker for the importer ticket, not as permission to hand-fill the field.
- This gate should update evidence notes only. Code fixes belong in `TWF-139`, `TWF-140`, or `TWF-146`.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `bun test`.
- Run `dotnet test`.
- Live QA must preserve command transcripts, copied `Player.log`, exported JSON, binary metadata, and parity report.

## Notes

- This is a gate ticket. It should not implement importer fixes except tiny evidence-documentation changes.
