---
ticket: TWF-031
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-019
   - TWF-030
write_scope:
   - src/Wildfire.Timberborn/**
   - scripts/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-031-live-nonzero-delta-stimulus.md
---

# TWF-031: Add Guarded Live Delta Stimulus

## Goal

Create the smallest safe QA-only path that can produce at least one non-zero simulator delta in a loaded Timberborn save.

## Why

The live simulator, command bridge, startup automation, and delta-consumer hooks are now wired, but current live evidence still reports `last_delta_count=0`. The next sprint needs a controlled way to make the simulator do observable work before optimizing it or binding broad gameplay consequences.

## Requirements

- Add a narrow allowlisted QA command or utility path for a deterministic fire stimulus.
- Keep the simulation core host-agnostic.
- Keep Timberborn as an adapter; it may register allowed external changes but must not own fire rules or mutate the grid directly.
- Prefer an explicit, bounded target such as one known loaded-save cell or a small fixed test patch.
- Reject unknown commands, broad arbitrary coordinates, destructive actions, and generic debug execution.
- Report enough command/status detail to identify the stimulus request, accepted target, queued change count, and subsequent simulator tick.
- Preserve existing read-only command behavior.
- Document the command, safety boundary, prerequisites, and expected evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-019` provides the allowlisted command bridge surface.
- `TWF-030` provides reliable startup/load/unpause automation for live QA.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `bun run typecheck`.
- Run `dotnet test`.
- Run any relevant command-script help or dry-run checks with `bun`.
- QA must run the stimulus in live Timberborn only after the save is loaded through the guarded startup utility.
- QA evidence must include command output, copied `Player.log`, and final lock state.

## Notes

- This ticket is not a gameplay tuning ticket.
- Do not add broad UI automation or arbitrary map editing.
- If no safe deterministic target can be chosen from current runtime data, stop with a blocker note that names the exact missing API or evidence.
