# Desired-material planner allocation reduction

Production change `cf8e97d`, measured against unchanged planner source `5878283`. The planner and
registry files had no intervening behavior changes in the optimization's base `b343e17`.
Evidence, benchmark sources and raw samples: `/tmp/wildfire-planner-performance`.

## Managed measurement

.NET 10.0.101/arm64, Release, tiered compilation disabled; two warmups and three measured runs per
case. GC runs outside timed sections. Allocations use current-thread allocated bytes; setup is
excluded. The unchanged benchmark calls the actual production planner and existing Core coordinator
snapshot fixture. It verifies null for no-op and three requests for a two-slot overlapping move,
with no timing assertions. These are managed costs, not Unity Mono, GPU readback or game-frame timings.

| Grid (depth 23) | Scenario | Before ms | After ms | Before MiB | After MiB |
| --- | --- | ---: | ---: | ---: | ---: |
| 32×32 | No-op baseline | 20.75 | 3.33 | 33.02 | 2.97 |
| 32×32 | Sparse move | 21.20 | 3.64 | 33.03 | 2.97 |
| 64×64 | No-op baseline | 87.70 | 13.47 | 132.08 | 11.86 |
| 64×64 | Sparse move | 89.42 | 14.66 | 132.08 | 11.87 |
| 128×128 | No-op baseline | 347.18 | 53.56 | 528.29 | 47.44 |
| 128×128 | Sparse move | 358.77 | 58.55 | 528.30 | 47.45 |
| 256×256 | No-op baseline | 1427.65 | 215.13 | 2113.15 | 189.76 |
| 256×256 | Sparse move | 1444.52 | 233.44 | 2113.15 | 189.77 |

The installed native MapSize blueprint specifies default 128×128, maximum 256×256 and maximum game
terrain height 22. Actual `MapSize.Initialize` IL sets terrain depth to height+1, yielding 376,832
cells by default and 1,507,328 at configured maximum size. Runtime uses that terrain size. The QA
script's 500,000-cell guard is separate; Core itself validates positive checked dimensions and buffer
byte capacity. This does not establish gameplay support for every configured map or modified specs.

The baseline has solid layer 0 and OpenSoil/Water/Badwater in layer 1; other cells are Empty. The sparse
move adds one retained two-slot owner. Both shapes initially allocated about 1,470 bytes per cell:
608 from a resolved-cell scan, 664 from baseline-request comparisons that resolved those cells again,
and 198 from three complete validation/copy paths. The two unowned-object paths caused 86.5% of cost.

The optimized planner holds a read-only view of the registry's existing published desired dictionary
and reads typed baseline values directly. It creates requests only for real changes. A known-slot
membership check against the already validated registry replaces construction of an additional
paired snapshot and registry solely to check association. Every canonical/hidden-owner, shape,
source, snapshot, known and exact-archive validation remains. No persistent index, dirty state, fuel
ledger, execution guard or simulation rule was added.

Allocations fell **91.0%**, to about 132 bytes per cell. Remaining cost is two complete capture/validation
paths, including field-array copies and boxed enum validation. At maximum size that is still about
190 MiB and 215–233 ms in this managed fixture: do not characterize this as cheap per-frame polling.
Further capture/validation work needs separate measurement and authority-preserving design.

Measurement was bounded: before maximum-size peak RSS was 541 MiB, optimized multi-size peak 386 MiB.
No larger grid was run. Full native regression suite on the optimized source: **1,220 passed**, zero
failures/skips. No tests assert implementation-shaped allocation counts or timing thresholds.

## Actual GPU regression

The same four-case licensed Metal fixture used for
[the original planner proof](desired-material-planner-gpu-proof.md) passed on exact `cf8e97d`, Editor
exit 0. Its only source substitution was the output directory. All **20 before/plan/receipt/delta/after
artifacts** are semantically identical to the original run: swap, overlapping source closure with
four older queued inputs, exhausted archive reveal and unowned Water→Badwater with independent
native simulation control. No HLSL changes or shader/UAV warnings. Evidence:
`/tmp/wildfire-desired-material-planner-engine-optimized`.

- Native DLL SHA256: `9c761f2661c71a65c740c7881e0b2e0ac984d3da33913ee0bbd75e20375f70fc`.
- Core DLL SHA256: `368336ab838b62095dff3da1f0e7cea2bd32ef605dfa9c2fb20d1c878914142f`.
- Probe SHA256: `32c44ecff0020db25224a14e66d6c16f2368015d1ad36024955ff9d95ce5567b`.

Unity 6000.3.6f1/Metal on Apple M2 Pro reports eight UAV slots and 31 compute inputs. The game-targeted
assembly remains outside Assets; whole-mod Editor compatibility and native world activation are
not claimed. Controller ended idle with no engine process or build/deploy lock. No game, Steam launch
or deployment occurred.
