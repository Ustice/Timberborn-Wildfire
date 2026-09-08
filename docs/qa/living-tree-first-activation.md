# Living tree first activation uses physical wood

Source: `4a81c54`, based on root `67939b8`.

The native lifecycle audit in `/tmp/wildfire-tree-fresh-eligibility/REPORT.md` established that a mature tree's first Gatherable crop can remain disabled after Cuttable wood is available. Subsequent harvested crops remain enabled at zero while regrowing. Requiring every named yielder to be enabled consequently treated equivalent available timber inconsistently.

The bounded correction changes only current restored-observation Fresh eligibility for Tree bodies. After the existing supported-observation checks, a tree requires explicit nondead state, verified ordinary retained-wood composition and exactly one enabled Cuttable role with positive actual quantity. Auxiliary Gatherable availability/quantity does not decide whether the existing wood body may first activate. Non-tree behavior, dead-tree Fresh policy and initial formation are unchanged.

Known GPU active/archive/captured-source material remains independent of Fresh eligibility. Resetting resin does not reset tree fuel. No native amount, declaration, body profile, accounting capacity, inventory selection, archived state or persistence schema is changed; no permission is retained beyond the supplied current-observation scope.

## Evidence

Eight focused tests execute the new predicate and real material planner. The native-state fixture reads installed Pine Cuttable Log2 and Gatherable PineResin2 specs, invokes actual native Cuttable HasGrown and GatherableYieldGrower completion callbacks, and observes actual Yielder state. The first disabled fruit state and the later enabled-zero regrowth state both permit the same unknown wood slot. Disabled/empty Cuttable, dead/unobserved death and unproved composition reject before upload. Repeated native resin resets reexpose known exhausted archives at zero and never create Fresh fuel.

A callback fixture demonstrates the caller's required same-scope native reread: a backend-read callback removes native wood after planning starts, observation drift rejects before dispatch, and no simulation upload occurs. This is an explicit composition-contract fixture, not a newly implemented live executor. The initial and complete session paths retain their existing final reread/fidelity checks.

Native methods run with a supplied ComponentCache sufficient for Yielder's non-updatable enable boundary; no Unity GameObject, whole-template lifecycle, actual harvest inventory transfer, game or GPU launch is claimed. The native retained-wood composition predicate itself is covered independently by the exact spec/component tests documented in [known-tree restoration](known-tree-material-restore.md).

Validation logs: `/tmp/wildfire-living-tree-fresh-focused.log`, `/tmp/wildfire-living-tree-fresh-red.log`, and `/tmp/wildfire-living-tree-fresh-full.log`. At `4a81c54`, **8 focused tests and 1,276 full native tests passed**, zero failures/skips. Replacing only the predicate with its previous all-yielders-enabled form made the actual native first-fruit test fail (expected eligible owner, actual empty list); the source was restored before the successful full suite.

Initial deadwood and immature-body material policy remain separate. Positive native timber and harvest-cycle state identify different facts; this correction adopts no new body-fuel budget, partial-harvest generation or refill rule.
