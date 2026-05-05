---
ticket: TWF-144
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-141
  - TWF-143
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-144-gate-slow-reactable-wildfire-tuning.md
---

# TWF-144: Gate Slow Reactable Wildfire Tuning

## Goal

Accept or block the first real tuning target: a readable wildfire that spreads through connected fuel but moves slowly enough for players to respond.

## Requirements

- Use the generated QA scenario after importer parity passes.
- Run the `slow-reactable` preset.
- Prove trees sustain flame longer than the old immediate burnout behavior.
- Prove connected fuel spreads visibly across multiple cells.
- Prove water or badwater suppresses or slows affected cells.
- Prove burnout and residual heat eventually cool.
- Preserve recordings, shader captures, status output, command transcripts, and copied `Player.log`.
- If tuning fails, record which parameter relationship failed instead of adding a new fake stimulus.

## Dependencies

- `TWF-141` proves real importer parity.
- `TWF-143` provides the tuning preset.

## Role

- QA.
- Follow [../roles/qa.md](../roles/qa.md).

## Implementation Notes

- Use the same generated scenario and preset for every comparison run.
- Record the parameter preset name, tick count, ignition target, affected checkpoint IDs, and elapsed real time.
- If fire goes out too quickly, report whether the issue appears to be imported fuel, burn-down pressure, cooling, water, or visual threshold.
- If fire spreads too quickly, report whether the issue appears to be neighbor heat, burning-neighbor bonus, cadence, or flammability.
- Do not add a new synthetic cell to make the gate pass.

## Verification

- Run `git diff --check`.
- Run shader snapshot verification for the accepted preset.
- Live QA must include normal gameplay recording and final `status` or `qa-readiness` output with active preset and nonzero fire deltas.

## Notes

- This gate replaces accepting behavior from fixed center-cell proof commands.
- 2026-05-05 live QA attempt blocked before tuning evidence. `bun scripts/deploy-timberborn-mod.ts --apply --clean` passed, then `bun scripts/load-latest-save-and-unpause.ts --launch --wait=60` reached startup Mods, Experimental Mode, and main menu, preflighted the 50x50 Diorama autosave at `57500` cells, and clicked Continue. Timberborn then sat without a loaded-save signal for more than the 30-second lock threshold. Evidence was captured under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-144-live-blocked-20260505T014152Z/`.
- 2026-05-05 blocker detail: copied `Player.log` only reached early game/Steam startup and did not emit Wildfire runtime initialization tokens. No `slow-reactable` preset, ignition, spread, suppression, burnout, recording, or final status evidence was collected. Do not accept or reject tuning from this run.
