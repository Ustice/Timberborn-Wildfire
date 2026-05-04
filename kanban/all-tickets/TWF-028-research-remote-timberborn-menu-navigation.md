---
ticket: TWF-028
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies:
  - TWF-017
write_scope:
  - docs/research/timberborn-remote-menu-navigation.md
  - kanban/all-tickets/TWF-028-research-remote-timberborn-menu-navigation.md
---

# TWF-028: Research Remote Timberborn Menu Navigation

## Goal

Determine whether QA can navigate Timberborn menus remotely without screen clicks, and document the smallest safe path if it is possible.

## Why

Coordinate-based menu automation is fragile and interrupts the visible desktop. If Timberborn exposes a safer menu-navigation path through commands, UI Toolkit APIs, debug controls, keyboard focus, save/load APIs, or another remote mechanism, QA should use it. If no practical path exists, we should close this ticket with evidence instead of guessing.

## Requirements

- Research whether Timberborn menus can be navigated without physical screen clicks or mouse-coordinate automation.
- Check repo-local systems first: Wildfire command bridge, QA scripts, Timberborn adapter code, and existing menu-coordinate docs.
- Check Timberborn-visible APIs, UI Toolkit patterns, debug/developer menu behavior, keyboard shortcuts, save/load entry points, and modding references as needed.
- Separate confirmed evidence from inference.
- If a feasible path exists, document the proposed mechanism, safety boundary, risks, and the smallest follow-up implementation ticket.
- If no feasible path exists, document what was checked and why the ticket can close as not possible for now.
- Do not implement a new automation path in this ticket unless the coordinator explicitly converts it to worker scope.
- Do not click through the live UI as part of the research unless needed to confirm a documented keyboard/debug path.

## Dependencies

- `TWF-017` provides the startup/log harness and QA evidence conventions this research should plug into.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- Link or cite the source files, docs, logs, screenshots, or public references used.
- Provide a clear yes/no/unknown conclusion with confidence.

## Notes

- The best answer may be "not possible without screen interaction in this sprint"; that is an acceptable closeout if it is evidence-backed.

## Researcher Notes

- Worktree: `~/repos/wildfire-TWF-028`
- Branch: `codex/TWF-028-remote-menu-navigation`
- Research doc: `docs/research/timberborn-remote-menu-navigation.md`
- Conclusion: current Wildfire QA cannot navigate Timberborn menus remotely without screen interaction. The existing file command bridge is remote, but it only supports `status` and `help`; menu traversal remains coordinate-driven.
- Confidence: medium-high for the current no-go conclusion; medium for the future command-oriented path because the exact Timberborn save/load service API still needs focused discovery.
- Evidence checked:
  - Wildfire command bridge and file bridge: `src/Wildfire.Timberborn/TimberbornQaCommandBridge.cs`, `src/Wildfire.Timberborn/TimberbornQaCommandFileBridge.cs`, `scripts/invoke-timberborn-command.ts`, `kanban/all-tickets/TWF-012-in-game-command-bridge.md`, and `kanban/all-tickets/TWF-019-bind-qa-command-bridge-to-timberborn.md`.
  - Existing menu automation docs: `docs/timberborn-menu-coordinate-guide.md`, `docs/timberborn-bottom-menu-guide.md`, `docs/TEST_PLAN.md`, and `kanban/all-tickets/TWF-015-load-latest-save-utility.md`.
  - Timberborn adapter bindings: `src/Wildfire.Timberborn/WildfireConfigurator.cs` and `src/Wildfire.Timberborn/TimberbornFireRuntime.cs`.
  - Timberborn UI/modding references: `docs/reference/timberborn-ui.md`, `docs/reference/modding-guide.md`, `docs/reference/blueprint-reference.md`, local Timberborn UXML/keybinding assets under `~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/StreamingAssets/Modding/`, and <https://timberborn.wiki.gg/wiki/Developer_Mode>.
- Key evidence:
  - `TimberbornQaCommandBridge` only registers `status` and `help`.
  - `scripts/invoke-timberborn-command.ts` only allowlists `status` and `help`.
  - Timberborn UXML names main-menu/load/save controls, but inspected menu buttons use `focusable="false"`, so keyboard focus navigation is not an immediate substitute for clicks.
  - Timberborn keybindings cover `Cancel`, `Confirm`, root/tool cycling, console toggle, developer mode, and debug mode, but not named main-menu/load-dialog selection.
  - Debug/developer panels are useful for observation and dev-only operations, but no checked source showed a safe remote menu-navigation API.
- Recommended follow-up:
  - Do not build a generic remote menu navigator.
  - Add a new worker ticket for an allowlisted `qa-readiness` bridge command that reports loaded-game/readiness evidence without mutating saves.
  - Only after that, research or implement a separate `load-latest-save` bridge command if a Timberborn-owned save/load service can be identified and guarded by disposable-save checks. Otherwise keep `TWF-015` coordinate-driven.
- Recommended board move: `04-verify`, because the research artifact is written and the ticket can be reviewed against the cited evidence. Do not move status symlinks from this researcher worktree.
