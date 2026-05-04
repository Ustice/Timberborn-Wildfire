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
  - TWF-151
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
- `TWF-151` aligns live and snapshot count semantics after this gate found mismatches.

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
- 2026-05-04 QA result: blocked. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-141-parity-20260504/`.
- Generated scenario path `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/generated-scenarios/twf-141-parity-20260504/twf-141-parity.timber` exported successfully, but it is `256x256x23` with `1507328` cells, above the live auto-dispatch cap of `500000`, so it cannot be the live parity target yet.
- Closest live target was `Wildfire Diorama 50 QA/2026-05-04 19h47m, Day 3-9.autosave.timber`. Snapshot export and live import both reported `50x50x23` and `57500` cells, but material counts diverged: snapshot export reported `terrain=12576`, `trees=494`, `buildings=44`, `storage=2`, `infrastructure=137`, `water=289`, `badwater=1`; live import reported `terrain=2503`, `trees=1305`, `buildings=335`, `storage=23`, `infrastructure=0`, `water=32`, `badwater=90`.
- `TWF-151` was created to align or explicitly split source-count versus resolved-cell-count semantics before this parity gate can pass.
