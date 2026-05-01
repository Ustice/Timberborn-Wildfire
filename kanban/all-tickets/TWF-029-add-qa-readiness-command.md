---
ticket: TWF-029
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-012
   - TWF-017
   - TWF-019
   - TWF-028
write_scope:
   - src/Wildfire.Timberborn/**
   - scripts/invoke-timberborn-command.ts
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-029-add-qa-readiness-command.md
---

# TWF-029: Add QA Readiness Command

## Goal

Add an allowlisted `qa-readiness` command to the Timberborn command bridge so QA can remotely check loaded-game readiness without navigating menus.

## Why

`TWF-028` found that generic remote Timberborn menu navigation is not practical or safe with current surfaces. The safer path is command-oriented: add one explicit remote command per QA outcome instead of trying to drive UI menus.

## Requirements

- Add `qa-readiness` to the existing command bridge allowlist.
- Keep the command read-only and non-destructive.
- Report whether the Wildfire command bridge is alive, whether the game-context runtime is loaded, whether the simulator is integrated, and the current simulator dimensions/tick fields already exposed by `status`.
- Include any additional loaded-game readiness fields that can be read safely from existing Wildfire state without probing Timberborn UI.
- Log `wildfire_command_request command=qa-readiness` and a searchable `wildfire_command_result command=qa-readiness ...` result.
- Update `scripts/invoke-timberborn-command.ts` so it can invoke `qa-readiness`.
- Do not add generic menu navigation, generic VisualElement invocation, save loading, save deletion, or UI-click commands.
- Document the command and its limitations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-012` provides the command bridge.
- `TWF-017` provides startup/log harness conventions.
- `TWF-019` binds the bridge to live Timberborn.
- `TWF-028` provides the research conclusion and safety boundary.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Run `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6` against a loaded Timberborn save when local game state is available.
- Capture `Player.log` evidence for `wildfire_command_request command=qa-readiness` and `wildfire_command_result command=qa-readiness success=true`.

## Notes

- This ticket intentionally replaces the idea of generic remote menu navigation with a narrow, safe command.
- If live Timberborn QA is not available, complete the code and unit tests, then record the missing live evidence and smallest unblock.
