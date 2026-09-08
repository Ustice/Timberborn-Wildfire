# OWNED4 native initial and complete-restore engine proof

Actual licensed-engine probe on source `e5885d122c0e32db0663d2144eae3dc7c07b4606`, completed 2026-09-08 00:38 UTC. Native Release build passed with zero warnings/errors. The first engine attempt compiled the unchanged prepared fixture and exited 0 with `WILDFIRE_OWNED4_SESSION_PASS`.

Evidence directory: `/tmp/wildfire-owned4-session-engine-run`. It contains the exact probe, staged DLL/shader hashes, build and Unity logs, initial native capture, complete-restore observation, exact snapshot, bindings and encoded OWNED4 save. `run.py` records process preflight and holds the shared build/deploy lock while Unity runs.

## Exercised path

Two real Unity GameObjects carry actual native ComponentCache, EntityComponent, BlockObject, Cuttable, named Yielder and LivingNaturalResource components. Native initialization and provider methods run over explicitly supplied placement/state/dependencies. Both native resources remain nondead with actual Log3 and initial Log3; their supplied blueprint declaration is Log5. Explicit test accounting choices Actual and Declared produce capacities36 and60, respectively. This does not adopt a first-release natural-fuel policy.

One actual GoodStack initializes an actual empty disabled named Inventory; the other body has no inventory. Actual provider capture records the original named GoodStack declaration and the other body's explicit empty declaration set. Dormant empty storage does not invent a competing physical material part.

The actual environment provider reads supplied native terrain, soil and water backing arrays through native getters. Production initial projection/compiler/registry/history and `TimberbornComputeFireSimulator.CreateFromSnapshot` create the staged session. Production OWNED4 encode/decode and `PrepareCompleteRestore` create a second actual native backend, with before/after native reads.

The proof asserts:

- Exact snapshot arrays, parameters, seed, tick, pending-input order, known slots, archive set and attempt token survive the new native factory and complete restore. Encoded OWNED4 text round-trips exactly.
- Both durable native Guid bindings survive; each has two local slots. Only the upper contributor owns the two GPU cells. The hidden contributor has no invented GPU history.
- The complete restored registry contains both contributors at each overlapping cell and restores material-only OpenSoil and Badwater baseline definitions. `DesiredWorldCapability` is `CompleteStaged`.
- Removing the required LivingNaturalResource from one actual cache, with its entity/Guid/quantity unchanged, rejects complete restore with the exact invariant error before backend allocation. Restoring the cache permits the positive restore.
- Changing the actual supplied native moisture array inside each initial/complete-restore factory callback causes the final native reread to reject. Both newly allocated actual GPU backends are disposed. The array is restored after each case.
- Native current/initial yield, dormant inventory emptiness/disabled state and natural nondead state remain unchanged. Strict effect sinks reject any attempted consequence writes; the resource guard remains save-safe after the rejected staging attempts.

Initial arrays independently recorded in `initial-snapshot.json`:

```text
Cells:     4096, 7944, 4872, 4096, 3072, 3072
Companion: 1, 5245956, 5245956, 1, 167772161, 11534345
TargetIds: 0, 1, 1, 0, 0, 0
SlotIds:   0, 1, 2, 0, 0, 0
```

Transport is zero, seed17, tick0, no pending inputs or archives, known pairs `(1,1)` and `(1,2)`.

## Exact artifacts and limits

SHA-256:

- Native DLL: `aa8fc4b36df8f945b9f9268b9ac73be90621f9ba27cbbbdfe6ab2bcc69c8fd73`
- Core DLL: `38de49efebe18b22e12cf68f2d7b237fc34d3692626a445597a12c3e43214820`
- FireSim.compute: `97ddf54667267b7ae34d22eecea3408f6187a959de1ff7c0366b2c5f2af93bd0`
- Probe: `d8a3807f185797a35a2c99aff0832ecf30f1189c56bcd9289d9fcbafb5e45e1c`

Unity6000.3.6f1 used Metal on Apple M2 Pro, resolving native dependencies from installed Timberborn assemblies while using editor UnityEngine types. This remains narrower than whole-mod compatibility with the game's different Unity version. No shader/UAV errors appeared; a licensing access-token update error occurred before successful execution, and shutdown reported `Persistent allocates 26 individual allocations`. The latter has no allocation stack trace and is not attributed to production code by this proof.

This is supplied native state and real engine allocation/readback, not full native template creation, a native world/save load, running-colony coverage or a new shader simulation proof. No ordinary tick or handoff executes; complete staging does not waive the desired-material reconciliation gate. The fixture retains its six-layer test grid and does not address the separately confirmed upper native building-domain gap.

After execution there were no Unity/game/Blender processes, the build/deploy lock was absent, and `/root/baseline_audit` retained idle controller ownership. No Steam launch, game launch or deployment occurred.

## Cleanup-order diagnosis addendum

A separate controlled rerun at `/tmp/wildfire-owned4-session-cleanup` kept the exact original e5885d1 native/Core/shader bytes and changed only fixture cleanup/exit control plus completion instrumentation. Original proof files remain unchanged. The original fixture called `EditorApplication.Exit` inside its try/catch, before the finally cleanup; the corrected fixture records the result, completes cleanup, then calls Exit. Cleanup attempts every resource even if one disposal throws, verifies native backend disposal and GameObject destruction, and forces exit1 for any cleanup error.

The rerun exited0 and logged `WILDFIRE_OWNED4_CLEANUP_COMPLETE sessions=2/2 backends=4/4 gameobjects=4/4 errors=0 exit_code=0`, followed by `WILDFIRE_OWNED4_EXIT_AFTER_CLEANUP code=0`. All five capture/snapshot/binding/save artifacts were byte-identical to the original successful run. The original `Persistent allocates 26` shutdown notice was absent, with no `Leak Detected` message. This controlled comparison supports premature fixture exit as the cause of that notice; it provides no evidence of a production allocation leak. The familiar licensing token-update error remained unrelated to successful execution.

`comparison-inputs.json` fingerprints identical production inputs; `comparison-result.json` records artifact equality, notice comparison and final idle/no-engine/no-build-lock state. Reuse the corrected probe and exit-after-cleanup recipe for future engine evidence. No production change or additional game/deploy operation was made.
