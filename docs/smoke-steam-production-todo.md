# Smoke And Steam Production Todo

## Current Goal

Make smoke and steam release-ready in `~/repos/wildfire-smoke-steam-release-analysis` without CPU-bound visuals.

## Hard Boundaries

- Do not add CPU-bound visual objects, pooled GameObjects, or per-cell Timberborn visual entities.
- Keep the simulation core host-agnostic.
- Keep the visual path GPU-buffer/shader-driven.
- Temporary screenshots are for inspection only. Delete them before final.
- Do not end the turn until the visuals pass a critical in-game review or a real blocker is documented. This is the active session guardrail if context is compressed.

## Ordered Todo

1. Commit the current working state after the first puff shader/opacity pass.

   - Current checks already run: `git diff --check` and `dotnet test --filter TimberbornGpuFieldRendererTests`.
   - Stage intentionally and commit the current worktree state before deeper iteration.

2. Increase opacity slightly from the current deployed version.

   - Jason said the current pass is better but still needs more opacity.
   - Keep puff count stable unless later inspection proves count is the issue.

3. Implement intensity-aware smoke puff presentation.

   - Use smoke intensity to affect puff size and opacity.
   - Use deterministic/stochastic-looking placement and shade jitter.
   - Avoid every cell always having the same visible three puffs.
   - Existing puffs should not jump around just because smoke intensity updates; changes should mainly affect newly visible/newer puff slots.
   - Prefer deterministic hash/phase/lifecycle in shader or GPU-side buffers over CPU-bound state.

4. Deploy and launch Timberborn after each serious visual iteration.

   - Use `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60`.
   - Launch with `open "$HOME/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app"`.

5. Review in-game critically.

   - Use x3 speed to accelerate inspection.
   - Take three temporary screenshots over time.
   - Inspect the whole scene: smoke readability, grid artifacts, jumps, UI obstruction, fire/smoke/steam confusion, color/shade, opacity, and whether the result looks production-ready.
   - Delete the screenshots after inspection.

6. Iterate until release-ready.

   - If stochastic placement does not work, try another GPU/shader-driven approach.
   - Do not use region aggregation unless later evidence shows shader/lifecycle variation cannot solve it.
   - Re-run `git diff --check`, focused tests, deploy dry-run/build as needed, and final live visual inspection.
