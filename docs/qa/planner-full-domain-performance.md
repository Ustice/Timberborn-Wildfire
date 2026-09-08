# Planner cost at the full native world depth

Measured actual production planner from `a68c6f5690955ddf99729a94efb7d57ae6f8cb27`, using the matching freshly built native/Core assemblies from the full-domain engine checkpoint. Benchmark source, raw samples, runtime/fingerprints and peak RSS are in `/tmp/wildfire-planner-full-domain-performance`. No production optimization or engine/game run was made for this measurement.

Same established methodology: .NET10.0.101 arm64, Release, `DOTNET_TieredCompilation=0`, two warmups plus three samples per case. Setup and explicit GC occur outside timed regions. Current-thread allocated bytes and Stopwatch elapsed time cover the production Plan call with the existing real Core snapshot/coordinator scripted backend. It asserts null for a rich-baseline no-op and exactly three requests for a two-slot overlapping move.

The new required Fresh eligibility input is explicit: empty for no-owner no-op, the fixture's single observed owner Guid for the move. Both moving slots already have authoritative known state; no Fresh request is generated. This measures the current mandatory eligibility validation as well as the existing planner logic.

| Grid | Cells | Scenario | Median ms | Allocated MiB |
| --- | ---: | --- | ---: | ---: |
| 128×128×33 | 540,672 | No-op baseline | 77.37 | 68.07 |
| 128×128×33 | 540,672 | Sparse move | 83.78 | 68.08 |
| 256×256×33 | 2,162,688 | No-op baseline | 307.22 | 272.27 |
| 256×256×33 | 2,162,688 | Sparse move | 336.08 | 272.28 |

All samples had identical allocation counts within their scenario. Measured calls caused6 Gen0 collections at128 and24 at256, with no Gen1/Gen2 collection inside the measured regions. Combined process wall time was5.08s, peak resident set550,191,104 bytes (524.70MiB), and zero swaps. Peak includes setup, warmups and both grid sizes; it is not per-call allocation or game memory usage.

The historical optimized23-layer figures were53.56/58.55ms and47.44/47.45MiB at128 (no-op/move), and215.13/233.44ms and189.76/189.77MiB at256. The new measurements are roughly1.43–1.44× those timings and1.435× those allocations. Depth raises cell count by33/23; however, source now also includes mandatory eligibility and related validated snapshot behavior, and timing was sampled later. This comparison does not isolate an exact causal depth-only effect.

The baseline remains solid layer0, alternating OpenSoil/Water/Badwater at layer1, and Empty elsewhere; the sparse case has one retained two-slot owner. This covers native full-world grid dimensions, not dense-colony ownership counts or worst-case handoff request generation. It does not include native entity/environment capture, Unity Mono runtime behavior, real GPU readback/upload/dispatch, consequence callbacks, rendering or gameplay-frame timing.

Recommendation for schedule design: even the no-op full-grid path is a substantial operation (about307ms/272MiB at256 on this managed fixture). Do not treat it as a per-frame readiness check. Stage a complete guarded reconciliation when native changes require it, with an explicit before-ordinary-tick readiness boundary. Deciding how to coalesce changes or reduce repeated full snapshots requires a separate coherent design; this measurement adds no dirty-state registry, fuel ledger, scheduler or optimization. The pending ordinary-colony/live evidence remains necessary.

## Core-only enum membership follow-up

Actual production planner benchmark, 2026-09-07. Copied the exact prior full-domain binary set into two isolated directories and replaced **only Wildfire.Core.dll** in the after directory with the Core-only `492cb56` optimization from the prior small-grid comparison. All other files, including Bench.dll, native DLL, dependencies and PDBs, remained byte-identical. No rebuild or source edits were made for this measurement. SHA-256 manifests verify this causal boundary; the unchanged old PDB is not used for execution or source attribution.

Same .NET SDK 10.0.101 arm64 Release methodology: DOTNET_TieredCompilation=0, two warmups plus three samples per scenario, setup/explicit GC outside timed regions. Exact existing source includes explicit Fresh eligibility and uses a rich baseline no-op or one retained two-slot sparse move. Returned plans still satisfy the original null/three-request assertions.

| Grid | Scenario | Median ms before → after | Allocated MiB before → after |
| --- | --- | ---: | ---: |
| 128×128×33 | rich_baseline_noop | 76.50 → 15.12 | 68.070 → 18.566 |
| 128×128×33 | sparse_move | 84.25 → 22.47 | 68.077 → 18.572 |
| 256×256×33 | rich_baseline_noop | 320.47 → 60.70 | 272.269 → 74.254 |
| 256×256×33 | sparse_move | 340.56 → 90.75 | 272.275 → 74.260 |

Every sample had identical allocation counts within its scenario. No-op savings are 51,909,104 bytes at128 and207,634,448 bytes at256, approximately72.72%; the sparse case saves exactly the same bytes. Gen0 collections inside each call changed6→0 at128 and24→0 at256; no measured Gen1/Gen2 collections. Combined before/after process wall times were5.12s/1.31s; peak RSS549,928,960/547,569,664 bytes, both zero swaps. Similar peak RSS is expected to include unchanged setup/state buffers and is not per-call allocated volume.

This new before run exactly reproduces the historical full-domain allocation counts (68.070/68.077MiB at128,272.269/272.275MiB at256); its timing is close to, but not identical with, the historical77.37/83.78 and307.22/336.08ms. The paired comparison isolates Core replacement; small timing samples remain machine-load-sensitive.

Bench SHA-256: `9791bf6548316b50534842aeac7b3577418207254625efebd9c0acca811ef04f`. Native: `439fd598dd71d5cd2fb251ca175d9dd3bc77ea734066a538a6776694d40ba6ea`. Core before: `a800c9755bedb7c6fa61ed226d10872a0cc25c31eaefd146c5df73f66cccc4ca`; after: `e7b68f48807d2e990ab7130232cbaed54c72d56c164d0cca004d854b40f5b6f6`.

Evidence: `/tmp/wildfire-planner-full33-enum-comparison` contains unchanged source, complete hashes, raw samples, summary and /usr/bin/time -l output. This excludes Unity Mono, GPU transfer/dispatch, native capture, rendering and game frame scheduling. No optimization, larger grid, engine run or scheduler was added. Even improved full-grid validation remains substantial; the result does not establish per-frame reconciliation suitability.
