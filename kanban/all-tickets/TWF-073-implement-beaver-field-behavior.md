---
ticket: TWF-073
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-071
   - TWF-072
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-073-implement-beaver-field-behavior.md
---

# TWF-073: Implement Beaver Field Behavior

## Goal

Apply the accepted first-release beaver behavior for wildfire fields in live Timberborn.

## Why

Once beaver exposure can be detected, Wildfire needs beavers to respond coherently to dangerous or obstructive fields. The behavior should be understandable to players, safe for saves, and bounded by Timberborn APIs.

## Requirements

- Implement only the beaver behaviors accepted by `TWF-071`.
- Use `TWF-072` exposure telemetry as the source of evidence and QA observability.
- Keep behavior reversible and safe across save/reload where possible.
- Do not move fire rules into Timberborn; Timberborn may only react to field exposure.
- Add deterministic tests for behavior decision logic.
- Preserve logs, status counters, screenshots or recordings, command output, and final QA lock state.
- Document accepted live behavior evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-071` defines the accepted beaver behavior contract.
- `TWF-072` proves beaver exposure detection and telemetry.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture live evidence of one accepted beaver behavior response, plus proof that no critical exceptions appear in `Player.log`.

## Notes

- If the only safe release behavior is avoidance or explicit no-op telemetry, document that honestly and defer deeper injury/death/panic work.
