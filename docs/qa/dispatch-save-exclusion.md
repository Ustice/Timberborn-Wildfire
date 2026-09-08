# Normal fire dispatch save exclusion

Source: `6953d3e`, based on `460a0cf`. This follows the [escaping-dispatch failure correction](legacy-dispatch-fail-stop.md).

Normal `TimberbornFireRuntime.DispatchFireUpdate` now excludes world saves and guarded captures from before dispatcher/QA/input preparation until its completion path and diagnostics return. The same existing NativeResourceTransaction owns one transient dispatch-active flag. Finally always releases this scope; existing poison is never reset. Recursive entry through the normal runtime dispatch rejects before cadence, input preparation or another simulator step.

Save admission and inner-operation admission are deliberately separate. Save, CaptureAtRest, Attach and ResetForWorldLoad reject an active dispatch. Existing transfers and resource steps continue to own their original inner latch; they may run between operations while saves stay excluded. A native callback that invokes TransferInventory during such a gap is **allowed**. Nested transfer/resource operations inside an already active inner operation still reject. This is save exclusion, not arbitrary callback mutation isolation.

The dispatch-host preflight is now named ThrowIfStepUnsafe and checks existing operation/poison state. Internal single-simulator read-model observations remain available; they do not count as a full world capture. Direct coordinator Tick, standalone FireSystem.Tick, exposed raw-simulator calls, or arbitrary third-party/reflection bypasses are outside the normal runtime entry guarantee.

The scope adds no exception-to-poison policy. NotApplied/pre-step failures release normally; Committed/Indeterminate and incomplete native delivery retain the existing host fail-stop. A final observational exception after returned followups remains nonpoisoning. Explicit lifecycle invalidation caught by a callback cannot turn into a successful outer dispatch, and a throwing callback preserves its original cause. No swallowed legacy followup catch changed.

## Evidence

- Behavioral RED on the original source: **8 failed / 4 passed**. Actual native Runtime.Save reached null-saver validation during preparation, Core listener, followup and final logging; guarded capture executed; recursive Runtime dispatched a second step; Attach/Reset admitted; caught late invalidation returned success.
- GREEN: native Runtime/Save regressions plus scoped step/read-model/lifecycle cases. Native Save assertions prove admission, not serialized-save publication. Existing native runtime fixtures use explicit managed dependencies and an already-synchronized empty-world tick; they do not claim a complete Unity scene.
- Actual Core coordinator tests exercise ordinary dispatch, one water input/commit and valid rejected-zero ash application: one swap and listener delivery, exact commit count, no fallback second tick, and saves excluded after the inner call returns.
- Existing legacy sink can enter its transfer guard after a returned resource step while save/capture remain blocked.
- Actual native Citizen-first death with established fixture district membership unregisters once between operations and leaves goods unspent. The same death during an active inner transfer continues later native subscribers, skips rejected cleanup, preserves poison and leaves goods unspent. This tests the shared personal-inventory helper through the satchel; it does not fabricate positive Unity district liveness or activate workers.
- Attached conditional application fixture preserves accepted-commit privilege, consumes exactly one unit through actual native inventory accounting, and spends nothing for a valid rejection. Privilege is absent after return even though dispatch save exclusion remains active. Receipt source is a controlled managed simulator, not new shader execution.
- A direct FireSystem single-simulator persistence/read-model read succeeds inside the scope while CaptureAtRest rejects before its callback.
- Full **Core 228 passed / native 1,488 passed**, zero failures/skips. `git diff --check` passed. Production net **+27 lines**, five files, no new type, no new persisted data.

No engine, game, UI, deployment, shader, worker admission, OWNED activation or gameplay policy changes. Direct-call bypass limits remain intentional; stronger mutation ownership belongs with coherent owned-step delivery. [Concrete native followup gaps](legacy-native-followup-gaps.md) remain separate from this save-admission guarantee.
