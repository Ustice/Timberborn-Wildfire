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
  - TWF-162
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

Steam is clean in Wildfire. Do not add toxic steam, contaminated steam, or a steam contamination lane while tuning this effect.

## Requirements

- Use the existing pooled presentation and visual-field surface.
- Prefer field-based steam or vapor presentation over one effect object per wet hot cell.
- Use compact deltas only to wake or bound visual regions; do not map one changed cell to one visible effect.
- Prefer Timberborn-native steam or vapor-like prefabs before custom art.
- Tune presentation concerns such as prefab choice, scale, placement, lifetime, intensity thresholds, and water-versus-smoke selection.
- Keep water suppression semantics in the GPU simulation and adapter inputs; do not add Timberborn-owned fire rules.
- Read simulator steam state directly; do not infer steam from water-delta shortcuts.
- Capture high-resolution recordings and screenshots showing steam as distinct from fire and smoke.
- Preserve command output, copied `Player.log`, artifact paths, and final QA lock state.
- Document accepted steam-effect evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides the recording tool.
- `TWF-066` and `TWF-067` establish the active fire and smoke baselines that steam must remain visually distinct from.
- `TWF-162` aligns steam transport and authority with the smoke/ash field model.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture high-resolution recording evidence plus status/log proof of steam-effect selection or the explicit reason steam remains deferred.

## Notes

- If current visual-field channels cannot distinguish steam cleanly from smoke, record the smallest shader or visual-field follow-up instead of forcing misleading presentation.
- Relevant source-of-truth note: [docs/steam-simulation-model.md](../../docs/steam-simulation-model.md).
- Relevant design references: `docs/DESIGN.md` section 17 and `docs/ARCHITECTURE.md` "Field Visual Presentation Service".
- Readability repair is parked on `~/repos/wildfire-TWF-070-steam-visual` / `codex/TWF-070-steam-visual` as uncommitted work after commit `07875a16`. It retunes clean field-driven steam to `puffs_per_cell=8`, `base_color=0.92,0.98,1.00`, `radius=1.18`, `height_offset=0.08`, `max_height=2.85`, `max_opacity=0.72`, `up_speed=3.10`, and `down_speed=0.70`, with a dedicated `wildfire_timberborn_gpu_indirect_renderer_steam_tuning` log token. Fresh review is required before integration because this repair changes the previously reviewed renderer constants.
- Coordinator review reran `dotnet test Wildfire.slnx --no-restore --verbosity minimal --filter "FullyQualifiedName~TimberbornGpuFieldRendererTests"` on 2026-05-20: passed `8/8`. Worker also reported full `dotnet test` `451/451`, `dotnet build`, and `git diff --check`.
- Live visual QA rerun on 2026-05-20 is still blocked, but for visual quality rather than command availability. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-070-steam-live-20260520T060819Z/`. The TWF-070 branch deployed successfully and `wildfire_timberborn_gpu_indirect_renderer_steam_tuning field_source=atmospheric_fields clean=true contaminated=false puffs_per_cell=5 base_color=0.98,0.99,1.00 radius=0.92 height_offset=0.03 max_height=2.05 max_opacity=0.48 up_speed=2.35 down_speed=1.20` appeared in `Player.log`. `qa-water-suppression-stimulus tree` queued water on target `x=1 y=0 z=4`; after unpausing, `qa-readiness --require-water-changed` passed with `tick_count=1036`, `last_positive_water_changed_count=1`, `last_delta_consumer_visual_effect_events=1`, `last_delta_consumer_visual_effect_failures=0`, and `gpu_field_renderer_material_ready=true`. However, the loaded `Fuel` autosave was already ash-saturated (`ash_field_entries=750`), and high-resolution recording frames showed a dark ash field plus active fire and gray/black smoke rather than clean white near-ground/rising steam. Do not accept this ticket until rerun on a cleaner baseline or until the visual presentation is adjusted enough that steam remains distinct from smoke/fire/ash.
- 2026-05-20 live QA rerun accepted by Jason after the readability repair. Evidence root: `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-070-steam-readability-rerun-20260520T143703Z/`. The normal startup utility stopped safely because macOS reported a `3840 x 2160` physical capture while the coordinate guide assumes `1920 x 1080`, so QA launched Timberborn directly and preserved manual startup screenshots. `Player.log` opened `~/Documents/Timberborn/ExperimentalSaves/Fuel/Fuel.timber`; initial `qa-readiness` proved `ash_field_entries=0`, `loaded_game_ready=true`, `simulator_integrated=true`, and `gpu_field_renderer_material_ready=true`. `qa-delta-stimulus tree` followed by `qa-water-suppression-stimulus tree` produced `last_positive_water_changed_count=2`, `last_delta_consumer_visual_effect_events=311`, and `last_delta_consumer_visual_effect_failures=0`. High-resolution recording evidence is at `recordings/2026-05-20T14-53-24-316Z-high/recording.mov` with extracted frames under `frames/`; Jason reviewed the live view and said the steam is subtle but visible and looks good. Move out of `07-blocked`; fresh review is still required before integration because the readability repair changed renderer constants after the previous deterministic review.
