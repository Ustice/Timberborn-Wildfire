# Wildfire Source Map

Use this page when you know the concept you need to change, but not the file yet.

## Start Here

- Product and simulation design: [DESIGN.md](DESIGN.md).
- Durable code ownership: [ARCHITECTURE.md](ARCHITECTURE.md).
- Current status and validation cautions: [HANDOFF.md](HANDOFF.md).
- Validation commands and evidence expectations: [TEST_PLAN.md](TEST_PLAN.md).
- Active backlog: [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues).

## Code Surfaces

| Need | Start In | Nearby Tests |
| --- | --- | --- |
| Packed cells, fields, simulator contracts, material schema | `src/Wildfire.Core/` | `tests/Wildfire.Core.Tests/PackedCellTests.cs`, `tests/Wildfire.Core.Tests/WildfireMaterialFieldSchemaTests.cs`, `tests/Wildfire.Core.Tests/WildfireTransportFieldStateTests.cs` |
| Compute buffers, shader dispatch, visual fields, shader snapshots | `src/Wildfire.Unity/` | `tests/Wildfire.Core.Tests/ComputeBufferGridTests.cs`, `tests/Wildfire.Core.Tests/UnityComputeFireSimulatorTests.cs`, `tests/Wildfire.Core.Tests/ShaderSnapshotHarnessTests.cs` |
| CLI scenarios and fixture inspection | `src/Wildfire.Cli/` | `tests/Wildfire.Core.Tests/CliScenarioTests.cs` |
| Timberborn runtime lifecycle and dispatch | `src/Wildfire.Timberborn/Runtime/` | `tests/Wildfire.Core.Tests/TimberbornFireDeltaConsumerTests.cs` |
| Timberborn world import and cell mapping | `src/Wildfire.Timberborn/Mapping/` | `tests/Wildfire.Core.Tests/TimberbornFireCellMapperTests.cs`, `tests/Wildfire.Core.Tests/TimberbornWorldCellImporterTests.cs` |
| Timberborn compute simulator binding | `src/Wildfire.Timberborn/Simulation/` | `tests/Wildfire.Core.Tests/UnityComputeFireSimulatorTests.cs` |
| Fire, smoke, steam, ash, and burned-texture presentation | `src/Wildfire.Timberborn/Visuals/` | `tests/Wildfire.Core.Tests/TimberbornGpuFieldRendererTests.cs`, `tests/Wildfire.Core.Tests/TimberbornGpuVisualFieldSurfaceTests.cs` |
| Burn damage and world consequences | `src/Wildfire.Timberborn/Consequences/` | `tests/Wildfire.Core.Tests/Timberborn*ConsequenceTests.cs`, `tests/Wildfire.Core.Tests/TimberbornBurnDamageStateTests.cs` |
| Simulator-backed ash read model, collection, and application | `src/Wildfire.Timberborn/Ash/` | `tests/Wildfire.Core.Tests/TimberbornAshFieldServiceTests.cs`, `tests/Wildfire.Core.Tests/TimberbornFertilizeDesignationServiceTests.cs` |
| Beaver exposure and field behavior | `src/Wildfire.Timberborn/Beavers/` | `tests/Wildfire.Core.Tests/TimberbornBeaverFieldExposureTelemetryTests.cs`, `tests/Wildfire.Core.Tests/TimberbornBeaverFieldBehaviorTests.cs` |
| Player tools and bottom-bar buttons | `src/Wildfire.Timberborn/Tools/` | `tests/Wildfire.Core.Tests/TimberbornFertilizeDesignationServiceTests.cs` |
| QA command bridge and file-backed command polling | `src/Wildfire.Timberborn/Qa/` | `tests/Wildfire.Core.Tests/TimberbornQaCommandBridgeTests.cs` |
| Alerts and camera focus | `src/Wildfire.Timberborn/Alerts/` | `tests/Wildfire.Core.Tests/TimberbornPlayerFireAlertTests.cs` |
| Compatibility probes and runtime gates | `src/Wildfire.Timberborn/Compatibility/` | `tests/Wildfire.Core.Tests/TimberbornCompatibilityProbeTests.cs` |
| Release settings and presets | `src/Wildfire.Timberborn/Settings/` | `tests/Wildfire.Core.Tests/WildfireReleaseSettingsTests.cs`, `tests/Wildfire.Core.Tests/TimberbornReleaseLogNoisePolicyTests.cs` |
| Save/load persistence | `src/Wildfire.Timberborn/Persistence/` | `tests/Wildfire.Core.Tests/TimberbornWildfirePersistenceTests.cs` |
| Mod packaging, deploy, QA automation, and generated references | `scripts/` | `tests/*.test.ts`, `bun run typecheck` |

## Common Commands

```bash
bun run typecheck
dotnet test Wildfire.slnx --no-restore
bun scripts/deploy-timberborn-mod.ts --apply --clean --lock-timeout 60
bun scripts/invoke-timberborn-command.ts qa-readiness --wait=6 --require-advanced-tick
```
