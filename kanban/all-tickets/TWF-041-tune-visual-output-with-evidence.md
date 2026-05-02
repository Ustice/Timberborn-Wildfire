---
ticket: TWF-041
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-039
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-041-tune-visual-output-with-evidence.md
---

# TWF-041: Tune Visual Output With Evidence

## Goal

Tune fire, smoke, ash, and visibility output using shader snapshots and live Timberborn visual evidence.

## Why

The design says exact visual constants should be tuned from shader snapshots and visual validation. After the visual field is bound, this ticket makes the output reviewable and repeatable instead of relying on subjective live impressions alone.

## Requirements

- Review visual-field channel derivation for fire, smoke, ash, and visibility.
- Add or update accepted shader snapshot evidence for at least two meaningful scenarios.
- Capture live visual evidence from a loaded Timberborn save.
- Keep packed-cell storage unchanged unless a design decision explicitly changes it.
- Document accepted constants, commands, artifact paths, and interpretation in `docs/TEST_PLAN.md`.
- If ash requires stored burn history, record that as a design decision or follow-up instead of silently adding a field.

## Dependencies

- `TWF-039` provides the visual-field binding needed for live validation.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness when shader behavior changes.
- QA must capture screenshots or artifacts showing the tuned output in Timberborn.

## Notes

- This ticket may decide that ash remains a temporary heat/fuel approximation for now.
