# Smoke effects: native mutation and actor history completion

A native smoke status or worker-speed callback can throw **after changing native state**. The adapter now distinguishes that actor's incomplete delivery from an unchanged preflight refusal. Runtime propagates incomplete delivery to the existing `NativeResourceCoordinator` guard, which rejects save and further dispatch until world reload. No additional poison state, transaction scheduler, native rollback, or speed ledger was added.

The boundary is per actor. If A completes its native effect and matching behavior history, then B fails a read before mutation, A remains valid and saveable; B can be revisited normally. Unsupported results also remain safe skips. There is no all-actors atomicity requirement or automatic failure classification based on a completed prefix. Cooldown, fair ordering, supported-operation budget, debuff levels, native speed restoration rules, and saved behavior schema are unchanged.

The extracted native adapter reads entity/component capabilities, setter availability and proposed speed before statuses change. Missing Worker remains an intentional speed no-op; missing StatusSubject remains a status no-op. Missing required setter returns the existing Unsupported result before status mutation. Method availability does not establish healthy animator dependencies. Once a native register/toggle/setter call begins, failure is classified as incomplete, preserving the original cause. Status handles are retained before registration so partial registration does not discard them.

Ordinary recovery now propagates status deactivation callback failures. Explicit Clear/teardown retains the existing best-effort handling for NullReferenceException/InvalidOperationException, separately from ordinary recovery. This does not claim complete native teardown correctness.

After an Applied result, the dispatcher publishes that actor's behavior history under the same incomplete-delivery classification. An explicit Failed result has no complete receipt and is also rejected as incomplete; production native mutation exceptions cannot be converted into Failed or Unsupported by the worker-speed actuator. Final summary logging and player alert publication remain observational. A failed warning about those observations does not poison completed actor state. An incomplete delivery's original cause survives even if the outer failure warning also throws.

## Actual native and runtime regression evidence

Baseline `fb57b86` / original native adapter:

- Actual installed Worker getter starts at 1; its actual setter writes .5 and then throws because the supplied fixture has no CharacterAnimator. Getter afterwards is .5. This proves native mutation before an animation-dependency failure, not a real animator subscriber or Unity animation callback.
- Actual native StatusToggle.Activate changes IsActive to true and then throws a supplied StatusToggled subscriber exception. Deactivate changes it to false and throws the same exception. Both exact callback cutpoints execute on supplied native managed objects.
- The first six source regressions produced **4 failures / 2 passes**: speed/application failures lacked incomplete classification; ordinary recovery swallowed the callback failure; completed-prefix preflight and final-summary controls already passed.
- Counterfactual removal of only the Runtime typed rethrow produced **2 failures / 4 passes**: the actual native callback was swallowed, or a failing warning replaced its cause. Restoring the rethrow preserves the native cause and blocks real runtime save/continuation.
- Tests invoke actual Runtime.DispatchFireUpdate, native speed/status adapter and dispatcher, with scripted simulator/position/visual observations. They verify reentrant Save refusal during the callback, subsequent save/dispatch refusal after incomplete application/recovery, A-complete/B-preflight-safe behavior, preserved prior history on failed recovery, optional component no-ops, and final summary/player-alert failures with both working and throwing warning logs.

Final Release native suite: **1,531 passed, 0 failed, 0 skipped**. The build reported no warnings or errors.

The old source-string teardown test was replaced with an executed native deactivation callback during explicit Clear. The original mutable callback fixture remains supplied test state; no Unity GameObject liveness, floating-status UI, animation, full beaver prefab, game save load or live colony behavior is claimed. No engine, game, Steam, deployment or desktop action was performed for this source slice.

Artifacts: `/tmp/wildfire-smoke-native-failure-audit/` contains original native setter/status results, native IL, hashes, `red.log`, `runtime-counterfactual-red.log`, focused green and `full-native.log`. Native WorkSystem SHA-256 is `e1b32c9b6c2c97223c9e3ca1d95c60e72f2afe7aa276175f5819b810fc69ca7e`; full dependency/probe hashes are in `hashes.json`.
