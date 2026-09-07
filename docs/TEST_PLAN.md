# Wildfire Validation Runbook

Choose validation that reaches the changed behavior. Portable contract tests, actual shader execution, and live game integration answer different questions. Record the revision, command, environment, result, and remaining uncertainty; a historical pass is evidence for its recorded build only.

## Portable checks

Install Bun dependencies and use .NET 10. From the repository root:

```bash
bun install --frozen-lockfile
bun run typecheck
bun run blueprints:check
bun run test:portable
bun run test:scripts
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --layer=0
```

[Hosted CI](../.github/workflows/ci.yml) runs typechecking, blueprint generation checks, the checked-in portable .NET suite, script behavior tests, and a deploy plan. `typecheck` generates the Prisma client before TypeScript checking. [Wildfire.Portable.slnx](../Wildfire.Portable.slnx) includes Core/CLI/compute-contract tests and shader test discovery, with no native Timberborn dependency. The portable command explicitly disables Unity execution so unavailable shader checks are visible as skipped.

Run relevant `bun test` files in [tests/](../tests/) for focused script behavior checks, or `bun run test:scripts` for all of them. Typechecking and executing tests are separate checks. For a deployment-plan change, the hosted-safe command is:

```bash
bun scripts/deploy-timberborn-mod.ts --plan-only --configuration Release --mods-dir /tmp/wildfire-mods-plan
```

## Full .NET suite and native adapter build

```bash
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj
dotnet test Wildfire.slnx
```

The [native test project](../tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj) references Timberborn and is separate from the portable tests. The full solution builds both, so incompatible game assemblies can block native tests without preventing portable validation. The adapter and native tests share [Timberborn.Managed.props](../build/Timberborn.Managed.props), defaulting to the local macOS Steam install. Override the managed assembly directory explicitly when needed:

```bash
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj \
  -p:TimberbornManagedPath=/path/to/Timberborn/Managed
```

A working SDK alone is insufficient. Selecting another assembly directory does not establish platform support.

Classify build/API incompatibility separately from failed assertions. Capture compiler errors and assembly/game versions when available. A portable pass does not compensate for an adapter compilation failure. See the [2026-09-04 review baseline](history/2026-09-04/review-baseline.md) for the earlier test arrangement and one dated failure, not a permanent environment rule.

## Shader execution

Portable fake-dispatcher tests verify upload, dispatch, and readback contracts without executing HLSL. Use a licensed Unity Editor with compute-capable graphics for shader behavior. The opt-in test entry point is:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 \
WILDFIRE_UNITY_EXECUTABLE=/path/to/Unity \
dotnet test tests/Wildfire.Shader.Tests/Wildfire.Shader.Tests.csproj
```

The shader project has no Timberborn assembly dependency. Disabled cases report skipped with an opt-in reason; enabled cases must execute Unity and assert the capture. Check compile, buffer, dispatch, and readback results, and retain fixture/capture output. Snapshot changes need a behavioral explanation, not automatic acceptance of newly generated values.

Fixture execution serializes the current Core simulation parameters instead of maintaining separate runner tuning. A fixture may supply a complete `parameters` override. Older files without that object receive Core defaults when loaded and serialized; CLI fixture exports include the parameters required by the direct Unity runner.

For direct fixture execution independent of the test project:

```bash
mkdir -p /tmp/wildfire-shader-review
dotnet run --project src/Wildfire.Cli -- \
  --scenario=single-ignition --seed=21 --width=5 --height=5 --depth=1 --layer=0 \
  --export-fixture=/tmp/wildfire-shader-review/input.fixture.json
/path/to/Unity -batchmode -quit \
  -projectPath "$PWD/src/Wildfire.Unity/UnityBatchmodeProject" \
  -executeMethod Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture \
  -logFile /tmp/wildfire-shader-review/unity.log -- \
  --fixture /tmp/wildfire-shader-review/input.fixture.json \
  --shader "$PWD/src/Wildfire.Unity/FireSim.compute" \
  --output /tmp/wildfire-shader-review/output.capture.json --ticks 2
```

Core's checked-in tests exercise the shared GPU encoder, queue, and tick coordinator used by both simulator backends. The batchmode runner is another binding of the shader protocol. Shader success must be paired with native-path checks when the change affects the game upload layout, buffer capacity, or bindings.

## Live Timberborn validation

Use live validation for native consequences, saved-state behavior, rendered output, and integration timing. Coordinate one controller for the shared game, deploy folder, and save state. Use a disposable QA save for stimuli that damage entities, change inventories, or alter terrain. A menu load is not a loaded-world runtime pass.

The [deploy runbook](reference/timberborn-deploy-pipeline.md) describes build, staging, locking, and asset bundles. The command bridge supports a focused readiness check after a save loads:

```bash
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
```

The bridge defaults to diagnostics, including in Debug builds. Forced stimuli, inventory changes, and preset changes require the exact process argument `--wildfire-enable-qa-mutations` when starting Timberborn on a copied/disposable save. Confirm `command_access=development` in the fresh bridge startup log first. Relaunch without the argument for a release rejection check: a mutation request must return `message=qa_mutations_disabled`. See [command access and the complete inventory](release/qa-command-access.md) for the launch route and file transport boundary.

Choose stimuli from the bridge's supported commands and the relevant issue's acceptance criteria. One suppression example is:

```bash
bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed
```

Correlate command response, advancing tick, fresh `Player.log`, and the specific world effect. Runtime initialization distinguishes waiting, ready, unsupported, and failed states; unsupported/failed worlds do not repeatedly import on every update. Reset/load deliberately when retesting changed initialization prerequisites. Capture and inspect the whole scene for visual work. Counters establish execution; they do not establish rendering quality. For persistence changes, exercise save, reload, and the affected state rather than relying on serializer round trips alone.

Last-dispatch counters can return to zero after an effect has occurred. The `last_positive_*` telemetry and the original dispatch log retain evidence that a later status sample may miss.

If startup or transport fails, inspect process and log state before retrying. Distinguish game failure, tool failure, and a missing prerequisite. [QA tooling](qa-tooling.md) documents automation commands and optional reliability analysis; [UI references](reference/timberborn-ui.md) and [debug panels](timberborn-debug-panels.md) support targeted inspection. Old screenshots provide landmarks, not current coordinates.

## Focused regression boundaries

- GPU protocol changes: encoding of every supported override, ordered/capacity-bounded batches, failure-stage queue consumption, delta capacity, and actual shader captures.
- Runtime initialization: wait/retry, reject oversized worlds before import, fail once, reset on load/unload, and dispose an unpublished candidate if setup fails, and preserve original save encoding until readiness. Corrupt/unsupported saved state must fail initialization without silently replacing the payload.
- Stimulus scheduling: user-tool and QA input timing, unrelated-change priority, reset, and successful-tick proof accounting. `sustained_ignition_dispatch_ticks` reports duration (default 12, slow preset 96), not fire simulation cadence.
- Ash observation: read transport at most once per synchronized tick, preserve packed cells for persistence only, and verify transport-only changes reach gameplay consumers.
- Native consequences: preserve consumed/produced/existing-stock accounting and validate entity rebuild outcomes on a disposable save. Unit tests and build success do not prove native rollback safety.

## Release validation

Packaging and publication have their own [versioning](release/versioning.md) and [Workshop](release/workshop.md) runbooks. Validate the packaged artifact in the target game environment; package contents alone do not establish platform support. An authenticated upload is a separate action from generating or inspecting a package.
