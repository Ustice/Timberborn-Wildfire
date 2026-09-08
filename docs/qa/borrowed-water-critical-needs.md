# Empty water-return trip yields to critical needs

The connected finite water executor now finishes an empty-handed return when a native need becomes critical. Native `BehaviorManager.Tick` keeps ticking its current executor and only resumes root arbitration after that executor finishes; placing the recovery root after critical-needs roots alone cannot interrupt an active return. Previously `TickWater` entered `TickWaterReturn` before its general actor eligibility checks, and the return branch considered only its deadline, observations and movement. It could therefore delay urgent needs until arrival or the two-hour deadline.

The change adds one critical-needs check before continuing that empty return. It does not alter loaded-cargo cancellation: the independent return assignment and actual native bucket survive for later recovery. No employer, scheduler, source, stock or saved-state policy changes.

The regression executes the actual installed `NeedManager.AnyNeedIsInCriticalState` and `BehaviorManager.TickRunningExecutor`, with explicit native need/character backing inputs, a current borrowed executor, a real Runtime field observation over supplied simulator data, and the existing walk-driver fixture. Against `c8dfd88`, the critical case fails because the manager still holds the executor; the noncritical control passes. After the fix, critical return stops/releases and clears the manager's current executor; ordinary return remains running. This is managed native control-flow proof, not positive Unity liveness, physical travel or a full adult lifecycle.

Evidence: `/tmp/wildfire-borrowed-water-needs-red.log` (one discriminating failure, one pass) and `/tmp/wildfire-borrowed-water-needs-green.log` (full native suite). The earlier fixture-construction failure is separately retained in `/tmp/wildfire-borrowed-water-needs-fixture-failure.log`; it is not the behavioral RED. Existing native loaded-cancellation and save/restore tests remain in the full suite.

This fix follows the immutable connected Claude packet pinned at `c8dfd88`. It was found by local source/native review while that one request ran, and must not be described as code Claude reviewed.
