# Remaining planner allocation: measured deferral

Measured source `b1185e1`, 2026-09-07, using freshly built Release Core/native assemblies and a copy of the established full-domain managed benchmark. No production optimization, runtime activation, game, Unity or deployment occurred. The isolated evidence directory `/tmp/wildfire-planner-allocation-attribution` contains source, binary SHA-256 manifest, runtime information, raw samples, summaries and regression logs.

The same process, inputs and existing null/three-request assertions measure three separate operations: actual coordinator snapshot capture, `ValidateAndClone` on an already captured snapshot, and the complete production planner. Each operation has two warmups and three measured samples. Explicit GC and input setup are outside timing; `DOTNET_TieredCompilation=0`. Allocations below are identical across all three samples in each row. Timing is the sample median, not an isolated gameplay or performance-improvement claim; concurrent shared-machine activity was not controlled.

| Grid / case | Capture ms / bytes | Validate+clone ms / bytes | Complete plan ms / bytes |
| --- | ---: | ---: | ---: |
| 128×128×33 no-op | 13.781 / 9,733,496 | 8.415 / 9,732,824 | 77.184 / 19,467,904 |
| 128×128×33 sparse move | 17.147 / 9,734,200 | 5.022 / 9,733,176 | 96.288 / 19,474,448 |
| 256×256×33 no-op | 48.773 / 38,929,784 | 31.801 / 38,929,112 | 287.595 / 77,860,480 |
| 256×256×33 sparse move | 67.591 / 38,930,488 | 34.117 / 38,929,464 | 438.517 / 77,867,024 |

The five full-domain arrays contain 18 bytes per cell (cells 2, transport 4, companion 4, target 4, slot 4). `FireSimStepCoordinator.CaptureSnapshotCore` validates and copies backend-owned arrays, and `TimberbornDesiredMaterialReconciliation.Plan` validates and copies the publicly returned mutable snapshot before subsequent simulator callbacks. Together they account for all but 1,584 bytes of each no-op plan, over 99.997% of the 256-grid allocation. The sparse move has 7,072 bytes beyond those measured operations. Complete-plan byte counts exactly reproduce the previous enum-optimized benchmark, 18.566/18.572 MiB at 128 and 74.254/74.260 MiB at 256. Current timings are slower than that earlier run; they do not establish a source regression or causal comparison across machine-load conditions.

## Why the duplicate isolation remains

`IFireSimSnapshotSimulator.CaptureSnapshot` returns a record containing mutable arrays. The generic planner cannot assume the implementation has relinquished every alias. Following capture, grid property and archive/known-slot calls return through that same simulator. The new regression retains the returned snapshot in a probe and changes its companion array during the subsequent Width getter. With the existing copy, a required OpenSoil handoff remains in the plan. Removing the planner's `ValidateAndClone` makes the plan incorrectly null. Restoring the source restores the test. No upload or stock/material mutation is performed by this scenario.

The coordinator's earlier copy protects a different boundary: backend readback arrays may alias backend state. Neither necessary public-boundary isolation can simply be removed because both installed adapters currently delegate capture to the coordinator.

Source inspection found no existing internal coordinator-to-planner ownership-transfer entry. The planner has a generic public-interface constraint, and currently has no production callsite. A future owned-runtime capture API could transfer an already isolated, validated result directly into internal planning before aliases escape; that requires an explicit cross-layer contract and counterfactual tests. It is a potential later optimization, not an existing guarantee, and was not added here. No pool, cache, ledger, scheduler or weakened validator was introduced.

The focused mutation regression passes, fails when the planner isolation call is removed, and passes after restoration. Full native suite passes 1,319 tests with no failures/skips. Production planner source is byte-unchanged from the base commit; only the test probe, regression and this document change.

Full-domain capture must not become an ordinary per-frame readiness poll. The measured cost remains a scheduling/activation gate; this work neither activates production planning nor establishes Unity Mono, real GPU transfer, native capture, dense-colony or gameplay-frame performance.
