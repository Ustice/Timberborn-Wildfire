# Snapshot enum validation allocation

Source checkpoint `492cb56`. Complete snapshot validation previously called `Enum.IsDefined(Type, object)` twice per active or archived companion field. Those byte arguments box on every call. The validator now derives private byte-membership tables from those same enum definitions once, retaining sparse, aliased and future declared byte values without imposing a numeric-range assumption. Every snapshot array is still copied and the complete ownership graph still validated.

Validation: 192 Core tests and 1,276 native tests passed. An exhaustive test checks every 256×8 material/contamination combination for both active and archived cells against the declared enum schema. Existing reserved-bit, malformed-graph, array-isolation and restoration tests remain unchanged. Independent source review found no new API or dynamic-code dependency beyond the existing .NET Standard surface; actual game-runtime performance remains unmeasured.

A causal allocation comparison in `/tmp/wildfire-snapshot-enum-membership` reused the existing full-domain benchmark executable and native adapter DLL byte-for-byte, replacing only Core. Both variants used 32×32×33 cells, .NET 10 Release with tiering disabled, two warmups and three samples. Hashes and raw samples are retained there. Median per-plan allocation:

| Case | Before | After |
|---|---:|---:|
| Rich-baseline no-op | 4,464,912 bytes | 1,220,224 bytes |
| Sparse move | 4,471,456 bytes | 1,226,768 bytes |

This removes approximately 73% of allocation in these cases without changing their expected handoff counts. Measured median host times were 14.844→2.914 ms and 15.373→4.108 ms, but concurrent game activity makes timing less controlled; allocation is the principal evidence. These small-grid .NET measurements do not establish Mono, GPU readback, native world capture, full-grid latency or per-frame readiness. Earlier 128/256×33 measurements predate this optimization and remain historical evidence, not current performance claims.

A subsequent [paired full-height comparison](planner-full-domain-performance.md) repeated the unchanged benchmark at 128×128×33 and 256×256×33 with only Core replaced. It confirms the same 72.72% allocation reduction; the 256-grid no-op measured 320.47→60.70 ms and 272.269→74.254 MiB. This supersedes the missing full-grid measurement above, while Mono, native capture, GPU transfer and running-colony timing remain unmeasured.
