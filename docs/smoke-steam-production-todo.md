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

1. Done: commit the first puff shader/opacity pass.

   - Current checks already run: `git diff --check` and `dotnet test --filter TimberbornGpuFieldRendererTests`.
   - Stage intentionally and commit the current worktree state before deeper iteration.

2. Done: increase opacity from the first deployed version.

   - Jason said the current pass is better but still needs more opacity.
   - Keep puff count stable unless later inspection proves count is the issue.

3. Done: implement intensity-aware smoke puff presentation.

   - Use smoke intensity to affect puff size and opacity.
   - Use deterministic/stochastic-looking placement and shade jitter.
   - Avoid every cell always having the same visible three puffs.
   - Existing puffs should not jump around just because smoke intensity updates; changes should mainly affect newly visible/newer puff slots.
   - Prefer deterministic hash/phase/lifecycle in shader or GPU-side buffers over CPU-bound state.

4. Done: deploy and launch Timberborn after the soil-contamination steam/smoke revision.

   - Use `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60`.
   - Launch with `open "$HOME/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app"`.
   - Latest live inspection reached the contaminated soil target and showed blackened trees.
   - A first contaminated plume pass read as a red rectangular smear; shader breakup, tint, and per-puff billboard rotation were adjusted and redeployed.

5. Done: review in-game critically.

   - Use x3 speed to accelerate inspection.
   - Take three temporary screenshots over time.
   - Inspect the whole scene: smoke readability, grid artifacts, jumps, UI obstruction, fire/smoke/steam confusion, color/shade, opacity, and whether the result looks production-ready.
   - Delete the screenshots after inspection.
   - Final x3 screenshots showed wind-reactive contaminated smoke with irregular puff silhouettes, no grid of translucent balls, and no large red panel artifact.
   - Water-suppression telemetry produced water-change proof, but did not create a useful visible steam plume from this Fuel camera pass; steam release tuning is covered by the GPU renderer/shader tests.

6. Done: iterate until release-ready.

   - If stochastic placement does not work, try another GPU/shader-driven approach.
   - Do not use region aggregation unless later evidence shows shader/lifecycle variation cannot solve it.
   - Re-run `git diff --check`, focused tests, deploy dry-run/build as needed, and final live visual inspection.
   - Final checks passed: `git diff --check` and `dotnet test --filter "TimberbornFireCellMapperTests|TimberbornGpuFieldRendererTests|TimberbornQaCommandBridgeTests|UnityShaderExecutionHarnessTests"`.

7. Done: replace adjacency contamination with per-cell soil contamination.

   - Contaminated smoke must use the imported soil/groundwater contamination level for the cell, not adjacent badwater or neighbor source inference.
   - The current implementation packs a 0-7 soil contamination band into companion-field bits 25-27 and reads that in `FireSim.compute`.
   - Focused tests passed: `dotnet test --filter "TimberbornFireCellMapperTests|TimberbornGpuFieldRendererTests|TimberbornQaCommandBridgeTests"`.
