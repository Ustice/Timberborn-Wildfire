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

## Implementation Notes

- Build the tool under `scripts/**` with Bun and TypeScript; keep command parsing and artifact writing consistent with `scripts/load-latest-save-and-unpause.ts` and the shared WildfireQA artifact root.
- Use macOS-native capture tools first, likely `screencapture` for still bounds discovery and a native recorder path for clips, before adding dependencies.
- Include a dry-run plan that prints mode, output directory, intended resolution, duration, Timberborn PID, window or display target, lock behavior, and the command sequence without recording.
- Recordings should land in a timestamped artifact directory with a small manifest that names mode, resolution, frame rate when known, source bounds, save or scenario name, command outbox/inbox paths, copied `Player.log`, and output files.
- Ownership boundary: this ticket adds capture tooling and test-plan commands only. Visual tuning, behavior comparison, and video analysis remain in `TWF-066` through `TWF-070`.
- QA should capture one short high-resolution clip for visual readability and one short low-resolution clip for behavior timing, plus a dry-run transcript and final shared-lock state.

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `dotnet test` only if C# production or test files change.
- QA must capture one short high-resolution clip and one short low-resolution clip, plus copied command output and final QA lock state.

## Notes

- Prefer macOS-native capture facilities available on this machine before adding heavy dependencies.
- The tool should make artifact comparison easier; it does not need to perform video analysis in this ticket.
- Worker commit `b2838af` passed fresh Tech-Lead re-review after an earlier failed review. The review confirmed companion evidence metadata, dry-run/no-activate behavior, and display geometry limitations were corrected or documented.
- Live QA passed on 2026-05-03 with evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-065-live-screen-recording-20260503T051842Z`.
- High-resolution clip evidence is `clips/2026-05-03T05-20-27-659Z-high/recording.mov`, a 10-second `1920x1080` QuickTime MOV. Associated evidence shows `qa-delta-stimulus`, dispatch `tick=47 delta_count=2`, `active_pooled_effects=1`, `visible_effects_enabled=true`, `native_effect_prefab=CampfireFire`, and a player alert sent.
- Low-resolution clip evidence is `clips/2026-05-03T05-21-05-006Z-low/recording.mov`, a 12-second `1280x720` cropped QuickTime MOV. Associated command output passed with `qa-water-suppression-stimulus`, delayed `qa-readiness`, `last_positive_water_changed_tick=85`, and `last_positive_water_changed_count=1`.
- Additional evidence includes recording metadata JSON files, `dry-run-display-rect-output.txt`, no-activate frontmost-app proof that stayed on `Finder`, high/low key-event extracts, copied `Player.log` evidence, and `final-qa-lock-state.txt` showing no held lock files.
- A strict immediate high-run `qa-readiness --require-nonzero-delta` missed the transient `last_delta_count`; `Player.log` captured the actual nonzero dispatch at tick `47`, so this did not block the recording-tool QA gate.
