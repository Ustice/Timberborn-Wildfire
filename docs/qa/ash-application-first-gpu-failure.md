# Conditional ash application: first GPU failure — 2026-09-07

Actual licensed Unity 6000.3.6f1 / Metal Apple M2 Pro run of exact isolated source `941f24954f5df286dfe15a6ee287d11ca34529d8`. The seven `AshApplicationShaderTests` executed with shader opt-in: **6 failed, 1 passed, 0 skipped**, duration 92 seconds. The malformed/reused-command rejection case passed. Every valid application case failed with `Missing or invalid GPU clean ash application receipt`. Related collection/external tests and the full suite were held pending diagnosis; no successful application or native gameplay claim follows from managed tests.

Production HLSL and its actual imported `Assets/WildfireGenerated/FireSim.compute` both hash to `2eaf0b9c143c80ead7616bc5f21775bbca8000063fdaf23093e441b1f0f72f2c`. The actual log records successful compile, Metal device Apple M2 Pro, supportedRandomWriteTargetCount=8 and maxComputeBufferInputsCompute=31. The inspected failing capture reports no shader warning. Neither fixture nor production shader was changed during this run.

Concrete `ash-apply-open-soil` capture: uploaded four-word command `[0,4096,33554432,0]` (cell0, exclusive application mask, limit1) is returned unchanged in `appliedChangeWords`, without receipt validity/result bits. Initial packed cell2048, atmospheric33114, companion167772161 become cell2048, atmospheric336, companion167772161 after simulation; no ash was added. Its fixture/capture/log directory is `/tmp/wildfire-ash-application-gpu-proof/tmp/wildfire-shader-harness/1b3820aab37e467c84d326f1bcbb3976/`. This establishes missing application mutation/receipt; it does not establish a compiler cause.

Evidence root `/tmp/wildfire-ash-application-gpu-proof/` preserves source/shader hash, exact command, `application.log`, `results/application.trx`, isolated per-fixture JSON and actual Unity logs. The source owner received the exact command/capture for diagnosis. The test process and Unity processes terminated; no game or deployment overlapped. No unchanged shader retry occurred. This result is separate from the previously canceled unsupported ash-capacity4–7 hypothesis.

## One-variable diagnostic: unchanged failure

After the subsequent live game exited, the controller ran one separately prepared shader candidate: only the application branch local `validRequest` was renamed to `validApplicationRequest`. Candidate and imported shader SHA256 both `9e0c685125c466cc4dbda7f4641e2c4220b23f6c54f770ca6a230cd2b93443fd`. The unchanged original open-soil fixture SHA256 was `a9bfba36aba1eacffd60da0ea8efa86288f30c195dc070e6274ae400179d9911`.

A fresh copied Unity project executed and exited 0. Actual applied words remained `[0,4096,33554432,0]`, rather than the required receipt `[0,4096,704643072,0]`; final packed cell 2048, atmospheric 336 and companion 167772161 matched the original failing case. Extracted generated Metal still contains an application `0x1000` branch with only increment/continue and no application field/receipt writes. Renaming this local alone therefore did not resolve the omission. No further candidate or broad suite was run from this result.

Artifacts are under `/tmp/wildfire-ash-application-rename-probe1/`: exact source, fixture, command, capture, Unity log, generated Metal strings and cache hashes. Metal/Apple M2 Pro reported UAV limit 8 and compute input limit 31. Dispatch/readback returned on the same process after the observed wait; it was not classified as a hang. The controller returned to an idle exclusive lock with game and Unity absent.

## Request/output separation diagnostic: unchanged failure

A second authorized one-case experiment started again from the exact original `941f249` shader, not the rename candidate. It built the receipt in a separate local scalar and assigned `change.AddFields` after validation; request rules, conditions, layouts and fixture stayed unchanged. Candidate/imported SHA256 was `12fea6bc5afc33b495d1df948b785d0904deacc61265b87314019fe8b7a92863`; the fixture retained SHA256 `a9bfba36aba1eacffd60da0ea8efa86288f30c195dc070e6274ae400179d9911`.

