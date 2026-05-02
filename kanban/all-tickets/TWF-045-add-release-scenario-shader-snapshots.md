---
ticket: TWF-045
agent_level: Medium
role: worker
requires_qa: false
doc_only: false
dependencies:
   - TWF-043
   - TWF-044
write_scope:
   - src/Wildfire.Cli/**
   - src/Wildfire.Unity/**
   - tests/Wildfire.Core.Tests/**
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-045-add-release-scenario-shader-snapshots.md
---

# TWF-045: Add Release Scenario Shader Snapshots

## Goal

Add accepted shader snapshot coverage for the scenarios that should protect release behavior.

## Why

The test plan names release-relevant scenarios, but the release gate needs accepted evidence after game-feel tuning settles. Snapshot coverage should make shader behavior reviewable without launching Timberborn every time.

## Requirements

- Add or refresh accepted snapshots for single ignition, line of fuel, water barrier, vertical fuel column, sparse forest, building cluster, and mixed terrain/fuel/water unless a scenario is explicitly deferred.
- Record seed, dimensions, tick count, final packed cells, per-tick delta counts, and useful visual checksums or artifacts.
- Keep snapshot changes intentional and reviewable scenario by scenario.
- Ensure append-buffer counter reset behavior remains covered.
- Update `docs/TEST_PLAN.md` with commands and accepted artifact locations.

## Dependencies

- `TWF-043` settles game-feel constants.
- `TWF-044` settles or defers release-blocking simulation decisions.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run the opt-in Unity shader harness for any snapshots that require real shader execution.

## Notes

- Do not bless broad snapshot churn without explaining the behavior change that caused it.

Worker pass on 2026-05-02 in worktree `~/repos/wildfire-TWF-045`.

Accepted release snapshot artifact directory:

- `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/`

Accepted scenarios:

- `single-ignition`, seed `21`, grid `5x5x1`, ticks `2`: per-tick deltas `[5, 5]`, final hot cells `5`, burning cells `0`, max heat `7`, water cells `0`, fuel total `175`, checksum `visual-fnv1a32:50C4978E`.
- `line-of-fuel`, seed `42`, grid `12x5x1`, ticks `4`: per-tick deltas `[5, 5, 5, 2]`, final hot cells `5`, burning cells `1`, max heat `12`, water cells `0`, fuel total `104`, checksum `visual-fnv1a32:120F70AE`.
- `water-barrier`, seed `42`, grid `12x5x1`, ticks `4`: per-tick deltas `[5, 5, 5, 5]`, final hot cells `1`, burning cells `0`, max heat `2`, water cells `5`, fuel total `385`, checksum `visual-fnv1a32:40818F57`.
- `vertical-fuel-column`, seed `17`, grid `5x5x4`, ticks `4`: per-tick deltas `[6, 6, 2, 1]`, final hot cells `6`, burning cells `1`, max heat `11`, water cells `0`, fuel total `44`, checksum `visual-fnv1a32:5F05530F`.
- `sparse-forest`, seed `73`, grid `16x10x1`, ticks `3`: per-tick deltas `[5, 5, 5]`, final hot cells `5`, burning cells `1`, max heat `12`, water cells `0`, fuel total `978`, checksum `visual-fnv1a32:E4355BFA`.
- `building-cluster`, seed `91`, grid `14x10x1`, ticks `3`: per-tick deltas `[5, 1, 5]`, final hot cells `1`, burning cells `0`, max heat `1`, water cells `0`, fuel total `1179`, checksum `visual-fnv1a32:D12ED5D7`.
- `mixed-terrain`, seed `123`, grid `16x10x3`, ticks `3`: per-tick deltas `[6, 6, 6]`, final hot cells `5`, burning cells `0`, max heat `4`, water cells `10`, fuel total `3286`, checksum `visual-fnv1a32:67BFDEEA`.

Files changed:

- `src/Wildfire.Cli/ScenarioCatalog.cs`: aligned release `building-cluster` cells to the TWF-043 wood-like building band.
- `tests/Wildfire.Core.Tests/ShaderSnapshots/release/*.capture.json`: committed exact accepted release capture JSONs with full final packed cells and per-tick old/new delta records.
- `tests/Wildfire.Core.Tests/UnityShaderExecutionHarnessTests.cs`: opt-in Unity harness now asserts all seven accepted release scenarios scenario-by-scenario with seed, dimensions, ticks, delta counts, final semantic summary, and visual checksum.
- `tests/Wildfire.Core.Tests/UnityComputeFireSimulatorTests.cs`: added repeated-tick append-buffer counter reset coverage.
- `docs/TEST_PLAN.md`: added TWF-045 accepted artifact locations, commands, snapshot table, and append-counter evidence.
- `kanban/all-tickets/TWF-045-add-release-scenario-shader-snapshots.md`: recorded worker evidence and results.

Tech-lead cleanup on 2026-05-02:

- Exact accepted capture JSONs now live in `tests/Wildfire.Core.Tests/ShaderSnapshots/release/`. These repo files contain the durable `finalPackedCells` arrays and every per-tick `cellIndex`/`oldCell`/`newCell` delta record.
- `UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled` now loads those committed capture JSONs and compares exact final cells, per-tick delta record sets, headers, and visual checksums with `ShaderSnapshotComparison`. Per-tick GPU append order is treated as unstable, so expected and actual delta records are sorted by `cellIndex`, `oldCell`, and `newCell` before comparison. The test still fails if values move between cells or any old/new transition changes, even when aggregate totals remain unchanged.
- `AcceptedReleaseShaderSnapshotResourcesContainExactCellsAndDeltas` parses the committed resources during normal `dotnet test`.
- `src/Wildfire.Cli/ScenarioCatalog.cs` aligns the `building-cluster` helper with the accepted TWF-043 release wood-like band: fuel `15`, flammability `1`, heat loss `3`.
- The aligned building band intentionally updates the accepted `building-cluster` snapshot to per-tick deltas `[5, 1, 5]`, final hot cells `1`, max heat `1`, and checksum `visual-fnv1a32:D12ED5D7`.

Integration cleanup on 2026-05-02:

- `ShaderSnapshotComparison` now treats per-tick delta append order as unstable by sorting expected and actual deltas by `cellIndex`, `oldCell`, and `newCell` before exact record comparison.
- Added comparison tests proving permuted delta records pass while changed old/new records still fail.
- Docs now state that committed captures preserve full records and tests compare deterministic record sets rather than GPU append order.

Commands run:

- `dotnet run --project src/Wildfire.Cli -- --scenario=<scenario> --seed=<seed> --width=<width> --height=<height> --depth=<depth> --layer=0 --export-fixture=<artifact fixture>` for all seven accepted scenarios.
- `/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/repos/wildfire-TWF-045/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture ...` for all seven accepted scenarios.
- `git diff --check`: pass.
- `dotnet test`: pass, `132` tests after integration cleanup.
- `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled`: pass, `1` opt-in Unity shader test covering all seven release scenarios and comparing exact committed captures as deterministic delta record sets.

Deferred scenarios:

- None. All required release scenarios were accepted.

Blockers:

- None at worker handoff. Live Timberborn validation is not required by this ticket, but `TWF-046` should still use these snapshots as part of coherent live-loop validation.
