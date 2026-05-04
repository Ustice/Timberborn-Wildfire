---
ticket: TWF-012
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-008
write_scope:
  - src/Wildfire.Timberborn/**
  - docs/TEST_PLAN.md
---

# TWF-012: Create In-Game Command Bridge

## Goal

Create a small in-game test harness and command bridge that QA utilities can use to ask the running Timberborn mod for status or trigger safe debug actions.

## Why

External click automation can open menus and load saves, but it cannot reliably prove the mod state by itself. A command bridge gives tests a way to query the game-side integration and trigger narrowly scoped actions without manual clicking.

## Requirements

- Define a minimal command surface for QA, such as status, simulator dimensions, tick count, queued change count, and last delta count.
- Provide a safe way to invoke commands in-game.
- Keep commands read-only by default.
- Any mutating commands must be explicit, narrow, and safe to repeat.
- Log command requests and results with searchable tokens.
- Document how QA scripts should connect to the bridge.
- Do not expose broad arbitrary code execution.

## Dependencies

- `TWF-008` Timberborn fixed-cadence dispatch, unless this ticket is implemented first as a bridge scaffold with placeholder status.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Validate in Timberborn if the local environment is available.

## Notes

- Keep the first bridge boring. A small reliable status endpoint is better than a large fragile harness.
- Worker update 2026-05-01:
  - Added `TimberbornQaCommandBridge` as the smallest in-process command/test harness scaffold.
  - Supported commands are read-only: `status` and `help`. Empty command text normalizes to `status`; unknown commands return failure without executing anything dynamic.
  - `TimberbornQaCommandResult.ResultToken` logs searchable `wildfire_command_result` fields for command, success/failure, simulator integration, dimensions, `tick_count`, `queued_changes`, and `last_delta_count`.
  - Before TWF-008 is integrated, simulator state intentionally reports `placeholder`; future TWF-008 runtime state can be supplied through `ITimberbornQaCommandStateProvider`.
  - Did not add arbitrary code execution, a mutating command, C# fire-spread parity, or Timberborn-owned fire rules.
  - Live validation is blocked because this repository still has no Timberborn UI, console, file-polling, or HTTP hook that invokes the bridge from inside the running game.
  - Smallest unblock: add a narrow Timberborn binding that forwards only known bridge commands to `TimberbornQaCommandBridge.Execute` and captures `wildfire_command_request` / `wildfire_command_result` in logs.
  - Evidence: `git diff --check` passed; `dotnet test` passed 57 tests; `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
- Worker review follow-up 2026-05-01:
  - Added narrow unit coverage for `TimberbornQaCommandBridge` safety and observability.
  - Covered `status`, empty command normalization to `status`, case/first-token normalization, `help`, unknown command failure, dynamic-looking command rejection, state-provider wiring, request/result logging, and result token fields for real and placeholder simulator state.
  - Focused evidence: `dotnet test --filter FullyQualifiedName~TimberbornQaCommandBridgeTests` passed 10 tests.
  - Full evidence: `git diff --check` passed; `dotnet test` passed 67 tests; `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
- Worker review follow-up 2026-05-01:
  - Made `TimberbornQaCommandBridge.Execute` defensive for null command text before normalization.
  - Deliberate behavior: null command text returns a logged failure token with `command=null` and does not query simulator state; blank command text remains the documented `status` shortcut.
  - Added deterministic regression coverage for the null-command path.
  - Evidence: `git diff --check` passed; `dotnet test` passed 68 tests; `dotnet build Wildfire.slnx` passed with 0 warnings and 0 errors.
