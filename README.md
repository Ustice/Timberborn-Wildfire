# Wildfire

[Wildfire](https://steamcommunity.com/sharedfiles/filedetails/?id=3730392791) is a Timberborn mod built around a reusable cellular automata fire simulation. GPU shaders own fire rules; Timberborn supplies world observations and applies gameplay consequences.

Start with the [documentation index](docs/INDEX.md), [source map](docs/source-map.md), or [GitHub Issues](https://github.com/Ustice/Timberborn-Wildfire/issues). Read the document relevant to your change; the index distinguishes current contracts from historical plans and evidence.

## Project layout

- `src/Wildfire.Core/`: packed cells, field formats, parameters, fixtures, and simulator contracts.
- `src/Wildfire.Cli/`: seeded scenario preview and fixture export.
- `src/Wildfire.Unity/`: compute abstractions, shaders, and the Unity batchmode harness.
- `src/Wildfire.Timberborn/`: native compute binding, runtime, gameplay, visuals, persistence, and QA bridge.
- `tests/`: .NET and TypeScript tests.
- `scripts/`: development, packaging, deployment, and QA tooling.
- `docs/`: design, architecture, validation, reference material, and dated history.

## Development

Use Bun and .NET 10. The portable starting checks are:

```bash
bun install --frozen-lockfile
bun run typecheck
bun run blueprints:check
bun run test:portable
bun run test:scripts
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --layer=0
```

The portable suite runs the checked-in Core, CLI, and compute-contract tests without Timberborn. Shader tests are reported as skipped unless explicitly enabled. The full `dotnet test Wildfire.slnx` suite additionally requires compatible installed Timberborn managed assemblies. Shader execution needs a licensed Unity Editor and compute-capable graphics. See the [validation runbook](docs/TEST_PLAN.md) for commands and the limits of each check.
