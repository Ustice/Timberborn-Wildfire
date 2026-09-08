# Full native world-domain OWNED4 engine proof

Actual licensed Unity6000.3.6f1 / Metal Apple M2 Pro proof on exact source `a68c6f5690955ddf99729a94efb7d57ae6f8cb27`. The native/Core DLLs were freshly built from a detached worktree; Release build had zero warnings/errors. The unchanged prepared fixture compiled and passed on its first engine attempt (exit0).

Evidence: `/tmp/wildfire-owned-full-domain-engine-run`, including `source.txt`, `native-build.log`, `staged-sha256.json`, `unity.log`, `command.json`, `initial-native-capture.json`, `complete-restore-observation.json`, `initial-snapshot.json`, `bindings.json`, `owned-save.txt` and the exact `UnityProject/Assets/Editor/OwnedFullDomainSessionProbe.cs`.

## Actual boundary exercised

The probe reads the installed MapSize blueprint, supplies those values to actual native MapSizeSpec, and calls actual MapSize.Initialize. It obtains TerrainSize1×1×23 and TotalSize1×1×33. The production `ForOwnedWorld` environment provider captures native padded terrain arrays containing only23 layers, with native terrain-column ceilings1 and23. The soil sample at ceiling23 reads moisture12/contamination0.6 from the actual native column-indexed getters. Positive liquid depth1.5 wets cells23/24; their baseline definitions are OpenSoil and Badwater respectively.

Two real Unity GameObjects carry supplied native component/cache/dependency state with actual native initializers, Blocks/Placement, Cuttable/Yielder and LivingNaturalResource. Their two occupied slots overlap at z31/32. They retain exact distinct Guids, actual Log3/initial Log3, declared Log5, one original named disabled-empty GoodStack declaration and one explicitly empty inventory declaration set. Explicit test accounting choices Actual/Declared yield capacities36/60; these do not adopt production fuel policy.

Production CaptureDuringScope → PrepareInitial → actual native complete-snapshot GPU factory → OWNED4 encode/decode → actual CaptureForRestoreDuringScope → PrepareCompleteRestore succeeds. Exact saved GPU arrays, parameters, seed/tick, pending inputs, known slots, archive state and attempt token survive; the OWNED4 text round-trips byte-identically. Both restored upper cells retain both hidden contributors. Restored material-only OpenSoil23/Badwater24 baselines are present, and desired-world capability is CompleteStaged.

Discriminating failures also pass:

- Asking the owned initializer for the terrain-only23 grid rejects before any backend allocation.
- After saving a valid33-layer snapshot, changing the supplied native TotalSize to23 makes complete restore reject before allocation. The unchanged save re-encodes identically; native size is restored afterward. This is native/saved mismatch rejection, not old-save migration.
- Removing an actual cache's required LivingNaturalResource rejects before allocation; restoring it permits the positive path.
- Changing the actual supplied native moisture array inside either initial or complete-restore factory callback is caught by the final provider reread. Each newly allocated actual GPU backend is disposed. Native arrays are restored afterward.
- Native yield quantities, initial quantities, dormant empty inventory/disabled state and natural nondead state remain unchanged. Strict consequence sinks permit no effect calls; rejected staging leaves the resource guard save-safe.

The snapshot contains33 cells, TargetId1 at31/32 and SlotIds1/2 there. Both durable Guid bindings retain two local slots; only the visible owner has GPU-known slots, no hidden historical fuel is invented. Seed17, tick0, no pending changes or archives.

## Cleanup, fingerprints and limits

The fixture logs success, then deterministic cleanup `sessions=2/2 backends=4/4 gameobjects=4/4 errors=0`, then exit-after-cleanup0. No Persistent-allocation/Leak Detected or shader/UAV error appeared. The familiar licensing access-token update error preceded successful execution and remains recorded in the log.

SHA-256:

- Native: `439fd598dd71d5cd2fb251ca175d9dd3bc77ea734066a538a6776694d40ba6ea`
- Core: `a800c9755bedb7c6fa61ed226d10872a0cc25c31eaefd146c5df73f66cccc4ca`
- Shader: `97ddf54667267b7ae34d22eecea3408f6187a959de1ff7c0366b2c5f2af93bd0`
- Probe: `0ec9b8452ee336b37a6c9f917c8296a9b0f6e53256e257f7584ac3cfed027c73`

This proves supplied native state, actual engine buffer construction/readback and complete staging at the full native domain. It does not prove native construction/support placement, full prefab or world loading, ordinary Tick, GPU reconciliation, a live colony, old-save migration, natural fuel policy or33-layer performance. The deliberately placed upper Pine objects do not claim that gameplay planting permits that location. Editor6000.3 engine use with installed game assemblies remains narrower than whole-mod compatibility with the game's different Unity version. CompleteStaged does not waive the reconciliation-before-tick gate.

Prior proofs are unchanged. No Steam/game launch, deployment or production edit occurred. Final process check found no Unity/game/Blender, the shared build/deploy lock was released, and `/root/baseline_audit` remains sole idle controller.
