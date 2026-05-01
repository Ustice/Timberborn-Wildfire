# Wildfire Test Plan

## Unit Tests

Core tests should cover:

- Packed cell round trips.
- Field setters.
- Ignition threshold behavior.
- Water suppression.
- Heat loss.
- Deterministic random hash.
- External change queuing.
- Active frontier inclusion.
- Candidate deduplication.
- Listener next-tick mutation behavior.

## Snapshot Tests

Given the same seed, initial grid, and tick count, CPU snapshots should be stable.

## CLI Scenarios

Seeded scenarios should include:

- Single ignition point.
- Line of fuel.
- Water barrier.
- Vertical fuel column.
- Sparse forest.
- Building cluster.
- Mixed terrain, fuel, and water.

## Timberborn QA

Timberborn validation does not begin until the backend and adapter contract are stable enough to avoid debugging sim rules inside the game.

Runtime QA tickets must record:

- Save or fixture.
- Build/deploy/launch command.
- UI or debug commands used.
- Log paths and relevant extracted events.
- Screenshots when visual behavior is claimed.
- Pass/fail per acceptance criterion.
