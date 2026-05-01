# Wildfire Handoff

## Current State

- Fresh Wildfire repository scaffolded in `/Users/jasonkleinberg/repos/wildfire`.
- Core CPU simulation exists with packed cells, deterministic hash randomness, active frontier tracking, consolidated per-cell tick deltas, listener notification, and unit tests.
- CLI harness exists with named seeded scenarios, seed/dimension/layer/tick options, and packed-cell-derived rendering.
- Unity and Timberborn projects are placeholders; they should remain host adapters.
- CPU snapshot scenarios now provide the first parity oracle for future GPU work.
- Prometheus sprint lessons are incorporated as docs, role instructions, local skills, and a file-based ticket board, not as copied Prometheus runtime code.

## Current Ticket State

- `TWF-001` through `TWF-005` are in `kanban/by-status/06-done/`.
- `TWF-006` is in `kanban/by-status/02-ready/` for the first full-grid Unity compute snapshot parity harness.

## Next Exact Action

Start with:

- `kanban/by-status/02-ready/TWF-006-full-grid-unity-compute-snapshot-parity.md`

## Known Gaps

- CLI pause display has a known polish issue: paused interactive rendering reports `tick=0`.
- GPU compute backend is not implemented.
- Timberborn adapters are not implemented.
- No live Timberborn build/deploy pipeline exists yet.
