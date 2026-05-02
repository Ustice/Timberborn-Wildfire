# Wildfire Test Plan

## Scope

Validation should prove the shared packed data model, deterministic scenario inputs, shader execution, compact delta readback, and Timberborn adapter behavior.

## Current Automated Coverage

- Packed cell round-trips and field setters.
- Burning-threshold helper behavior.
- Seeded scenario catalog coverage.
- Scenario dimension and seed overrides.
- Seeded sparse layout determinism.
- CLI fixture export shape and deterministic JSON output.
- Shader snapshot harness contract: CLI fixture loading, buffer-grid creation from fixtures, stable accepted-snapshot JSON shape, actionable snapshot diffs, and explicit current execution blocker.
- Unity batchmode shader execution harness: opt-in local test loads `FireSim.compute`, dispatches a seeded `TWF-000` fixture, reads back final packed cells, compact deltas, and visual-field checksum through the existing shader snapshot harness.
- GPU visual field wrapper contract: `wildfire.visual_fields` is allocated as one `float4`-equivalent entry per packed cell, full-grid dispatch receives the visual buffer, shader source writes visual samples from post-step packed cell values, and deterministic tests cover fire, smoke, ash, and visibility derivation.
- Timberborn cell mapping scaffold: deterministic terrain/building/resource/water source folding into packed cells, named material bands for stockpile resources, vegetation, wood-like buildings, and non-burnable buildings, sorted `SetCell` change emission, field-width clamping, wet-cell overlay behavior, vertical footprint expansion, material priority, and out-of-bounds source rejection.
- Timberborn QA command bridge scaffold: read-only `status` and `help` commands, simulator runtime state when available, searchable command request/result tokens, and explicit no-arbitrary-execution command dispatch.
- Timberborn deploy pipeline scaffold: Bun/TypeScript deploy script, generated Wildfire manifest, managed assembly staging into `~/Documents/Timberborn/Mods/Wildfire/Scripts`, private FireSim and diagnostic AssetBundle staging into `~/Documents/Timberborn/Mods/Wildfire/ComputeShaders`, local build/deploy lock, dry-run/help output, and running-game guard for real deploy/remove.
- Timberborn fixed-cadence dispatch scaffold: adapter initialization from mapped cells through an injected GPU simulator factory, external change registration through `IGpuFireSimulator.RegisterChange`, centralized cadence options, one dispatch per processed game update, compact-delta return/subscription surface, command-bridge status fields, and lifecycle log tokens for attach/init/change/wait/dispatch/readback/failure events.
- Timberborn compute-backed simulator factory: live adapter loads `wildfire_compute_mac` from the deployed AssetBundle, creates Unity `ComputeBuffer` resources, dispatches `ApplyExternalChanges` and `SimulateFullGrid`, reads compact deltas, and initializes `TimberbornFireRuntime` from real terrain sources supplied by `MapSize` and `ITerrainService`.
- Timberborn GPU visual-field surface binding: the live compute simulator binds the `VisualFields` compute buffer once as a Timberborn-facing DI singleton surface with one `float4`-equivalent entry per cell, channel order `fire,smoke,ash,visibility`, a consumer-facing binding view for future renderer/effect/debug-inspector systems, bounded sample inspection for specific cell indices, dispatch-update telemetry, and `qa-readiness`/`status` fields that prove the visual surface is bound without routing gameplay consequences through visual output.
- Timberborn pooled fire/smoke/ash effect routing: compact delta visual-effect events select bounded visual-field samples through `ITimberbornGpuVisualFieldSurface`, the adapter maintains a capped pool of active fire/smoke/ash presentation anchors instead of one object per simulated cell, visual presentation failures are isolated from gameplay consequences, and `qa-readiness`/`status` fields expose active pooled effects, last-dispatch updated regions, stable last-nonzero updated regions, presentation failures, and native-prefab visibility state.
- Tuned visual-field output: `TWF-041` accepts named fire/smoke/ash/visibility constants in the C# mirror and `FireSim.compute`, two Unity shader checksum snapshots, and live Timberborn pooled-effect evidence for the tuned output.
- Tuned fire game-feel output: `TWF-043` accepts named ignition, spread, burn, heat-loss, flammability-pressure, and water-suppression constants in `FireSim.compute`, Timberborn adapter material bands, and three Unity shader snapshots that assert semantic delta and hot-cell outcomes in addition to visual checksums.
- Release scenario shader snapshots: `TWF-045` accepts seven real Unity compute captures for single ignition, line of fuel, water barrier, vertical fuel column, sparse forest, building cluster, and mixed terrain/fuel/water, with committed exact final packed-cell arrays, per-tick old/new delta records, visual checksums, logs, and normal-test append-counter reset coverage.
- Timberborn debug fire overlay state: the adapter consumes compact deltas, filters them to visual-state changes, stores the latest packed cell only for affected overlay indices, derives fuel/heat/water/burning/spent state from that packed cell, and exposes per-dispatch updated-cell counters separately from the persistent overlay cell count.
- Timberborn player-facing fire alert state: compact delta alert events are aggregated into at most one native quick warning per dispatch, warning text reports new fire cells, burned-out cells, and max heat, and status telemetry exposes the last player alert tick, counts, notification send state, and presentation failures.
- Runtime diagnostics: Unity and Timberborn GPU paths emit concise `wildfire_*` tokens for simulator initialization/disposal, queued change batches, dispatch kernel start/completion with elapsed milliseconds, compact delta readback counts, listener notification counts, and adapter startup/shutdown without logging per-cell changes.

Run:

```bash
dotnet test
```

Run the shader snapshot harness slice:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
```

Run the real Unity compute-shader execution harness locally:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled
```

## Release Simulation Decision Validation

`TWF-044` closes the release-blocking simulation design questions without adding runtime mechanics. Validation for the initial release should prove the conservative path, not speculative variants:

