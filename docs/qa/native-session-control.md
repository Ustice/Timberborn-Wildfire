# Native QA session control

The existing process opt-in `--wildfire-enable-qa-mutations` gates save-copy and speed commands. These commands use the existing inbox/outbox; they do not launch the game or change saved Steam options.

| Command | Behavior |
|---|---|
| `qa-save-copy <request-guid> <QA-name>` | Queue one native save into the currently loaded save's existing settlement. Name must start `QA-`, contain only ASCII letters/digits/hyphen/underscore, and be 4–80 characters. |
| `qa-save-status <request-guid>` | Diagnostic query for the retained request; reports unknown, queued, running, completed or failed. |
| `qa-game-speed <0\|1>` | Request native pause/normal speed. Native speed locks and late-update application remain authoritative; the response reports requested and current speed separately. |

A save runs on the next regular file-bridge update, independently of whether a new inbox command arrives. It requires a loaded save, current Ready runtime and safe resource coordinator at admission and again around native `Ticker.FinishFullTick`. It rejects changed loaded-save identity, new-game state, missing directories and existing destinations. Unload cancels pending work. One transient request/result is retained; there is no retry or durable job registry.

The native `GameSaver.SaveWithoutFinishingTick` invokes the registered `SaveWriter` entries into an exclusive `CreateNew` read/write temporary file. Native ZIP Update mode requires read/write/seek support. Native serialization is not wrapped in `CaptureAtRest`, because native executor Save guards must remain callable. After closure, two-argument `File.Move` publishes without overwrite, including a destination created during serialization. Completed requires publication, closed-file length and SHA256. Failed output retains the original exception and temporary path; a separate published flag distinguishes any later measurement failure. The native global replaceable save queue and overwrite-capable repository writer are not used.

Status includes original loaded-save/settlement, final/temporary paths, request ID, publication, bytes/hash, actual runtime tick at serialization and native PartialDayNumber before/after finishing the tick. Native ITickService exposes an interval, not a global tick ID; no native counter is invented. Speed requests do not force a tick or unlock the game. Warden actor observation and helmet visuals remain separate work.

Managed/native validation on base `6478c5d`: **28 focused and 1,769 full native tests passed, zero failures/skips**. Actual installed GameSaver/SaveWriter with two supplied registered entry writers produced a closed ZIP with both payloads and matching hash. The original draft's write-only file reproduced a native failure (1 failed/23 passed), then read/write fixed it. Tests also cover preexisting/racing destinations, native-writer exception/partial closure, strict names/arguments, command policy, loaded identity mutation/replacement, pending request refusal, unload, unsafe/nonready state, and actual native speed request/lock behavior.

These are native archive and QA sequencing tests, not a complete game save/load. Runtime/load state and entry payloads are supplied. Native FileService construction queries Unity platform permissions; the lifecycle fixture uses only its actual pure path-combination method on an uninitialized instance, without supplying or testing permission truth. Native SpeedManager's real frame application calls Unity Time.timeScale, which cannot execute in these managed tests. No engine, UI, game, deployment, physical actor trip, or background-under-lock acceptance is claimed.

Evidence: `/tmp/wildfire-qa-session-{focused-red,focused-green,all-focused,full}.log`, `/tmp/wildfire-qa-session-lifecycle{,2}.log`, and `/tmp/wildfire-qa-native-session/AVAILABILITY.md`. The two lifecycle setup failures (Unity platform permissions and Time.timeScale) are preserved separately from the native writer defect. CLI known-command/help integration is maintained in the borrowed-duty command slice.
