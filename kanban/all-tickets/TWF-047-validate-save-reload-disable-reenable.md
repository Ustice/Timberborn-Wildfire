---
ticket: TWF-047
agent_level: High
role: qa
requires_qa: true
doc_only: false
dependencies:
   - TWF-046
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - docs/HANDOFF.md
   - kanban/all-tickets/TWF-047-validate-save-reload-disable-reenable.md
---

# TWF-047: Validate Save Reload Disable Reenable

## Goal

Prove Wildfire behaves safely across save, reload, mod disable, and mod re-enable flows.

## Why

Players will not only run a perfect single session. Release needs confidence that active or recently active fire state does not corrupt saves, trap players, or make the save unrecoverable when the mod is absent.

## Requirements

- Validate saving and reloading a game after fire activity.
- Validate restart and reload with Wildfire still enabled.
- Validate disabling or removing the mod leaves the save recoverable, or document a clear limitation if Timberborn cannot support that safely.
- Validate re-enabling the mod rebuilds runtime state or reports a safe degraded state.
- Add save/load code or tests only if validation exposes a real gap.
- Preserve logs, screenshots, commands, save names, and artifact paths.
- Update `docs/TEST_PLAN.md` and `docs/HANDOFF.md` with accepted evidence or blockers.

## Dependencies

- `TWF-046` proves the coherent live gameplay loop before save lifecycle hardening.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test` if production code changes are made.
- QA must capture live save/reload evidence and confirm no new critical exceptions appear in `Player.log`.

## Notes

- Prefer recoverable behavior over persisted simulation complexity. Computed caches should be rebuildable after load.