- Cadence: live Timberborn QA should continue to use `qa-readiness --require-advanced-tick` and inspect `cadence_interval_ms=1000`, advancing `tick_count`, and `wildfire_timberborn_dispatch_completed` tokens. If `TWF-048` exposes cadence as a release setting, it needs separate setting-boundary tests and one live run per accepted preset.
- Neighbor model: release shader snapshots should assume 6-neighbor spread only. `TWF-045` should include enough scenarios, such as single ignition, line of fuel, vertical fuel column, and water barrier, to catch accidental diagonal or wind-like spread.
- Wind: no release validation is required beyond confirming there is no wind input, host-owned wind modifier, or wind setting in the public release surface.
- Ash: snapshot and visual validation should treat ash as derived visual output. Passing evidence may show ash decays with heat; persistent ash must not be required for release screenshots or gameplay consequences.
- Vertical building mapping: deterministic tests should keep covering multi-cell and vertical `TimberbornCellFootprint` expansion, sorted cell mapping, and out-of-bounds rejection. Live validation should use mapped building/consequence evidence rather than adding Timberborn-owned fire rules.
- Water: water validation should continue to use `qa-water-suppression-stimulus` followed by `qa-readiness --require-advanced-tick --require-water-changed`, plus `Player.log` proof that the queued `SetWater=3` change produced a GPU delta and a water-change consumer count.
- Heat loss: scenario snapshots and mapping tests should pin material-driven heat-loss bands for terrain, vegetation, stockpile resources, wood-like buildings, and non-burnable buildings. Weather, biome, or season-driven heat-loss changes are out of release scope unless a later adapter ticket adds explicit tests.
- Dispatch strategy: keep full-grid dispatch as passing release behavior unless `TWF-051` changes the decision after `TWF-046`. Until then, validation should preserve `TWF-034` profiling evidence and watch live-loop dispatch/readback timing for regressions rather than requiring active-frontier buffers.

## CLI Fixture Export

Use the CLI fixture exporter when shader tests need deterministic packed-cell inputs without launching Timberborn:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=mixed-terrain --seed=42 --width=32 --height=18 --depth=3 --layer=0 --export-fixture=artifacts/mixed-terrain.fixture.json
```

The JSON fixture contains:

- `formatVersion`.
- Scenario name and seed.
- Grid `width`, `height`, and `depth`.
- Selected layer `index`, flat `offset`, and `cellCount`.
- Packed cell value metadata with `valueType: "uint16"` and index order `x + y * width + z * width * height`.
- Full-grid packed cell `values` in flat index order.

Fixture files are deterministic for the same scenario, seed, dimensions, and layer. Shader harnesses should load the JSON, upload the `values` array as the initial packed grid, and use the selected layer metadata only as the preview or snapshot slice.

## Shader Snapshot Coverage

Current `TWF-002` coverage proves the wrapper dispatch contract in .NET tests only. `TWF-006` adds the fixture-driven snapshot harness shape in `Wildfire.Unity`: it reads CLI fixture JSON, creates a `ComputeBufferGrid` from the fixture cells, defines the accepted snapshot JSON shape, compares final packed grids, per-tick compact deltas, and optional visual checksums, and exposes an `IShaderSnapshotExecutor` boundary.

The harness command validates shape, comparison, blocker handling, and Unity executor failure surfacing:

```bash
dotnet test --filter FullyQualifiedName~ShaderSnapshotHarnessTests
```

`TWF-018` adds `UnityBatchmodeShaderSnapshotExecutor`, which launches `src/Wildfire.Unity/UnityBatchmodeProject` in Unity batchmode. The Unity Editor runner copies the repository `FireSim.compute` into the temporary project asset area, imports it as a real `ComputeShader`, dispatches `SimulateFullGrid` for a seeded `TWF-000` fixture, reads the append-buffer delta counter and records, reads final packed cells, reads visual fields, and writes snapshot JSON back through the existing `ShaderSnapshotHarness`.

The real shader execution test is opt-in because it requires a local Unity Editor installation, licensing, and compute-shader capable graphics access. CI should keep running the normal .NET harness tests unless the runner image explicitly provides Unity and a graphics device. Use `WILDFIRE_UNITY_EXECUTABLE` when Unity is not installed at the default macOS Hub path.

`TWF-004` adds .NET coverage for the compact delta readback wrapper: `wildfire.deltas` is allocated through the append-buffer abstraction, its append counter is reset before dispatch, the append counter is read after dispatch, compact `CellDelta` records are decoded, and subscribed listeners are notified from the readback result. Those wrapper tests remain contract-only; use the TWF-018 Unity batchmode harness for HLSL compile/runtime proof.

`TWF-005` adds .NET coverage for the visual-field data path only: the visual field is a `float4`-equivalent buffer handle, dispatch records carry it to the compute boundary, and shader source writes the visual sample from packed cell output. The TWF-018 Unity batchmode harness proves shader visual-field readback via checksum, but rendered pixels, GPU texture binding, and material sampling still need later visual validation.

`TWF-045` adds accepted shader snapshot fixtures for the release behavior scenarios:

- Single ignition point.
- Line of fuel.
- Water barrier.
- Vertical fuel column.
- Sparse forest.
- Building cluster.
- Mixed terrain/fuel/water.

For each accepted snapshot, record:

- Scenario name.
- Seed.
- Grid dimensions.
- Tick count.
- Final packed cell grid or semantic final-cell summary.
- Per-tick compact delta counts.
- Per-tick compact delta records for changed cells only, with old and new packed values.
- Evidence that the append-buffer counter is reset before each dispatch/readback cycle.
- Visual field checksum or image artifact when useful.

Update snapshots intentionally only after reviewing the diff scenario by scenario. Regenerate the CLI fixture, run the shader snapshot command, inspect final packed-cell differences, semantic summaries, and per-tick delta differences, and commit or record the changed accepted snapshot JSON with the rule or shader change that justifies it. Avoid broad visual-only approval for behavior changes.

`TWF-041` accepted visual-output tuning constants are mirrored in `FireVisualField` and `FireSim.compute`:

- Fire: base `0.45`, heat weight `0.55`.
- Smoke: base `0.12`, fuel weight `0.52`, heat weight `0.24`.
- Ash: base `0.18`, inverse-fuel weight `0.5`, heat weight `0.32`.
- Visibility: heat weight `0.55`, smoke weight `0.9`, ash weight `0.8`, with raw fire intensity still allowed to dominate visibility.

Interpretation:

- Fire remains the strongest channel for hot burning cells.
- Heavy-fuel cells just at ignition can read as smoke-dominant before peak fire.
- Ash is stronger on low-fuel residual-heat terrain, but it is still temporary. `PackedCell` has no burn-history field, so ash decays with heat; persistent ash requires an explicit future storage/design decision.
- Visibility no longer lets heat alone dominate every visual sample; it weights residual heat below active fire and smoke so the pooled presentation lane stays less noisy.

Accepted shader snapshot evidence for this tuning pass lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/`:

- `single-ignition`, seed `21`, grid `5x5x1`, ticks `2`: fixture `single-ignition-seed21-5x5x1.fixture.json`, capture `single-ignition-seed21-5x5x1-tick2.capture.json`, checksum `visual-fnv1a32:8710B4BB`.
- `line-of-fuel`, seed `42`, grid `12x5x1`, ticks `4`: fixture `line-of-fuel-seed42-12x5x1.fixture.json`, capture `line-of-fuel-seed42-12x5x1-tick4.capture.json`, checksum `visual-fnv1a32:BFDB9857`.
- Unity log evidence: `single-ignition-unity.log` and `line-of-fuel-unity.log`, both with `phase=compile`, `phase=buffer`, `phase=dispatch`, and `phase=readback` `status=ok` tokens.

