---
ticket: TWF-047
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-046
  - TWF-093
  - TWF-094
  - TWF-095
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-047-validate-save-reload-disable-reenable.md
---

# TWF-047: Validate Save Reload Disable Reenable

## Goal

Integrate save lifecycle validation by accepting the enabled save/reload, disabled-mod recovery, and re-enable/rebuild child tickets.

## Why

Players will not only run a perfect single session. Release needs confidence that active or recently active fire state does not corrupt saves, trap players, or make the save unrecoverable when the mod is absent.

## Requirements

- Confirm `TWF-093` validates save/reload with Wildfire enabled after fire activity.
- Confirm `TWF-094` validates disabling or removing the mod.
- Confirm `TWF-095` validates re-enabling the mod and rebuilding or degrading runtime state safely.
- Add save/load code or tests only if validation exposes a real gap.
- Preserve logs, screenshots, commands, save names, and artifact paths.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-046` proves the coherent live gameplay loop before save lifecycle hardening.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Implementation Notes

- Treat this as an integration gate for `TWF-093`, `TWF-094`, and `TWF-095`, not as a fourth independent live experiment unless child evidence conflicts or leaves a gap.
- Reconcile the child ticket notes first: save name, enabled baseline artifact, disabled-mod method, re-enable deploy state, copied `Player.log` files, screenshots, command transcripts, and any documented degraded states.
- The accepted parent evidence should name one coherent lifecycle story from enabled fire activity through reload, disable or removal, and re-enable. If the children used different saves, call that out as residual risk.
- Only add code or tests if child validation exposes a real save lifecycle defect. Otherwise keep this ticket to QA acceptance, `docs/TEST_PLAN.md`, `docs/HANDOFF.md`, and canonical ticket notes.
- A safe blocker should identify the child ticket that owns the missing evidence and the smallest rerun or implementation fix needed before release.

## Verification

- Run `git diff --check`.
- Run `dotnet test` if production code changes are made.
- QA must capture live save/reload evidence and confirm no new critical exceptions appear in `Player.log`.

## Notes

- Prefer recoverable behavior over persisted simulation complexity. Computed caches should be rebuildable after load.
- Child tickets: `TWF-093`, `TWF-094`, and `TWF-095`.
- QA/reviewer acceptance passed on 2026-05-03 using child evidence from `TWF-093`, `TWF-094`, and `TWF-095`.
- The parent lifecycle uses one coherent save: `Wildfire testing (7)` at `/Users/jasonkleinberg/Documents/Timberborn/ExperimentalSaves/Wildfire testing/Wildfire testing (7).timber`.
- `TWF-093` proves enabled save/reload after fire activity with recovered command/status, visual surface, alert, and post-reload stimulus evidence.
- `TWF-094` proves player-facing Mods-dialog disable recovery: Wildfire unchecked, Timberborn missing-mod warning accepted, save loaded into gameplay, Wildfire bridge absent as expected, and no disabled-load critical scan hits.
- `TWF-095` proves re-enable rebuild: Wildfire rechecked through Mods dialog, Timberborn restarted, `Wildfire testing (7)` explicitly loaded, runtime/simulator rebuilt at `128x128x23`, command/status recovered after a transient malformed-command race, and post-unpause `qa-readiness` advanced to `tick_count=4`.
- No save lifecycle defect was exposed, so no code or test changes are required for `TWF-047`.
