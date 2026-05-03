---
ticket: TWF-070
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
   - TWF-065
   - TWF-066
   - TWF-067
write_scope:
   - src/Wildfire.Timberborn/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-070-tune-visible-steam-effect.md
---

# TWF-070: Tune Visible Steam Effect

## Goal

Make steam readable as its own live visual effect when water suppression or wet hot cells produce a steam-like state.

## Why

Steam is different from smoke: it should communicate water meeting heat, not fuel burning. Wildfire already has water-suppression proof, and the pooled presentation path can resolve native steam-like prefabs such as `SteamEngineSmoke`. Release tuning needs a separate pass so suppression feedback is visible and not confused with smoke or ash.

## Requirements

- Use the existing pooled presentation and visual-field surface.
- Prefer Timberborn-native steam or vapor-like prefabs before custom art.
- Tune presentation concerns such as prefab choice, scale, placement, lifetime, intensity thresholds, and water-versus-smoke selection.
- Keep water suppression semantics in the GPU simulation and adapter inputs; do not add Timberborn-owned fire rules.
- Capture high-resolution recordings and screenshots showing steam as distinct from fire and smoke.
- Preserve command output, copied `Player.log`, artifact paths, and final QA lock state.
- Document accepted steam-effect evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides the recording tool.
- `TWF-066` and `TWF-067` establish the active fire and smoke baselines that steam must remain visually distinct from.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture high-resolution recording evidence plus status/log proof of steam-effect selection or the explicit reason steam remains deferred.

## Notes

- If current visual-field channels cannot distinguish steam cleanly from smoke, record the smallest shader or visual-field follow-up instead of forcing misleading presentation.