Regenerate the accepted TWF-041 snapshots with:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-seed21-5x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-041/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-seed21-5x5x1.fixture.json" --shader ~/repos/wildfire-TWF-041/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/single-ignition-seed21-5x5x1-tick2.capture.json" --ticks 2
dotnet run --project src/Wildfire.Cli -- --scenario=line-of-fuel --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-seed42-12x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-041/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-seed42-12x5x1.fixture.json" --shader ~/repos/wildfire-TWF-041/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-041-shader-snapshots/line-of-fuel-seed42-12x5x1-tick4.capture.json" --ticks 4
```

`TWF-043` accepted fire game-feel tuning keeps the `TWF-044` release decisions intact: the shader still reads the six cardinal 3D neighbors only, there is no wind input, and Timberborn supplies material and water bands without owning fire rules.

Accepted `FireSim.compute` game-feel constants:

- Ignition: `FIRE_IGNITION_BASE_HEAT=11`, `FIRE_WATER_IGNITION_PENALTY=2`.
- Spread: `FIRE_RETAINED_HEAT_WEIGHT=2`, `FIRE_SPREAD_HEAT_WEIGHT=1`, `FIRE_BURNING_NEIGHBOR_HEAT_BONUS=3`, `FIRE_BURNING_NEIGHBOR_DIRECT_HEAT=1`.
- Water suppression: `FIRE_WATER_SUPPRESSION_HEAT=2`, `FIRE_WATER_EVAPORATION_HEAT=10`.
- Burn pressure: `FIRE_FLAMMABILITY_BURN_PRESSURE=2`, `FIRE_WATER_BURN_PRESSURE_PENALTY=3`, `FIRE_BURN_HEAT_BASE=1`.

Accepted Timberborn adapter material bands:

- Wood-like buildings: fuel `15`, flammability `1`, heat loss `3`.
- Stockpile resources: fuel `8`, flammability `2`, heat loss `3`.
- Vegetation: fuel `10`, flammability `3`, heat loss `1`.
- Non-burnable buildings and solid terrain keep their existing non-fuel and high-heat-loss behavior.

Interpretation:

- Single ignition now radiates to adjacent cells in broad grass-like fuel instead of having neighbor heat disappear into integer averaging.
- Line-of-fuel remains bounded and legible: the ignition advances along the fuel line, then settles as available heat and stochastic burn rolls decline.
- The water barrier remains an effective suppression case: with `SetWater=3`, the accepted snapshot leaves only one hot cell after four ticks, proving water raises ignition difficulty and suppresses burn pressure without adding host-owned fire rules.
- Wood-like buildings burn longer and less explosively because they carry more fuel, lower flammability, and lower heat loss than the old band; vegetation remains the fast-catching material.

Accepted shader snapshot evidence for this tuning pass lives under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/`:

- `single-ignition`, seed `21`, grid `5x5x1`, ticks `2`: fixture `single-ignition-seed21-5x5x1.fixture.json`, capture `single-ignition-seed21-5x5x1-tick2.capture.json`, checksum `visual-fnv1a32:50C4978E`, per-tick deltas `[5, 5]`, final hot cells `5`.
- `line-of-fuel`, seed `42`, grid `12x5x1`, ticks `4`: fixture `line-of-fuel-seed42-12x5x1.fixture.json`, capture `line-of-fuel-seed42-12x5x1-tick4.capture.json`, checksum `visual-fnv1a32:120F70AE`, per-tick deltas `[5, 5, 5, 2]`, final hot cells `5`.
- `water-barrier`, seed `42`, grid `12x5x1`, ticks `4`: fixture `water-barrier-seed42-12x5x1.fixture.json`, capture `water-barrier-seed42-12x5x1-tick4.capture.json`, checksum `visual-fnv1a32:40818F57`, per-tick deltas `[5, 5, 5, 5]`, final hot cells `1`.
- Unity log evidence: `single-ignition-unity.log`, `line-of-fuel-unity.log`, and `water-barrier-unity.log`, all with `phase=compile`, `phase=buffer`, `phase=dispatch`, and `phase=readback` `status=ok` tokens.
- Screenshot evidence: no live Timberborn screenshot was captured in the worker pass. QA must capture live screenshots after deploying these constants, and should attach the copied `Player.log` plus command evidence from the live sequence below before marking the ticket accepted.

Regenerate the accepted TWF-043 snapshots with:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-043/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1.fixture.json" --shader ~/repos/wildfire-TWF-043/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/single-ignition-seed21-5x5x1-tick2.capture.json" --ticks 2
dotnet run --project src/Wildfire.Cli -- --scenario=line-of-fuel --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-043/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1.fixture.json" --shader ~/repos/wildfire-TWF-043/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/line-of-fuel-seed42-12x5x1-tick4.capture.json" --ticks 4
dotnet run --project src/Wildfire.Cli -- --scenario=water-barrier --seed=42 --width=12 --height=5 --depth=1 --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-043/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1.fixture.json" --shader ~/repos/wildfire-TWF-043/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-043-game-feel/water-barrier-seed42-12x5x1-tick4.capture.json" --ticks 4
```

Run the accepted TWF-043 shader harness assertions with:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled
```

Live Timberborn QA for this tuning pass should deploy the mod, load and unpause a save, run `qa-delta-stimulus` or `qa-building-burnout-stimulus` for visible fire, run `qa-water-suppression-stimulus` plus `qa-readiness --require-advanced-tick --require-water-changed` for suppression proof, capture screenshots of the visible loop, and copy `Player.log` tokens showing the command request/result, queued GPU changes, compute dispatch/readback, visual/presentation update, and water-change consumer count.

## Release Shader Snapshot Evidence

