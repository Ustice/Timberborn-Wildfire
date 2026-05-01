# Wildfire

Wildfire is a Timberborn mod project built around a reusable, deterministic cellular automata fire simulation.

The simulation core is intentionally independent from Timberborn so fire rules can be tested from a CLI harness, prototyped in Unity compute shaders, and then integrated into Timberborn through adapters.

## Project Layout

- `src/Wildfire.Core/` contains the packed-cell model, deterministic rules, CPU simulator, deltas, and listener contracts.
- `src/Wildfire.Cli/` contains the terminal harness for tuning and seeded scenarios.
- `src/Wildfire.Unity/` is the planned compute shader prototype surface.
- `src/Wildfire.Timberborn/` is the planned Timberborn adapter layer.
- `tests/Wildfire.Core.Tests/` contains deterministic CPU-core tests.
- `docs/` contains design, architecture, handoff, validation, and milestone status.
- `kanban/` contains coordination, roles, tickets, and status lanes.

## Start Here

- Read [docs/INDEX.md](docs/INDEX.md) for the document map.
- Read [docs/DESIGN.md](docs/DESIGN.md) for the product and simulation spec.
- Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for durable ownership boundaries.
- Read [kanban/process.md](kanban/process.md) before starting a ticket-board sprint.

## Commands

```bash
bun run test
./tickets move TWF-001 ready
dotnet run --project src/Wildfire.Cli -- 64 32 4
```