The fresh copied Unity project completed with exit 0 on Metal/M2 Pro (UAV 8, compute inputs 31). Actual words were again `[0,4096,33554432,0]`, with final cell 2048, atmospheric 336 and companion 167772161. The generated application `0x1000` branch still only incremented and continued. Separating the output scalar alone therefore did not resolve the omission. No full suite or additional candidate was run. Exact artifacts, imported source, capture, Unity log and generated Metal are under `/tmp/wildfire-ash-application-request-copy-engine-probe2/`; both prior failed attempts remain intact. The controller released its engine lock after the process exited.

## Independent predicate instrumentation: entire evidence store omitted

After the current Warden game exited, one fresh copied Unity project executed the reviewed predicate diagnostic, imported shader SHA256 `c1183149fda4b4954a030885ea843b5b2bf59b488b204355924f6d95e08507be`, with the identical fixture SHA256 `a9bfba36aba1eacffd60da0ea8efa86288f30c195dc070e6274ae400179d9911`. The source preserves the original application body and independently encodes four request predicates plus their conjunction into SetValues with tag `0xa5000000`; all true would produce `0xa500001f`. This intentionally violates normal receipt identity, so only raw capture is interpreted. This shader was never deployed to a game.

Actual Unity exited 0, Metal/Apple M2 Pro, UAV8/compute inputs31. Raw words remained `[0,4096,33554432,0]`, without either the diagnostic tag or a valid application receipt. Final cell2048, atmospheric336 and companion167772161 match the prior failures. The generated application `0x1000` branch again consists only of loop-index increment and `continue`; neither the tag constant nor its store survives. **No predicate truth value can be inferred from this absent write.** Independent instrumentation was eliminated with the original effects; the exact responsible compilation pass remains unidentified.

Evidence `/tmp/wildfire-ash-application-predicate-engine-probe3/` contains the exact candidate/fixture, fresh project, command, actual capture, Unity/compiler log and `generated-metal.txt`. Apply-kernel cache SHA256 `f18c2456aebda5a1628c98ef5752c176c4a0433915e348c28803b124104a7d2f`; actual imported source matches the candidate hash. Original and both earlier attempts remain preserved. No broader suite or additional variant followed; game and engine are absent and the controller retains exclusive idle ownership.

## Bytecode disassembly localizes the omission before Metal translation

One additional diagnostic changed only the original shader's pragma list by adding `hlslcc_bytecode_disassembly`. Fresh project/imported candidate SHA256 `5cb0031a38ade33282c6c55015a7bd7b9218fb3f5309f0b434688688182059a2`; the same open-soil fixture retained SHA256 `a9bfba36aba1eacffd60da0ea8efa86288f30c195dc070e6274ae400179d9911`. Actual Unity6000.3.6f1 exited0 on Metal/M2Pro. Raw receipt and final fields repeated the original failure exactly.

The complete generated source now includes its upstream bytecode disassembly in `#if 0`, headed Microsoft HLSL Shader Compiler10.1. **The input bytecode already omits the application stores.** Lines1121–1126 of `apply-complete-generated-metal.txt` test mask4096 and only increment/move the loop index then continue. Collection immediately above retains its atmospheric and command-receipt `store_structured` instructions. This localizes the omission before HLSLcc translation; it does not identify the precise earlier transformation or establish runtime predicate values.

`/tmp/wildfire-ash-application-bytecode-engine-probe4/` preserves full Apply and Simulate generated text including disabled disassembly, original binary cache files, exact source/fixture, import/compiler logs, command and raw capture. Apply cache59268bytes SHA256 `559481c46076d734586c5601c690d99b2a22a73e4a1e4314dc8ed1cf840dc7e9`. No source workaround, new variant or broad suite followed; the diagnostic was never deployed. The source owner received the artifacts before the controller resumed the separately reviewed native live retry.