`TWF-045` accepts the release shader snapshot set after the `TWF-043` game-feel tuning and `TWF-044` conservative release decisions. Exact accepted capture JSONs are committed under `tests/Wildfire.Core.Tests/ShaderSnapshots/release/`; those files contain the durable `finalPackedCells` arrays and every per-tick delta record with `cellIndex`, `oldCell`, and `newCell`. Local fixture, capture, and Unity log mirrors live under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/`.

| Scenario | Seed | Grid | Ticks | Per-tick deltas | Final semantic summary | Visual checksum | Accepted files |
| --- | ---: | --- | ---: | --- | --- | --- | --- |
| `single-ignition` | `21` | `5x5x1` | `2` | `[5, 5]` | hot `5`, burning `0`, max heat `7`, water cells `0`, fuel total `175` | `visual-fnv1a32:50C4978E` | `single-ignition-seed21-5x5x1.fixture.json`, `single-ignition-seed21-5x5x1-tick2.capture.json`, `single-ignition-unity.log` |
| `line-of-fuel` | `42` | `12x5x1` | `4` | `[5, 5, 5, 2]` | hot `5`, burning `1`, max heat `12`, water cells `0`, fuel total `104` | `visual-fnv1a32:120F70AE` | `line-of-fuel-seed42-12x5x1.fixture.json`, `line-of-fuel-seed42-12x5x1-tick4.capture.json`, `line-of-fuel-unity.log` |
| `water-barrier` | `42` | `12x5x1` | `4` | `[5, 5, 5, 5]` | hot `1`, burning `0`, max heat `2`, water cells `5`, fuel total `385` | `visual-fnv1a32:40818F57` | `water-barrier-seed42-12x5x1.fixture.json`, `water-barrier-seed42-12x5x1-tick4.capture.json`, `water-barrier-unity.log` |
| `vertical-fuel-column` | `17` | `5x5x4` | `4` | `[6, 6, 2, 1]` | hot `6`, burning `1`, max heat `11`, water cells `0`, fuel total `44` | `visual-fnv1a32:5F05530F` | `vertical-fuel-column-seed17-5x5x4.fixture.json`, `vertical-fuel-column-seed17-5x5x4-tick4.capture.json`, `vertical-fuel-column-unity.log` |
| `sparse-forest` | `73` | `16x10x1` | `3` | `[5, 5, 5]` | hot `5`, burning `1`, max heat `12`, water cells `0`, fuel total `978` | `visual-fnv1a32:E4355BFA` | `sparse-forest-seed73-16x10x1.fixture.json`, `sparse-forest-seed73-16x10x1-tick3.capture.json`, `sparse-forest-tick3-unity.log` |
| `building-cluster` | `91` | `14x10x1` | `3` | `[5, 1, 5]` | hot `1`, burning `0`, max heat `1`, water cells `0`, fuel total `1179` | `visual-fnv1a32:D12ED5D7` | `building-cluster-seed91-14x10x1.fixture.json`, `building-cluster-seed91-14x10x1-tick3.capture.json`, `building-cluster-tick3-unity.log` |
| `mixed-terrain` | `123` | `16x10x3` | `3` | `[6, 6, 6]` | hot `5`, burning `0`, max heat `4`, water cells `10`, fuel total `3286` | `visual-fnv1a32:67BFDEEA` | `mixed-terrain-seed123-16x10x3.fixture.json`, `mixed-terrain-seed123-16x10x3-tick3.capture.json`, `mixed-terrain-tick3-unity.log` |

Each Unity log has `phase=compile`, `phase=buffer`, per-tick `phase=dispatch`, and per-tick `phase=readback` `status=ok` tokens. The opt-in test `UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled` regenerates each scenario through real Unity compute execution and compares the full capture against the committed JSON with `ShaderSnapshotComparison`, so a moved heat/fuel/water value or changed old/new delta record fails even if aggregate totals remain unchanged. Per-tick GPU append order is not part of the production contract; comparison sorts expected and actual delta records by `cellIndex`, `oldCell`, and `newCell` before comparing the record set. The Unity batchmode runner resets the append-buffer counter with `deltas.SetCounterValue(0)` before every tick dispatch, reads it with `ComputeBuffer.CopyCount`, and the non-Unity wrapper test `TickResetsAppendCounterBeforeEveryFullGridDispatch` keeps the repeated-tick reset contract covered in normal `dotnet test`.

Regenerate one accepted snapshot with:

```bash
dotnet run --project src/Wildfire.Cli -- --scenario=<scenario> --seed=<seed> --width=<width> --height=<height> --depth=<depth> --layer=0 --export-fixture="$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<fixture>.fixture.json"
"/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath ~/repos/wildfire-TWF-045/src/Wildfire.Unity/UnityBatchmodeProject -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture -logFile "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<scenario>-unity.log" -- --fixture "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<fixture>.fixture.json" --shader ~/repos/wildfire-TWF-045/src/Wildfire.Unity/FireSim.compute --output "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/twf-045-release-snapshots/<capture>.capture.json" --ticks <ticks>
```

Run the accepted release snapshot assertions with:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 WILDFIRE_UNITY_EXECUTABLE=/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity dotnet test --filter FullyQualifiedName~UnityBatchmodeExecutorCapturesSeededFixtureWhenEnabled
```

## Timberborn Validation

Live Timberborn validation should start only after the GPU simulator and adapter path can:

- Upload terrain/building/water cells.
- Register external heat and water changes.
- Dispatch on a fixed cadence.
- Read compact deltas.
- Update overlays or effects from changed cells.
- Apply gameplay consequences from deltas.

Current `TWF-007` coverage proves the mapper contract in .NET tests only. It does not prove live Timberborn API binding, map-service discovery, terrain-height queries, building footprint extraction, vegetation/resource component lookup, water-depth sampling, mod loading, or in-game dispatch because the repository still has no Timberborn mod project reference or live-game harness wired to this adapter scaffold.

