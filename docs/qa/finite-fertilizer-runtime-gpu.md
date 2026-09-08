# Finite fertilizer Runtime and GPU component proof

On immutable `62e1e6142759ff062c14d24e9aed3640a43e4278`, two fresh supplied native scenes passed actual licensed Unity6000.3.6f1/Metal (Apple M2 Pro). Terminal71832/PID69046 exited0 on2026-09-08. Installed native game assemblies are from6000.5.5. Production source and bindings were unchanged.

## Executed boundary

Real Farmhouse/Carrot/public-source GameObjects execute native Entity.PreInitialize/Initialize/PostInitialize, placement/soil/range checks and public inventory/district registration. Actual Worker employment, source validity, one-unit reservation and `FertilizerSatchel.TryPickup` succeed. Native out-of-hours, Hunger, blocked-source and emptying-status controls reject before quantities change. Source status uses the actual native factory, Resources sprite and icon activation; this is not native prefab-factory proof.

After pickup, the fixture explicitly supplies actor position and Ready phase. The actual `TimberbornFireRuntime` constructor and initialization use an explicitly published session containing the real native compute simulator/readers. Normal `DispatchFireUpdate` runs the prepared concrete executor, GPU marker/receipt, native disposition, field revision and transport followups. No direct low-level application or last-sync-tick override substitutes for this path.

| Actual result | Clean | Earlier tainted layer |
|---|---:|---:|
| GPU receipt (cell1349, limit1) | Applied, Added1 | Tainted, Added0 |
| Native pickup commits | 1 | 1 |
| Completed consumption / native district tally | 1 | 0 |
| Final source / satchel / source reservation | 0 / 0 / 0 | 0 / 1 / 0 |
| Actor phase | Ending | Ending |
| Final transport word / ash / contamination | 512 / 1 / 0 | 29184 / 1 / 7 |
| Current transport-derived growth requests | 1 | 0 |
| Ordinary native growth | .00249999762 | .00249999762 |
| Expected extra growth | .00008333333 | 0 |
| Final native growth | .002583325 | .00249999762 |

Raw applied words `[CellIndex,SetMask,AddFields,SetValues]` are `[1349,4096,704643072,0]` and `[1349,4096,2717908992,0]`. The negative queues atomic SetAsh1+SetAshContamination7 before the marker, testing Tainted before Full at limit1. Native pickup emits `None/+1`; only clean emits `Consumed/-1`. Quantities and district consumption independently confirm the completed effect.

Both actual GPU steps have tick1, zero packed deltas and one field revision; each64×5×33 snapshot contains exactly one nonzero transport cell. Dispatch and observation do not grow the plant. A separate actual native `TickableSingletonService.TickAll` advances clock25→26/day.25→.26, then invokes the explicitly composed late-growth ticker. Its real readiness reader sees settled flags and current Runtime transport. Registered native TimeTrigger progress before the bonus matches elapsed day / Carrot duration4; positive multiplier is independently checked against1+.1/3. Tainted produces no growth request or extra progress. This interval does not complete the plant.

Native BehaviorManager/Inventory/GoodReserver Save succeeds at rest, retaining actor-owned FertilizerBehavior/FertilizerExecutor and Ending phase; the rejected case serializes its one native satchel unit. This run does not reload. [Stage A](finite-fertilizer-native-persistence.md) remains the separate persistence/cancel proof.

## Scope and remaining gates

Adult liveness/Initialized state, narrow needs setup, component composition, authored navigation edges, source access, clock inputs and post-pickup positions/Ready phase are supplied. The scene uses an authored inert GPU soil baseline with no burning body, and a Null visual surface. It proves no physical trip, natural arrival, full adult or prefab initialization, Pine/TreeReacher route, whole-world projection/load, rendering, shipping designation lifecycle or production automatic binding. The late ticker is explicitly composed and remains unbound in shipping code. Full-queue Ready and callback-drift engine cases are not included. Current-world readiness and accepted GPU-to-native soil growth are now demonstrated within this component boundary; these results do not activate either worker or ticker.

## Evidence and fixture corrections

Full report and artifacts: `/tmp/wildfire-fertilizer-stage-c-taint-corrected-run/REPORT.md`. The directory retains both result/raw-command/complete-GPU-snapshot/actor-save JSONs, scene inputs, actual-command.json, unity.log, process outcome and controller snapshots. Prepared/final manifests verify all36 source/DLL/shader/asset/project inputs unchanged. Four resolver-observed native dependency hashes match the first attempt; this is not a complete assembly-closure manifest.

Four failed fixture attempts remain intact:

1. `/tmp/wildfire-fertilizer-stage-c-engine-run`,30169/PID67005: icon-only native Empty status does not enter the global alert collection. Corrected assertion proves icon0→1→0, sprite/subject identity and actual validity.
2. `/tmp/wildfire-fertilizer-stage-c-status-corrected-run`,81075/PID67463: native ReadOnlyHashSet is not non-generic IEnumerable. Replaced the fixture cast with its actual Contains API and audited remaining collection contracts.
3. `/tmp/wildfire-fertilizer-stage-c-membership-corrected-run`,57538/PID68000: fixture CaptureAtRest around native Save correctly triggered save reentry exclusion. Removed only that redundant outer capture; native guards remain.
4. `/tmp/wildfire-fertilizer-stage-c-save-corrected-run`,70849/PID68425: the complete positive passed and saved. Negative contamination on ash0 correctly normalized0. The fifth input sets ash1 and contamination7 atomically; no production change.

Each final scene cleans up3/3 bodies and5/5 root GameObjects, zero errors, before Editor exit. No Persistent allocation or UAV warning appears. License refresh, shutdown thread and debugger-listen notices remain in the full log; they are not attributed to production, and notice absence is not a general leak proof. No game, Steam, deployment or heat action occurred.

| Artifact | SHA-256 |
|---|---|
| Native DLL | `98e77c6659e795220a0d34a1349e267cb1550a0ed94de14f3e73b7d569b7b8f7` |
| Core DLL | `99d00a96644b4239dd5d278e2df7b9fd8d8dc7d91f8aae5dd3bd218450a3ba06` |
| Test-only helper | `21afc9dd7fd079a736e9ea2a762638040cac5943d851a7b6c9021f04ec2bfe6d` |
| Shader | `97a49b3feaa9592803bb372108f70e21c2494c66c1a0e43089d3ee521fd6f5a7` |
| Final Cases | `3fad4e69877e7b2b6ed572e59429555b29948705cfd312d1fe884110259a741f` |
| Final Source | `f5da52733f04b80a062e32e31049da839f482afdf1deacd9e363bceda8ede383` |
| Native Empty sprite | `9c6d9e5dd531f02544cdb8ceb1fb7b22b5438ad81a2ad4ccf32bcc3f2af2bb30` |
