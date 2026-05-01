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
- Run `bun run test`.
- Run `dotnet build Wildfire.slnx`.
- Validate in Timberborn if the local environment is available.

## Notes

- Keep the first bridge boring. A small reliable status endpoint is better than a large fragile harness.