Current `TWF-012` and `TWF-019` coverage adds an in-process command bridge plus a narrow Timberborn game-context file binding. QA can invoke read-only `status`, `qa-readiness`, or `help` from a loaded game by writing one command to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt`, or by running:

```bash
bun scripts/invoke-timberborn-command.ts status
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
```

The Timberborn adapter polls that inbox from `TimberbornQaCommandFileBridge`, forwards the command to `TimberbornQaCommandBridge`, deletes the inbox, and writes the latest result to `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-outbox.txt`. The script and bridge expose only known allowlisted commands. Unknown manual inbox commands are rejected by the bridge and logged as failures rather than executed.

Current `TWF-008` coverage adds a Timberborn game-context runtime singleton for fixed-cadence dispatch. `TimberbornFireRuntime` is the command-bridge state provider, so `status` and `qa-readiness` report `bridge_alive=true`, `runtime_loaded`, `loaded_game_ready`, `simulator_integrated`, dimensions, `tick_count`, `queued_changes`, and `last_delta_count` after a simulator is attached. When no simulator factory has been attached by the live host yet, simulator fields intentionally return `placeholder` and `loaded_game_ready=false`.

`qa-readiness` is intentionally a loaded-game readiness probe, not a UI automation command. It does not navigate menus, click Timberborn UI, load saves, delete saves, invoke arbitrary `VisualElement` callbacks, mutate the Wildfire grid, or trigger debug/destructive actions. Treat `success=true` as "the command was handled safely"; treat `loaded_game_ready=true` plus numeric dimensions and tick fields as the loaded-game readiness signal. For live QA that needs to prove fixed-cadence dispatch is advancing, unpause the loaded save first and add `--require-advanced-tick`; the command script then fails unless the result includes `tick_count` greater than `0`.

`TWF-031` adds one non-read-only QA stimulus command: `qa-delta-stimulus`. This command has no user-supplied coordinates or parameters. It requires an initialized `TimberbornFireRuntime`, chooses the deterministic center cell of the current fire grid, and queues one fixed `SetCell` external change through `IGpuFireSimulator.RegisterChange`; it does not mutate Timberborn terrain, buildings, saves, UI state, or simulator buffers directly. The command result message includes `target_index`, `target_x`, `target_y`, `target_z`, and `set_cell`; the same result token also reports current `queued_changes` and `tick_count` so QA can tie the request to the next simulator dispatch.

Use the stimulus only after the guarded startup utility has loaded and unpaused a save:

```bash
bun scripts/load-latest-save-and-unpause.ts --launch
bun scripts/invoke-timberborn-command.ts qa-delta-stimulus --wait=6 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-nonzero-delta
```

The first command queues the bounded fixed cell change. The follow-up `qa-readiness` or `status` command is the proof point after at least one subsequent fixed-cadence tick; `--require-nonzero-delta` fails unless the result reports `last_delta_count` greater than `0`. Live QA evidence should include both command outputs plus `Player.log` tokens for `wildfire_command_request command=qa-delta-stimulus`, `wildfire_timberborn_qa_delta_stimulus_queued`, `wildfire_timberborn_changes_registered source=qa_delta_stimulus`, and the subsequent `wildfire_timberborn_dispatch_completed ... delta_count=<nonzero>` line.

`TWF-038` adds one QA-only water suppression command: `qa-water-suppression-stimulus`. This command accepts no coordinates or parameters. It requires an initialized `TimberbornFireRuntime`, chooses the deterministic center cell of the current fire grid, and queues exactly one `SetWater=3` external change through `IGpuFireSimulator.RegisterChange`; it does not mutate Timberborn water, terrain, buildings, saves, UI state, simulator buffers, or arbitrary coordinates directly. The command bridge rejects arguments for QA-only simulator change commands, so broad coordinate mutation attempts such as `qa-water-suppression-stimulus x=1 y=2` fail before state is queried. The command result message includes `target_index`, `target_x`, `target_y`, `target_z`, `set_water`, and `queued_water_changes`; the same result token reports current `queued_changes`, `tick_count`, `last_delta_count`, `last_delta_consumer_water_changed`, `last_positive_water_changed_tick`, and `last_positive_water_changed_count` so QA can tie the accepted target to the next simulator dispatch and prove the consumer saw a water-field change even if later zero-delta ticks have overwritten the last-dispatch fields.

Use the suppression stimulus only after the guarded startup utility has loaded and unpaused a save:

```bash
bun scripts/load-latest-save-and-unpause.ts --launch
bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed
```

The first command queues the bounded water change and should report `queued_changes=1` without advancing the target itself. The follow-up `qa-readiness` or `status` command is the proof point after at least one subsequent fixed-cadence tick; passing evidence needs numeric `tick_count` advancement and `last_positive_water_changed_count` greater than `0`, with `last_positive_water_changed_tick` naming the dispatch that produced it. `last_delta_count` alone is not sufficient for this ticket because non-water external changes can append deltas, and `last_delta_consumer_water_changed` may return to `0` after later settled ticks. Live QA evidence should include both command outputs plus `Player.log` tokens for `wildfire_command_request command=qa-water-suppression-stimulus`, `wildfire_timberborn_qa_water_suppression_queued`, `wildfire_timberborn_changes_registered source=qa_water_suppression`, the subsequent `wildfire_timberborn_dispatch_completed ... delta_count=<nonzero>` line, and `wildfire_timberborn_delta_consumer_completed ... water_changed=<nonzero>`. If the deterministic center cell already has water level `3`, reload or use a different save before marking the ticket failed; do not add ad hoc coordinates to the command.

`TWF-033` binds the first Timberborn-facing consequence to the existing delta-consumer sink surface. The live runtime records debug visual state only for changed cells delivered by compact deltas; it does not mutate Timberborn terrain, saves, buildings, resources, UI, or simulator buffers. Passing live evidence requires the `qa-delta-stimulus` sequence above, `Player.log` proof of the subsequent non-zero dispatch and consumer pass, and a follow-up `qa-readiness` or `status` result showing `last_delta_consumer_debug_visual_cells` greater than `0`. The follow-up command may report `last_delta_count=0` if later simulator ticks have already consumed and settled the stimulus. `Player.log` should include `wildfire_timberborn_delta_consequence_sink_bound lane=debug_visual_state`, `wildfire_timberborn_dispatch_completed ... delta_count=<nonzero>`, and `wildfire_timberborn_delta_consumer_completed ... changed_cells=<nonzero> ... debug_visual_cells=<nonzero>`.

`TWF-037` tightens the same safe debug lane into the current inspection overlay. The overlay state remains adapter-local and rule-free: each entry is keyed by compact-delta cell index, stores the latest packed cell value, and derives visible inspection fields from `PackedCell` helpers instead of duplicating fire rules in Timberborn. Live QA should prove that updates are bounded to changed cells by capturing `Player.log` `wildfire_timberborn_delta_consumer_completed ... debug_visual_updated_cells=<nonzero> ... debug_visual_cells=<count>` after `qa-delta-stimulus`, then a `qa-readiness` or `status` result containing both `last_delta_consumer_debug_visual_updated_cells` and `last_delta_consumer_debug_visual_cells`. Screenshots are useful only if a later Timberborn UI panel or rendered overlay consumes this state; for this ticket, command counters plus the dispatch/consumer log pair are the required live evidence.

`TWF-039` binds the existing GPU visual-field buffer to a Timberborn-facing surface instead of creating one Timberborn entity per simulated cell. `WildfireConfigurator` exposes `ITimberbornGpuVisualFieldSurface` as a game singleton, and `TimberbornComputeFireSimulatorFactory` receives that same singleton before binding `VisualFields`, so future renderer/effect/debug-inspector systems can resolve the same live surface. The surface is adapter-local and visual-only: `TryGetBinding` exposes the bound `VisualFields` buffer handle with dimensions, cell count, 16-byte stride, and the channel order `fire,smoke,ash,visibility`; `TryGetComputeBuffer` gives Timberborn renderer/effect code the typed Unity buffer when the binding is live; `InspectCells` allows bounded readback of up to 256 explicit cell samples for renderer/effect development or debug inspection. Gameplay consequences continue to flow through compact C# deltas. Live QA should capture `Player.log` tokens for `wildfire_timberborn_gpu_visual_field_surface_bound ... channels=fire,smoke,ash,visibility` and `wildfire_timberborn_gpu_visual_field_surface_updated tick=<tick>`, plus a follow-up `qa-readiness` or `status` result showing `visual_field_surface_bound=true`, `visual_field_surface_cells=<map cell count>`, and `visual_field_surface_updated_tick` at or after the latest observed dispatch. Unit coverage proves the bounded inspection API with a fakeable reader seam and proves a factory/consumer reference can observe the same bind/update/unbind lifecycle because constructing Unity `ComputeBuffer` resources is only safe in the Unity/Timberborn runtime. A screenshot or visual artifact is still required before product acceptance once a material/effect renderer consumes the bound buffer; do not treat the status token alone as rendered-pixel proof.

`TWF-040` consumes that surface from the presentation lane. The pooled effect sink is bound as the Timberborn visual-effect sink, uses compact delta events only to choose candidate visual regions, reads at most the configured number of changed-cell visual samples per dispatch, and keeps active effect anchors capped by `MaxActiveEffects` instead of creating one Timberborn object per simulated cell. Visual presentation exceptions are caught and logged as `wildfire_timberborn_visual_effect_sink_failed` or `wildfire_timberborn_pooled_fire_effects_failed`; compact-delta gameplay, building, alert, and dispatch telemetry must continue after those presentation failures. The live presenter searches loaded Timberborn/Unity resources for native-looking fire or smoke prefabs such as `CampfireFire`, `Sparks_Trail`, `SmelterSmoke`, and `SteamEngineSmoke`. If no native prefab is resolved, visible effects are disabled honestly instead of creating invisible fallback anchors; QA should treat `pooled_fire_effects_visible_enabled=false` or `pooled_fire_effects_native_prefab_resolved=false` as a visible-effects blocker, not a screenshot pass. `status` and `qa-readiness` should include `active_pooled_fire_effects`, `updated_visual_regions`, `last_nonzero_updated_visual_regions`, `last_nonzero_updated_visual_regions_tick`, `max_pooled_fire_effects`, `max_updated_visual_regions`, `pooled_fire_effect_presentation_failures`, `pooled_fire_effects_visible_enabled`, `pooled_fire_effects_native_prefab_resolved`, and `pooled_fire_effects_native_prefab`. Passing live QA should capture a screenshot or visual artifact after `qa-delta-stimulus`, copy `Player.log`, and include log tokens for `wildfire_timberborn_delta_consequence_sink_bound lane=pooled_fire_smoke_ash_effects`, either `wildfire_timberborn_pooled_fire_effect_native_prefab_resolved ... prefab=<name>` or the explicit unavailable token, `wildfire_timberborn_pooled_fire_effects_updated ... active_pooled_effects=<count> ... last_nonzero_updated_visual_regions=<count>`, the TWF-039 surface bind/update tokens, and the final QA lock state. Screenshot approval remains a QA step because unit tests prove selection, routing, pooling limits, exception isolation, native-resolution telemetry, and counters, not rendered-pixel quality.

`TWF-036` binds the first building burnout consequence to the same compact-delta consumer pass. The Timberborn adapter considers only changed cells delivered by compact deltas, checks the live `IBlockService` for pausable buildings at each changed fire-grid coordinate, and calls `PausableBuilding.Pause()` only when a matched building cell reaches fuel depletion. This is intentionally bounded and reversible: it does not destroy buildings, mutate the fire grid, or write simulator state from Timberborn. The QA-only `qa-building-burnout-stimulus` bridge command has no user-supplied coordinates. It scans the current fire grid for the first unpaused pausable building cell, then queues exactly two ordered `SetCell` changes through `IGpuFireSimulator.RegisterChange` for that one cell: a building-like primed cell followed by a fuel-zero cell. If every scanned pausable building is already paused, the command reports no usable target instead of queueing a stimulus that cannot increment `building_burnout_applied_consequences`. This keeps the target discoverable without arbitrary coordinate mutation and lets the next dispatch emit a fuel-depleted compact delta on a known pausable building cell. Until the helper script allowlist is expanded, invoke the bridge directly with `printf 'qa-building-burnout-stimulus\n' > "$HOME/Library/Application Support/Mechanistry/Timberborn/WildfireQA/command-inbox.txt"`, then read `command-outbox.txt` after it updates. Capture the command result message fields `target_index`, `target_x`, `target_y`, `target_z`, `scanned_cells`, `primed_cell`, `set_cell`, and `queued_set_cell_changes=2`. Live QA should then capture `Player.log` `wildfire_timberborn_delta_consequence_sink_bound lane=building_burnout_pause`, `wildfire_timberborn_qa_building_burnout_stimulus_queued`, `wildfire_timberborn_changes_registered source=qa_building_burnout_prime`, `wildfire_timberborn_changes_registered source=qa_building_burnout_stimulus`, and `wildfire_timberborn_delta_consumer_completed ... building_burnout_considered_deltas=<nonzero> ... building_burnout_matched_cells=<nonzero> ... building_burnout_applied_consequences=<nonzero>`. The follow-up `qa-readiness` or `status` result must include `last_delta_consumer_building_burnout_considered_deltas`, `last_delta_consumer_building_burnout_matched_cells`, and `last_delta_consumer_building_burnout_applied_consequences`, with all three above zero for pass evidence.

`TWF-042` adds the first player-facing alert loop. The alert sink is still compact-delta driven: it consumes only `TimberbornFireAlertEvent` values derived from changed simulator cells, aggregates them per dispatch, and sends at most one native Timberborn quick warning with new-fire count, burned-out-cell count, and max heat. It does not add command UI, arbitrary coordinates, or core gameplay rules. Live QA should use an existing stimulus that causes alert deltas, such as `qa-delta-stimulus` for new fire cells or `qa-building-burnout-stimulus` for fuel depletion, then capture a screenshot showing the quick warning in the native Timberborn notification area. Capture `Player.log` tokens for `wildfire_timberborn_delta_consequence_sink_bound lane=player_fire_alert`, `wildfire_timberborn_delta_consumer_completed ... alerts=<nonzero>`, and `wildfire_timberborn_player_fire_alert_updated ... notification_sent=true ... fire_started=<count> ... fuel_spent=<count> ... max_heat=<heat>`. A follow-up `qa-readiness` or `status` result must include `last_delta_consumer_alerts=<nonzero>`, `last_player_fire_alert_tick`, `last_player_fire_alert_started_fires`, `last_player_fire_alert_fuel_spent`, `last_player_fire_alert_max_heat`, `player_fire_alert_notifications`, `player_fire_alert_notification_sent=true`, and `player_fire_alert_presentation_failures=0`. Screenshot approval remains a QA step because unit tests prove aggregation, notification routing, failure isolation, and status telemetry, not the rendered on-screen placement.

Current `TWF-021` coverage adds the live compute-backed attachment path. `TimberbornFireRuntimeInitializer` builds the initial `FireGrid` from `MapSize.TerrainSize`, converts terrain cells from `ITerrainService.GetAllHeightsInCell(...)` through `TimberbornTerrainAdapter`, and initializes the runtime through `ITimberbornFireSimulatorFactory`. The factory manually loads `ComputeShaders/wildfire_compute_mac`, creates a real Unity `ComputeShader` simulator, and leaves fire-spread behavior in `FireSim.compute`.

Current `TWF-023` coverage resolves the previous live blocker. The Wildfire batchmode builder already matched the official `~/repos/timberborn-modding/Assets/Tools/Editor/Scripts/ModBuilding/AssetBundleBuilder.cs` call shape: explicit `AssetBundleBuild[]`, `BuildAssetBundleOptions.None`, and `BuildTarget.StandaloneOSX`. The missing compatibility requirement was the Unity built-in package `com.unity.modules.assetbundle` in the minimal batchmode project's `Packages/manifest.json`. Without that package, Unity emitted `'AssetBundle' is not supported because the module AssetBundle is disabled in the build.` and Timberborn rejected the generated bundle. With the package present, the warning is gone and Timberborn loads the bundle.

The deploy script now builds and stages two private bundles: `wildfire_compute_mac` containing `Assets/WildfireGenerated/FireSim.compute`, and `wildfire_diagnostic_mac` containing `Assets/WildfireGenerated/Diagnostic.txt`. The Timberborn loader probes the diagnostic text bundle before loading the compute bundle, so live logs separate "all Wildfire bundles fail" from "ComputeShader bundles fail" without replacing or faking simulator behavior.

Live evidence from 2026-05-01 after `bun scripts/deploy-timberborn-mod.ts --apply --clean`, restarting Timberborn, and continuing the Wildfire save:

- `Player.log` showed `wildfire_timberborn_diagnostic_asset_loaded bundle=wildfire_diagnostic_mac asset=assets/wildfiregenerated/diagnostic.txt text_length=33`.
- `Player.log` showed `wildfire_timberborn_compute_asset_loaded bundle=wildfire_compute_mac asset=assets/wildfiregenerated/firesim.compute`.
- `Player.log` showed `wildfire_timberborn_gpu_simulator_created width=128 height=128 depth=23 cell_count=376832` and `wildfire_timberborn_runtime_simulator_initialized width=128 height=128 depth=23`.
- After unpausing, `Player.log` showed `wildfire_timberborn_gpu_dispatch_kernel_started kernel=SimulateFullGrid tick=20 groups=16x16x6`, `wildfire_timberborn_gpu_readback_completed tick=20 delta_count=0`, and `wildfire_timberborn_dispatch_completed tick=20 delta_count=0`.
- `bun scripts/invoke-timberborn-command.ts status --wait=6` returned `wildfire_command_result command=status success=true status=success simulator_integrated=true width=128 height=128 depth=23 tick_count=20 queued_changes=0 last_delta_count=0 message=ok`.
- For `TWF-029`, `bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick` returned `wildfire_command_result command=qa-readiness success=true status=success bridge_alive=true runtime_loaded=true loaded_game_ready=true simulator_integrated=true width=128 height=128 depth=23 tick_count=178 queued_changes=0 last_delta_count=0 message=loaded_game_ready` after the loaded save was unpaused.

Search `~/Library/Logs/Mechanistry/Timberborn/Player.log` for `wildfire_command_bridge_ready`, `wildfire_command_request`, `wildfire_command_result`, `wildfire_timberborn_adapter_started`, `wildfire_timberborn_runtime_ready`, `wildfire_timberborn_runtime_initialize_started`, `wildfire_timberborn_diagnostic_asset_loaded`, `wildfire_timberborn_compute_asset_loaded`, `wildfire_timberborn_gpu_factory_created`, `wildfire_timberborn_gpu_simulator_initialized`, `wildfire_timberborn_runtime_simulator_initialized`, `wildfire_timberborn_cadence_configured`, `wildfire_timberborn_gpu_queued_changes`, `wildfire_timberborn_gpu_dispatch_kernel_started`, `wildfire_timberborn_gpu_dispatch_kernel_completed`, `wildfire_timberborn_gpu_readback_counter`, `wildfire_timberborn_gpu_readback_completed`, `wildfire_timberborn_gpu_listeners_notified`, `wildfire_timberborn_dispatch_started`, `wildfire_timberborn_dispatch_completed`, `wildfire_timberborn_adapter_stopping`, and `wildfire_timberborn_adapter_stopped`.

Do not satisfy this stage by attaching a dispatch-only or C# no-op simulator. Live completion requires `wildfire_command_result ... simulator_integrated=true` with numeric dimensions, `tick_count`, `queued_changes`, and `last_delta_count`, plus Player.log evidence that the AssetBundle loaded and the real compute dispatch/readback path ran.

## Timberborn Startup Log Harness

Use the startup log harness when QA needs repeatable evidence that the deployed Wildfire mod loaded in Timberborn:

```bash
bun scripts/check-timberborn-startup.ts --attach --wait=30
```

Use `--launch` instead of `--attach` when Timberborn should be opened by bundle id:

```bash
bun scripts/check-timberborn-startup.ts --launch --wait=120
```

The harness serializes with deploy work through the shared QA lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, validates the documented `1920x1080` display resolution by default, captures a `Player.log` baseline before attach or launch work, activates `com.mechanistry.timberborn`, waits for required current-window `Player.log` tokens, and writes evidence under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/startup-harness/<timestamp>/`.

