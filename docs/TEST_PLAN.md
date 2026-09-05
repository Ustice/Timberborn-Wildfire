# Wildfire Validation Runbook

Choose validation that reaches the changed behavior. Portable contract tests, actual shader execution, and live game integration answer different questions. Record the revision, command, environment, result, and remaining uncertainty; a historical pass is evidence for its recorded build only.

## Portable checks

Install Bun dependencies and use .NET 10. From the repository root:

```bash
bun install --frozen-lockfile
bun run typecheck
bun run blueprints:check
bun scripts/run-hosted-dotnet-tests.ts
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --layer=0
```

[Hosted CI](../.github/workflows/ci.yml) runs typechecking, blueprint generation checks, the hosted .NET script, and a deploy plan. `typecheck` generates the Prisma client before TypeScript checking. The [hosted script](../scripts/run-hosted-dotnet-tests.ts) currently creates four smoke tests in a temporary project referencing only Core. It does not run all checked-in Core, Unity, or Timberborn tests.

Run relevant `bun test` files in [tests/](../tests/) for script behavior. Do not assume the typecheck command executes them. For a deployment-plan change, the hosted-safe command is:

```bash
bun scripts/deploy-timberborn-mod.ts --plan-only --configuration Release --mods-dir /tmp/wildfire-mods-plan
```

## Full .NET suite and native adapter build

```bash
dotnet test Wildfire.slnx
```

The [test project](../tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj) references CLI, Core, Unity, and Timberborn. Consequently, even a filtered portable test can fail before execution when the native adapter cannot compile. The adapter resolves managed assemblies from the local macOS Timberborn Steam install through its project file. A working SDK alone is insufficient.

Classify build/API incompatibility separately from failed assertions. Capture compiler errors and assembly/game versions when available. A four-test hosted pass does not compensate for an adapter compilation failure. See the [2026-09-04 review baseline](history/2026-09-04/review-baseline.md) for one dated instance, not a permanent environment rule.

## Shader execution

Portable fake-dispatcher tests verify upload, dispatch, and readback contracts without executing HLSL. Use a licensed Unity Editor with compute-capable graphics for shader behavior. The opt-in test entry point is:

```bash
WILDFIRE_RUN_UNITY_SHADER_HARNESS=1 \
WILDFIRE_UNITY_EXECUTABLE=/path/to/Unity \
dotnet test --filter FullyQualifiedName~UnityHarness
```

That test still inherits the full test project's native build dependencies. At the review baseline, disabled harness tests could return successfully without executing Unity; always establish whether the harness actually ran. Check compile, buffer, dispatch, and readback results, and retain fixture/capture output. Snapshot changes need a behavioral explanation, not automatic acceptance of newly generated values.

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

The batchmode runner is another binding of the shader protocol. Shader success must be paired with native-path checks when the change affects the game upload layout, buffer capacity, or bindings.

## Live Timberborn validation

Use live validation for native consequences, saved-state behavior, rendered output, and integration timing. Coordinate one controller for the shared game, deploy folder, and save state. Use a disposable QA save for stimuli that damage entities, change inventories, or alter terrain. A menu load is not a loaded-world runtime pass.

The [deploy runbook](reference/timberborn-deploy-pipeline.md) describes build, staging, locking, and asset bundles. The command bridge supports a focused readiness check after a save loads:

```bash
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
```

Choose stimuli from the bridge's supported commands and the relevant issue's acceptance criteria. One suppression example is:

```bash
bun scripts/invoke-timberborn-command.ts qa-water-suppression-stimulus --wait=6 --require-advanced-tick
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick --require-water-changed
```

Correlate command response, advancing tick, fresh `Player.log`, and the specific world effect. Capture and inspect the whole scene for visual work. Counters establish execution; they do not establish rendering quality. For persistence changes, exercise save, reload, and the affected state rather than relying on serializer round trips alone.

If startup or transport fails, inspect process and log state before retrying. Distinguish game failure, tool failure, and a missing prerequisite. [QA tooling](qa-tooling.md) documents automation commands and optional reliability analysis; [UI references](reference/timberborn-ui.md) and [debug panels](timberborn-debug-panels.md) support targeted inspection. Old screenshots provide landmarks, not current coordinates.

## Release validation

Packaging and publication have their own [versioning](release/versioning.md) and [Workshop](release/workshop.md) runbooks. Validate the packaged artifact in the target game environment; package contents alone do not establish platform support. An authenticated upload is a separate action from generating or inspecting a package.

## Evidence and history

Put durable acceptance results with their GitHub issue or a dated evidence report. Keep this document focused on repeatable validation rather than accumulating every run. Record local artifact paths when evidence cannot be committed, and identify missing artifacts honestly.

The [archived test plan](history/2026-09-04/TEST_PLAN.md) retains detailed May 2026 scenarios, release and save-lifecycle evidence, command examples, screenshots, and known failures. Its frozen claims and issue statuses are historical. Refresh both source and environment before reusing a procedure.
