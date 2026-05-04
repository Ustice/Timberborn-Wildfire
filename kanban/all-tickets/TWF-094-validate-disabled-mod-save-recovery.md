---
ticket: TWF-094
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-046
write_scope:
  - docs/TEST_PLAN.md
  - docs/HANDOFF.md
  - kanban/all-tickets/TWF-094-validate-disabled-mod-save-recovery.md
---

# TWF-094: Validate Disabled Mod Save Recovery

## Goal

Validate that disabling or removing Wildfire leaves affected saves recoverable, or document the exact safe limitation.

## Why

Players may unsubscribe, disable mods, or recover from a broken install. That path needs separate evidence because the mod cannot rely on its runtime services being present.

## Requirements

- Start from a save with active or recent Wildfire activity.
- Disable or remove Wildfire through the safest available local workflow.
- Attempt to load the save.
- Record whether Timberborn loads, warns, fails safely, or requires a documented limitation.
- Preserve logs, screenshots, save names, and exact mod state.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-046` proves the coherent live gameplay loop before disable testing.

## Parent Reference

- Parent gate: `TWF-047`.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Implementation Notes

- Start from the save produced or identified by `TWF-093` when possible so the enabled baseline and disabled recovery evidence describe the same risk surface.
- Use the safest player-like disable path available locally. If the only available path is removing the deployed Wildfire mod folder, record that limitation explicitly and do not present it as full Workshop unsubscribe coverage.
- Capture pre-disable evidence with Wildfire enabled, then capture the exact disabled state: mod folder state, manifest or deploy folder path, launch steps, save name, screenshots or load result, and copied `Player.log`.
- Passing evidence is either a recoverable load without new critical exceptions or a precise documented limitation. A crash, infinite load, or ambiguous missing-mod dialog without logs is a blocker, not a pass.
- Ownership boundary: do not change runtime code in this QA ticket. If recovery requires implementation work, write the smallest blocker with the observed Timberborn message, log tokens, and suspected owning ticket.

## Verification

- Run `git diff --check`.
- Passing evidence requires either recoverable load behavior or a clear documented limitation.

## Notes

- Do not treat development deploy-folder cleanup as the same thing as player-facing mod disable.
- QA passed on 2026-05-03 with evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-094-disabled-mod-recovery-20260503T041547Z`.
- The run used the player-facing Timberborn main-menu `Mods` dialog, unchecked `Wildfire v0.1.0.0`, observed the restart-required state, exited through the UI, relaunched, and confirmed Wildfire remained unchecked.
- The affected save was `Wildfire testing (7)` from `TWF-093`. With Wildfire disabled, Timberborn warned: `You are trying to load a game without mods that it was saved with.` The save-file mod list included `Wildfire v0.1.0.0` with a warning marker, and choosing `Yes` loaded the save into gameplay.
- Active mods in the disabled-load `Player.log` excluded Wildfire. The Wildfire command bridge timed out waiting for `command-outbox.txt`, which is the expected safe degraded state with the mod disabled and runtime absent.
- The disabled-load critical scan found no matching critical exception, error, or crash tokens in the copied logs. `git diff --check` passed.
- Key artifacts include `pre-disable-save-and-mod-state.txt`, `01-pre-disable-qa-readiness.txt`, `02-pre-disable-loaded-save.png`, `13-main-menu-mods-dialog-before-disable.png`, `14-main-menu-mods-dialog-wildfire-disabled.png`, `22-load-game-dialog-disabled-wildfire.png`, `23-load-dialog-selected-wildfire-testing-7.png`, `24-after-load-wildfire-testing-7-click.png`, `26-post-warning-wait.png`, `Player-disabled-load.log`, `Player-disabled-load-after-bridge-probe.log`, `27-disabled-qa-readiness-timeout.txt`, and `28-processes-final.txt`.
- QA intentionally left Timberborn running with `Wildfire testing (7)` loaded and Wildfire disabled/missing so `TWF-095` can validate re-enable rebuild from the preserved state.