Default required startup tokens are:

- `wildfire_command_bridge_ready`.
- `wildfire_timberborn_runtime_ready`.
- `wildfire_timberborn_diagnostic_asset_loaded`.
- `wildfire_timberborn_compute_asset_loaded`.
- `wildfire_timberborn_gpu_factory_created`.
- `wildfire_timberborn_runtime_simulator_initialized`.

Use `--require-command-status` only when a save is already loaded and the command bridge is expected to answer a read-only `status` request with `success=true` and `simulator_integrated=true`. Failure tokens after the `Player.log` baseline fail the run even when all success tokens are present. The startup harness does not click through the startup Mods dialog, load saves, unpause the simulation, or replace the live gameplay validation owned by later QA tickets. Screenshots are captured only on failure by default, or when explicitly requested with `--screenshot=always`.

## Timberborn QA Utilities

Use the local [Timberborn QA Utility skill](../.codex/skills/timberborn-qa-utility/SKILL.md) when building Bun/TypeScript scripts or guarded `cliclick`-style automation for live Timberborn QA.

UI automation must take coordinate targets from [timberborn-menu-coordinate-guide.md](timberborn-menu-coordinate-guide.md), verify the target app and expected screen before acting, and fail loudly rather than clicking through an unknown Timberborn state.

Use the latest-save startup utility when live QA needs to get from closed Timberborn, startup dialogs, or the standalone main menu into the latest loaded save and start simulation dispatch:

