# Borrowed duty native prototype

This is a development-only workforce experiment for the Folktails Fire Bell decision. It does not adopt the unresolved donor/communal policy or implement water collection, suppression, bell art, alarms, gear, or a player interface.

Current additional source: [inactive borrowed-water return recovery](borrowed-water-return-recovery.md). The empty-trip protocol and historical evidence below remain intact; the new return binding does not activate water collection or suppression.

## Implemented boundary

`BorrowedDutyFixture` admits one explicitly selected workplace employee at a native between-job decision boundary, only with process switch `--wildfire-enable-borrowed-duty`. The native employer is never changed. A beaver-owned registered behavior/executor performs one empty-handed trip to an explicit nearby safe point, then returns and releases to ordinary native arbitration. There is no saved old executor or job stack.

Admission requires matching donor and district, work hours, no work refusal or critical need, a living actor, empty hands, no stock/capacity reservation, no loaded warden water, no running executor, and a determinate resource session. Reservation checks use raw `Inventory` references: native `HasReserved*` can be false for a disabled inventory while a reservation remains owned. Warden stations are excluded. The fixture selects an explicit donor rather than searching or reassigning the workforce.

The offer temporarily precedes native workplace behaviors. The exact installed WorkSystem assembly fingerprint and private list shape are checked before changing order. The adapter caches each actual list and restores the previous neighbors when disarmed; unchanged lists are not rewritten every frame. The native job decision wraps the beaver-owned borrowed behavior, which deliberately does not implement `IJobBehavior`. Native `Worker.JobRunning` therefore becomes false without clearing `Worker.Workplace`.

Types remain registered when the process switch is absent. Save/load retains phase, elapsed time, cancellation, optional donor reference and route endpoints. Load only reads. First active manager-owned Tick checks cancellation/needs before replanning. Restored AtPoint transitions directly to return, never installing an unmonitored old outbound path. Idle Tick touches no movement.

Routes validate the actual installed native path and its refreshes through the existing reviewed field/owned-walker adapter. Arrival also requires native stopping proximity. Death wins and stops owned movement. Resource poison pauses the owned mover without stock mutation. Cancellation, invalid donor/employment, work-hours/need changes, and route failure lead to a safe return when available. Total duty is provisionally bounded to two game hours and sixteen cells from the starting point.

## Managed evidence and its limits

`BorrowedDutyNativeTests` executes the installed native decision wrapping, `Worker.JobRunning`, and `BehaviorManager.TickRunningExecutor` completion code. It verifies employer identity remains unchanged, the borrowed behavior owns execution without a return-to-workplace stack, and completion clears the native executor. Reinstalling an ordinary native job in the fixture restores the production flag; **actual root selection and production throughput in a running game remain unverified**.

`BorrowedDutyPersistenceTests` calls the real mod executor Save/Load through implementations of the installed native persistence interfaces for all four phases. Movement, employment, fixture, and resource dependencies are absent, proving these methods do not mutate those services. This is a component round trip, not a full saved world. A shared noncollectible native assembly context is required for DispatchProxy's cached interface implementation identity; other native fixtures remain collectible.

Validation: `dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --no-restore --verbosity minimal` passed **695 tests, zero failures/skips** on this isolated branch (nine borrowed-duty cases). No shader changes were made.

Additional checks cover native workplace neighbor restoration without repeated list mutations, process opt-in rejection before touching a donor, eligibility conflicts, elapsed cancellation persistence, and physical-arrival refusal. The restored AtPoint control-flow correction is source-reviewed; it has not been exercised with an actual native moving actor.

## Controller follow-up when game access returns

Use a copied Folktails save and the single controller. The existing file bridge now exposes these development commands through the controller script:

```text
bun scripts/invoke-timberborn-command.ts qa-borrowed-duty-status
bun scripts/invoke-timberborn-command.ts qa-borrowed-duty-arm <donor-guid> <x> <y> <z>
bun scripts/invoke-timberborn-command.ts qa-borrowed-duty-cancel
```

Arm and cancel require the existing `--wildfire-enable-qa-mutations` process argument. Arm additionally requires `--wildfire-enable-borrowed-duty`; no inbox request can enable either switch. Status remains read-only with neither flag, including a restored active actor. A nonnative bridge without the capability reports `borrowed_duty_unsupported` rather than pretending to accept an offer.

Coordinates are finite invariant-culture Unity world positions: **x/z horizontal, y vertical**. The native adapter validates map bounds and resolves exactly the supplied nonempty dashed native Guid through `EntityRegistry.GetEntity`, rejecting missing/deleted entities and nonworkplace/disabled donors. It never chooses a fallback or searches for a donor. Supply a known ordinary workplace identity from the controller's copied-save/entity evidence; this command does not add a donor picker. The chosen worker's existing sixteen-cell and real route checks remain authoritative; the adapter imposes no substitute donor-to-point range rule.

Arming reports `offer=armed`, donor identity and `offer_point`; it is an offer, not proof that a worker has started. A second offer is rejected until cancellation or acceptance. `active_count` counts nonidle executor phases; `running_count` separately counts actual native BehaviorManager ownership. Per-actor details include the current native EntityComponent Guid, phase, cancellation request and ownership flag. Status reads current identity rather than caching an ID during Awake. Once accepted, `offer=none` and a running actor distinguish execution from a still-pending offer. Cancellation disarms pending offers and requests return for active actors; it does not mean the return has already completed.

Production wiring is `BorrowedDutyConfigurator` binding the native QA adapter, injected into `TimberbornQaCommandFileBridge`, whose existing synchronous `UpdateSingleton` reads the file and calls the command bridge. No background thread, player UI or console bypass is added. Parser/access tests execute the command bridge with a fake native capability. Actual managed fixtures prove missing/deleted exact registry lookup, the separate admission switch, current native identity, and actual manager ownership/status cancellation. Three CLI subprocess tests use temporary inboxes and fake outboxes to prove transport only. Live scene liveness, nonworkplace component lookup, native file-bridge invocation and the full job trip remain controller proof.

QA-command validation: **722 native tests passed, zero failures/skips; 50 script tests passed; TypeScript typecheck passed**. This adds fifteen native/managed QA cases and three CLI cases to the earlier prototype evidence. No actual game inbox was written and no game/Steam/Unity/deployment action was performed.

1. Observe an ordinary worker finish its current job, accept one duty, and keep the same workplace throughout. Verify its native job production flag is false during duty and normal work resumes after release.
2. Save/reload Outbound, AtPoint and Returning, including a load without the admission switch. Confirm no duplicate offer, no outbound excursion from restored AtPoint, and bounded elapsed duty.
3. Reject cargo, reservations including a disabled destination, critical needs, district mismatch and warden work. Cancel, pause/delete/reassign the donor and end work hours during travel.
4. Obstruct or heat the installed route; verify movement stops before unsafe continuation and physical arrival is required. Test death and poisoned-session pause without resource writes.

A missing safe return, unavailable observation, or expired duty releases this empty-handed prototype to native arbitration. That bounded release is **not a guarantee that the next native civilian job avoids fire**. Fire Bell response behavior, meaningful in-game work interruption/return proof, night policy, final donor eligibility, and safe emergency recovery remain release work. No game, Unity, Steam or deployment action was performed for this prototype.
