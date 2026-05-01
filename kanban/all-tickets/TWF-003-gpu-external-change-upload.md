---
ticket: TWF-003
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-001
   - TWF-002
write_scope:
   - src/Wildfire.Core/**
   - src/Wildfire.Unity/**
   - tests/**
---

# TWF-003: Upload External Changes

## Goal

Implement the path from `RegisterChange(FireSimChange change)` to a GPU-side change application pass. Registered changes should apply at the start of the next GPU tick.

## Why

Timberborn systems need a safe way to tell the simulator about heat, water, terrain, and fuel changes. Queuing changes until the next dispatch keeps mutation centralized and prevents recursive update chains.

## Requirements

- Store registered changes in the Unity GPU simulator wrapper until `Tick()`.
- Upload queued changes to a GPU buffer before the simulation pass.
- Add a shader pass or kernel that applies `SetCell`, additive fields, and individual setters.
- Ignore or safely report out-of-range cell indices.
- Clear the uploaded change queue after successful dispatch.
- Preserve the rule that listeners and hosts never mutate buffers directly.
- Add tests for queue behavior in the wrapper where possible.

## Dependencies

- `TWF-001` buffer/grid scaffold.
- `TWF-002` full-grid shader baseline.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run test`.
- Run `dotnet build Wildfire.slnx`.

## Notes

- Be careful with additive overflow. Match the packed-cell field limits instead of allowing wraparound.
