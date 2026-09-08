# Native Warden supply and first response boundary

Actual macOS game evidence on 2026-09-08, exact production `2fd29afed9171648140afc6b0caebff9e30aa9d9`. This follows the [doorstep placement proof](warden-doorstep-placement-live.md). Native staffing/hauling now passes; the first response stops at a new path-start exception. No suppression or wearable fit is claimed.

## Build and safe world

Normal Release build plus all four licensed Unity bundles; all 40 copied files matched their staged hashes (41 installed including the generated manifest). Native DLL SHA256 `6b4141d2a7b8b86e2b6fe53d86499cfcad5ee2d4779d9d81fbba27d91175685f`; Core `4549a3bebeac2d593b73d84703eb67fba3f61978f03af05f118f15cf894b4854`. Actual Timberborn 1.1.2.4 / Unity 6000.5.5f1, launched once as PID25121 with process-only `--wildfire-enable-qa-mutations`; fresh bridge reported development access. Saved Steam options and other mod selections remained intact.

The disposable Iron Teeth Cliffside Easy copy has 9 adults and 4 kits. Both original healthy and placed/supplied derivatives are preserved. No original save was overwritten. Native paths, worker assignment and hauling supplied 20 Water to the original station at `(29,31,4)` and a second station at `(24,14,4)` near existing ground-level Pines. No forced inventory, new trees, or artificial fuel was used.

- Original station `7f7df8f2-00d6-43eb-93bf-74030fa53399`, worker Veakuajay `2507fdb2-6c48-46f4-9491-a5b59edbba3a`: actual station inventory 20, private charge 0, Idle.
- Second station `880ae291-2db8-4f5d-960c-415ed3a2add9`, worker Khemruy `e11a2866-29f3-40f5-96e5-e0df32ff3097`: same actual 20/0 state. Native exterior entrance `(25,17,4)` connected through ordinary paths. The original station is outside the selected target's range20.
- Preserved pre-stimulus `Response supplied 2fd29af.timber`: 150698 bytes, SHA256 `2b19c1310df22166a635f867822158c63273eff74ad1d7514e0bc4f808ce8588`. Both stations supplied, all13 actors alive, no queued stimulus. Native screenshots show worker1/1 and station20/20.

## Controlled fire and first failure

While paused, the actual selected Pine was `(27,10,4)`, index21027, imported TargetId160. `qa-delta-stimulus selected-tree` confirmed heat15 and queued18 existing imported burnable cells in its bounded ignition cluster at tick188. This command registers sustained heat, not additional fuel. The nearest native actor at the saved boundary was about12.79 tiles from that target; none was inside the planned cluster. One ordinary-speed UI action resumed simulation. GPU dispatch and native consumers completed ticks189–192; exposure sampling still found13 beavers and zero exposed actors at tick192.

The first uncaught exception at `2026-09-08 00:16:34Z` was `NullReferenceException` in `WardenExecutor.OnStartedNewPath`, exact native log offset `[0x00031]`, through `Walker.FindPath` → `Walker.GoTo` → `WalkToPositionExecutor.Launch` → `WardenExecutor.LaunchWalk` → `TryWalkToStation` → `TryLaunch`. The ticking actor was Khemruy. The UI's first stack omits offsets; the second Player log stack includes them.

The native error save establishes a precise boundary: Khemruy's `GoodReserver.StockReservation` references the second station's `Wildfire.WardenStation` inventory, Water1, `FixedAmount=true`, `ConsumeGood=false`. The station still contains Water20. Khemruy is alive and assigned to that station, at Unity position `(27.470787,4,32.29419)`. No Warden equipment/executor component was serialized on this error actor. No phase log beyond prior Idle appears. Thus reservation and attempted travel are proven; physical charge uptake, arrival, committed water application, return and active helmet rendering remain unproven.

The game unloaded its world at tick192 and created its normal error report. The controller saved the first evidence and used the observed Error scene Exit button; PID25121 terminated. No unchanged retry followed.

## Evidence

`/tmp/wildfire-warden-staffing-live/` contains build/deploy logs, recursive installed hashes, previous deployment backup, launch arguments, copied saves and:

- `native-water-fetching.jpg`, `native-station-full-water.jpg`, `second-station-full-water.jpg`;
- `selected-response-pine-before-stimulus.jpg`, `selected-tree-stimulus.log`, `response-actor-positions.json`;
- `first-response-error-screen.jpg`, `Player-first-response-failure.log`, `first-response-stack.txt`;
- extracted native actor/station JSON and error save under `response-error-report/`.

Original error ZIP `error-report-2026-09-08-00h16m37s.zip`: 278675 bytes, SHA256 `ea74b2fcf0059f9e2df5141aa21ee6726900b21d7788ed8feb73f614315b3139`.

Desktop mouse actions used the installed `cliclick` fallback, measured from native CoreGraphics window bounds and the observed screenshot. CUA's coordinate hit-test failure was not a permission denial. Native game window was1920×1080 at0,0; auxiliary negative-Y windows changed the screenshot union and were accounted for. No game-code input transport or duplicate process was introduced.
