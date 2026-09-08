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
