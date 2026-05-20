---
ticket: TWF-067
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-065
  - TWF-066
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-067-tune-visible-smoke-effect.md
---

# TWF-067: Tune Visible Smoke Effect

## Goal

Make smoke readable as a distinct visual state from active flame in live Timberborn recordings.

## Why

The visual field already derives smoke, but release tuning needs a separate pass so smoke does not become either invisible or visually confused with fire. Smoke should help players understand active or recently active burning without adding new simulation rules.

The current design requires smoke to read as a field or volume rather than a tile-by-tile effect. This ticket owns the smoke side of that presentation contract.

## Requirements

- Use the existing pooled presentation and visual-field surface.
- Follow `docs/DESIGN.md` section 17: cluster, blur, threshold, or otherwise aggregate neighboring smoke intensity into larger coherent regions.
- Use compact deltas only to wake or bound visual regions; do not map one changed cell to one visible effect.
- Prefer Timberborn-native smoke prefabs and conventions before custom art.
- Tune presentation concerns such as prefab choice, scale, placement, lifetime, intensity thresholds, and fire-versus-smoke selection.
- Keep smoke derived from simulator visual output; do not add Timberborn-owned fire rules.
- Capture high-resolution recordings and screenshots showing smoke as distinct from fire.
- Preserve command output, copied `Player.log`, artifact paths, and final QA lock state.
- Document accepted smoke-effect evidence in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-065` provides the recording tool.
- `TWF-066` establishes the fire-effect baseline that smoke must remain visually distinct from.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- QA must capture high-resolution recording evidence plus status/log proof of smoke-effect selection or the explicit reason smoke remains deferred.

## Notes

- If native smoke prefabs are too visually heavy or too subtle, record the smallest next production step instead of blending smoke into the fire pass.
- Relevant design references: `docs/DESIGN.md` section 17 and `docs/ARCHITECTURE.md` "Field Visual Presentation Service".
- 2026-05-19 coordinator reconciliation: moved back to `03-in-progress` because `~/repos/wildfire-twf-067-smoke-effect` contains unintegrated Timberborn smoke renderer tuning and TEST_PLAN updates, while `main` also has a separate smoke-movement shader commit (`7f5caae`). Next action is reviewer/tech-lead reconciliation of the worktree diff against current `main`, then QA only after the accepted smoke patch is on the integration path.
- 2026-05-19 reviewer result: failed against current `main` (`7f5caae`). The intended smoke renderer tuning looks scoped, but the worktree is stale and would regress main smoke movement/dilution tests plus ash-model `docs/TEST_PLAN.md` wording. Worker must rebase or cherry-pick only the intended smoke renderer, test, and QA-plan hunks onto current `main` before QA.
- 2026-05-19 worker repair in `~/repos/wildfire-twf-067-smoke-effect`: reconciled onto `main` commit `7f5caae`, preserved main's smoke movement/dilution tests and simulator-owned ash `docs/TEST_PLAN.md` wording, and kept the diff scoped to smoke renderer tuning, test assertions, and the smoke QA evidence paragraph. Verification passed `git merge --ff-only main`, targeted `TimberbornGpuFieldRendererTests` (`7/7`), `git diff --check`, and full `dotnet test` (`442/442`). Next action is fresh review, then live Timberborn smoke readability QA if review passes.
- 2026-05-19 reviewer result after worker repair: passed against `main` commit `7f5caae`. Worktree `HEAD` matches `main`, diff is limited to Timberborn smoke renderer tuning, matching renderer assertions, and the `TWF-067` smoke QA evidence paragraph. Main smoke movement/dilution tests and shader files are preserved. Reviewer reran `git diff --check`, targeted `TimberbornGpuFieldRendererTests` (`7/7`), and full `dotnet test` (`442/442`). Next action: live Timberborn smoke readability QA.
- 2026-05-19 live QA result: passed. Evidence root `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-067-smoke-live-20260519T085349Z` includes high-resolution recording and still frames showing a distinct gray/brown smoke volume above the central active yellow flame in normal gameplay. Runtime proof includes command-responsive `Fuel` save, `qa-delta-stimulus tree`, `tick_count=1141`, `visual_field_surface_bound=true`, the smoke tuning token (`puffs_per_cell=8 radius=1.38 height_offset=3.55 max_opacity=0.74 up_speed=2.80 down_speed=0.72`), and a clean renderer/presentation failure scan. One desktop overlay partly covered a separate bottom-right burn area, but the primary central smoke evidence is unobstructed.
- 2026-05-19 integration result: accepted diff integrated into `main`, scoped to `TimberbornGpuIndirectFireRenderer.cs`, `TimberbornGpuFieldRendererTests.cs`, and the `docs/TEST_PLAN.md` smoke evidence paragraph. Validation passed `git diff --check`, targeted `TimberbornGpuFieldRendererTests` (`7/7`), and full `dotnet test` (`442/442`).
