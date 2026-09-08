# Native Warden travel, charge and safe withdrawal

Actual macOS gameplay on 2026-09-08, exact source `85961c2` (native Transform binding fix; parent reported1360 native tests passed). The [earlier path-start exception](warden-native-supply-first-response-live.md) did not recur. Native Water pickup, travel attempts, unsafe-route withdrawal, return completion and retained-charge save are now proven. No committed suppression is claimed from this incident.

## Deployment and startup

A fresh detached checkout built Release DLLs and copied current Data using the supported deploy script. All `src/Wildfire.Unity` and `scripts` inputs were byte-identical between2fd29af and85961c2. The prior four StandaloneOSX bundles and manifests were hash-verified and copied with full original build-log provenance, so `--skip-asset-bundle` reused those outputs. All40 installed copied files matched their staged hashes; the generated manifest was the only additional file. Previous complete installed mod was backed up.

Native DLL SHA256 `d59553172b059c586c88a4d5a7a71c4324ea59bd7e5bc057150e7ab0a71c54b5`; Core `d7209f13d21139b7a326db6441e65fec9a98dd716390f6a1fa91b121b96b5369`. PID31274 was launched once with process-only `--wildfire-enable-qa-mutations`, fresh bridge access=development. No saved Steam options changed. No Unity/game overlap occurred.

`Response retry 85961c2.timber` was an exact bytecopy of the clean supplied2fd save, SHA256 `2b19c1310df22166a635f867822158c63273eff74ad1d7514e0bc4f808ce8588`, not the prior error save. Actual load completed8600ms; tick188 restored paused, second station Water20, Khemruy assigned, private charge0. Native selected coverage and20-tile/safe-route explanation appeared. Moving and rotating a placement preview updated coverage; Escape removed both preview and coverage without placing an entity or throwing.

## One heat-only incident

The selected existing Pine was again `(27,10,4)`, cell21027/TargetId160. The current native import queued20 heat-only cluster cells (the previous run had18); no new fuel, tree or inventory was injected. The same paused native save contained13 living beavers, nearest12.79 horizontal tiles from the selected target, outside the bounded cluster. No further stimulus was issued during the incident.

Events below are located after the indicated **last completed simulator tick** in `response-event-tick-order.json`; logs do not independently timestamp each Warden event. Configured dispatch cadence is1000ms, so three intervals is nominally about3 active seconds, not an independently measured wall-time interval. Pause gaps must not be counted as response time.

| Last tick | Actual event |
| --- | --- |
|192|Khemruy Fetching, cell21027, Responding; former path-start failure passed.|
|195|Returning: target no longer burning. No Water pickup yet.|
|205|Idle: Response complete; then Fetching naturally burning cell20725.|
|206|That approach became unsafe; return completed. Selected cell20824 next.|
|207|Approaching: Carrying water to fire.|
|208|Returning: installed route became unsafe.|
|209|Idle: Water retained for next response; another automatic attempt at20824.|
|211|Returning: installed route became unsafe.|
|213|Idle: Water retained; then No safely reachable fire.|

Actual native panel changed station20→19 and private charge0→1. This is backed by the native save, not inferred from generic water-field deltas. No `Applying`, `AwaitingApplication`, `Water applied`, or positive committed suppression event occurred. Native field water changes elsewhere are not Warden application evidence.

At tick212 the saved simulator still had182 burning cells,105 within horizontal radius20 of second station access; at tick224 it had10 total,3 within that radius (upper canopy cells25521/30019/30518). These are geometry counts, not safe/reachable candidates. The selected initial cell's burning level was0 at both checkpoints. Thus the incident continued after the first target died; it does not prove all response is impossible or prescribe a fire-speed change. Actor starting far from the station and subsequent safety gates are separate constraints.

## Native saved state and presentation limits

`Charged withdrawal 85961c2.timber`,141244 bytes, SHA256 `04f482ae91e5f685204135764278326fe6e78c84c6133b64e740f4062fd827ef`, stores tick212, Khemruy `Wildfire.WardenExecutor.Phase=5` (Returning), station880ae291-2db8-4f5d-960c-415ed3a2add9, target20824, approach `(24.5,4,10.5)`, return destination `(25.5,4,17.5)`. Actor position `(24.0000019,4,17.5)` is beside the station; `Inventory:Wildfire.WardenEquipment` contains exactly Water1.

`Returned charged 85961c2.timber`,137399 bytes, SHA256 `01b47304e145d381870a3d265dc01b8e6b3b249f7349d63515a956c91c0322a9`, stores tick224, Water1 still in equipment and no active executor payload. Khemruy resumed ordinary native activity at `(33.5,4,26.7057953)`. Both checkpoints have13 living beavers. Final runtime sampling reports13 sampled/0 exposed; this does not claim an exposure challenge. These saves have not yet been reloaded to verify active-phase continuation.

The active draft helmet is visible as an offset object in the unselected view near the actor; fit is not accepted. `unselected-active-helmet-position.jpg` and exact charged actor position are retained for source/anchor review. Hats mod remains enabled, so a later nearby orange hat must not be attributed to Wildfire by appearance alone. No asset pose was changed in this run.

## Evidence and stop boundary

All artifacts are under `/tmp/wildfire-warden-transform-live/`: bundle provenance and installed hashes; previous2fd mod backup; launch/action logs; clean, charged-returning and charged-idle saves; native actor/station JSON; saved burning counts; readiness outputs; full Player response/exit logs; selected coverage, rotated preview, actual charge/unsafe refusal and unselected helmet screenshots. The controller saved the bounded healthy checkpoint and exited through the native menu/confirmation. PID31274 terminated normally with no new primary exception; no additional fire or live retry followed before the queued isolated shader diagnostic.
