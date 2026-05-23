# Wildfire Organization Plan

This plan improves project discoverability without changing fire behavior.

## Progress Points

1. Refresh entry-point documentation.

   - Keep `README.md` current with the real project surfaces.
   - Keep `docs/INDEX.md` focused on source-of-truth documents.
   - Remove stale links to the archived file-kanban process.

2. Add a source map for common change paths.

   - Point readers from product concepts to source folders, tests, commands, and validation docs.
   - Prefer pointers over copied implementation descriptions.

3. Organize `Wildfire.Timberborn` by responsibility.

   - Keep the assembly and namespace stable.
   - Move adapter files into folders matching architecture concepts: runtime, mapping, simulation, visuals, consequences, ash, beavers, tools, QA, settings, persistence, alerts, and compatibility.
   - Update documentation paths after the move.

4. Split large modules only after the folder move is stable.

   - Start with `TimberbornDeltaConsumers.cs` and `TimberbornQaCommandBridge.cs`.
   - Keep public types and tests intact while extracting contracts, summaries, and null adapters into adjacent files.
   - Treat this as a follow-up implementation slice because it changes more review surface than a folder-only move.

5. Align test organization with production responsibilities.

   - Keep the current test project until a rename is explicitly worth the churn.
   - Add test folders by production concept when touching those tests for real behavior work.

## Completion Criteria

- A new contributor can start from `README.md`, then use `docs/INDEX.md` and `docs/source-map.md` to find the right code surface.
- `src/Wildfire.Timberborn` is no longer a flat list of adapter files.
- Documentation links to moved source files resolve to current paths.
- `dotnet test` and `bun run typecheck` still pass.
