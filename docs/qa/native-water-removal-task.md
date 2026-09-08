# Native water removal and receipt precision

Validated 2026-09-08 against installed Timberborn managed assemblies. This is a synchronous native task fixture over supplied finite columns, not Unity/game, source lifecycle, or normal water-scheduler acceptance.

`NativeWaterRemovalTaskTests` adds 13 cases using the existing `NativeShorelineWaterFixture` converter, native WaterInputService credit, guarded bucket fill and native WaterInput Save/Load. The fixture reads installed MapSize/WaterSimulator specs, initializes the native map/index services, and uses actual WaterDepthSetter, MutableWaterColumnRetriever and UpdateWaterChangesTask. The existing helper now constructs native WaterService normally; only unreached constructor dependencies are null.

## What executes

- Native WaterService queues clean/contaminated demand; native WaterChangeService.Tick publishes the queue. No test writes a receipt entry.
- Actual UpdateWaterChangesTask.Run mutates exclusive native WaterColumn arrays and generates the service's receipt dictionary.
- Clean, insufficient, dry, mixed and all-dirty controls distinguish actual clean/dirty removal. Ordered requests accumulate at one coordinate; outside-column requests have no entry; an upper column uses native vertical stride and leaves the lower column intact. An empty subsequent batch clears prior receipts without further removal.
- Installed pressure factor8 is preserved. A .2 clean request from depth1/overflow.5/contamination.5 yields approximately .12708324 clean and .07291657 contaminated removal. Downstream credit must retain both actual grades; requested clean volume is not the receipt.

## Precision and whole-good admission

The actual native WaterGoodToWaterAmountConverter maps one Water good to .2 volume. A clean depth1 column returns .19999999 on the first .2 removal, leaving depth.8. The native buffer remains strictly below .2: tests do not add an admission epsilon.

Deficit-only retry fails in two distinct ways: DemandCleanWaterAmount on the tiny deficit queues nothing because its native buffer threshold is already met; bypassing that demand check and directly requesting the deficit from WaterService removes zero at the column's float precision. A later full .2 demand produces a second actual receipt. Native WaterInputService credits it; the existing guarded exact bucket fill produces one Water good, leaving approximately .19999997 in the native buffer and .6 in the column. Native Save/Load preserves that surplus, and total column + buffer + converted good conserves initial volume within a 5e-6 **assertion tolerance only**.

This proves a conservative full-quantum retry on sufficient finite water without invented credit. It neither guarantees progress on an exhausted/dirty source nor implements a shipping source-demand policy. TryFill can queue native prefetch; an unexecuted request is never treated as credited water or cargo.

Native pumps are fractional Manufactory consumers: the limiter requests proposed progress times cycle volume, caps progress from available buffer, and the progress callback subtracts that actual progress volume. The traced conversion/demand/consumer methods do not provide a rounding allowance for a custom exact whole-unit threshold.

## Boundaries and ownership

Columns, source coordinates, and map extent are supplied test facts. The fixture does not initialize a real entity, assert Unity liveness, arm source ownership, restore a native factory world, or execute normal source scheduler credit. Existing shoreline admission remains an experimental resource seam, not production actor eligibility.

Run returns only after its actual synchronous work completes. Fixture arrays/dictionary are exclusively owned; no parallel native job runs. Native production schedules this task after a predecessor and must finish pending work before receipt read/credit. The fixture does not spoof scheduler settled state. A task exception could leave partial columns/receipts; no retry or credit after failure is proven.

The task struct and fixture arrays are managed, without disposable native containers. Receipts are batch output cleared by the next Run, not a saved balance. Source lifetime ownership and full positive native factory restoration remain separate gates in [water ownership QA](native-water-ownership-component.md).

## Evidence

- Test source: `fee43d8`; no production change.
- Focused native tests:23 passed,0 skipped (13 new +10 existing shoreline).
- Full Release native suite: 1,666 passed, 0 failed, 0 skipped on the isolated test branch based on `7d1ffc9`.
- Scratch report, exact144-line executable,12 original task cases plus precision output, native IL and SHA256 identities: `/tmp/wildfire-native-water-removal-task/`.
- No production source, gameplay policy, engine session or deployment changed.
