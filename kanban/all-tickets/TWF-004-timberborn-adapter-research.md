---
ticket: TWF-004
agent_level: High
role: researcher
requires_qa: false
doc_only: true
dependencies:
   - TWF-001
write_scope:
   - kanban/all-tickets/TWF-004-timberborn-adapter-research.md
---

# TWF-004: Research Timberborn Adapter Surface

## Goal

Identify the Timberborn APIs and data sources needed to convert terrain, buildings, resources, and water into packed fire cells without making Timberborn own simulation rules.

## Requirements

- Inspect current Timberborn modding references or installed assemblies available to the repo.
- Identify likely terrain, building, water, overlay, and effect integration points.
- Separate confirmed API evidence from inference.
- Recommend the first narrow Timberborn CPU backend ticket.

## Dependencies

- TWF-001 should define the stable core contract first.

## Role

- Researcher using [../roles/researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check` when the ticket is updated.

## Notes

- This ticket should not implement the adapter.

## Research Findings

Short answer: the first Timberborn adapter should be a read-only CPU snapshot builder that injects Timberborn services, samples terrain/block/water/soil state, and produces `ushort[]` packed cells for `Wildfire.Core`. Timberborn should provide source data and later consume deltas; it should not own rule evaluation or mutate core cells directly.

No public web sources were needed for this pass. Evidence came from the installed Timberborn assemblies and local Timberborn modding references available on this machine.

## Confirmed API Evidence

- Wildfire core contract is already host-agnostic. `src/Wildfire.Core/FireSimContracts.cs` exposes `IFireSimulator`, `FireSimChange`, `CellDelta`, and `Subscribe`; `src/Wildfire.Core/PackedCell.cs` defines the packed fields that the adapter must fill.
- `src/Wildfire.Timberborn/TimberbornFireSystem.cs` is currently only a thin placeholder over `IFireSimulator`. It does not yet include map scanning, entity registration, overlays, or effects.
- Installed Timberborn assemblies are present at `/Users/jasonkleinberg/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/Managed/`.
- Local modding plugin copies are present at `/Users/jasonkleinberg/repos/timberborn-modding/Assets/Plugins/Timberborn/`, including `Timberborn.TerrainSystem.dll`, `Timberborn.BlockSystem.dll`, `Timberborn.WaterSystem.dll`, `Timberborn.SoilMoistureSystem.dll`, `Timberborn.MapIndexSystem.dll`, `Timberborn.SingletonSystem.dll`, `Timberborn.TerrainSystemRendering.dll`, `Timberborn.Particles.dll`, `Timberborn.NaturalResources.dll`, and `Timberborn.Growing.dll`.
- Terrain sampling is confirmed through `Timberborn.TerrainSystem.ITerrainService`. `monodis --method Timberborn.TerrainSystem.dll` shows `get_Size`, `GetTerrainHeight`, `TryGetRelativeHeight`, `GetTerrainHeightBelow`, `GetAllHeightsInCell`, `UnsafeCellIsTerrain`, `CellIsField`, `CellIsCutout`, `Underground`, `OnGround`, `Contains`, `GetColumnCount`, `GetColumnFloor`, and `GetColumnCeiling`.
- Terrain change notifications are confirmed on `ITerrainService`: `PreTerrainHeightChanged`, `TerrainHeightChanged`, `MinMaxTerrainHeightChanged`, and `FieldOrCutoutChanged`.
- Lower-level terrain column data is confirmed through `IThreadSafeColumnTerrainMap`: `ColumnCounts`, `TerrainColumns`, `GetColumnCount`, `GetColumnCeiling`, `GetColumnFloor`, plus column movement/reset events. This is likely useful later for efficient resync, but the first adapter ticket can stay with `ITerrainService`.
- Block/building occupancy is confirmed through `Timberborn.BlockSystem.IBlockService`. With `MONO_PATH` pointed at Timberborn managed assemblies, `monodis --method Timberborn.BlockSystem.dll` shows `get_Size`, `AnyObjectAt`, `AnyTopObjectAt`, `GetObjectsAt`, `GetStackedObjectsAt`, `GetStackedObjectsWithUndergroundAt`, `GetObjectsWithComponentAt<T>`, `GetFirstObjectWithComponentAt<T>`, `GetBottomObjectAt`, `GetPathObjectAt`, `GetMiddleObjectComponentAt<T>`, `GetTopObjectComponentAt<T>`, `GetEntrancesAt`, and `Contains`.
- Block lifecycle hooks exist in `Timberborn.BlockSystem`: `BlockObjectSetEvent`, `BlockObjectUnsetEvent`, `IFinishedStateListener`, `IPostPlacementChangeListener`, and `IPrePlacementChangeListener`. These are good later integration points for incremental resync after the first full snapshot path works.
- Water depth is confirmed through `Timberborn.WaterSystem.IThreadSafeWaterMap`. `monodis --method Timberborn.WaterSystem.dll` shows `ColumnCount`, `ColumnFloor`, `ColumnCeiling`, `WaterDepth`, `ColumnCounts`, `WaterColumns`, `AnyColumnChanged`, and `MaxWaterColumnCountChanged`.
- Read-only water columns are confirmed through `ReadOnlyWaterColumn.WaterDepth` and `ReadOnlyWaterColumn.Contamination`. For the packed cell `water` field, `IThreadSafeWaterMap.WaterDepth(index3D)` is the narrowest starting API.
- Soil moisture is confirmed through `Timberborn.SoilMoistureSystem.ISoilMoistureService.SoilMoisture(int index)`. Use `MapIndexService.CoordinatesToIndex3D` before calling it.
- Existing Prometheus code proves these services can be injected together. `/Users/jasonkleinberg/repos/Timberborn-Prometheus/Assets/Mods/Prometheus/Scripts/Core/TimberbornEnvironmentAdapter.cs` injects `ITerrainService`, `IBlockService`, `IThreadSafeWaterMap`, `ISoilMoistureService`, and `MapIndexService`, then samples terrain height, block occupancy, water depth, and soil moisture.
- Prometheus also proves a guard that Wildfire should preserve: only sample soil moisture for valid terrain cells at or below terrain top. Its `TrySampleSoilMoisture` calls `FireGridEnvironmentSampler.ShouldSampleSoilMoisture(...)` before `CoordinatesToIndex3D` and `SoilMoisture`.
- Building fuel evidence is available in Prometheus. `/Users/jasonkleinberg/repos/Timberborn-Prometheus/Assets/Mods/Prometheus/Scripts/Fire/Exposure/FireExposureController.cs` reads `Building.Spec.BuildingCost` and maps material ids/amounts to fire fuel quanta.
- Natural resource/crop surfaces are visible in assemblies. `Timberborn.NaturalResources.dll` exposes `NaturalResource`, `NaturalResourceSpec`, and `NaturalResourcePlantedEvent`; `Timberborn.Growing.dll` exposes `Growable` and `GrowableSpec`.
- Singleton lifecycle integration is confirmed through `Timberborn.SingletonSystem`: `ILoadableSingleton`, `IPostLoadableSingleton`, `IUnloadableSingleton`, `IUpdatableSingleton`, `ILateUpdatableSingleton`, and `OnEventAttribute`. Prometheus uses these through `PrometheusConfigurator` to register world-ready updatable and load/unload providers.
- Effect candidates are confirmed through assemblies and Prometheus. `Timberborn.Particles.dll` exposes particle runtime types such as `ParticlesCache`, `ParticlesRunner`, and `ParticlesRunnerCreator`; Prometheus currently discovers native particle prefabs with `Resources.LoadAll<GameObject>` and `Resources.FindObjectsOfTypeAll<ParticleSystem>` in `FireNativeParticleSourceCatalog`.
- Native effect names used by Prometheus include `FoodFactorySmoke`, `SmelterSmoke`, `CampfireFire`, `SteamEngineSmoke`, and `Sparks_Trail`. These are evidence that native Timberborn particle prefabs can be reused, but not yet evidence of a stable official fire-specific effect API.
- Overlay rendering has two plausible evidence paths. `Timberborn.TerrainSystemRendering.dll` exposes `TerrainHighlightingService`, `TerrainMaterialMap`, `TerrainLayerSliceUpdater`, and terrain mesh services. Prometheus also proves a fallback custom overlay path by creating a Unity `GameObject` with `Mesh`, `MeshRenderer`, transparent `Material`, and per-cell `Texture2D` mask in `FireBurnedGroundAshDepositMarkerSpawner`.

## Inference And Design Guidance

- Terrain cells should be packed as `terrain=1` only where Timberborn reports solid terrain or a relevant block/building object. Air/empty cells should remain low or zero fuel, low heat, and `terrain=0`.
- Water should initially map from `IThreadSafeWaterMap.WaterDepth(...)` into the 0-3 packed `water` field. Standing water should suppress or mark cells wet, but the core should decide how water changes heat and ignition.
- Soil moisture should feed the packed `water` field or initial heat loss only after the adapter has proved valid terrain indexing. Do not call `ISoilMoistureService.SoilMoisture` for arbitrary air/water/block cells.
- Buildings should start with `IBlockService.AnyObjectAt`/`AnyTopObjectAt` for occupancy and then graduate to `GetObjectsAt` or `GetObjectsWithComponentAt<Building>` to classify building fuel. The first ticket does not need complete material-aware fuel, but it should leave a metadata seam for it.
- Resources and crops probably need entity/component scanning rather than terrain scanning alone. Likely inputs are `NaturalResource`, `Growable`, and existing `BlockObject` placement/occupancy, but exact fuel mapping should be a second ticket after a terrain/block/water snapshot exists.
- Overlay/effect consumers should subscribe to core deltas rather than inspect or mutate core buffers. Timberborn can maintain a separate adapter-owned projection for visual intensity, pooled particles, and overlays.
- Do not create one Timberborn entity per fire cell. For CPU backend Phase 3, use sparse delta consumers and pooled or batched visuals. The Prometheus ash overlay is useful as proof of possibility, not the desired first production shape.
- Prefer a deterministic adapter-unit test surface before live Timberborn QA: wrap sampled Timberborn data behind a small adapter-owned sampler interface, test the packing rules against fake terrain/block/water/soil inputs, and only then bind the implementation to Timberborn services.

## Recommended First Timberborn CPU Backend Ticket

Create a narrow ticket named "Build Timberborn Packed Cell Snapshot Sampler".

Scope:

- Add a `Wildfire.Timberborn` snapshot sampler that reads injected Timberborn services and builds an initial `ushort[]` for `CpuFireSimulator`.
- Inputs: map dimensions, `ITerrainService`, `IBlockService`, `IThreadSafeWaterMap`, `ISoilMoistureService`, and `MapIndexService`.
- Output: packed cells only; no ticking ownership, no deltas, no overlays, no damage, no particle effects.
- Include adapter-local tests using fake sampler data for terrain height, block occupancy, water depth, and guarded soil moisture.
- Acceptance should prove `Wildfire.Core` remains dependency-free and Timberborn only translates host state into packed cells.

Suggested initial packing behavior:

- Terrain or block present: `terrain=1`.
- Empty air: `terrain=0`, `fuel=0`, `flammability=0`.
- Terrain surface: low-to-medium `fuel`, low `flammability`, `heatLoss` from terrain/moisture defaults.
- Block/building: `fuel` present, `flammability` defaulted until material classification lands.
- Water depth above threshold: `water=1..3`, reduced or zero flammability.
- Soil moisture, only when valid: quantize into `water=0..3` or adjust `heatLoss`, but keep the rule in adapter packing rather than simulation stepping.

## Confidence

Medium-high for the terrain, block, water, soil moisture, map-index, and singleton lifecycle APIs because they are confirmed by installed assemblies and existing Prometheus injection code.

Medium for building/resource fuel classification because `Building.Spec.BuildingCost`, `NaturalResource`, and `Growable` are visible, but Wildfire still needs a deliberate first-pass mapping policy.

Medium-low for final overlay/effect API shape because native particle reuse and custom mesh overlays are proven, but a scalable Wildfire visual path should be designed after the CPU snapshot and delta contract exist.

## Verification

- Research verification 2026-04-30: `git diff --check` passed.