```bash
bun scripts/load-latest-save-and-unpause.ts --launch --wait=240
```

The default `--launch` route is the guarded signal-driven cold-start path. It launches by bundle id, retries macOS activation while Timberborn finishes connecting to AppleEvents, samples screenshot frames while Timberborn loads, presses `Enter` only after the startup Mods or Experimental Mode gates are positively identified, retries those gates while they remain visible, clicks only the documented `main.continue` coordinate, then waits for top-HUD loaded-save classification before unpause/status proof. The frame evidence is saved as PNG samples plus `fast-frame-samples.csv` because true video recording from Bun is brittle across macOS screen-recording permissions and display-capture failures.

Use `--attach` when Timberborn is already running:

```bash
bun scripts/load-latest-save-and-unpause.ts --attach --wait=120
```

The `--attach` route stays classifier-driven for already-running sessions. If `--launch` is used while Timberborn is already running, the utility also uses the classifier path instead of sending fast startup inputs into an unknown live state.

The utility serializes with deploy/startup work through the shared QA lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, validates the documented `1920x1080` display resolution by default, activates `com.mechanistry.timberborn`, captures screenshots for each identified transition under `~/Library/Application Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/<timestamp>/`, and preserves classifier screenshots as fallback/debug aids.

The narrow allowed path is startup confirmation by `Enter`, main-menu `Continue`, loaded-save HUD, and `hud.speed1` to unpause or set normal speed. The classifier attach path may click documented startup Mods `OK` or Experimental Mode Information `Start!` only after positive screen identification. The utility does not navigate arbitrary menus, select saves, delete saves, save the game, open debug panels, exit Timberborn, or invoke destructive actions.

