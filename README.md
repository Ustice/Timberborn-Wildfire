# Wildfire

Wildfire is a Timberborn mod project built around a reusable, deterministic cellular automata fire simulation.

The simulation core is intentionally independent from Timberborn so packed scenario inputs, GPU simulator contracts, and host adapters can evolve without making Timberborn own fire rules.

## Project Layout

- `src/Wildfire.Core/` contains the packed-cell model, grid helpers, GPU simulator contracts, deltas, and listener contracts.
- `src/Wildfire.Cli/` contains the terminal preview for seeded scenarios.
- `src/Wildfire.Unity/` contains compute-buffer, shader-dispatch, shader-snapshot, and visual-field code.
- `src/Wildfire.Timberborn/` contains the Timberborn adapter layer, organized by runtime responsibility.
- `tests/Wildfire.Core.Tests/` contains core, Unity, and Timberborn adapter tests.
- `docs/` contains design, architecture, handoff, validation, and milestone status.
- `kanban/` contains GitHub issue workflow notes, role guidance, migration notes, and archived file-board pointers.

## Start Here

- Read [docs/INDEX.md](docs/INDEX.md) for the document map.
- Read [docs/source-map.md](docs/source-map.md) to find the code surface for a concept.
- Read [docs/DESIGN.md](docs/DESIGN.md) for the product and simulation spec.
- Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for durable ownership boundaries.
- Read [kanban/github-issue-workflow.md](kanban/github-issue-workflow.md) before starting issue-backed work.

## Commands

```bash
bun run typecheck
dotnet test Wildfire.slnx --no-restore
dotnet run --project src/Wildfire.Cli -- --scenario=single-ignition --layer=0
```
