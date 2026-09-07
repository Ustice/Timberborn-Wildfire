# Supplied native initial-session engine proof

Executed 2026-09-07 on exact source `46c84a4`, including the complete restore-fidelity check and soil saturation fix. Native Release build: zero warnings/errors. Licensed Unity 6000.3.6f1, Metal on Apple M2 Pro: **fixture passed, Editor exit 0**. Evidence, runnable probe, readback artifacts and logs: `/tmp/wildfire-owned-initial-session-engine`.

The unchanged production mod assembly is loaded outside Assets by reflection; installed native game assemblies target Unity 6000.5.5. This is a bounded session/factory proof, not full-mod Editor compatibility or a loaded colony.

## Exercised path

Two real Unity GameObjects host native ComponentCache instances initialized with supplied EntityComponent, BlockObject, Cuttable and named Yielder components. Actual native initialization, registry registration, Blocks/Placement and component getters run. Both supplied Pine bodies have Log3 current/initial yield, Log5 declaration and a two-cell vertical footprint at cells 1/2. One explicit accounting selection uses actual 3, the other declared 5; their body capacities are 36/60. These supplied choices do not settle gameplay accounting policy or reproduce the full Pine template.

The existing native environment fixture supplies native map/service backing arrays: solid cells 0/3, soil surfaces 1/4, clean fractional water at 1, contaminated depth 1.5 above 4. The real environment getters and provider `CaptureDuringScope` reread these facts inside the initial session's same guard. They are supplied native state, not data captured from a loaded game.

`PrepareInitial` runs the actual projector, body compiler, registry, history and real `TimberbornComputeFireSimulator.CreateFromSnapshot`. Its initial readback retains complete material history and exactly matches the input packet. Two capture callbacks surround one actual backend allocation.

- Cells: `[4096,7944,4872,4096,3072,3072]`.
- Companion words: `[1,5245956,5245956,1,167772161,11534345]`.
- Target IDs: `[0,1,1,0,0,0]`; slot IDs: `[0,1,2,0,0,0]`.
- Both native Guids retain two local slots. Only visible target 1 slots 1/2 are GPU-known; hidden target 2 has bindings and body/witness history without invented active state or archives.
- Tick 0, seed 17, zero transport, no pending inputs, attempt token 0 and no archives.

Session capture encodes OWNED3 with static witnesses, decodes it and calls `PrepareRestore` with a fresh actual native simulator and the same guard. Retained-body capture rereads actual components twice. Exact cells, companion, transport, target/slot, parameters, seed, known sets and input/history state match; the saved OWNED3 text is identical after restore. The new restore-fidelity boundary executes successfully.

Native current/initial yield quantities remain 3 and components remain enabled. Actual native body liveness is used; all other effect interfaces throw on every method, so unintended consequence effects fail the fixture. No harvest, inventory transfer or reservation lifecycle is exercised.

A separate initial attempt injects a changed final *captured observation* after allocating an actual native backend. Publication is rejected and the actual backend's disposed state is verified. This proves cleanup after late mismatch; it is not a reproduced concurrent native mutation race.

## Boundaries and fingerprints

No simulation tick, game launch, save loading, deployment or full native template lifecycle occurred. Restored desired projections and rich environmental baseline are still an activation gap: snapshot conservation does **not** establish a ready-to-tick world. Unsupported ordinary colony profiles remain separately gated.

- Native DLL SHA256: `f008249475dcae1ba3557d50d734ade901a91a42954e5ac4fa00f3ceedadded5`.
- Core DLL SHA256: `6c557fb5106d89d845bd8fd9b050c0c60889e0ea908a22eb0af7d0ec560fa068`.
- Probe SHA256: `7a3ece2e26ed8bdd6556a22b5ce5ec39e9e8f94d5e1148cadad891780f91d8f5`.

The same probe first passed on 1f1c9d1; that evidence is preserved in `passed-1f1c9d1`. An earlier setup attempt omitted the shader copy and failed at the native factory's null-shader guard; `attempt-1-missing-shader` is excluded from successful proof. No source was changed for either fixture correction.

No shader/UAV warnings appeared in the successful final run. Controller ended idle with no Unity/game/Blender process and no build/deploy lock. One read-only Steam refresh at 23:41 UTC found the latest recorded login state still `WaitingForCredentials` at 12:45:53 local; it supplied no newer positive authentication evidence and triggered no launch.
