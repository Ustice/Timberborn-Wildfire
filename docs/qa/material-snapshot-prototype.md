# Complete simulator material snapshots

`IFireSimSnapshotSimulator.CaptureSnapshot()` captures one idle, known-committed simulator boundary. `FireSimSnapshot` version 1 includes grid, tick, immutable parameters, seed, current cells and transport, exact companion/target/slot fields, known slots, inactive GPU-authored archives with their capture origins, last attempt token, and ordered pending ordinary inputs. Derived visual fields are rebuilt from cells **and transport**. Listeners, native Guid bindings, weather-provider state, and external visual publication belong to the host.

`FireSimSnapshotValidation.ValidateAndClone` validates the complete graph before allocation: exact dimensions and field widths, known material enums, paired nonzero identities, unique active slots, known slots partitioned exactly into active and inactive material, valid archive origins/tokens, finite parameters, and ordinary-only queued inputs. No acknowledged handoff or collection can be replayed from the queue. Archives remain bound to both owner and local slot; raw history is preserved, without claiming the current shader advances it.

Both `UnityComputeFireSimulator.CreateFromSnapshot` and `TimberbornComputeFireSimulator.CreateFromSnapshot` construct a **new unpublished simulator** with both buffer sides initialized. A rejected snapshot or failed staged construction cannot mutate an existing simulator. The native factory uses the null external visual surface until a future paired host publication path is ready. There is no in-place complete restore.

Capture rejects reentrant stepping/input mutation and any simulator-wide indeterminate GPU step, including a zero-input simulation/readback/swap failure. A listener exception after successful swap and authority publication retains a committed state; a host input callback failure after authority publication poisons the simulator rather than attempting rollback. A read-only capture failure may be retried; detected GPU/host identity divergence poisons the session.

The legacy cells/transport initialization path is once-only before simulation or pending input, guarded against partial-upload retries. Its capability becomes `LegacyMaterialHistoryUnavailable`: guarded legacy capture remains possible, but complete capture rejects rather than inventing missing hidden fuel or burn history. A faulted simulator cannot bypass this through legacy save/restore. Native WF2 pairing and load activation are separate from this simulator API; no legacy enumeration identities are automatically promoted to native Guid authority.

## Reproducible checks

```sh
dotnet test tests/Wildfire.Core.Tests/Wildfire.Core.Tests.csproj --configuration Release
dotnet test tests/Wildfire.Timberborn.Tests/Wildfire.Timberborn.Tests.csproj --configuration Release
python3 scripts/qa/material-snapshot/run-probe.py --output /tmp/wildfire-material-snapshot-proof
```

The probe requires installed Timberborn assemblies and licensed Unity 6000.3.6f1. It checks for existing Unity/game/Blender processes and holds the shared build lock. It builds the actual production native DLL and loads that unchanged DLL lazily outside Unity's imported asset graph, exercising its real factory and GPU backend through Core interfaces. It installs nothing and never launches/deploys the game. Output records revision/status, DLL/probe/shader hashes, full Unity logs, and serialized snapshots/receipts.

Lazy loading is deliberate: the installed game was built with Unity 6000.5 and its unrelated rendering/UI dependency graph uses APIs absent in this 6000.3 editor (`UnityEngine.MathematicsModule`, `UGUIHelpURL`). Eagerly importing the whole game graph fails before the probe. The harness does not copy incompatible engine DLLs or stub exercised dependencies. The documented `-disable-assembly-updater` option avoids a separately observed API-updater import stall; this is simulator-factory evidence, not whole-mod Editor compatibility.

Actual licensed Metal evidence on Apple M2 Pro:

- Seeded partial/exhausted slots (fuel 3/0, raw history 5/9) are hidden, captured as GPU archives, serialized, disposed, restored into a new native simulator, and re-exposed exactly. Pending water applies once before the marker; stale attempt replay and malformed active-slot aliases reject.
- Separately, an initially hot/dry fuel-3 cell actually burns to zero on tick 1 with an owner-tagged GPU delta. Hide/save/dispose/new-restore/re-exposure retains zero fuel. This is independent of the authored-zero fixture.
- Core tests cover malformed graph/deep-copy boundaries, pending ordering, constructor resource cleanup, reentrant capture, uncertain step failure, restored visual transport, and conservative legacy capability. Native tests retain existing runtime contracts.

The shader emits an existing Metal warning: `There are more uavs (10) than the maximum supported (8)` for `ApplyExternalChanges`. Both this native probe and the earlier 34-case shader suite produce semantically correct readbacks, but the declared resource-limit warning remains an unresolved release concern. This snapshot change does not silence it or claim wider hardware support. No game world/save publication or native registry activation was tested here.
