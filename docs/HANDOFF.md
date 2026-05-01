# Wildfire Handoff

## Current State

- Wildfire repository scaffolded in `~/repos/wildfire`.
- Core packed-cell, grid, GPU simulator contract, delta, listener, and change-registration types exist.
- CLI scenario preview exists with named seeded scenarios, seed/dimension/layer options, and packed-cell-derived rendering.
- Unity and Timberborn projects are placeholders; they should remain host adapters.
- The old alternate C# execution path and its snapshot tests were removed; future simulation work should target the GPU simulator path only.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- No active sprint tickets are currently defined.
- The previous ticket set was removed so the board can be rebuilt from the updated design document.
- `kanban/all-tickets/TEMPLATE.md` remains as the starting point for new tickets.

## Next Exact Action

Start with:

- Read `docs/DESIGN.md`.
- Create a fresh ticket set in `kanban/all-tickets/` that reflects the updated design.
- Move dependency-ready tickets into `kanban/by-status/02-ready/`.

## Known Gaps

- GPU compute simulator is not implemented.
- Timberborn adapters are not implemented.
- No live Timberborn build/deploy pipeline exists yet.
