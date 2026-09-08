# Ash harvesting remains native job work

`AshHarvestBehavior` now implements native `IJobBehavior`. Its beaver-owned transfer previously dropped the marker inherited by its workplace offer. Employment, cargo, phases, ordering, reservations, movement and persistence are unchanged. Borrowed duty deliberately remains a non-job behavior.

Installed native IL establishes the distinction:

- `WorkerRootBehavior.WorkAtWorkplace` wraps the first accepted workplace decision with `Decision.TransferNow`.
- `Decision.TransferNow` preserves an already-supplied behavior. The ash offer supplies `AshHarvestBehavior`, so the outer native wrapper does not restore the workplace's job marker.
- `Worker.JobRunning` calls `BehaviorManager.IsRunningBehavior<IJobBehavior>`, which tests only the current `_runningBehavior`.
- `Workplace.AnyWorkerHasJobRunning` reads that flag for assigned workers. `Workplace.UnassignWorkerIfNonworking` selects a worker with the exact predicate `!worker.JobRunning`.

Scanning 33,331 installed native methods with zero scan failures found these further callers: `WorkplacePowerConsumptionSwitch.UpdatePowerConsumption` and `WorkplaceIlluminator.UpdateIllumination` use `AnyWorkerHasJobRunning`. Thus this marker affects native active-job classification and nonworking-worker selection; no measured production-rate or live lighting claim is made. The gatherer is performing its employer's ash job, unlike the intentionally borrowed worker.

`AshHarvestNativeJobTests` executes actual native nested decision wrapping, Worker.JobRunning, the workplace aggregate, and the exact nonworking predicate using supplied managed state. The original source fails the new assertion (expected true, actual false). Adding the marker passes. It also executes native executor completion plus the ash behavior's release decision, then supplies subsequent job/non-job decisions to prove classification follows the current behavior. This is not a claim of full live root arbitration or physical harvest completion. Existing `BorrowedDutyNativeTests` verify the opposite flag remains intentional there.

Evidence: `/tmp/wildfire-ash-job-marker/` contains the original failing test log, corrected results, exact native caller scan and IL. The independent fertilizer worker report remains `/tmp/wildfire-fertilizer-worker-next/REPORT.md`. Loaded ash holding/critical-needs policy remains outside this change.

Validation: the new test fails on the original source; 4 focused ash/borrowed native tests pass after correction. The complete native suite passes 1,443 tests, zero failed/skipped. No engine or deployment action was performed.
