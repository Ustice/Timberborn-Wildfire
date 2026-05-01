# Wildfire Handoff

## Current State

- Fresh Wildfire repository scaffolded in `/Users/jasonkleinberg/Documents/New project`.
- Core CPU simulation exists with packed cells, deterministic hash randomness, active frontier tracking, deltas, listener notification, and unit tests.
- CLI harness exists for seeded terminal rendering.
- Unity and Timberborn projects are placeholders; they should remain host adapters.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- `TWF-001` is in `kanban/by-status/02-ready/` for CPU-core hardening.
- `TWF-002` through `TWF-005` are staged in `kanban/by-status/01-todo/` as milestone tickets.

## Next Exact Action

Start with `kanban/by-status/02-ready/TWF-001-core-rules-and-frontier-hardening.md`.

Verify the CPU rules against the design spec, add missing edge-case tests, and keep the core independent from Timberborn or Unity.

## Known Gaps

- CLI controls are minimal and need scenario/config work.
- CPU delta consolidation can be improved if external change and rule processing both touch the same cell in one tick.
- GPU compute backend is not implemented.
- Timberborn adapters are not implemented.
- No live Timberborn build/deploy pipeline exists yet.
