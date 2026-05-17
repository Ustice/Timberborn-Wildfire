# Codex Goal Prompt

In `~/repos/wildfire-fire-sim-field-model`, improve the current FireSim field-model branch so live fire behavior is slower, more readable, visually debuggable, and does not cause large per-tick pauses.

## Observed Problem From Live Timberborn

- Fire spreads way too fast after the field-model change.
- Each simulation tick causes noticeable pauses or hitches.
- Contamination spread is not visually inspectable enough.
- Contaminated smoke should tint burgundy, with stronger tint as contamination increases.

Treat these as related but separate failures:

- Gameplay tuning.
- Tick performance.
- Contamination visualization.

## Start By Reading

- `AGENTS.md`
- `docs/fire-sim-field-model-plan.md`
- `docs/reference/timberborn-deploy-pipeline.md`
- `src/Wildfire.Unity/FireSim.compute`
- `src/Wildfire.Unity/UnityComputeFireSimulator.cs`
- `src/Wildfire.Timberborn/TimberbornComputeFireSimulator.cs`
- `src/Wildfire.Timberborn/TimberbornGpuFieldRenderer.cs`
- `tests/Wildfire.Core.Tests/UnityShaderExecutionHarnessTests.cs`

## Constraints

- Keep the simulation core host-agnostic.
- Timberborn is an adapter; do not put fire rules in Timberborn systems.
- Prefer deterministic tests and Unity shader harness fixtures before live Timberborn validation.
- Keep the five current field-model tuning levers unless you stop and justify changing the plan.
- Do not solve the hitch by simply hiding ticks or skipping validation. Find the source of the stall.
- Do not add new full-size simulation buffers.
- Use `bun`, not `npm`.

## Implementation Goals

1. Add or adjust deterministic shader-harness tests that prove a slow, reactable fire:

   - A single ignition should expand over multiple ticks, not fill the reachable area immediately.
   - Wind should bias shape without turning the first tick into a large downwind jump.
   - Water/moisture should still visibly slow ignition.
   - Fuel burn-down should not instantly consume normal sources unless the source is intentionally low-fuel.

2. Tune the heat model in `FireSim.compute`:

   - Investigate `HeatReach`, `WindStretch`, `HeatKernelWeight`, and how heat is applied in `StepCell`.
   - Prefer per-tick heat-pressure limits, slower heat accumulation, or smaller effective reach over magic one-off clamps.
   - Preserve readable radial no-wind behavior and downwind/crosswind/upwind ordering.
   - Avoid adding new full-size simulation buffers.

3. Diagnose and reduce per-tick pauses:

   - Profile whether the hitch comes from shader loop cost, full-grid dispatch/readback, delta append volume, asset bundle/deploy state, logging, or Timberborn adapter work.
   - Check whether `HEAT_SEARCH_RADIUS=7` or nested per-cell neighborhood scans are too expensive for live grids.
   - If needed, reduce search radius, split expensive logic, add active-region gating, or make dispatch cadence safer, but keep deterministic proof.
   - Add telemetry or tests that make the stall source visible.

4. Make contamination visually inspectable:

   - Show contamination spreading through the existing visual field path.
   - Tint smoke burgundy based on smoke contamination intensity: clean smoke stays normal, lightly contaminated smoke is subtly burgundy, heavily contaminated smoke is clearly burgundy.
   - Keep ash contamination visually distinct if there is already a safe visual channel for it, but do not add a new full-size simulation buffer.
   - Add deterministic tests for packed atmospheric contamination to visual output mapping.
   - Add a live/debug validation surface so a contaminated source can prove that contaminated smoke moves downwind and remains visually distinguishable from clean smoke.
   - Preserve the existing fire/smoke/ash readability; burgundy contamination should be a tint, not an opaque overlay.

## Validation

- `git diff --check`
- `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter FullyQualifiedName~UnityShaderExecutionHarnessTests`
- `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --filter FullyQualifiedName~UnityShaderExecutionHarnessTests`
- `dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj`
- `bun run typecheck`
- Rebuild and redeploy with `bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60`

## Live Check After Deploy

- Start Timberborn only after deploy succeeds and the deploy lock is released.
- Observe a local ignition on a real save.
- Confirm that fire is slow/reactable.
- Confirm that tick hitches improved.
- Confirm that contaminated smoke is visibly burgundy-tinted and distinguishable from clean smoke.
- If live validation cannot be completed, document the exact blocker and next command.

## Done When

- Fire spread is visibly slower and reactable.
- Wind still shapes spread without explosive jumps.
- Tick hitches are explained and materially reduced, or the exact remaining bottleneck is documented with evidence.
- Contaminated smoke is visibly burgundy-tinted in proportion to contamination strength.
- Clean smoke and contaminated smoke are distinguishable in deterministic tests and live/debug validation.
- Tests and build/deploy validation are reported clearly.
