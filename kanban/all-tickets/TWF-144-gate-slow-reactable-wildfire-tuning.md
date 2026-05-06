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

- Use the best stable real-field scenario after importer parity passes. Prefer the generated QA scenario only if it is the best available surface; otherwise use a stable real save.
- Run the `slow-reactable` preset.
- Prove trees sustain flame longer than the old immediate burnout behavior.
- Prove connected fuel spreads visibly across multiple cells.
- Prove water or badwater suppresses or slows affected cells.
- Prove burnout and residual heat eventually cool.
- Preserve recordings, shader captures, status output, command transcripts, and copied `Player.log`.
- If tuning fails, record which parameter relationship failed instead of adding a new fake stimulus.

## Dependencies

- `TWF-133` identifies or validates the scenario surface when a new generated artifact or save-selection pass is needed. It should not block this gate if a stable save already exercises the required fields.
- `TWF-141` proves real importer parity.
- `TWF-143` provides the tuning preset.

## Role

- QA.
- Follow [../roles/qa.md](../roles/qa.md).

## Implementation Notes

- Use the same selected scenario and preset for every comparison run.
- `50x50` saves such as Diorama or Fuel can satisfy this Sprint 9 real-field gate if they exercise the required fields. `TWF-156` owns the separate Sprint 10 `256x256` proof.
- Record the parameter preset name, tick count, ignition target, affected checkpoint IDs, and elapsed real time.
- If fire goes out too quickly, report whether the issue appears to be imported fuel, burn-down pressure, cooling, water, or visual threshold.
- If fire spreads too quickly, report whether the issue appears to be neighbor heat, burning-neighbor bonus, cadence, or flammability.
- Do not add a new synthetic cell to make the gate pass.
- Do not reject the current tuning only because the generated scenario is imperfect; reject it only from real-field behavior evidence.

## Verification

- Run `git diff --check`.
- Run shader snapshot verification for the accepted preset.
- Live QA must include normal gameplay recording and final `status` or `qa-readiness` output with active preset and nonzero fire deltas.

## Notes

- This gate replaces accepting behavior from fixed center-cell proof commands.
- 2026-05-05 live QA attempt blocked before tuning evidence. `bun scripts/deploy-timberborn-mod.ts --apply --clean` passed, then `bun scripts/load-latest-save-and-unpause.ts --launch --wait=60` reached startup Mods, Experimental Mode, and main menu, preflighted the 50x50 Diorama autosave at `57500` cells, and clicked Continue. Timberborn then sat without a loaded-save signal for more than the 30-second lock threshold. Evidence was captured under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-144-live-blocked-20260505T014152Z/`.
- 2026-05-05 blocker detail: copied `Player.log` only reached early game/Steam startup and did not emit Wildfire runtime initialization tokens. No `slow-reactable` preset, ignition, spread, suppression, burnout, recording, or final status evidence was collected. Do not accept or reject tuning from this run.
- 2026-05-05 coordinator correction: added missing `TWF-133` dependency because this gate requires the same generated QA scenario for tuning comparisons. A healthy Diorama save can prove live command responsiveness, but it should not be treated as this gate's final tuning scenario.
- 2026-05-06 direction update: the latest tuning/field-visual commits are intended to satisfy this gate on stable real-field evidence. Treat the scenario choice as flexible: use generated QA evidence if it works, but switch to another stable save if that better proves connected fuel, slow spread, suppression, burnout, and player response time.
- 2026-05-06 coordinator: moved to `04-verify` for real-field QA. This is no longer blocked by generated-scenario completeness. The separate `256x256` local-fire performance proof belongs to Sprint 10 in `TWF-156`.
