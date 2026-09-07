# Wildfire Source Map

Use this page when you know the concept you need to change, but not the file yet.

## Start Here

- Product and simulation design: [DESIGN.md](DESIGN.md).
- Durable code ownership: [ARCHITECTURE.md](ARCHITECTURE.md).
- Validation commands and evidence expectations: [TEST_PLAN.md](TEST_PLAN.md).
- Active backlog: [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues).

## Code Surfaces

| Need | Start In | Nearby Tests |
| --- | --- | --- |
| Packed cells, fields, simulator contracts, material schema | `src/Wildfire.Core/` | `tests/Wildfire.Core.Tests/PackedCellTests.cs`, `tests/Wildfire.Core.Tests/WildfireMaterialFieldSchemaTests.cs`, `tests/Wildfire.Core.Tests/WildfireTransportFieldStateTests.cs` |
| Compute backend, buffers, visual fields, shader snapshots | `src/Wildfire.Unity/` | `tests/Wildfire.Core.Tests/ComputeBufferGridTests.cs`, `tests/Wildfire.Core.Tests/UnityComputeFireSimulatorTests.cs`, `tests/Wildfire.Core.Tests/ShaderSnapshotHarnessTests.cs` |
| Shared GPU encoding, queue, tick ordering and committed host inputs | `src/Wildfire.Core/FireSimGpuProtocol.cs`, `src/Wildfire.Core/FireSimChangeQueue.cs`, `src/Wildfire.Core/FireSimStepCoordinator.cs`, `src/Wildfire.Core/FireSimStepInput.cs` | `tests/Wildfire.Core.Tests/FireSimGpuProtocolTests.cs`, `tests/Wildfire.Core.Tests/FireSimStepCoordinatorTests.cs`, `tests/Wildfire.Core.Tests/FireSimStepInputTests.cs` |
| Real Unity shader execution | `src/Wildfire.Unity/UnityBatchmodeProject/` | `tests/Wildfire.Shader.Tests/UnityShaderExecutionHarnessTests.cs` |
| CLI scenarios and fixture inspection | `src/Wildfire.Cli/` | `tests/Wildfire.Core.Tests/CliScenarioTests.cs` |
| Timberborn initialization, composition, and dispatch | `src/Wildfire.Timberborn/Runtime/TimberbornRuntimeInitialization.cs`, `src/Wildfire.Timberborn/Runtime/TimberbornFireRuntime.cs`, `src/Wildfire.Timberborn/Runtime/TimberbornFireSystem.cs` | `tests/Wildfire.Timberborn.Tests/TimberbornRuntimeInitializationTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornFireDeltaConsumerTests.cs` |
| Timberborn world import and cell mapping | `src/Wildfire.Timberborn/Mapping/` | `tests/Wildfire.Timberborn.Tests/TimberbornFireCellMapperTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornWorldCellImporterTests.cs` |
| Timberborn compute simulator binding | `src/Wildfire.Timberborn/Simulation/` | Shared protocol/coordinator tests cover common behavior; native buffer bindings still require actual game validation. |
| Fire, smoke, steam, ash, and burned-texture presentation | `src/Wildfire.Timberborn/Visuals/` | `tests/Wildfire.Timberborn.Tests/TimberbornGpuFieldRendererTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornGpuVisualFieldSurfaceTests.cs` |
| Burn damage and world consequences | `src/Wildfire.Timberborn/Consequences/` | `tests/Wildfire.Timberborn.Tests/Timberborn*ConsequenceTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornBurnDamageStateTests.cs` |
| Transport observation, ash read model, collection, and application | `src/Wildfire.Timberborn/Simulation/ITimberbornTransportFieldReader.cs`, `src/Wildfire.Timberborn/Ash/` | `tests/Wildfire.Timberborn.Tests/TimberbornAshFieldSynchronizerTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornAshFieldServiceTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornFertilizeDesignationServiceTests.cs` |
| Beaver exposure and field behavior | `src/Wildfire.Timberborn/Beavers/` | `tests/Wildfire.Timberborn.Tests/TimberbornBeaverFieldExposureTelemetryTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornBeaverFieldBehaviorTests.cs` |
| Player tools and bottom-bar buttons | `src/Wildfire.Timberborn/Tools/` | `tests/Wildfire.Timberborn.Tests/TimberbornFertilizeDesignationServiceTests.cs` |
| QA command bridge, process opt-in, target selection and stimuli | `src/Wildfire.Timberborn/Qa/TimberbornQaController.cs`, `src/Wildfire.Timberborn/Qa/TimberbornQaTargetSelector.cs`, `src/Wildfire.Timberborn/Qa/TimberbornQaCommandBridge.cs`, `src/Wildfire.Timberborn/Qa/TimberbornQaCommandPolicy.cs` | `tests/Wildfire.Timberborn.Tests/TimberbornQaCommandBridgeTests.cs`; [command access](release/qa-command-access.md) |
| Sustained user-tool and QA ignition | `src/Wildfire.Timberborn/Runtime/TimberbornSustainedIgnitionScheduler.cs` | `tests/Wildfire.Timberborn.Tests/TimberbornQaCommandBridgeTests.cs` |
| Alerts and camera focus | `src/Wildfire.Timberborn/Alerts/` | `tests/Wildfire.Timberborn.Tests/TimberbornPlayerFireAlertTests.cs` |
| Native assembly path, inventory accounting, rebuild requests, probes | `build/Timberborn.Managed.props`, `src/Wildfire.Timberborn/Compatibility/` | `tests/Wildfire.Timberborn.Tests/TimberbornCompatibilityProbeTests.cs` |
| Release settings and explicit stimulus-duration presets | `src/Wildfire.Timberborn/Settings/`, `src/Wildfire.Timberborn/Simulation/TimberbornFireSimParameterPresets.cs` | `tests/Wildfire.Timberborn.Tests/WildfireReleaseSettingsTests.cs`, `tests/Wildfire.Timberborn.Tests/TimberbornReleaseLogNoisePolicyTests.cs` |
| Save/load persistence | `src/Wildfire.Timberborn/Persistence/` | `tests/Wildfire.Timberborn.Tests/TimberbornWildfirePersistenceTests.cs` |
| Mod packaging, deploy, QA automation, and generated references | `scripts/` | `tests/*.test.ts`, `bun run typecheck` |

## Common Commands

```bash
bun run typecheck
dotnet test Wildfire.slnx --no-restore
bun run test:portable
bun run test:scripts
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
```
