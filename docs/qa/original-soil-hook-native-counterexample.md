# Original soil hook: native authority counterexample

One licensed CPU-only Unity run on 2026-09-08 executed the unchanged `683a506` native wrapper and adapter. It **reported one applied cell while native authoritative contamination stayed zero**; the native rendering-change queue gained one entry. This confirms the incorrect effect claim identified in [the legacy followup audit](legacy-native-followup-gaps.md).

The actual `SoilContaminationService` and `TerrainMaterialMap` instances used explicitly supplied managed arrays/queue for a 2×2×2 fixture. Candidate cell 7, strength 3, used explicit XY index 3. The real original wrapper found and invoked native `UpdateContamination`; the adapter returned candidate/applied counts 1/1. Actual native `Contamination(3)` returned 0 before and after; the native queue count became 1. Neither native class nor the production DLL was patched. No renderer stub was used.

This proves a service-level counterexample. It does not exercise a simulator, native world/column mapping, contaminated-object lifecycle, terrain singleton initialization, texture upload, or persisted world. No scene objects or native graphics resources were allocated by the fixture. Assembly resolution was removed before `EditorApplication.Exit`; exit 0 followed cleanup logging. No Timberborn process, Steam action, deployment, UI operation, or save modification occurred.

The source-only recipe was adapted solely to reflection bindings: the exact mod DLL stayed outside `Assets`, its original internal API and public adapter were constructed by reflection, and the candidate used an array of its actual reflected type. Matching Core supplied `Func<FireGrid?>`; the optional log argument was explicitly null. This avoided changing the production assembly or adding it to Unity's import graph.

Runtime was licensed Editor **6000.3.6f1**, `-batchmode -nographics`, `graphics=Null`, versus installed game engine **6000.5.5f1**. This is not whole-mod compatibility proof. One attempt succeeded; no corrective retry was needed.

SHA-256:

| Artifact | Hash |
| --- | --- |
| Wildfire.Timberborn.dll, original 683a506 | `1ba1b705f6e0ac6512d693b1bd8c6896740da9cd8b7b5b9da7149e6f3cdc993d` |
| Matching Wildfire.Core.dll | `04072fb84d38336e467967b1750a73e4c3a1afa19ae4864c2fb80592bf64c3f0` |
| Adapted probe | `ccbca5f372e08b544b5e8e91ebd45babb9f2cb71dddd417d5d0218bc9984fac4` |
| Native SoilContaminationSystem | `dca062222ef4b6334faddd79aa5cc94528e98cf9df1ed86c5781f08837bda97c` |
| Native TerrainSystemRendering | `14f756b26038146b3b410f1985ef01222fc73eeb633da1f5ba7339fc609b6922` |

Artifacts: `/tmp/wildfire-original-soil-hook-engine-run/` contains `result.json`, full `unity.log`/`stdout.log`, `command.json`, before/final hashes, native hashes, adapted source under `UnityProject/Assets/Editor`, and controller state. Source recipe remains `/tmp/wildfire-soil-authority-review/OriginalSoilHookEngineProbe.cs`. No supported replacement native write is implied by this counterexample; that capability requires separate design and validation.
