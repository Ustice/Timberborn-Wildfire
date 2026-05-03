---
ticket: TWF-065
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-046
write_scope:
   - scripts/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-065-add-screen-recording-qa-tool.md
---

# TWF-065: Add Screen Recording QA Tool

## Goal

Add a repeatable screen-recording QA tool that can capture high-resolution clips for visual-effect tuning and low-resolution clips for fire-behavior tuning.

## Why

Current QA evidence proves visual-effect activation mostly through logs, status counters, and still screenshots. Tuning fire, smoke, ash, and fire behavior needs time-based evidence: high-resolution recordings for effect readability and lower-resolution recordings for comparing spread, suppression, and burnout behavior across runs.

## Requirements

- Use Bun and TypeScript for the tool.
- Provide a high-resolution recording mode for visual effects.
- Provide a low-resolution recording mode for fire behavior and fast comparison.
- Save recordings into a timestamped WildfireQA artifact directory.
- Capture enough metadata to make clips reviewable later: mode, resolution, frame rate when available, command sequence, save or scenario name, Timberborn PID, source display or window bounds, and output paths.
- Include a safe dry-run or plan mode that prints what would be captured.
- Work with the existing guarded Timberborn QA flow instead of introducing broad UI automation.
- Document exact commands and expected artifact layout in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-046` proves the coherent live loop that the recording tool should capture.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `dotnet test` only if C# production or test files change.
- QA must capture one short high-resolution clip and one short low-resolution clip, plus copied command output and final QA lock state.

## Notes

- Prefer macOS-native capture facilities available on this machine before adding heavy dependencies.
- The tool should make artifact comparison easier; it does not need to perform video analysis in this ticket.
