# Wildfire

Wildfire is a Timberborn mod project built around a reusable, deterministic cellular automata fire simulation.

The simulation core is intentionally independent from Timberborn so packed scenario inputs, GPU simulator contracts, and host adapters can evolve without making Timberborn own fire rules.

## Project Layout

- `src/Wildfire.Core/` contains the packed-cell model, grid helpers, GPU simulator contracts, deltas, and listener contracts.
- `src/Wildfire.Cli/` contains the terminal preview for seeded scenarios.
- `src/Wildfire.Unity/` is the planned compute shader prototype surface.
- `src/Wildfire.Timberborn/` is the planned Timberborn adapter layer.
- `tests/Wildfire.Core.Tests/` contains packed-cell and scenario tests.
- `docs/` contains design, architecture, handoff, validation, and milestone status.
- `kanban/` contains coordination, roles, tickets, and status lanes.

## Start Here

- Read [docs/INDEX.md](docs/INDEX.md) for the document map.
- Read [docs/DESIGN.md](docs/DESIGN.md) for the product and simulation spec.
- Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for durable ownership boundaries.
- Read [kanban/process.md](kanban/process.md) before starting a ticket-board sprint.

## Commands

```bash
dotnet test
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --layer=0
```
