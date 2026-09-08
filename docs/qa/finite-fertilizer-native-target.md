# Finite fertilizer native target admission

The first bounded CPU Unity attempt passed on immutable `c8b1426dd157616f8ed5aefcacf5d9ae543f2e71`, terminal40843 exit0. Unity6000.3.6f1 ran batchmode/nographics with actual native assemblies and supplied GameObject/component graphs. This deliberately predates the current tree-destination requirement; it isolates target group/range/soil/placement admission.

## Executed boundary

Actual MapSize and TerrainMap/ColumnTerrainMap/TerrainService loading produced native soil ceiling4 in depth33. Actual BlockService, BlockValidator/MatterBelowValidator/StackableBlockService and all nine installed Game-context object validators remained present. GameInitializer retained its default false state; no initialized-state field was forced to pass a predicate.

Actual Entity.PreInitialize→Initialize→PostInitialize and BlockObjectInit drove validation, AddToService/SetObject and finished-state listeners for the supplied EfficientFarmHouse.Folktails and plants. Neither Initialized, AddedToService nor placement coordinates were assigned. The employer's actual BuildingAccessible lifecycle computed access `(1.5,4,1.5)` and enabled its Workplace. PlanterBuilding.Awake derived its allowed group from installed specs; no allowed-template set or range cache was seeded.

| Concrete production `FertilizerJobTarget.TryCreate` case | Accepted |
|---|---:|
| Initialized/growing Carrot `(5,1,4)`, native soil/range | true |
| Initialized/growing/in-range Pine, wrong Farmhouse group | false |
| Placed Carrot `(26,1,4)`, outside native computed distance | false |
| Same valid Carrot after native graph disconnect/invalidation | false |
| Same Carrot after reconnect/invalidation | true |
| Fresh Carrot `(10,1,5)`, unsupported ground | false |

The disconnected retained target also failed `IsCurrent`. A direct native CanPlant assertion established Carrot=true/Pine=false. The fresh invalid placement threw the expected native `Cannot place BlockObject ...` exception before registration; actual `GetObjectsAt` reference checks proved invalid owner absent and valid Carrot still registered. Growth progress stayed0→0.

## Evidence and cleanup

Scratch artifacts: `/tmp/wildfire-fertilizer-stage-b-engine-run/REPORT.md`, `result.json`, `scene-inputs.json`, `unity.log`, `outcome.json`, native hashes, before/final source hashes and process/controller snapshots. Reviewed preparation: `/tmp/wildfire-fertilizer-stage-b-engine-preparation/`.

| Exact artifact | SHA-256 |
|---|---|
| Native DLL | `22c1ae87f08e03c2b35f021c0513a4bf28cc8d1dc9f5dc58888f29bd5f8a621d` |
| Core DLL | `2978033ff79cb386b56abf7785f116c740431af22e4160cd52bb954007934881` |
| Target probe | `b522b60fddb999fea51f3ac98af709cd2eb08d722ac812586aeb78f23242a9b2` |
| Native scene | `f3c979d848a5cb99ede1606238ce0c7e48d46507592de85868681c49cda5551b` |

Before/final fingerprints agree. Cleanup removed actual growth timers, registered blocks and present native component/type registrations, then destroyed5 body graphs/6 root GameObjects with zero errors **before** Editor exit. Registry cleanup queries membership rather than relying on a flag set after Initialize returns. Audited scene services own managed arrays/collections; the run has no Persistent allocator notice. Licensing refresh and shutdown Curl42/thread messages remain in the full log, independently of the passing fixture.

## Remaining acceptance

This closes positive native target admission, with supplied minimal component composition/model children and authored navigation topology. It does not prove full prefab decoration, terrain-generated walkability, adult/employment/district/public-source admission, pickup/travel, current TreeReacher approach, concrete Ready executor through Runtime or GPU ash application. Source Inventory is live identity only; observation arrays supply target dimensions only. Optional reproduction was disabled through actual native Awake, and clock/timer input100/50/.24 was supplied without a real world clock tick.

The pre-tree pin cannot establish current Pine admission; current Carrot logic is unchanged. Editor6000.3.6f1 differs from game6000.5.5. No game, UI, Steam, deployment or Stage C execution occurred. These limits remain separate from [actor persistence/cancel proof](finite-fertilizer-native-persistence.md) and the remaining [finite job acceptance](finite-fertilizer-job.md).
