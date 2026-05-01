---
ticket: TWF-030
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-013
   - TWF-017
   - TWF-019
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-030-load-latest-save-and-unpause-utility.md
---

# TWF-030: Add Latest-Save Startup Utility

## Goal

Create a guarded Bun/TypeScript QA utility that gets Timberborn from a closed or freshly opened state into the latest loaded save, then unpauses it so live Wildfire runtime evidence can begin.

## Requirements

- Use Bun and TypeScript.
- Launch or attach to Timberborn through bundle id `com.mechanistry.timberborn`.
- Validate the documented `1920 x 1080` display assumption before clicking.
- Click through the startup Mods dialog when it appears.
- Click through the Experimental Mode Information modal when it appears.
- Continue the latest save from the main menu.
- Wait until the loaded-save HUD is visible.
- Unpause the simulation.
- Fail loudly if the expected screen is not visible before a click.
- Save screenshots for each transition: startup Mods, Experimental Mode Information, main menu, loaded save before unpause, and loaded save after unpause.
- Use only documented coordinate targets from `docs/timberborn-menu-coordinate-guide.md`, or update that guide with verified coordinates as part of this ticket if any required target is missing.
- Do not add generic menu navigation, arbitrary UI invocation, save deletion, save selection, or destructive debug commands.
- Document command usage, prerequisites, output paths, and limitations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-013` provides the coordinate-guide contract and core menu targets.
- `TWF-017` provides startup harness conventions, lock behavior, and evidence-bundle expectations.
- `TWF-019` provides the live command bridge for post-load status checks.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).
- Use the local [Timberborn QA Utility skill](../../.codex/skills/timberborn-qa-utility/SKILL.md).

## Verification

- Run `git diff --check`.
- Run the utility help or dry-run mode with `bun`.
- Run the utility against live Timberborn from a fresh launch state.
- Confirm screenshots exist and are `1920 x 1080`.
- Run `bun scripts/invoke-timberborn-command.ts status --wait=6` after unpause and capture `wildfire_command_result command=status success=true status=success simulator_integrated=true`.
- Confirm `Player.log` shows post-unpause dispatch evidence such as `wildfire_timberborn_dispatch_completed`.

## Notes

- The first version should encode the known startup path from current QA: startup Mods dialog, Experimental Mode Information modal, main menu Continue, loaded-save HUD, unpause.
- This ticket is intentionally narrower than general Timberborn menu automation.
- If Timberborn starts directly at a later state, the utility may skip already-satisfied screens only after positively identifying the current screen.