## Loop-merge candidate: first actual application pass

After the native charge/return game checkpoint and graceful exit, one reviewed structural candidate replaced the application's terminal `continue` with an `else` around the unchanged generic tail, joining the normal loop increment. The diagnostic disassembly pragma remained. All application statements, field rules, caps, resource bindings and fixture were unchanged; original and prior attempts remain preserved.

Candidate/imported SHA256 `b6718ea1638d938549b02fd3a341e2bf99a0183403bdf9b1049db4a6e58ccdd1`, same fixture `a9bfba36aba1eacffd60da0ea8efa86288f30c195dc070e6274ae400179d9911`. **Actual one-tick raw capture passed:** `[0,4096,704643072,0]`, exactly the expected completed application receipt. Final cell2048 and companion167772161 remain unchanged; atmospheric848 includes the added ash after normal simulation, compared with336 in failing attempts. Unity exited0 on the same Metal/M2Pro device.

Both generated Metal (application branch at line523 onward) and embedded input bytecode (line1202 onward) now retain validation, landing evaluation, atmospheric mutation and receipt stores. This proves the structural candidate works for this one compiler/fixture; it does not identify the internal optimizer pass. The Simulate cache remains byte-identical to probe4. Broader seven-case application and shared shader regressions, and the final production source without the diagnostic pragma, remain required before adoption.

Artifacts `/tmp/wildfire-ash-application-loop-merge-engine-probe5/` include exact shader/fixture, raw capture, full generated Apply/Simulate source and embedded disassembly, original caches, command and compiler logs. Apply cache66220bytes SHA256 `3b9ad3ad6ab3796876d69d8a29e56501c3b6f6af2ef7db2050443353eb0730af`; Simulate SHA256 `13ddb1b0aba2ecf22bd1d6a802e69cfd19f7c597589774cb4de4b96cb80aabd5`. The controller reported this pass before any broader run or production shader deployment.

## Plain production candidate: full shared GPU regression pass

Exact immutable source `d27b17385e9c9d50694dd9a4fcb488c45bfcee3b` was tested in a new detached checkout. Plain `FireSim.compute` SHA256 `97a49b3feaa9592803bb372108f70e21c2494c66c1a0e43089d3ee521fd6f5a7` contains the loop-merge change without any disassembly pragma. Final imported source matches it byte-for-byte. No fixtures or production rules were adjusted during validation.

The sole controller ran the authorized stages sequentially with `WILDFIRE_RUN_UNITY_SHADER_HARNESS=1`, Unity6000.3.6f1, Release configuration:

| Stage | Actual result |
| --- | --- |
| Original `AshApplicationShaderTests` |7 passed,0 failed,0 skipped|
| `AshCollectionShaderTests` plus `ExternalChangeShaderTests` |9 passed,0 failed,0 skipped|
| Complete `Wildfire.Shader.Tests` project |46 passed,0 failed,0 skipped|

All processes exited0. The three stages retained101 actual captures. The original open-soil case returns `[0,4096,704643072,0]` and atmospheric848; competing applicants, ordered removal/collection, taint, invalid surface, malformed/reused requests and unchanged-field controls pass. The full suite also covers typed baseline/material handoffs and ordinary fire/transport behavior. Device events identify Metal/AppleM2Pro, supportedRandomWriteTargetCount8 and maxComputeBufferInputsCompute31. Across retained logs, no shader warning or UAV-limit warning was found.

Evidence `/tmp/wildfire-ash-application-final-gpu-proof/`: `SUMMARY.json`, all stage commands/logs/TRX, `capture-index.json`, `shader-warning-audit.json`, and each fixture/capture/Unity log under `tmp/wildfire-shader-harness/`. The original failed6/7 run and four diagnostic failures remain preserved separately. This establishes the final plain source's behavior on the recorded compiler/device, not an identified internal compiler bug, Windows support, or native worker application. No shader deployment occurred during these tests; game/Unity processes were absent after completion and the controller released its engine lock.
