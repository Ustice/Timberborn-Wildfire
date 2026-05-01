---
ticket: TWF-019
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-012
   - TWF-016
write_scope:
   - src/Wildfire.Timberborn/**
   - scripts/**
   - docs/TEST_PLAN.md
---

# TWF-019: Bind QA Command Bridge To Timberborn

## Goal

Expose the `TWF-012` command bridge through a narrow Timberborn runtime surface so QA can invoke known Wildfire commands from the running game.

## Why

`TWF-012` can define a safe in-process command bridge, but that alone does not let the live game run or answer commands. Live validation needs a concrete binding that forwards only approved commands into the bridge and emits searchable result evidence.

## Requirements

- Choose the narrowest viable Timberborn invocation surface, such as a debug UI button, file-polling command inbox, or local-only endpoint.
- Forward only named Wildfire QA commands to the bridge.
- Reject unknown commands with a logged failure.
- Do not expose arbitrary code execution.
- Keep read-only commands as the default.
- Gate any mutating command behind explicit naming and safe-repeat semantics.
- Log `wildfire_command_request` and `wildfire_command_result` tokens with command name, success/failure, and concise state fields.
- Document how QA scripts or humans invoke the command path.
- Keep Timberborn as an adapter and do not move fire rules into Timberborn.

## Dependencies

- `TWF-012` command bridge scaffold.
- `TWF-016` deploy path so the binding can be loaded and validated in Timberborn.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Deploy the mod through `TWF-016` when available.
- Invoke at least one read-only command in Timberborn and capture log evidence.

## Notes

- Prefer boring and local over clever. The first binding only needs to prove that QA can ask the running game for Wildfire status.

## Worker Notes

- Worker worktree: `~/repos/wildfire-TWF-019` on branch `codex/TWF-019-command-binding`.
- Implemented the first binding as a game-context Bindito singleton in `Wildfire.Timberborn`, not in `Wildfire.Core`.
- Binding surface is a local file inbox/outbox under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/`.
- Runtime command path:

   1. QA writes `status` or `help` to `command-inbox.txt`, preferably with `bun scripts/invoke-timberborn-command.ts status`.
   2. `TimberbornQaCommandFileBridge` polls from the loaded game, deletes the inbox, and forwards the command to `TimberbornQaCommandBridge`.
   3. `UnityTimberbornQaCommandLogSink` writes `wildfire_command_request` and `wildfire_command_result` to `Player.log`.
   4. The latest result token is written to `command-outbox.txt`.

- The helper script rejects unknown commands before writing the inbox. Manual unknown inbox files still reach the safe bridge rejection path and log a failure token.

## Verification Evidence

- `git diff --check`: passed.
- `dotnet test`: passed, 71 tests.
- `dotnet build Wildfire.slnx`: passed with 0 warnings and 0 errors.
- `bun scripts/invoke-timberborn-command.ts --help`: passed.
- `bun scripts/deploy-timberborn-mod.ts --apply --allow-open-game --skip-build`: passed; copied `netstandard2.1` `Wildfire.Timberborn.dll`, `Wildfire.Core.dll`, and PDBs to `~/Documents/Timberborn/Mods/Wildfire/Scripts`.
- Live Timberborn startup screens cleared manually: Mods `OK`, Experimental Mode `Start!`, then main-menu `Continue`.
- Loaded save: `Wildfire testing - 2026-05-01 07h56m, Day 1-2.autosave`.
- `bun scripts/invoke-timberborn-command.ts status --wait=6`: passed and returned:

```text
wildfire_command_result command=status success=true status=success simulator_integrated=false width=placeholder height=placeholder depth=placeholder tick_count=placeholder queued_changes=placeholder last_delta_count=placeholder message=ok
updated_at_utc=2026-05-01T12:22:18.7560140Z
```

- `~/Library/Logs/Mechanistry/Timberborn/Player.log` live evidence:

```text
96:wildfire_command_bridge_ready inbox=/Users/jasonkleinberg/Library/Application_Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt outbox=/Users/jasonkleinberg/Library/Application_Support/Mechanistry/Timberborn/WildfireQA/command-outbox.txt known_commands=help,status
97:wildfire_command_request command=status
98:wildfire_command_result command=status success=true status=success simulator_integrated=false width=placeholder height=placeholder depth=placeholder tick_count=placeholder queued_changes=placeholder last_delta_count=placeholder message=ok
```
