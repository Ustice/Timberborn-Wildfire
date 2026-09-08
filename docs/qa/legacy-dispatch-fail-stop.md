# WF1 incomplete-dispatch fail-stop

Source base: `54b0d83`. Classification commit: `fcdca3a`. Host delivery commit: `2d1ef43`.

A simulator step could finish buffer swap and then fail in an ordinary listener, native consequence, or later FireSystem callback while the native resource guard still admitted saves. FireSystem could also return successfully and a runtime synchronization failure prevent the remaining followups. This change closes those escaping-failure paths without wrapping the entire dispatch in a resource transaction.

## Completion boundary

`FireSimStepCoordinator.RunStep` now reports its existing `FireSimStepInputException` outcome for ordinary as well as resource-input steps. External argument/state validation stays outside admission and remains raw. The outcome comes from the coordinator's actual execution stage, never from a tick-count comparison:

- `NotApplied`: neither external-change application nor simulation began.
- `Indeterminate`: mutation began, but readback, swap or optional resource commit did not finish.
- `Committed`: readback, swap and optional commit finished; a subsequent listener or adapter observation failed. The step must not be replayed.

Both compute adapters preserve `Committed` if their post-return Info callback throws. The native adapter's failure warning is best-effort and cannot replace the original outcome. The other four input/collection/application/handoff entry wrappers in both adapters directly return the coordinator call; none has an analogous post-return diagnostic.

The sole runtime binding replaces `StepWithHostInput` with one internal `ITimberbornFireDispatchHost`, implemented by the same `NativeResourceCoordinator` already used by native Save. It binds Tick, save-safety admission, and incomplete-dispatch invalidation together; it owns no state. Each FireSystem Tick captures this binding once and checks safety before QA/sustained-input preparation.

A returned step followed by any escaping FireSystem failure invalidates that coordinator. A typed Committed or Indeterminate failure before return also invalidates it. A later method throwing a misleading NotApplied exception cannot undo the returned-step fact. Catch diagnostics preserve the original exception.

There is deliberately no completion marker after the last delta sink: even a FireSystem log failure prevents Runtime's remaining effects. Runtime separately marks the interval after dispatcher return through synchronization, ash world effects, and beaver followups. Escaping failures within that interval invalidate. Final runtime logging and the following renderer update occur after these calls return; a failure there does not itself poison resource state.

The lower-level resource transaction remains unchanged: its typed Committed case is intentionally nonpoisoning until the world-delivery layer observes that required delivery did not finish. Private resource-commit failures remain Indeterminate. Existing native sinks may still enter their own resource guard; there is no new nested outer TransferInventory.

## Discriminating evidence

Initial RED: four failures and four passing controls on the original source. Listener-after-swap, returned native consequence failure, and final FireSystem log failure left save admission open; ordinary listener failure also lost its typed stage. Controls distinguished NotApplied, pre-step logging, private commit failure and a valid rejected-zero application.

A separate public `UnityComputeFireSimulator` recording-backend RED showed raw IOException from post-return Info after the actual adapter had swapped its recording buffers and notified its listener. GREEN asserts Committed, identical inner cause, one dispatch, swapped buffer, and listener delivery.

New dispatch tests cover:

- Actual Core listener-after-swap failure for ordinary and one-shot steps, exactly one resource commit where applicable, and no subsequent native delivery.
- Native consequence failure after return, including a misleading typed NotApplied throw; saves and the next dispatch are blocked before sustained-input preparation.
- Debug presentation failure before effects versus an existing caught VFX failure that allows later effects to finish.
- Final FireSystem logging preventing runtime followups, and a throwing warning preserving the original native cause.
- Ordinary and one-shot NotApplied reset failures remaining safe and retryable.
- Valid rejected-zero application completing one dispatch, native delivery and followup without resource commit or a second ordinary tick.
- A successful ordinary dispatch whose native sink enters the existing transfer guard without nesting.
- Private resource callback failure preserving Indeterminate.

The native-runtime fixture executes the actual native-assembly Runtime dispatch method, FireSystem, fixed-cadence dispatcher, coordinator and Save admission. An actual synchronization rejection for unavailable transport observations blocks Save and next dispatch; even a throwing catch warning preserves that original cause. A separate already-synchronized empty-world fixture returns from the existing ash and unavailable-beaver paths, then throws at final runtime Info; Save reaches its ordinary null-saver validation rather than a poisoned guard.

These fixtures use explicit managed dependency objects and native contract proxies, not a complete Unity scene. The successful runtime case supplies an already-synchronized tick and no actors; it does not fake a GPU transport read. Save tests prove admission, not serialization. The public Unity adapter test uses recording compute buffers/dispatcher, not shader execution. The native compute post-return path is source-reviewed and compiled, not engine-executed in this slice.

## Validation and remaining limits

- Core: **228 passed, zero failures/skips**.
- Native adapter: **1,459 passed, zero failures/skips**.
- `git diff --check` passed.
- No shader, engine, game, desktop or deployment action was performed.

Production delta: nine files, 85 added / 21 removed lines (net +64), including one 11-line internal interface. The actual transaction gains no fields or behavior. `DispatchFireUpdate` extracts the existing runtime dispatch body so the completion boundary is testable without calling Unity.Time; production still has one caller.

This proves escaping-failure/continuation fail-stop for the bound WF1 runtime. It does **not** add whole-dispatch exclusion against a reentrant Save while legacy sinks are running. Standalone unbound FireSystem diagnostic fixtures have no native save guard. Existing legacy ash-world and beaver helper catches that swallow their own failures remain outside this proof; returning from those methods is not proof that every swallowed native operation succeeded. Unknown third-party simulators that throw raw exceptions after unreported mutation cannot be classified by guessing from CurrentTick. No automatic retry, refund, extra fuel/quantity ledger, OWNED activation, scheduler change or worker/fertilizer policy was introduced.
