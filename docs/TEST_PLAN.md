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

Starter CPU snapshots record the packed cell grid in hex plus per-tick delta counts for reviewable regression diffs.

Later GPU validation should use the CPU snapshot suite as the oracle:

- Run the same named scenario, seed, dimensions, initial packed cells, and tick count on the GPU backend.
- Read back the packed GPU cell grid after the final tick and format it with the same row/layer order as the CPU snapshot.
- Compare CPU and GPU packed cells exactly first.
- If exact matching is not possible, record each differing cell with coordinates, CPU packed value, GPU packed value, and the rule or shader limitation that explains the difference.
- Keep accepted tolerances scenario-specific and bounded; do not replace CPU snapshots with broad image-only or visual checks.

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
