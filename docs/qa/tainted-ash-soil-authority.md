# Tainted ash soil authority and unavailable capability

Source `e63faad` removes the rendering-only native hook and reports the soil effect as unavailable. Full validation on base `683a506`: **228 Core / 1,515 native tests passed, zero skipped**. No Unity/game launch, deployment or lasting soil-source implementation is included.

## Reproduced defect and proof limit

The old wrapper discovers concrete `SoilContaminationService.UpdateContamination(Vector3Int,float)` and reports `IsAvailable=true`. An actual installed service with supplied native cache zero reproduces that false capability: the regression expected false and observed true. The corrected native fixture retains the actual `Contamination(int)` getter, tests clean and existing0.8 values unchanged, and verifies the installed render method's field/call targets.

Installed native IL establishes the complete chain: `UpdateContamination → TerrainMaterialMap.SetSoilContamination → queued MapDataChange → UpdateContaminationTexture → PixelData/changed layers → texture upload/copy`. No consumer in that chain updates soil authority or contaminated-object state. The public native soil service is read-only; its getter reads `_threadSafeContaminationLevels`. The former mock-only test expecting a0.9 write codified the wrong contract and has been removed.

The attempted full native map fixture failed during `TerrainMaterialMap` static initialization: `Shader.PropertyToID` requires Unity. Therefore **applied1 / native getter0 / render queue1 has not been executed here**. This is native capability RED plus installed instruction evidence, not a full engine readback. No native method was replaced with a success stub and no native assembly was patched.

## Current behavior

- Summary distinguishes `NoCandidates`, `Applied` and `Unavailable`. Runtime explicitly binds unavailable, reports current candidate count and zero applied cells, and preserves contaminated ash entries/GPU authority.
- An unavailable diagnostic is attempted once per service instance, including if the diagnostic sink throws. Repeated observations update the summary; empty input reports NoCandidates. Clear resets observations without repeating the same capability warning.
- Existing QA output adds `tainted_ash_poison_outcome`; the old unconditional `soil_poisoning_applied` log claim is gone. High-level consequence adapter remains for a future verified implementation.
- This correction neither poisons soil nor cleanses it. It does not alter growth, water washout, native plant state, transport/shader fields or save formats.

## Why a private setter is not the fix

Native simulator candidates and levels are saved authority. Its parallel jobs compute candidates, equalize levels and mark changed columns; main-thread service Tick mirrors changed levels into contaminated-object state/render/cache. Private `SetContaminationLevel` enters/exits objects before writing the cache; callbacks may start/stop native plant dying timers or throw.

A cache injection is neither saved nor reliably corrected next tick: unchanged native zero→zero leaves the changed bit false, allowing an injected cache/object ghost until a genuine native change, reset or reload. It cannot stand in for a soil source.

The eventual route is a transient immutable per-native-terrain-column source derived from current completed ash amount/contamination, combined with native local sources before candidate spread/decay. Map through native soil-ceiling/column identity, not raw XY or fire-cell z. Publish on the main thread and freeze the source and column generation across the native parallel job. Never query Unity/GPU from worker rows. Source removal stops contribution; native residual decay remains. Native save restores its own arrays, while the transient source is rebuilt before the first eligible job after reload.

There is no installed public source-registration seam: concrete internal tasks are constructed by `SoilContaminationSimulationTaskStarter`. A lasting implementation needs one version-verified insertion/replacement point and scheduling/load proof, not an assumed DI hook or a second saved contamination ledger. No patch dependency is added. Source strength still needs explicit calibration; the removed visual0.9 target does not establish an approved dose.

## Controller-only follow-up

`/tmp/wildfire-soil-authority-review/OriginalSoilHookEngineProbe.cs` is source-only for original `683a506` mod/Core. In licensed Unity it creates a private native service/map fixture: two2×2 pixel layers, eight cache slots, one candidate(1,1,1), native map queue, threshold0.1/max shade0.9. Invoke Run once; expected original evidence is `applied=1 native_before=0 native_after=0 render_queue=1`. No real world registration, texture upload, plant, job or save is needed. It has not been run or claimed as engine proof.

A future actual source must additionally prove native job ordering, levels/service getter/SoilIsContaminated/object dying timer/render agreement, residual decay after removal, stronger background badwater, elevated terrain columns, terrain changes, and save/reload before first job. A tint or one immediate getter sample is insufficient.

Artifacts: `/tmp/wildfire-soil-authority-review/REPORT.md`, `red-capability.log`, `engine-dependent-attempt.log`, `core-full.log`, `native-full.log`, original test/probe sources and full native IL. Native hashes:

```
Timberborn.SoilContaminationSystem.dll dca062222ef4b6334faddd79aa5cc94528e98cf9df1ed86c5781f08837bda97c
Timberborn.TerrainSystemRendering.dll 14f756b26038146b3b410f1985ef01222fc73eeb633da1f5ba7339fc609b6922
```

## Subsequent native engine counterexample

A [licensed CPU-only Unity run of the original hook](original-soil-hook-native-counterexample.md) subsequently executed the exact `683a506` wrapper and adapter. It observed candidate/applied1, actual native getter0 before and after, and native render queue1. This closes the service-level execution gap above; it does not implement a replacement source or prove native terrain-column mapping, plant effects, texture upload or world persistence.
