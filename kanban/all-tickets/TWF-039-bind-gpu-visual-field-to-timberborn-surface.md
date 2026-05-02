---
ticket: TWF-039
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-037
write_scope:
   - src/Wildfire.Unity/**
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-039-bind-gpu-visual-field-to-timberborn-surface.md
---

# TWF-039: Bind GPU Visual Field To Timberborn Surface

## Goal

Expose the GPU visual field through a Timberborn visual surface so fire, smoke, ash, or heat intensity can be rendered from simulator output.

## Why

The design says GPU visuals may stay GPU-side while gameplay reactions go through C# deltas. The repository already has visual-field buffer and checksum proof; the next design step is to connect that output to a real Timberborn-facing surface without turning visual cells into entities.

## Requirements

- Keep gameplay consequences separate from GPU visual output.
- Use the existing visual-field channels: fire, smoke, ash, and heat or visibility.
- Avoid one Timberborn entity per simulated cell.
- Add the smallest binding needed to render or inspect visual-field data in Timberborn.
- Preserve shader snapshot and checksum evidence for visual-field writes.
- Add deterministic tests for binding configuration or data routing where possible.
- Document live visual QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-037` gives a delta-driven inspection path and confirms the changed-cell surface is useful before richer visual binding.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness if shader or visual-field behavior changes.
- QA must capture live screenshots or visual artifacts plus relevant `Player.log` evidence.

## Notes

- If Timberborn requires a different visual bridge than the current buffer abstraction supports, record the minimal adapter change instead of adding a parallel renderer.
