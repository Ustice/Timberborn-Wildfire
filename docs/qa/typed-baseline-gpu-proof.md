# Typed baseline GPU proof

Controller evidence, 2026-09-07. Logs/captures: `/tmp/wildfire-typed-baseline-proof`.

Exact detached source: `63aeaed`, `/Users/jasonkleinberg/repos/wildfire-worktrees/typed-baseline-qa`.

- Licensed BaselineShaderTests: **3 passed, 0 skipped**, 13 captures (`focused-baseline-restored`).
- Licensed MaterialHandoffShaderTests: **10 passed, 0 skipped**, 13 captures (`material-regression`).
- Exact native Release build: **0 warnings, 0 errors**.
- Real unchanged production native simulator factory: **10 cases passed**, Unity exit 0 (`native-probe`).

The native cases exercise all five canonical profiles for owned and unowned cells. Owned removal retains exact GPU fuel/history archive; unowned updates invent no known owner/archive. Two older queued inputs precede the marker despite ordinary capacity one. Receipts preserve destination heat/wetness and environmental soil/ash fields. A separate native simulator initialized from the applied GPU receipt plus original transport produces equal final cell, companion and transport after one tick.

Actual device: Metal, Apple M2 Pro, UAV limit 8, compute inputs 31. All 26 shader captures and the native probe have no shader compilation/UAV warnings. Native log includes an access-token update error from licensing startup; the existing licensed Editor still executed all ten cases and exited successfully.

Native DLL SHA256: `0bffc961667ea7c48865e90002eab0ceda01864962a763b7de7527f04649cf43`. Core DLL SHA256: `38564c7dd3b4bfa61ccb2966a83e3d95fdd81a5cf4491ce7d2c0269933fcabfd`. Probe hash is recorded in `native-probe/assembly-sha256.json`.

Scope: Unity 6000.3.6f1 loads the unmodified game-targeted assembly outside Assets through reflection; installed game assemblies target 6000.5.5. This proves the exercised simulator factory, not whole-mod Editor compatibility, native world initialization, or a loaded game. No game/deployment occurred. Controller exited idle with no Unity/game process.

The first `focused-baseline` invocation used --no-restore on a fresh checkout and executed no tests. It is retained as failed evidence setup and excluded from all counts; `focused-baseline-restored` is the executed proof.
