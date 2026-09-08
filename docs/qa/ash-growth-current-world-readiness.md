# Inactive current-world ash growth readiness

Source checkpoint `d0115e8` adds a concrete, **unbound** readiness producer. It does not activate the late growth ticker or fertilizer worker, change a configurator/template, or alter application recurrence policy.

## Current authority and caching

`TimberbornFireRuntime.ReadAshGrowthObservation` requires global wildfire enablement and the existing initialized, nonpoisoned current-world field observation. It derives requests only from positive clean transport ash, validates exact grid/field lengths and the legitimate 0–3 ash range, and uses the existing strength-to-multiplier formula. Restored AshFieldService entries never establish this readiness.

The derived grid/request list is copied and cached by the exact successfully published `FireFieldObservation` object. Native intervals with unchanged system/revision reuse it. A successful field refresh clears the derived cache, avoiding retention of an obsolete full-field snapshot. This adds no second GPU readback, persistent readiness state, quantity ledger, or per-tick full-world scan. Native entity/base/column eligibility remains freshly checked by the growth adapter each interval.

The existing field observation cache now stages the exact system, revision, and readers, reads both fields, and rechecks those authorities plus initialization/poison state before publication. A callback cannot stamp old data with a newer world/revision. Its existing public observation record contract remains unchanged. The global switch gates growth separately; ordinary field availability is still independent of response policy.

`TimberbornAshGrowthReadiness(runtime, tickableSingletonService)` must be constructed on the native thread. Its `Read` method verifies that thread, `!IsStartingParallelTick`, and `ParalleTicklIsFinished` before and after observation. Future composition passes this method into the existing ticker, whose `CaptureAtRest` covers preparation and whose same-coordinator `TransferInventory` covers actual growth. The provider adds no nested guard. Wrong-window calls fail as read-only rejection.

## Validation

- Five counterfactual tests reproduced old false publication when an actual supplied reader callback changed the current system, revision, reader, initialization state, or lifecycle poison: **5 failed, expected false / actual true**. The corrected source passes all five.
- **23 new readiness cases** cover actual Runtime/initialization/settings logic with supplied field readers; copied request isolation and reuse; current transport overriding genuinely restored fertile entries; same-revision replacement worlds; strength changes/removal/taint; global disablement before and during a read; every unready initialization state; malformed lengths/unused ash bit; existing guard save/mutation exclusion and caught lifecycle invalidation.
- Native settled-window tests execute the installed TickableSingletonService property getters with supplied state, including a distinct real managed thread and a read callback changing completion state. They do not claim a Unity frame or full native scheduler initialization.
- Final full native suite on base `37226ee`: **1,610 passed**, zero failures/skips. Two preexisting worker-speed nullable warnings remain in that base; root has a separate correction. No Core/protocol source changed, and no engine/game/deploy was performed.

Evidence: `/tmp/wildfire-growth-readiness-proof/cache-red.log`, `cache-green.log`, `focused.log`, and `full-native.log`. The focused log contains the preceding 22-case pass; the final additional unused-bit case is included in the full run.

## Remaining adoption proof

The source producer closes the missing concrete-readiness seam, not the binding proof. Controller validation must distinguish the supplied CPU/native initialized-plant application experiment from actual normal Runtime observation of an accepted GPU fertilizer application. Verify that ordinary scheduler/coordinator stepping, complete consequences/followups, and FieldRevision make accepted changes visible; rejected/no-input outcomes remain coherent; poison prevents growth publication; no restore-time bonus occurs.

The direct low-level `TryApplyCleanAsh` entry remains inactive and does not update normal Runtime revision/followups. Do not use it as an actor shortcut to make readiness tests pass. There is still no configurator/template binding in this change. Soil/root checks and elapsed-time ownership are documented in [the native growth proof](ash-native-elapsed-growth.md).
