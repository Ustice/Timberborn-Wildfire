---
ticket: TWF-027
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-025
write_scope:
  - docs/timberborn-debug-panels.md
  - docs/reference/screenshots/timberborn-debug-panels/**
---

# TWF-027: Document Timberborn Debug Panels

## Goal

Create a Timberborn debug-panel reference that explains what debug panels are available, how to open them, and which panels matter for Wildfire QA and development.

## Why

Wildfire QA and live validation will lean on Timberborn's debug tools. A focused reference should prevent repeated manual discovery and make it clearer which panels are useful, risky, or irrelevant for mod validation.

## Requirements

- Open Timberborn debug/development panels using the verified `Alt-Shift-Z` path or another documented Timberborn debug entrypoint if evidence shows it is required.
- Identify each relevant debug panel, tab, or submenu visible in the loaded-save state.
- Capture clean screenshots for the important panels.
- Document what each panel is for, what useful state or controls it exposes, and why it matters or does not matter for Wildfire.
- Call out panels or controls that are unsafe for routine QA because they mutate game state.
- Keep this as a reference document, not an automation script.
- Do not rely on contaminated screenshots that show Codex or another desktop app.
- Do not change gameplay code, scripts, or unrelated documentation.

## Dependencies

- `TWF-025` owns the broad bottom-menu and developer-menu screenshot sweep. Use its findings as the entry map before writing this deeper debug-panel reference.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Confirm screenshots have expected dimensions or documented crop/composite dimensions.
- Inspect screenshots to ensure they show Timberborn debug panels, not Codex or another desktop app.
- Verify the guide links render to existing image paths.

## Notes

- This ticket should favor concrete panel names, visible controls, and QA relevance over generic prose.
- If a panel cannot be captured safely, document the blocker and the smallest user action needed.

## Coordinator Notes

- 2026-05-01: Initial worker started in `~/repos/wildfire-TWF-027` and captured one partial screenshot at `docs/reference/screenshots/timberborn-debug-panels/01-expanded-debug-and-object-debugger.png`, but did not produce the guide or ticket evidence and stopped responding to status requests.
- Current blocker: ticket needs a fresh worker pickup from the partial worktree or a clean restart. Smallest next action is to inspect `~/repos/wildfire-TWF-027`, decide whether to reuse the partial screenshot, then complete the guide/evidence within the ticket scope.
- 2026-05-01 worker checkpoint: created the assigned worktree `/Users/jasonkleinberg/repos/wildfire-TWF-027` on branch `codex/TWF-027-debug-panels`; read AGENTS, docs index, kanban board docs, delegate skill, Worker role, this ticket, and the TWF-025 bottom-menu guide from the main checkout because those coordinator files were not committed on `main`.
- 2026-05-01 worker checkpoint: verified TWF-025 clean debug evidence exists in the main checkout: `33-debug-panel-alt-shift-x.png`, `34-debug-panel-object-selected.png`, `35-debug-mode-panel-expanded.png`, `36-object-debugger-panel-expanded.png`, and `composite-dev-debug-panels.png`. The four source screenshots are `1920 x 1080`; the composite is `1862 x 5664`.
- 2026-05-01 worker checkpoint: live Timberborn was available and showed the expanded `Debug mode panel` plus `Object debugger`; visible debug sections included Automation, Performance, Cursor, Mesh metrics, Sound system, Mechanical system, Navigation, Terrain columns, Parallel singletons, Time scale, Clock, Water rendering data, Water rendering times, Water columns, and Weather.
- 2026-05-01 blocker/partial-progress note: an attempted fresh `screencapture` in the TWF-027 screenshot folder captured VS Code instead of Timberborn and is contaminated. It must not be used in the guide. Next exact action is to remove or overwrite that contaminated capture, then take a clean Timberborn-only focused screenshot set before writing `docs/timberborn-debug-panels.md`.

## QA Closeout - 2026-05-01

- QA inspected `/Users/jasonkleinberg/repos/wildfire-TWF-027/docs/reference/screenshots/timberborn-debug-panels/01-expanded-debug-and-object-debugger.png`.
- Result: failed. The image is a `1920 x 1080` VS Code desktop capture, not a Timberborn debug-panel capture.
- No `docs/timberborn-debug-panels.md` guide exists in the worktree, so the ticket requirements are not met.
- This ticket remains blocked rather than done. The partial worktree can be removed after preserving this blocker note because it contains no acceptable deliverable.
- Smallest unblock: capture clean Timberborn-only screenshots of the debug panels from the loaded-save state, write `docs/timberborn-debug-panels.md`, verify image dimensions and links, then rerun `git diff --check`.

## Worker Results - 2026-05-01

- Created `docs/timberborn-debug-panels.md` as the focused Timberborn debug-panel reference.
- Used only the clean main-checkout screenshots under `docs/reference/screenshots/timberborn-debug-panels/`: `01-loaded-save-baseline.png`, `02-developer-mode-alt-shift-z.png`, and `03-debug-panel-alt-shift-x.png`.
- Documented the verified `Alt-Shift-Z` developer-mode entrypoint and `Alt-Shift-X` debug-panel entrypoint.
- Documented visible Development controls, Debug mode panel sections, `Performance` metrics, Object debugger singleton context, Wildfire QA relevance, and unsafe mutation controls.
- Verified all three linked screenshot paths exist and are `1920 x 1080` with `sips -g pixelWidth -g pixelHeight`.
- Visually inspected all three screenshots to confirm they show Timberborn, with the debug/developer panels visible where expected, not Codex or another desktop app.
- Ran `git diff --check`; result passed.
- Did not touch board symlinks or unrelated docs/code.
- Blockers: none for the focused reference. Deeper object-property safety remains intentionally undocumented because the provided evidence only shows the singleton list, not expanded object rows.

## Coordinator Verification - 2026-05-01

- Inspected `docs/timberborn-debug-panels.md` and verified it covers the captured Development controls panel, Debug mode panel, Object debugger, QA relevance, and unsafe mutation controls.
- Verified screenshot paths referenced by the guide exist under `docs/reference/screenshots/timberborn-debug-panels/`.
- Re-ran `sips -g pixelWidth -g pixelHeight docs/reference/screenshots/timberborn-debug-panels/*.png`; all three images are `1920 x 1080`.
- Re-ran `git diff --check`; passed.
- Accepted the ticket for `06-done`.
