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