Use screenshot classification mode to debug captured UI evidence without acquiring the shared QA lock, launching Timberborn, or clicking:

```bash
bun scripts/load-latest-save-and-unpause.ts --classify-screenshot ~/Library/Application\ Support/Mechanistry/Timberborn/WildfireQA/latest-save-startup/<timestamp>/<screen>.png
```

The classifier reports the visible Timberborn screen separately from blocking overlays, for example `screen=startup-mods blocking_overlay=mac-system-alert`. Live automation fails before clicking any Timberborn coordinate when a real macOS system alert overlay is detected. Clear system alerts manually before retrying the live startup path.

By default, the utility requests read-only `status` after unpause and requires `wildfire_command_result command=status success=true status=success simulator_integrated=true` plus a numeric `tick_count` greater than `0`. Use `--skip-post-status` only when validating UI flow without a deployed Wildfire command bridge. After a passing live run, inspect the copied `Player.log` in the artifact directory for `wildfire_timberborn_dispatch_completed`.

## Timberborn Deploy Pipeline

Use the deploy script to build and stage the Timberborn adapter without making Timberborn own simulation rules:

```bash
bun scripts/deploy-timberborn-mod.ts
```

The default mode is a dry-run. It acquires the shared build/deploy lock at `~/Library/Application Support/Timberborn/WildfireQA/locks/build-deploy.lock`, runs `dotnet build Wildfire.slnx --configuration Debug`, and prints `wildfire-deploy` lines for the mod id, version, target directory, manifest, and each planned assembly copy.

Expected deployed folder shape:

```text
~/Documents/Timberborn/Mods/Wildfire/
  manifest.json
  Scripts/
    Wildfire.Timberborn.dll
    Wildfire.Core.dll
    Wildfire.Timberborn.pdb
    Wildfire.Core.pdb
  ComputeShaders/
    wildfire_compute_mac
    wildfire_compute_mac.manifest
    wildfire_diagnostic_mac
    wildfire_diagnostic_mac.manifest
```

`Scripts/` contains the managed assemblies, following the official Timberborn mod builder's code-output convention. `ComputeShaders/` contains the Unity-built compute shader bundle generated from `src/Wildfire.Unity/FireSim.compute` and a text-only diagnostic bundle generated by `DiagnosticTextAssetBundleBuilder`; both are intentionally outside Timberborn's built-in `AssetBundles/` auto-load folder so the adapter can load exact private paths after startup. The script only stages known build artifacts from `src/Wildfire.Timberborn/bin/<Configuration>/netstandard2.1/` and known bundles from the Unity batchmode project; it does not copy `docs/`, `kanban/`, `.git/`, or other internal repository content into the deployed mod.

The deploy script must validate generated bundle manifests before copying them. A valid compute bundle manifest includes `Assets/WildfireGenerated/FireSim.compute`; a valid diagnostic bundle manifest includes `Assets/WildfireGenerated/Diagnostic.txt`. If either manifest describes another mod's assets or a stale wrong bundle, treat the deploy as invalid and rebuild before running live QA.

Run the real deploy only when Timberborn is closed or QA explicitly approves writing while the game is open:

```bash
bun scripts/deploy-timberborn-mod.ts --apply
```

Optional command flags:

- `--configuration=Release` builds and stages Release artifacts.
- `--skip-build` reuses existing build output.
- `--skip-asset-bundle` reuses existing `wildfire_compute_mac` and `wildfire_diagnostic_mac` bundles instead of running Unity batchmode.
- `--unity-executable=/path/to/Unity` selects a Unity Editor when `WILDFIRE_UNITY_EXECUTABLE` is not set.
- `--mods-dir=/path/to/Mods` targets a non-default Timberborn Mods directory.
- `--dry-run --remove` prints the cleanup action for the deployed Wildfire folder without deleting it.

Cleanup is intentionally manual for live QA safety. Close Timberborn first, then inspect the dry-run remove output. Only remove `~/Documents/Timberborn/Mods/Wildfire` when QA no longer needs the deployed mod evidence.

Player.log proof remains a live-QA step after a real deploy. Capture `~/Library/Logs/Mechanistry/Timberborn/Player.log` evidence that Timberborn discovered the Wildfire folder or loaded `Wildfire.Timberborn.dll`; if Timberborn cannot load the assembly yet, record the exact loader error and keep the fix in `Wildfire.Timberborn` or the deploy script rather than moving fire rules into `Wildfire.Core`.
