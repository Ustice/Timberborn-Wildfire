# Timberborn Native API Reference

This document maps the Timberborn native managed-code footprint that Wildfire can reasonably use from mods: public interfaces and services first, then publicish/internal systems that are reachable by reflection, generic type-name lookup, resource scanning, or Unity component traversal.

Evidence source: local Steam install at `~/Library/Application Support/Steam/steamapps/common/Timberborn/Timberborn.app/Contents/Resources/Data/Managed`, reflected on 2026-05-28 with .NET `MetadataLoadContext`. The scan loaded 490 Timberborn assemblies and found 6634 Timberborn types: 2384 public types and 1038 internal/public-member service-like types. Treat this as version-specific; re-scan after Timberborn updates.

## Mental Model

Timberborn is organized around small assemblies named after gameplay or UI domains. Runtime composition is mostly dependency-injected singletons and entity components. A mod normally hooks in by adding a configurator, registering services/singletons/fragments/tools, and receiving native services through constructors.

The safest access order is:

1.   Public interface or service injected by Timberborn.
2.   Public component found on a selected or looked-up entity.
3.   Public generic lookup where the generic type is known at compile time.
4.   Public generic lookup where the type is resolved by full name and invoked by reflection.
5.   Internal component/service invoked by reflection.
6.   Private field/method reflection.
7.   Raw Unity object mutation.

For Wildfire, stop before step 7. Use native ownership APIs such as `EntityService.Delete(...)`, `IBlockService`, `ITerrainService`, `IWaterService`, and native lifecycle components whenever possible.

## System Map

| Domain | Main assemblies | How it relates |
| --- | --- | --- |
| Composition and lifecycle | `Timberborn.SingletonSystem`, `Timberborn.TickSystem`, `Timberborn.WorldPersistence`, `Timberborn.TemplateSystem` | Bindito-style configurators wire services; singleton marker interfaces drive load/update/tick/save order. |
| World grid | `Timberborn.BlockSystem`, `Timberborn.TerrainSystem`, `Timberborn.TerrainPhysics`, `Timberborn.LevelVisibilitySystem` | Block objects sit on terrain columns; terrain/cutout/field events are the main invalidation signals. |
| Water and weather | `Timberborn.WaterSystem`, `Timberborn.WaterObjects`, `Timberborn.WaterSourceSystem`, `Timberborn.HazardousWeatherSystem`, `Timberborn.WeatherSystem` | Water has mutable service APIs and thread-safe read maps; hazardous weather and cycles are singleton-backed game state. |
| Navigation and districts | `Timberborn.Navigation`, `Timberborn.PathSystem`, `Timberborn.GameDistricts`, `Timberborn.BuildingsNavigation` | Nav mesh, roads, districts, construction reachability, and pathfinding are separate but event-coupled services. |
| Entities and blueprints | `Timberborn.EntitySystem`, `Timberborn.BlueprintSystem`, `Timberborn.TemplateInstantiation`, `Timberborn.TemplateCollectionSystem` | Blueprint specs instantiate entity components; entity deletion should go through `EntityService`, not Unity destroy. |
| Goods and work | `Timberborn.Goods`, `Timberborn.InventorySystem`, `Timberborn.GoodStackSystem`, `Timberborn.WorkSystem`, `Timberborn.Workshops`, `Timberborn.Carrying` | Goods specs feed inventories, stacks, workshops, hauling, and worker behavior. |
| Natural resources | `Timberborn.NaturalResources`, `Timberborn.Growing`, `Timberborn.Gathering`, `Timberborn.Yielding`, `Timberborn.NaturalResourcesLifecycle`, `Timberborn.NaturalResourcesMoisture`, `Timberborn.NaturalResourcesContamination` | Crops/trees combine growth, gatherability, yield, moisture, contamination, and model refresh components. |
| Construction and buildings | `Timberborn.Buildings`, `Timberborn.ConstructionSites`, `Timberborn.RecoverableGoodSystem`, `Timberborn.BuildingStatuses`, `Timberborn.Demolishing` | Finished/unfinished state, construction progress, recoverable goods, status icons, and demolition are distinct systems. |
| UI and tools | `Timberborn.CoreUI`, `Timberborn.EntityPanelSystem`, `Timberborn.ToolSystem`, `Timberborn.ToolButtonSystem`, `Timberborn.BottomBarSystem`, `Timberborn.*UI` | Tools are singleton input processors/descriptors; entity panel fragments are pluggable UI units. |
| Automation and HTTP | `Timberborn.Automation`, `Timberborn.AutomationBuildings`, `Timberborn.HttpApiSystem` | Automation samples/commits through singleton contracts; HTTP endpoints/page sections are injectable extension points. |
| Rendering and assets | `Timberborn.Rendering`, `Timberborn.Timbermesh`, `Timberborn.TimbermeshAnimations`, `Timberborn.Particles`, `Timberborn.AssetSystem` | Models, particles, animation controllers, materials, and loaded assets are reachable through public interfaces and resource lookup. |
| Meta systems | `Timberborn.GameSave*`, `Timberborn.SettingsSystem`, `Timberborn.Modding`, `Timberborn.Localization`, `Timberborn.FileSystem`, `Timberborn.Debugging*` | Save/load, settings, localization, file access, mod state, console/debug panels, and metrics form the app shell. |

## Composition And Lifecycle

`Timberborn.SingletonSystem` is the spine. Singleton interfaces are not singletons in the global-static sense; they are managed objects registered into Timberborn's lifecycle and called by repository/loader/tick services. Entity components use separate entity lifecycle interfaces under `Timberborn.EntitySystem` and `Timberborn.WorldPersistence`.

| Contract | Kind | Public members |
| --- | --- | --- |
| `Timberborn.SingletonSystem.EventBus` | class | `Void Post(Object eventObject)`<br>`Void PostLoad()`<br>`Void Register(Object subscriber)`<br>`Void Unregister(Common.ReadOnlyList<Object> subscribers)`<br>`Void Unregister(Object subscriber)` |
| `Timberborn.SingletonSystem.ILateUpdatableSingleton` | interface | `Void LateUpdateSingleton()` |
| `Timberborn.SingletonSystem.ILoadableSingleton` | interface | `Void Load()` |
| `Timberborn.SingletonSystem.INonSingletonLoader` | interface | `Void LoadNonSingletons()` |
| `Timberborn.SingletonSystem.INonSingletonPostLoader` | interface | `Void PostLoadNonSingletons()` |
| `Timberborn.SingletonSystem.IPostLoadableSingleton` | interface | `Void PostLoad()` |
| `Timberborn.SingletonSystem.ISingletonRepository` | interface | `Collections.Generic.IEnumerable<T> GetSingletons<T>()` |
| `Timberborn.SingletonSystem.IUnloadableSingleton` | interface | `Void Unload()` |
| `Timberborn.SingletonSystem.IUpdatableSingleton` | interface | `Void UpdateSingleton()` |
| `Timberborn.SingletonSystem.OnEventAttribute` | class | - |
| `Timberborn.SingletonSystem.OrderingAttribute` | class | - |
| `Timberborn.SingletonSystem.SingletonAttribute` | class | - |

High-signal singleton classes and publicish singleton implementations:

| Type | Visibility | Assembly | Lifecycle / extension contracts |
| --- | --- | --- | --- |
| `Timberborn.Achievements.BadtideStreakAchievement` | publicish | Timberborn.Achievements | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Achievements.BuildEveryStructureAchievement` | publicish | Timberborn.Achievements | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IPostLoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Achievements.ExplodeDynamiteInSingleDayAchievement` | publicish | Timberborn.Achievements | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Achievements.ProducePlanksInDayAchievement` | publicish | Timberborn.Achievements | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Achievements.ReachPopulationWithoutDwellingsAchievement` | publicish | Timberborn.Achievements | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Achievements.TreePlantingCounter` | publicish | Timberborn.Achievements | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Achievements.WorkAllDayForWeekAchievement` | publicish | Timberborn.Achievements | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.AchievementSystem.AchievementService` | publicish | Timberborn.AchievementSystem | `Timberborn.SingletonSystem.IPostLoadableSingleton` |
| `Timberborn.AlertPanelSystem.AlertPanel` | publicish | Timberborn.AlertPanelSystem | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.AnalyticsUI.AnalyticsConsentBox` | public | Timberborn.AnalyticsUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AreaSelectionSystemUI.MeasurableAreaDrawer` | public | Timberborn.AreaSelectionSystemUI | `Timberborn.SingletonSystem.ILateUpdatableSingleton`<br>`Timberborn.ToolPanelSystem.IToolFragment` |
| `Timberborn.AutomationBuildings.SamplingPopulationService` | publicish | Timberborn.AutomationBuildings | `Timberborn.Automation.ISamplingSingleton` |
| `Timberborn.AutomationBuildings.SamplingResourcesService` | publicish | Timberborn.AutomationBuildings | `Timberborn.Automation.ISamplingSingleton`<br>`Timberborn.SingletonSystem.IPostLoadableSingleton` |
| `Timberborn.AutomationBuildings.SpeakerSoundService` | public | Timberborn.AutomationBuildings | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutomationBuildings.SpringReturnService` | publicish | Timberborn.AutomationBuildings | `Timberborn.Automation.ICommittingSingleton` |
| `Timberborn.AutomationBuildingsUI.MemoryModeDescriptions` | publicish | Timberborn.AutomationBuildingsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutomationBuildingsUI.NumericComparisonModeDropdownFactory` | public | Timberborn.AutomationBuildingsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutomationBuildingsUI.PinnedIndicatorsPanel` | public | Timberborn.AutomationBuildingsUI | `Timberborn.SingletonSystem.IPostLoadableSingleton` |
| `Timberborn.AutomationBuildingsUI.PinnedLeversPanel` | publicish | Timberborn.AutomationBuildingsUI | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.IPostLoadableSingleton` |
| `Timberborn.AutomationBuildingsUI.RelayModeDescriptions` | publicish | Timberborn.AutomationBuildingsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutomationBuildingsUI.SpeakerSoundDropdownProvider` | publicish | Timberborn.AutomationBuildingsUI | `Timberborn.DropdownSystem.IDropdownProvider`<br>`Timberborn.DropdownSystem.IExtendedDropdownProvider`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutomationBuildingsUI.TimerModeDescriptions` | publicish | Timberborn.AutomationBuildingsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutomationUI.AutomationDebuggingPanel` | publicish | Timberborn.AutomationUI | `Timberborn.DebuggingUI.IDebuggingPanel`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutomationUI.TransmitterPickerToolHighlighter` | publicish | Timberborn.AutomationUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutosavingUI.AutosaveNotifier` | publicish | Timberborn.AutosavingUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutosavingUI.PanelAutosaveBlocker` | publicish | Timberborn.AutosavingUI | `Timberborn.Autosaving.IAutosaveBlocker`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.AutosavingUI.SettingsAutosaveBlocker` | publicish | Timberborn.AutosavingUI | `Timberborn.Autosaving.IAutosaveBlocker`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BatchControl.DistrictDropdownProvider` | publicish | Timberborn.BatchControl | `Timberborn.DropdownSystem.IDropdownProvider`<br>`Timberborn.DropdownSystem.IExtendedDropdownProvider`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BeaverContaminationSystem.GlobalBeaverContaminationStatisticsProvider` | public | Timberborn.BeaverContaminationSystem | `Timberborn.PopulationStatisticsSystem.IContaminationStatisticsProvider`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.Beavers.BeaverNameService` | publicish | Timberborn.Beavers | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Benchmarking.BenchmarkPanel` | publicish | Timberborn.Benchmarking | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.BenchmarkingUI.BenchmarkDebuggingPanel` | publicish | Timberborn.BenchmarkingUI | `Timberborn.DebuggingUI.IDebuggingPanel`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BlockObjectTools.BlockObjectToolGroupSpecService` | public | Timberborn.BlockObjectTools | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BlockObjectToolsUI.BlockObjectToolWarningPanel` | publicish | Timberborn.BlockObjectToolsUI | `Timberborn.SingletonSystem.IUpdatableSingleton`<br>`Timberborn.ToolPanelSystem.IToolFragment` |
| `Timberborn.BlockSystem.BlockService` | publicish | Timberborn.BlockSystem | `Timberborn.BlockSystem.IBlockService`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BlockSystem.PreviewBlockService` | public | Timberborn.BlockSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BlockSystemUI.BlockObjectBoundsDrawerFactory` | public | Timberborn.BlockSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BlueprintSystem.SpecService` | publicish | Timberborn.BlueprintSystem | `Timberborn.BlueprintSystem.ISpecService`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BonusSystem.BonusTypeSpecService` | public | Timberborn.BonusSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.Bots.BotPopulation` | public | Timberborn.Bots | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.BottomBarSystem.BottomBarPanel` | publicish | Timberborn.BottomBarSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BuilderPrioritySystemUI.BuilderPrioritizableHighlighter` | publicish | Timberborn.BuilderPrioritySystemUI | `Timberborn.SingletonSystem.IPostLoadableSingleton` |
| `Timberborn.Buildings.BuildingService` | public | Timberborn.Buildings | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BuildingsNavigation.BoundsNavRangeDrawingService` | publicish | Timberborn.BuildingsNavigation | `Timberborn.Navigation.ISingletonPreviewNavMeshListener`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.BuildingsUI.AccessibleDebugger` | public | Timberborn.BuildingsUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.CameraSystem.CameraService` | public | Timberborn.CameraSystem | `Timberborn.SingletonSystem.ILateUpdatableSingleton`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.CameraSystem.CameraStateRestorer` | publicish | Timberborn.CameraSystem | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Console.ConsolePanel` | publicish | Timberborn.Console | `Timberborn.Console.IConsolePanel`<br>`Timberborn.InputSystem.IPriorityInputProcessor`<br>`Timberborn.SingletonSystem.ILateUpdatableSingleton`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUnloadableSingleton` |
| `Timberborn.ConstructionGuidelines.ConstructionGuidelinesRenderingService` | public | Timberborn.ConstructionGuidelines | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILateUpdatableSingleton`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.ConstructionGuidelinesUI.ConstructionGuidelinesTogglePanel` | publicish | Timberborn.ConstructionGuidelinesUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.ConstructionGuidelinesUI.ConstructionModeGuidelinesShower` | public | Timberborn.ConstructionGuidelinesUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.ConstructionMode.ConstructionModeService` | public | Timberborn.ConstructionMode | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.ConstructionSitesUI.ConstructionSitePanelDescriptionUpdater` | public | Timberborn.ConstructionSitesUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.CoreUI.DelayedButtonEnabler` | public | Timberborn.CoreUI | `Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.CoreUI.UIScaler` | public | Timberborn.CoreUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUnloadableSingleton` |
| `Timberborn.CoreUI.Underlay` | public | Timberborn.CoreUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.CursorToolSystem.CursorDebuggingPanel` | publicish | Timberborn.CursorToolSystem | `Timberborn.DebuggingUI.IDebuggingPanel`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.DebuggingUI.DebuggingPanel` | public | Timberborn.DebuggingUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.DebuggingUI.DevPanel` | public | Timberborn.DebuggingUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.DebuggingUI.ObjectDebuggingPanel` | publicish | Timberborn.DebuggingUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.DebuggingUI.ObjectViewer` | publicish | Timberborn.DebuggingUI | `Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.DecalSystem.DecalService` | publicish | Timberborn.DecalSystem | `Timberborn.DecalSystem.IDecalService`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.DecalSystem.UserDecalTextureRepository` | public | Timberborn.DecalSystem | `Timberborn.SingletonSystem.IUnloadableSingleton` |
| `Timberborn.DeconstructionSystemUI.BuildingDeconstructionTool` | public | Timberborn.DeconstructionSystemUI | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.DeconstructionSystemUI.DeconstructionSoundPlayer` | publicish | Timberborn.DeconstructionSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.DemolishingUI.DemolishableMarkerService` | publicish | Timberborn.DemolishingUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.DemolishingUI.DemolishableSelectionTool` | public | Timberborn.DemolishingUI | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.DemolishingUI.DemolishableUnselectionTool` | public | Timberborn.DemolishingUI | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.DiagnosticsUI.FramesPerSecondPanel` | public | Timberborn.DiagnosticsUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.DiagnosticsUI.MeshMetricsDebuggingPanel` | public | Timberborn.DiagnosticsUI | `Timberborn.DebuggingUI.IDebuggingPanel`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.DuplicationSystemUI.DuplicateSettingsTool` | publicish | Timberborn.DuplicationSystemUI | `Timberborn.ConstructionMode.IConstructionModeEnabler`<br>`Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.DuplicationSystemUI.DuplicationInputProcessor` | publicish | Timberborn.DuplicationSystemUI | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUnloadableSingleton` |
| `Timberborn.DwellingSystem.GlobalDwellingStatisticsProvider` | public | Timberborn.DwellingSystem | `Timberborn.PopulationStatisticsSystem.IDwellingStatisticsProvider`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.EntityNaming.NumberedEntityNamerService` | publicish | Timberborn.EntityNaming | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.EntityPanelSystem.EntityBadgeService` | public | Timberborn.EntityPanelSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.EntityPanelSystem.EntityPanel` | publicish | Timberborn.EntityPanelSystem | `Timberborn.EntityPanelSystem.IEntityPanel`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.ErrorReportingUI.CrashBox` | publicish | Timberborn.ErrorReportingUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUnloadableSingleton` |
| `Timberborn.ErrorReportingUI.LoadingIssuePanel` | publicish | Timberborn.ErrorReportingUI | `Timberborn.CoreUI.IPanelBlocker`<br>`Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.Explosions.ExplosionService` | public | Timberborn.Explosions | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.TickSystem.ITickableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Explosions.ExplosionVisualizerService` | publicish | Timberborn.Explosions | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.FactionSystem.FactionSpecService` | public | Timberborn.FactionSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.FactionValidators.FactionSpecValidationService` | publicish | Timberborn.FactionValidators | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.FileBrowsing.DiskSystemEntryElementFactory` | public | Timberborn.FileBrowsing | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.FileBrowsing.FileFilterProvider` | public | Timberborn.FileBrowsing | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.FireworkSystem.FireworkLaunchService` | publicish | Timberborn.FireworkSystem | `Timberborn.Automation.ISamplingSingleton` |
| `Timberborn.FireworkSystem.FireworkSpecService` | public | Timberborn.FireworkSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.Forestry.TreeCuttingArea` | public | Timberborn.Forestry | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IPostLoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.ForestryUI.TreeCuttingAreaSelectionTool` | public | Timberborn.ForestryUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.ForestryUI.TreeCuttingAreaUnselectionTool` | publicish | Timberborn.ForestryUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.ForestryUI.TreeCuttingAreaVisualizer` | public | Timberborn.ForestryUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameCycleSystem.GameCycleService` | public | Timberborn.GameCycleSystem | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.GameDistrictsMigration.ManualMigrationDistrictSetter` | public | Timberborn.GameDistrictsMigration | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IPostLoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.GameDistrictsUI.CitizenNameTintChanger` | publicish | Timberborn.GameDistrictsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameDistrictsUI.DistrictConnectionDrawingService` | publicish | Timberborn.GameDistrictsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameDistrictsUI.DistrictConnectionLineRenderer` | publicish | Timberborn.GameDistrictsUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.GameDistrictsUI.DistrictConnectionLineRotator` | publicish | Timberborn.GameDistrictsUI | `Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.GameDistrictsUI.DistrictPanel` | publicish | Timberborn.GameDistrictsUI | `Timberborn.BatchControl.IHideableByBatchControl`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameFactionSystem.FactionNeedService` | public | Timberborn.GameFactionSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameFactionSystem.FactionService` | public | Timberborn.GameFactionSystem | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.GameFactionSystem.NeedModificationService` | public | Timberborn.GameFactionSystem | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.GameOverUI.GameOverBox` | public | Timberborn.GameOverUI | `Timberborn.CoreUI.IPanelBlocker`<br>`Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameSaveRepositorySystemUI.LoadGameBox` | public | Timberborn.GameSaveRepositorySystemUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameSaveRepositorySystemUI.SaveThumbnailCache` | public | Timberborn.GameSaveRepositorySystemUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUnloadableSingleton` |
| `Timberborn.GameSaveRepositorySystemUI.SaveVersionCompatibilityService` | public | Timberborn.GameSaveRepositorySystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameSaveRuntimeSystemUI.SaveGameBox` | public | Timberborn.GameSaveRuntimeSystemUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameScene.DateSalter` | publicish | Timberborn.GameScene | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.GameScene.GameSceneSerializedWorldSupplier` | public | Timberborn.GameScene | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.INonSingletonPostLoader`<br>`Timberborn.WorldPersistence.ISerializedWorldSupplier` |
| `Timberborn.GameSound.GameUISoundController` | public | Timberborn.GameSound | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameSound.SoundSystemDebuggingPanel` | publicish | Timberborn.GameSound | `Timberborn.DebuggingUI.IDebuggingPanel`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.GameWonderCompletion.MapNameService` | public | Timberborn.GameWonderCompletion | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.GameWonderCompletion.WonderCompletionCountdownStarter` | public | Timberborn.GameWonderCompletion | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.TickSystem.ITickableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.GameWonderCompletionUI.WonderCompletionPanel` | public | Timberborn.GameWonderCompletionUI | `Timberborn.CoreUI.IPanelBlocker`<br>`Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.Goods.GoodService` | publicish | Timberborn.Goods | `Timberborn.Goods.IGoodService`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.Goods.GoodsGroupSpecService` | public | Timberborn.Goods | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.HazardousWeatherSystem.BadtideWeather` | public | Timberborn.HazardousWeatherSystem | `Timberborn.HazardousWeatherSystem.IHazardousWeather`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.HazardousWeatherSystem.DroughtWeather` | public | Timberborn.HazardousWeatherSystem | `Timberborn.HazardousWeatherSystem.IHazardousWeather`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.HazardousWeatherSystem.HazardousWeatherHistory` | public | Timberborn.HazardousWeatherSystem | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.HazardousWeatherSystem.HazardousWeatherService` | public | Timberborn.HazardousWeatherSystem | `Timberborn.GameCycleSystem.ICycleDuration`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.HazardousWeatherSystemUI.HazardousWeatherApproachingTimer` | public | Timberborn.HazardousWeatherSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IPostLoadableSingleton` |
| `Timberborn.HazardousWeatherSystemUI.HazardousWeatherNotificationPanel` | publicish | Timberborn.HazardousWeatherSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.HazardousWeatherSystemUI.HazardousWeatherSoundPlayer` | publicish | Timberborn.HazardousWeatherSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.HazardousWeatherSystemUI.HazardousWeatherUIHelper` | public | Timberborn.HazardousWeatherSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.HttpApiSystem.HttpApi` | public | Timberborn.HttpApiSystem | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUnloadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.Illumination.IlluminationService` | public | Timberborn.Illumination | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.InputSystem.CursorService` | public | Timberborn.InputSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.InputSystem.InputService` | public | Timberborn.InputSystem | `Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.InputSystem.KeywordService` | public | Timberborn.InputSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.InputSystemUI.KeywordMatchNotifier` | publicish | Timberborn.InputSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.InventorySystemUI.ModifyInventoryBox` | publicish | Timberborn.InventorySystemUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.KeyBindingSystem.KeyBindingGroupSpecService` | public | Timberborn.KeyBindingSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.KeyBindingSystem.KeyBindingSpecService` | public | Timberborn.KeyBindingSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.KeyBindingSystemUI.KeyBindingsBox` | public | Timberborn.KeyBindingSystemUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.KeyBindingSystemUI.KeyBindingShortcutUpdater` | public | Timberborn.KeyBindingSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.LanguageUI.ChangeLanguageBox` | public | Timberborn.LanguageUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.LevelVisibilitySystem.LevelVisibilityService` | publicish | Timberborn.LevelVisibilitySystem | `Timberborn.LevelVisibilitySystem.ILevelVisibilityService`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IPostLoadableSingleton` |
| `Timberborn.LevelVisibilitySystemUI.LevelVisibilityDevModule` | publicish | Timberborn.LevelVisibilitySystemUI | `Timberborn.Debugging.IDevModule`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.LevelVisibilitySystemUI.LevelVisibilityPanel` | publicish | Timberborn.LevelVisibilitySystemUI | `Timberborn.LevelVisibilitySystemUI.ILevelVisibilityPanel`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.LevelVisibilitySystemUI.LevelVisibilityPicker` | publicish | Timberborn.LevelVisibilitySystemUI | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.LifeSystem.LifeService` | public | Timberborn.LifeSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.Localization.NewLocalizationService` | publicish | Timberborn.Localization | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MainMenuModdingUI.CreateModBox` | public | Timberborn.MainMenuModdingUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MainMenuModdingUI.ModManagerBox` | public | Timberborn.MainMenuModdingUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.MainMenuModdingUI.ModUploaderBox` | public | Timberborn.MainMenuModdingUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MainMenuPanels.MainMenuPanel` | public | Timberborn.MainMenuPanels | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MainMenuPanels.NewGameFactionPanel` | public | Timberborn.MainMenuPanels | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MainMenuPanels.NewGameMapPanel` | public | Timberborn.MainMenuPanels | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MainMenuPanels.NewGameModePanel` | public | Timberborn.MainMenuPanels | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorBrushesUI.AbsoluteTerrainHeightBrushTool` | public | Timberborn.MapEditorBrushesUI | `Timberborn.Brushes.IBrushWithHeight`<br>`Timberborn.Brushes.IBrushWithShape`<br>`Timberborn.Brushes.IBrushWithSize`<br>`Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.MapEditorConstructionGuidelinesUI.IBrushWithGuidelines`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.MapEditorBrushesUI.RelativeTerrainHeightBrushTool` | public | Timberborn.MapEditorBrushesUI | `Timberborn.Brushes.IBrushWithDirection`<br>`Timberborn.Brushes.IBrushWithHeight`<br>`Timberborn.Brushes.IBrushWithShape`<br>`Timberborn.Brushes.IBrushWithSize`<br>`Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.MapEditorConstructionGuidelinesUI.IBrushWithGuidelines`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.MapEditorBrushesUI.SculptingTerrainBrushTool` | public | Timberborn.MapEditorBrushesUI | `Timberborn.Brushes.IBrushWithDirection`<br>`Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.MapEditorConstructionGuidelinesUI.IBrushWithGuidelines`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.MapEditorBrushesUI.TerrainIntegrityService` | public | Timberborn.MapEditorBrushesUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorConstructionGuidelinesUI.MapEditorGuidelinesShower` | publicish | Timberborn.MapEditorConstructionGuidelinesUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorHazardousWeatherUI.MapEditorHazardousWeatherPanel` | publicish | Timberborn.MapEditorHazardousWeatherUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |
| `Timberborn.MapEditorNaturalResourcesUI.NaturalResourceLayerToggle` | publicish | Timberborn.MapEditorNaturalResourcesUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorNaturalResourcesUI.NaturalResourceRemovalBrushTool` | public | Timberborn.MapEditorNaturalResourcesUI | `Timberborn.Brushes.IBrushWithShape`<br>`Timberborn.Brushes.IBrushWithSize`<br>`Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.MapEditorConstructionGuidelinesUI.IBrushWithGuidelines`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.MapEditorNaturalResourcesUI.NaturalResourceSpawningBrushTool` | public | Timberborn.MapEditorNaturalResourcesUI | `Timberborn.Brushes.IBrushWithShape`<br>`Timberborn.Brushes.IBrushWithSize`<br>`Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.MapEditorConstructionGuidelinesUI.IBrushWithGuidelines`<br>`Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor` |
| `Timberborn.MapEditorPersistenceUI.SaveMapBox` | public | Timberborn.MapEditorPersistenceUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorScene.MapEditorSerializedWorldSupplier` | public | Timberborn.MapEditorScene | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.INonSingletonPostLoader`<br>`Timberborn.WorldPersistence.ISerializedWorldSupplier` |
| `Timberborn.MapEditorSimulationSystemUI.MapEditorSimulationPanel` | public | Timberborn.MapEditorSimulationSystemUI | `Timberborn.InputSystem.IInputProcessor`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorStockpilesUI.FixedStockpileFragment` | publicish | Timberborn.MapEditorStockpilesUI | `Timberborn.EntityPanelSystem.IEntityPanelFragment`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorStockpilesUI.FixedStockpileGoodProvider` | publicish | Timberborn.MapEditorStockpilesUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorUI.FilePanel` | public | Timberborn.MapEditorUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapEditorUI.MapEditorOptionsBox` | publicish | Timberborn.MapEditorUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.Options.IOptionsBox`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapIndexSystem.MapIndexService` | public | Timberborn.MapIndexSystem | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapItemsUI.OfficialMapItemFactory` | public | Timberborn.MapItemsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapItemsUI.UserMapItemFactory` | public | Timberborn.MapItemsUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapMetadataSystemUI.MapMetadataPanel` | publicish | Timberborn.MapMetadataSystemUI | `Timberborn.SingletonSystem.IPostLoadableSingleton`<br>`Timberborn.ToolPanelSystem.IToolFragment` |
| `Timberborn.MapMetadataSystemUI.MapMetadataTool` | public | Timberborn.MapMetadataSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor`<br>`Timberborn.ToolSystemUI.IWaterIgnoringTool` |
| `Timberborn.MapRepositorySystemUI.DevModeMapRepositoryChangeNotifier` | publicish | Timberborn.MapRepositorySystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapRepositorySystemUI.LoadMapBox` | public | Timberborn.MapRepositorySystemUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapRepositorySystemUI.MapVersionCompatibilityService` | public | Timberborn.MapRepositorySystemUI | `Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapRepositorySystemUI.NewMapBox` | public | Timberborn.MapRepositorySystemUI | `Timberborn.CoreUI.IPanelController`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MapStateSystem.MapSize` | public | Timberborn.MapStateSystem | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.MapThumbnailCapturing.MapThumbnailCameraMover` | public | Timberborn.MapThumbnailCapturing | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.WorldPersistence.ISaveableSingleton` |
| `Timberborn.MapThumbnailCapturing.MapThumbnailRenderTextureProvider` | public | Timberborn.MapThumbnailCapturing | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUnloadableSingleton`<br>`Timberborn.ThumbnailCapturing.IThumbnailRenderTextureProvider` |
| `Timberborn.MapThumbnailCapturingUI.ThumbnailCapturingPanel` | publicish | Timberborn.MapThumbnailCapturingUI | `Timberborn.SingletonSystem.IPostLoadableSingleton`<br>`Timberborn.ToolPanelSystem.IToolFragment` |
| `Timberborn.MapThumbnailCapturingUI.ThumbnailCapturingTool` | public | Timberborn.MapThumbnailCapturingUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton`<br>`Timberborn.ToolSystem.ITool`<br>`Timberborn.ToolSystemUI.IToolDescriptor`<br>`Timberborn.ToolSystemUI.IWaterIgnoringTool` |
| `Timberborn.MechanicalSystem.BatteryService` | publicish | Timberborn.MechanicalSystem | `Timberborn.TickSystem.ITickableSingleton` |
| `Timberborn.MechanicalSystemHighlighting.MechanicalGraphHighlightService` | publicish | Timberborn.MechanicalSystemHighlighting | `Timberborn.SingletonSystem.ILateUpdatableSingleton`<br>`Timberborn.SingletonSystem.ILoadableSingleton` |
| `Timberborn.MechanicalSystemUI.MechanicalGraphModelUpdater` | publicish | Timberborn.MechanicalSystemUI | `Timberborn.SingletonSystem.ILoadableSingleton`<br>`Timberborn.SingletonSystem.IUpdatableSingleton` |

## Key API Surfaces

These are the APIs most likely to matter for Wildfire, world consequences, tools, debug panels, and live QA. The member lists are public members only; internal members may exist behind reflection.

| Type | Visibility | Assembly | Public members |
| --- | --- | --- | --- |
| `Timberborn.AutomationBuildings.Detonator` | internal class | Timberborn.AutomationBuildings | `Boolean NeedsAutomatable {get;}`<br>`Void Awake()`<br>`Void Evaluate()`<br>`Void Load(WorldPersistence.IEntityLoader entityLoader)`<br>`Void Save(WorldPersistence.IEntitySaver entitySaver)` |
| `Timberborn.BlockSystem.BlockObject` | public class | Timberborn.BlockSystem | `BlockSystem.Blocks Blocks {get;}`<br>`BlockSystem.EntranceBlockSpec Entrance {get;}`<br>`BlockSystem.PositionedBlocks PositionedBlocks {get; set;}`<br>`BlockSystem.PositionedEntrance PositionedEntrance {get; set;}`<br>`Boolean AboveGround {get; set;}`<br>`Boolean AddedToService {get; set;}`<br>`Boolean GroundOnly {get; set;}`<br>`Boolean HasEntrance {get;}`<br>`Boolean IsFinished {get;}`<br>`Boolean IsPreview {get;}`<br>`Boolean IsUnfinished {get;}`<br>`Boolean Overridable {get; set;}`<br>`Boolean Positioned {get;}`<br>`Boolean Solid {get; set;}`<br>`Coordinates.FlipMode FlipMode {get; set;}`<br>`Coordinates.Orientation Orientation {get; set;}` |
| `Timberborn.BlockSystem.IBlockService` | public interface | Timberborn.BlockSystem | `Vector3Int Size {get;}`<br>`BlockSystem.BlockObject GetBottomObjectAt(Vector3Int coordinates)`<br>`BlockSystem.BlockObject GetPathObjectAt(Vector3Int coordinates)`<br>`BlockSystem.BlockObject GetUndergroundObjectAt(Vector3Int coordinates)`<br>`Boolean AnyNonOverridableObjectBelow(Vector3Int coordinates)`<br>`Boolean AnyNonOverridableObjectsAt(Vector3Int coordinates, BlockSystem.BlockOccupations occupations)`<br>`Boolean AnyObjectAt(Vector3Int coordinates)`<br>`Boolean AnyTopObjectAt(Vector3Int coordinates)`<br>`Boolean BlockNeedsGroundBelow(Vector3Int coordinates)`<br>`Boolean Contains(Vector2Int coordinates)`<br>`Boolean Contains(Vector3Int coordinates)`<br>`Collections.Generic.IEnumerable<BlockSystem.BlockObject> GetStackedObjectsAt(Vector3Int coordinates)`<br>`Collections.Generic.IEnumerable<BlockSystem.BlockObject> GetStackedObjectsWithUndergroundAt(Vector3Int coordinates)`<br>`Collections.Generic.IEnumerable<T> GetObjectsWithComponentAt<T>(Vector3Int coordinates)`<br>`Common.ReadOnlyList<BlockSystem.BlockObject> GetObjectsAt(Vector3Int coordinates)`<br>`Coordinates.Directions2D GetEntrancesAt(Vector3Int coordinates)` |
| `Timberborn.ConstructionSites.ConstructionSite` | public class | Timberborn.ConstructionSites | `Boolean HasFreeSpots {get;}`<br>`Boolean HasMaterialsToResumeBuilding {get;}`<br>`Boolean IsOn {get;}`<br>`Boolean IsReadyToFinish {get;}`<br>`Boolean ReadyToBuild {get;}`<br>`Boolean WasStarted {get;}`<br>`InventorySystem.Inventory Inventory {get; set;}`<br>`Single BuildTimeProgress {get;}`<br>`Single BuildTimeProgressInHours {get; set;}`<br>`Single MaterialProgress {get;}`<br>`Void ActivateLackOfResourcesStatus()`<br>`Void Awake()`<br>`Void DeactivateLackOfResourcesStatus()`<br>`Void FinishNow()`<br>`Void IncreaseBuildTime(Single hours)`<br>`Void InitializeEntity()` |
| `Timberborn.EntityPanelSystem.IEntityPanelFragment` | public interface | Timberborn.EntityPanelSystem | `UIElements.VisualElement InitializeFragment()`<br>`Void ClearFragment()`<br>`Void ShowFragment(BaseComponentSystem.BaseComponent entity)`<br>`Void UpdateFragment()` |
| `Timberborn.EntitySystem.EntityService` | public class | Timberborn.EntitySystem | `EntitySystem.EntityComponent Instantiate(BlueprintSystem.Blueprint template)`<br>`EntitySystem.EntityComponent Instantiate(BlueprintSystem.Blueprint template, Guid id)`<br>`Void Delete(BaseComponentSystem.BaseComponent entity)` |
| `Timberborn.Explosions.ExplosionOutcomeGatherer` | public class | Timberborn.Explosions | `Collections.Generic.Dictionary<Int32, Collections.Generic.HashSet<Vector3Int>> GetAffectedTilesPerRadius(Vector3 center, Single radius)`<br>`Void GetAffectedTerrainAndObjects(Common.ReadOnlyHashSet<Vector3Int> affectedTiles, Collections.Generic.HashSet<Vector3Int> affectedTerrain, Collections.Generic.HashSet<BlockSystem.BlockObject> affectedObjects)`<br>`Void GetAllAffectedTerrainAndObjects(Explosions.UnstableCore unstableCore, Collections.Generic.HashSet<Vector3Int> affectedTiles, Collections.Generic.HashSet<Vector3Int> affectedTerrain, Collections.Generic.HashSet<BlockSystem.BlockObject> affectedObjects)` |
| `Timberborn.Explosions.ExplosionService` | public class | Timberborn.Explosions | `Void Explode(Explosions.UnstableCore unstableCore)`<br>`Void Load()`<br>`Void Register(Explosions.UnstableCore unstableCore)`<br>`Void Save(WorldPersistence.ISingletonSaver singletonSaver)`<br>`Void Tick()`<br>`EventHandler<Common.ReadOnlyHashSet<Vector3Int>> TilesExplosion` |
| `Timberborn.Explosions.Tunnel` | internal class | Timberborn.Explosions | `Int32 BottomLevel {get;}`<br>`Boolean RemovesTerrainAt(Vector3Int coordinates)`<br>`Void Awake()`<br>`Void OnEnterFinishedState()`<br>`Void OnExitFinishedState()` |
| `Timberborn.Gathering.Gatherable` | public class | Timberborn.Gathering | `Boolean UsableWithCurrentFeatureToggles {get;}`<br>`Single YieldGrowthTimeInDays {get;}`<br>`Yielding.Yielder Yielder {get; set;}`<br>`Yielding.YielderSpec YielderSpec {get;}`<br>`Void Awake()`<br>`Void DeleteEntity()`<br>`Void PostInitializeEntity()`<br>`Void UpdateModel()`<br>`EventHandler Gathered` |
| `Timberborn.Goods.IGoodService` | public interface | Timberborn.Goods | `Common.ReadOnlyList<String> Goods {get;}`<br>`Boolean HasGood(String id)`<br>`Collections.Generic.IEnumerable<String> GetGoodsForGroup(String groupId)`<br>`Collections.Generic.IEnumerable<String> GetGoodsForType(String goodType)`<br>`Goods.GoodSpec GetGood(String id)`<br>`Goods.GoodSpec GetGoodOrNull(String id)` |
| `Timberborn.GoodStackSystem.IGoodStackService` | public interface | Timberborn.GoodStackSystem | `Common.ReadOnlyList<GoodStackSystem.GoodStack> GoodStacks {get;}` |
| `Timberborn.Growing.Growable` | public class | Timberborn.Growing | `Boolean GrowthInProgress {get;}`<br>`Boolean IsGrown {get;}`<br>`Single GrowthProgress {get;}`<br>`Single GrowthTimeInDays {get;}`<br>`Void Awake()`<br>`Void DeleteEntity()`<br>`Void HideModel()`<br>`Void IncreaseGrowthProgress(Single growthProgress)`<br>`Void InitializeEntity()`<br>`Void Load(WorldPersistence.IEntityLoader entityLoader)`<br>`Void Save(WorldPersistence.IEntitySaver entitySaver)`<br>`Void ShowMatureModel()`<br>`Void ShowSeedlingModel()`<br>`EventHandler HasGrown` |
| `Timberborn.HttpApiSystem.IHttpApiEndpoint` | public interface | Timberborn.HttpApiSystem | `Threading.Tasks.Task<Boolean> TryHandle(Net.HttpListenerContext context)` |
| `Timberborn.InventorySystem.Inventory` | public class | Timberborn.InventorySystem | `Boolean HasAnyUnreservedStock {get;}`<br>`Boolean HasUnwantedStock {get; set;}`<br>`Boolean IsEmpty {get;}`<br>`Boolean IsFull {get;}`<br>`Boolean IsFullyReserved {get;}`<br>`Boolean IsInput {get;}`<br>`Boolean IsOutput {get;}`<br>`Boolean IsUnblocked {get;}`<br>`Boolean PublicInput {get; set;}`<br>`Boolean PublicOutput {get; set;}`<br>`Common.ReadOnlyHashSet<String> InputGoods {get;}`<br>`Common.ReadOnlyHashSet<String> OutputGoods {get;}`<br>`Common.ReadOnlyList<Goods.GoodAmount> Stock {get;}`<br>`Common.ReadOnlyList<Goods.StorableGoodAmount> AllowedGoods {get;}`<br>`Int32 Capacity {get; set;}`<br>`Int32 TotalAmountInStock {get;}` |
| `Timberborn.NaturalResourcesModelSystem.NaturalResourceModel` | public class | Timberborn.NaturalResourcesModelSystem | `Void Awake()`<br>`Void Hide()`<br>`Void PostInitializeEntity()`<br>`Void Show()`<br>`Void Start()`<br>`EventHandler ModelChanged` |
| `Timberborn.Navigation.IDistrictService` | public interface | Timberborn.Navigation | `Boolean DistrictIsGloballyReachable(Navigation.District district, Vector3 start)`<br>`Boolean IsOnDistrictRoad(Navigation.District district, Vector3 road)`<br>`Boolean IsOnInstantDistrictRoad(Navigation.District district, Vector3 road)`<br>`Boolean IsOnInstantDistrictRoadSpill(Navigation.Accessible accessible)`<br>`Boolean IsOnInstantDistrictRoadSpill(Vector3 position)`<br>`Boolean IsOnPreviewDistrictRoad(Navigation.District district, Vector3 road)`<br>`Boolean IsPreviewDistrictInConflict(Nullable<Vector3Int> previewDistrictCenter)`<br>`Navigation.District AddDistrict(Vector3Int centerCoordinates)`<br>`Navigation.District AddPreviewDistrict(Vector3Int centerCoordinates)`<br>`Vector3 GetRandomDestinationInDistrict(Navigation.District district, Vector3 coordinates)`<br>`Void RemoveDistrict(Navigation.District district)`<br>`Void RemovePreviewDistrict(Navigation.District district)`<br>`Void SetObstacle(Vector3Int coordinates)`<br>`Void SetPreviewObstacle(Vector3Int coordinates)`<br>`Void UnsetObstacle(Vector3Int coordinates)`<br>`Void UnsetPreviewObstacle(Vector3Int coordinates)` |
| `Timberborn.Navigation.INavigationService` | public interface | Timberborn.Navigation | `Boolean DestinationIsReachable(Vector3 start, Vector3 end)`<br>`Boolean DestinationIsReachableUnlimitedRange(Vector3 start, Vector3 end)`<br>`Boolean FindInstantRoadPath(Vector3 access, Vector3 end, Single& distance)`<br>`Boolean FindPath(Vector3 start, Vector3 end, Collections.Generic.List<Navigation.PathCorner> pathCorners)`<br>`Boolean FindPathUnlimitedRange(Vector3 start, Collections.Generic.IReadOnlyList<Vector3> ends, Collections.Generic.List<Navigation.PathCorner> pathCorners, Single& distance)`<br>`Boolean FindPathUnlimitedRange(Vector3 start, Vector3 end, Collections.Generic.List<Navigation.PathCorner> pathCorners, Single& distance)`<br>`Boolean FindRoadPath(Vector3 start, Vector3 end, Single& distance)`<br>`Boolean FindRoadSpillOrTerrainPathUnlimitedRange(Vector3 start, Collections.Generic.IReadOnlyList<Vector3> ends, Collections.Generic.List<Navigation.PathCorner> pathCorners, Single& distance)`<br>`Boolean FindRoadToTerrainPath(Vector3 roadStart, Vector3 terrainEnd, Vector3& endOfRoad, Single& distanceFromClosestRoad, Single& totalDistance)`<br>`Boolean FindTerrainPath(Vector3 start, Vector3 end, Single& distance)`<br>`Boolean InStoppingProximity(Vector3 a, Vector3 b)`<br>`Boolean IsOnNavMesh(Vector3 position)`<br>`Nullable<Vector3> ClosestPositionOnNavMesh(Vector3 position, Single maxDistance)`<br>`Single HeuristicDistance(Vector3 start, Vector3 end)` |
| `Timberborn.SingletonSystem.ISingletonRepository` | public interface | Timberborn.SingletonSystem | `Collections.Generic.IEnumerable<T> GetSingletons<T>()` |
| `Timberborn.SoilContaminationSystem.ISoilContaminationService` | public interface | Timberborn.SoilContaminationSystem | `Boolean SoilIsContaminated(Vector3Int coordinates)`<br>`Single Contamination(Int32 index)` |
| `Timberborn.SoilMoistureSystem.ISoilMoistureService` | public interface | Timberborn.SoilMoistureSystem | `Boolean SoilIsMoist(Vector3Int coordinates)`<br>`Single SoilMoisture(Int32 index)` |
| `Timberborn.TerrainSystem.ITerrainService` | public interface | Timberborn.TerrainSystem | `Int32 MaxTerrainHeight {get;}`<br>`Int32 MinTerrainHeight {get;}`<br>`Vector3Int Size {get;}`<br>`Boolean CellIsCutout(Vector3Int cellCoordinates)`<br>`Boolean CellIsField(Vector3Int cellCoordinates)`<br>`Boolean Contains(Vector2Int coordinates)`<br>`Boolean Contains(Vector3Int coordinates)`<br>`Boolean IsVisible(Vector3Int coordinates)`<br>`Boolean OnGround(Vector3Int coords)`<br>`Boolean TryGetDistanceToTerrainAbove(Vector3Int coordinates, Int32& distance)`<br>`Boolean TryGetRelativeHeight(Vector3Int coordinates, Int32& relativeHeight)`<br>`Boolean Underground(Vector3Int coords)`<br>`Boolean UnsafeCellIsTerrain(Int32 index)`<br>`Collections.Generic.IEnumerable<Vector3Int> GetAllHeightsInCell(Vector2Int cellCoordinates)`<br>`Int32 GetColumnCeiling(Int32 index3D)`<br>`Int32 GetColumnCount(Int32 index)` |
| `Timberborn.TickSystem.ITickableSingletonService` | public interface | Timberborn.TickSystem | `Boolean IsStartingParallelTick {get;}`<br>`Boolean ParalleTicklIsFinished {get;}`<br>`TimeSpan LastParallelTickDuration {get;}`<br>`Void ForceFinishParallelTick()`<br>`Void TickAll()`<br>`EventHandler ForcedParallelTickFinished` |
| `Timberborn.TimbermeshAnimations.IAnimatorController` | public interface | Timberborn.TimbermeshAnimations | `Collections.Generic.IEnumerable<String> AnimationNames {get;}`<br>`Boolean HasParameter(String parameterName)`<br>`Void Disable()`<br>`Void Enable()`<br>`Void SetBool(String parameterName, Boolean state)`<br>`Void SetFloat(String parameterName, Single value)` |
| `Timberborn.TimeSystem.IDayNightCycle` | public interface | Timberborn.TimeSystem | `Boolean IsDaytime {get;}`<br>`Boolean IsNighttime {get;}`<br>`Int32 DayNumber {get;}`<br>`Single DayLengthInSeconds {get;}`<br>`Single DayProgress {get;}`<br>`Single DaytimeLengthInHours {get;}`<br>`Single FixedDeltaTimeInHours {get;}`<br>`Single FluidSecondsPassedToday {get;}`<br>`Single HoursPassedToday {get;}`<br>`Single NighttimeLengthInHours {get;}`<br>`Single PartialDayNumber {get;}`<br>`Int32 HoursToTicks(Single hours)`<br>`Single DayNumberHoursFromNow(Single hours)`<br>`Single FluidHoursToNextStartOf(TimeSystem.TimeOfDay timeOfDay)`<br>`Single HoursToNextStartOf(TimeSystem.TimeOfDay timeOfDay)`<br>`Single SecondsToHours(Single seconds)` |
| `Timberborn.ToolButtonSystem.ToolButtonService` | public class | Timberborn.ToolButtonSystem | `Common.ReadOnlyList<ToolButtonSystem.ToolButton> ToolButtons {get;}`<br>`Boolean TryGetNextRootButton(ToolButtonSystem.IToolbarButton& nextButton)`<br>`Boolean TryGetNextToolButton(ToolButtonSystem.IToolbarButton& toolButton)`<br>`Boolean TryGetPreviousRootButton(ToolButtonSystem.IToolbarButton& previousButton)`<br>`Boolean TryGetPreviousToolButton(ToolButtonSystem.IToolbarButton& toolButton)`<br>`ToolButtonSystem.ToolButton GetToolButton<TTool>()`<br>`ToolButtonSystem.ToolButton GetToolButton<TTool>(Predicate<TTool> predicate)`<br>`ToolButtonSystem.ToolGroupButton GetToolGroupButton(ToolButtonSystem.ToolButton toolButton)`<br>`Void Add(ToolButtonSystem.ToolButton toolButton)`<br>`Void Add(ToolButtonSystem.ToolGroupButton toolButton)`<br>`Void Load()`<br>`Void PostLoad()` |
| `Timberborn.ToolSystem.ITool` | public interface | Timberborn.ToolSystem | `Void Enter()`<br>`Void Exit()` |
| `Timberborn.WaterSystem.IThreadSafeWaterMap` | public interface | Timberborn.WaterSystem | `Boolean AnyColumnChanged {get;}`<br>`Common.ReadOnlyArray<Byte> ColumnCounts {get;}`<br>`Common.ReadOnlyArray<Vector2> FlowDirections {get;}`<br>`Common.ReadOnlyArray<WaterSystem.ReadOnlyWaterColumn> WaterColumns {get;}`<br>`Int32 MaxColumnCount {get;}`<br>`Boolean CellIsUnderwater(Vector3Int coordinates)`<br>`Boolean IsWaterOnAnyHeight(Vector2Int coordinates)`<br>`Boolean TryGetColumnFloor(Vector3Int coordinates, Int32& floor)`<br>`Byte ColumnCeiling(Int32 index3D)`<br>`Byte ColumnFloor(Int32 index3D)`<br>`Int32 CeiledWaterHeight(Vector3Int coordinates)`<br>`Int32 ColumnCount(Int32 index2D)`<br>`Single ColumnContamination(Vector3Int coordinates)`<br>`Single WaterDepth(Int32 index3D)`<br>`Single WaterDepth(Vector3Int coordinates)`<br>`Single WaterHeightOrFloor(Vector3Int coordinates)` |
| `Timberborn.WaterSystem.IWaterService` | public interface | Timberborn.WaterSystem | `Void AddCleanWater(Vector3Int coordinates, Single depth)`<br>`Void AddContaminatedWater(Vector3Int coordinates, Single depth)`<br>`Void AddDirectionLimiter(Vector3Int coordinates, WaterSystem.FlowDirection flowDirection)`<br>`Void AddFullObstacle(Vector3Int coordinates)`<br>`Void AddHorizontalObstacle(Vector3Int coordinatesToAdd)`<br>`Void RegisterWaterSource(WaterSystem.IWaterSource waterSource)`<br>`Void RemoveCleanWater(Vector3Int coordinates, Single depth)`<br>`Void RemoveContaminatedWater(Vector3Int coordinates, Single depth)`<br>`Void RemoveDirectionLimiter(Vector3Int coordinates)`<br>`Void RemoveFlowController(Vector3Int coordinates)`<br>`Void RemoveFullObstacle(Vector3Int coordinates)`<br>`Void RemoveHorizontalObstacle(Vector3Int coordinatesToRemove)`<br>`Void RemoveInflowLimit(Vector3Int coordinates)`<br>`Void RemovePartialObstacle(Vector3Int coordinates)`<br>`Void SetControllerToDecreaseFlow(Vector3Int coordinates)`<br>`Void SetControllerToIncreaseFlow(Vector3Int coordinates)` |
| `Timberborn.Workshops.Manufactory` | public class | Timberborn.Workshops | `Boolean HasAllIngredients {get;}`<br>`Boolean HasCurrentRecipe {get;}`<br>`Boolean HasFuel {get;}`<br>`Boolean IsReadyToProduce {get;}`<br>`Boolean NeedsInventory {get;}`<br>`Collections.Immutable.ImmutableArray<Workshops.RecipeSpec> ProductionRecipes {get; set;}`<br>`Common.ReadOnlyList<Goods.GoodAmount> ProcessedGoods {get;}`<br>`InventorySystem.Inventory Inventory {get; set;}`<br>`Single FuelRemaining {get; set;}`<br>`Single ProductionProgress {get; set;}`<br>`Workshops.RecipeSpec CurrentRecipe {get; set;}`<br>`Boolean HasUnreservedCapacityForCurrentProducts()`<br>`Void Awake()`<br>`Void DuplicateFrom(Workshops.Manufactory source)`<br>`Void IncreaseProductionProgress(Single workedHours)`<br>`Void InitializeInventory(InventorySystem.Inventory inventory)` |
| `Timberborn.Yielding.Yielder` | public class | Timberborn.Yielding | `Boolean IsYieldRemoved {get;}`<br>`Boolean IsYielding {get;}`<br>`Goods.GoodAmount Yield {get;}`<br>`Int32 InstantiationOrder {get;}`<br>`ReservableSystem.Reservable Reservable {get; set;}`<br>`Single RemovalTimeInHours {get;}`<br>`String Animation {get; set;}`<br>`String ComponentName {get;}`<br>`Vector3 CenterPosition {get;}`<br>`Vector3Int Coordinates {get;}`<br>`Yielding.IRemoveYieldStrategy RemoveYieldStrategy {get; set;}`<br>`Yielding.YielderSpec YielderSpec {get; set;}`<br>`Void Awake()`<br>`Void DecreaseYield(Goods.GoodAmount decreasedYield)`<br>`Void Disable()`<br>`Void Enable()` |

## Public Service Index

This is the complete reflected set of public types whose names or tags mark them as services. Some concrete implementations are public; other public service contracts are interfaces backed by internal implementations.

| Type | Kind | Assembly | Tags | Public member count |
| --- | --- | --- | --- | --- |
| `Timberborn.AutomationBuildings.SpeakerSoundService` | class | Timberborn.AutomationBuildings | service, singleton, lifecycle | 7 |
| `Timberborn.Autosaving.AutosaveNameService` | class | Timberborn.Autosaving | service | 2 |
| `Timberborn.BlockObjectTools.BlockObjectPlacerService` | class | Timberborn.BlockObjectTools | service | 1 |
| `Timberborn.BlockObjectTools.BlockObjectToolGroupSpecService` | class | Timberborn.BlockObjectTools | service, singleton, lifecycle | 4 |
| `Timberborn.BlockObjectTools.PlaceableBlockObjectSpecService` | class | Timberborn.BlockObjectTools | service | 2 |
| `Timberborn.BlockSystem.BlockObjectValidationService` | class | Timberborn.BlockSystem | service | 3 |
| `Timberborn.BlockSystem.BlockOccupancyService` | class | Timberborn.BlockSystem | service | 1 |
| `Timberborn.BlockSystem.IBlockOccupancyService` | interface | Timberborn.BlockSystem | interface, service | 1 |
| `Timberborn.BlockSystem.IBlockService` | interface | Timberborn.BlockSystem | interface, service | 22 |
| `Timberborn.BlockSystem.IPreviewServiceMember` | interface | Timberborn.BlockSystem | interface, service | 2 |
| `Timberborn.BlockSystem.OverridenBlockObjectService` | class | Timberborn.BlockSystem | service | 1 |
| `Timberborn.BlockSystem.PreviewBlockService` | class | Timberborn.BlockSystem | service, singleton, lifecycle | 12 |
| `Timberborn.BlockSystem.StackableBlockService` | class | Timberborn.BlockSystem | service | 4 |
| `Timberborn.BlueprintSystem.BlueprintSourceService` | class | Timberborn.BlueprintSystem | service | 2 |
| `Timberborn.BlueprintSystem.ISpecService` | interface | Timberborn.BlueprintSystem | interface, service | 3 |
| `Timberborn.BonusSystem.BonusTypeSpecService` | class | Timberborn.BonusSystem | service, singleton, lifecycle | 3 |
| `Timberborn.Buildings.BuildingService` | class | Timberborn.Buildings | service, singleton, lifecycle | 4 |
| `Timberborn.CameraSystem.CameraService` | class | Timberborn.CameraSystem | service, singleton, lifecycle | 31 |
| `Timberborn.ConstructionGuidelines.ConstructionGuidelinesRenderingService` | class | Timberborn.ConstructionGuidelines | service, singleton, lifecycle | 7 |
| `Timberborn.ConstructionMode.ConstructionModeService` | class | Timberborn.ConstructionMode | service, singleton, lifecycle | 10 |
| `Timberborn.CoreUI.ScrollBarInitializationService` | class | Timberborn.CoreUI | service, ui | 1 |
| `Timberborn.DecalSystem.IDecalService` | interface | Timberborn.DecalSystem | interface, service | 4 |
| `Timberborn.EntityNaming.UniquelyNamedEntityService` | class | Timberborn.EntityNaming | service | 1 |
| `Timberborn.EntityPanelSystem.EntityBadgeService` | class | Timberborn.EntityPanelSystem | service, singleton, lifecycle | 4 |
| `Timberborn.EntityPanelSystem.EntityDescriptionService` | class | Timberborn.EntityPanelSystem | service | 2 |
| `Timberborn.EntitySystem.EntityService` | class | Timberborn.EntitySystem | service | 3 |
| `Timberborn.EntitySystem.RegisteredComponentService` | class | Timberborn.EntitySystem | service | 1 |
| `Timberborn.ErrorReporting.ILoadingIssueService` | interface | Timberborn.ErrorReporting | interface, service | 3 |
| `Timberborn.ErrorReporting.WorldDataService` | static class | Timberborn.ErrorReporting | service | 4 |
| `Timberborn.Explosions.ExplosionService` | class | Timberborn.Explosions | service, singleton, tick, lifecycle | 5 |
| `Timberborn.FactionSystem.FactionSpecService` | class | Timberborn.FactionSystem | service, singleton, lifecycle | 4 |
| `Timberborn.FactionSystem.FactionUnlockingService` | class | Timberborn.FactionSystem | service | 4 |
| `Timberborn.FeatureToggleSystem.FeatureToggleService` | static class | Timberborn.FeatureToggleSystem | service | 3 |
| `Timberborn.FileSystem.FileService` | class | Timberborn.FileSystem | service | 13 |
| `Timberborn.FileSystem.IFileService` | interface | Timberborn.FileSystem | interface, service | 13 |
| `Timberborn.FireworkSystem.FireworkSpecService` | class | Timberborn.FireworkSystem | service, singleton, lifecycle | 4 |
| `Timberborn.GameCycleSystem.GameCycleService` | class | Timberborn.GameCycleSystem | service, singleton, lifecycle | 6 |
| `Timberborn.GameDistricts.DistrictContextService` | class | Timberborn.GameDistricts | service | 3 |
| `Timberborn.GameDistrictsMigration.MigrationService` | class | Timberborn.GameDistrictsMigration | service | 7 |
| `Timberborn.GameFactionSystem.FactionNeedService` | class | Timberborn.GameFactionSystem | service, singleton, lifecycle | 7 |
| `Timberborn.GameFactionSystem.FactionService` | class | Timberborn.GameFactionSystem | service, singleton, lifecycle | 3 |
| `Timberborn.GameFactionSystem.NeedModificationService` | class | Timberborn.GameFactionSystem | service, singleton, lifecycle | 3 |
| `Timberborn.GameSaveRepositorySystemUI.SaveVersionCompatibilityService` | class | Timberborn.GameSaveRepositorySystemUI | service, singleton, ui, lifecycle | 4 |
| `Timberborn.GameWonderCompletion.GameWonderCompletionService` | class | Timberborn.GameWonderCompletion | service | 5 |
| `Timberborn.GameWonderCompletion.MapNameService` | class | Timberborn.GameWonderCompletion | service, singleton, lifecycle | 5 |
| `Timberborn.Goods.GoodsGroupSpecService` | class | Timberborn.Goods | service, singleton, lifecycle | 3 |
| `Timberborn.Goods.IGoodService` | interface | Timberborn.Goods | interface, service | 6 |
| `` Timberborn.GoodStackSystem.GoodStackService`1 `` | class | Timberborn.GoodStackSystem | service | 3 |
| `Timberborn.GoodStackSystem.IGoodStackService` | interface | Timberborn.GoodStackSystem | interface, service | 1 |
| `Timberborn.HazardousWeatherSystem.HazardousWeatherService` | class | Timberborn.HazardousWeatherSystem | service, singleton, lifecycle | 8 |
| `Timberborn.Illumination.IlluminationService` | class | Timberborn.Illumination | service, singleton, lifecycle | 5 |
| `Timberborn.InputSystem.CursorService` | class | Timberborn.InputSystem | service, singleton, lifecycle | 6 |
| `Timberborn.InputSystem.InputService` | class | Timberborn.InputSystem | service, singleton | 33 |
| `Timberborn.InputSystem.KeywordService` | class | Timberborn.InputSystem | service, singleton, lifecycle | 2 |
| `Timberborn.KeyBindingSystem.InputBindingNameService` | class | Timberborn.KeyBindingSystem | service | 3 |
| `Timberborn.KeyBindingSystem.InputModifiersService` | class | Timberborn.KeyBindingSystem | service | 2 |
| `Timberborn.KeyBindingSystem.KeyBindingGroupSpecService` | class | Timberborn.KeyBindingSystem | service, singleton, lifecycle | 3 |
| `Timberborn.KeyBindingSystem.KeyBindingSpecService` | class | Timberborn.KeyBindingSystem | service, singleton, lifecycle | 4 |
| `Timberborn.KeyBindingSystemUI.KeyBindingShortcutService` | class | Timberborn.KeyBindingSystemUI | service, ui | 2 |
| `Timberborn.LevelVisibilitySystem.ILevelVisibilityService` | interface | Timberborn.LevelVisibilitySystem | interface, service | 9 |
| `Timberborn.LifeSystem.LifeService` | class | Timberborn.LifeSystem | service, singleton, lifecycle | 6 |
| `Timberborn.Localization.ILocalizationService` | interface | Timberborn.Localization | interface, service | 3 |
| `Timberborn.MapEditorBrushesUI.TerrainIntegrityService` | class | Timberborn.MapEditorBrushesUI | service, singleton, ui, lifecycle | 4 |
| `Timberborn.MapEditorNaturalResources.NaturalResourceLayerService` | class | Timberborn.MapEditorNaturalResources | service | 3 |
| `Timberborn.MapEditorPlacementRandomizing.BlockObjectPlacementRandomizingService` | class | Timberborn.MapEditorPlacementRandomizing | service | 1 |
| `Timberborn.MapIndexSystem.MapIndexService` | class | Timberborn.MapIndexSystem | service, singleton, lifecycle | 22 |
| `Timberborn.MapRepositorySystemUI.MapVersionCompatibilityService` | class | Timberborn.MapRepositorySystemUI | service, singleton, ui, lifecycle | 5 |
| `Timberborn.Metrics.IMetricsService` | interface | Timberborn.Metrics | interface, service | 4 |
| `Timberborn.MortalSystem.LongLastingCorpsesService` | class | Timberborn.MortalSystem | service | 2 |
| `Timberborn.NaturalResources.SpawnValidationService` | class | Timberborn.NaturalResources | service | 3 |
| `Timberborn.NaturalResourcesMoisture.FloodableNaturalResourceService` | class | Timberborn.NaturalResourcesMoisture | service, singleton, lifecycle | 3 |
| `Timberborn.Navigation.DummyNavigationCachingService` | class | Timberborn.Navigation | service | 4 |
| `Timberborn.Navigation.DummyNavigationDebuggingService` | class | Timberborn.Navigation | service | 1 |
| `Timberborn.Navigation.DummyNavigationService` | class | Timberborn.Navigation | service | 14 |
| `Timberborn.Navigation.DummyNavMeshService` | class | Timberborn.Navigation | service | 12 |
| `Timberborn.Navigation.IDistrictService` | interface | Timberborn.Navigation | interface, service | 16 |
| `Timberborn.Navigation.INavigationCachingService` | interface | Timberborn.Navigation | interface, service | 4 |
| `Timberborn.Navigation.INavigationDebuggingService` | interface | Timberborn.Navigation | interface, service | 1 |
| `Timberborn.Navigation.INavigationRangeService` | interface | Timberborn.Navigation | interface, service | 6 |
| `Timberborn.Navigation.INavigationService` | interface | Timberborn.Navigation | interface, service | 14 |
| `Timberborn.Navigation.INavMeshService` | interface | Timberborn.Navigation | interface, service | 12 |
| `Timberborn.Navigation.NavMeshGroupService` | class | Timberborn.Navigation | service | 3 |
| `Timberborn.NeedApplication.EffectProbabilityService` | class | Timberborn.NeedApplication | service, singleton, lifecycle | 4 |
| `Timberborn.NeedBehaviorSystem.DistrictNeedBehaviorService` | class | Timberborn.NeedBehaviorSystem | service | 5 |
| `Timberborn.NeedSpecs.NeedGroupSpecService` | class | Timberborn.NeedSpecs | service, singleton, lifecycle | 4 |
| `Timberborn.NewGameConfigurationSystem.GameModeSpecService` | class | Timberborn.NewGameConfigurationSystem | service, singleton, lifecycle | 3 |
| `Timberborn.PathSystem.IConnectionService` | interface | Timberborn.PathSystem | interface, service | 1 |
| `Timberborn.PathSystem.IPathService` | interface | Timberborn.PathSystem | interface, service | 1 |
| `Timberborn.Planting.PlantingService` | class | Timberborn.Planting | service, singleton, lifecycle | 14 |
| `Timberborn.PlantingUI.PlantablePreviewService` | class | Timberborn.PlantingUI | service, singleton, ui, lifecycle | 9 |
| `Timberborn.PlantingUI.PlantingModeService` | class | Timberborn.PlantingUI | service, singleton, ui, lifecycle | 7 |
| `Timberborn.PlantingUI.PlantingSelectionService` | class | Timberborn.PlantingUI | service, singleton, ui, lifecycle | 6 |
| `Timberborn.PlayerDataSystem.IPlayerDataService` | interface | Timberborn.PlayerDataSystem | interface, service | 8 |
| `Timberborn.PlayerDataSystem.PlayerDataFileService` | class | Timberborn.PlayerDataSystem | service | 3 |
| `Timberborn.Population.PopulationService` | class | Timberborn.Population | service, singleton, tick, lifecycle | 12 |
| `Timberborn.QuickNotificationSystem.QuickNotificationService` | class | Timberborn.QuickNotificationSystem | service | 2 |
| `Timberborn.RangedEffectBuildingUI.BuildingWithRangeUpdateService` | class | Timberborn.RangedEffectBuildingUI | service, singleton, ui, lifecycle | 10 |
| `Timberborn.RangedEffectBuildingUI.RangeObjectHighlighterService` | class | Timberborn.RangedEffectBuildingUI | service, singleton, ui, lifecycle | 8 |
| `Timberborn.RangedEffectBuildingUI.RangeTileMarkerService` | class | Timberborn.RangedEffectBuildingUI | service, singleton, ui, lifecycle | 9 |
| `Timberborn.RecoverableGoodSystem.GoodRecoveryRateService` | class | Timberborn.RecoverableGoodSystem | service, singleton, lifecycle | 3 |
| `Timberborn.Rendering.PostprocessingService` | class | Timberborn.Rendering | service, singleton, lifecycle | 3 |
| `Timberborn.Reproduction.DistrictBreedingPodService` | class | Timberborn.Reproduction | service | 3 |
| `Timberborn.ResourceCountingSystem.ResourceCountingService` | class | Timberborn.ResourceCountingSystem | service, singleton, lifecycle | 6 |
| `Timberborn.ResourceCountingSystemUI.ContextualResourceCountingService` | class | Timberborn.ResourceCountingSystemUI | service, singleton, ui, lifecycle | 4 |
| `Timberborn.ScienceSystem.BuildingUnlockingService` | class | Timberborn.ScienceSystem | service, singleton, lifecycle | 6 |
| `Timberborn.ScienceSystem.ScienceService` | class | Timberborn.ScienceSystem | service, singleton, lifecycle | 5 |
| `Timberborn.SelectionSystem.AreaHighlightingService` | class | Timberborn.SelectionSystem | service, singleton, lifecycle | 5 |
| `Timberborn.SelectionSystem.EntitySelectionService` | class | Timberborn.SelectionSystem | service, singleton, lifecycle | 14 |
| `Timberborn.SelectionSystem.HighlightRenderingService` | class | Timberborn.SelectionSystem | service, singleton, lifecycle | 5 |
| `Timberborn.SettlementNameSystem.SettlementReferenceService` | class | Timberborn.SettlementNameSystem | service, singleton, lifecycle | 3 |
| `Timberborn.SoilContaminationSystem.ISoilContaminationService` | interface | Timberborn.SoilContaminationSystem | interface, service | 2 |
| `Timberborn.SoilMoistureSystem.ISoilMoistureService` | interface | Timberborn.SoilMoistureSystem | interface, service | 2 |
| `Timberborn.SoundSystem.AudioClipService` | class | Timberborn.SoundSystem | service | 5 |
| `Timberborn.StartingLocationSystem.StartingLocationService` | class | Timberborn.StartingLocationSystem | service, singleton, lifecycle | 6 |
| `Timberborn.StatusSystem.IStatusIconOffsetService` | interface | Timberborn.StatusSystem | interface, service | 10 |
| `Timberborn.SteamWorkshopMapUploadingUI.SteamWorkshopMapDataService` | class | Timberborn.SteamWorkshopMapUploadingUI | service, singleton, ui, lifecycle | 4 |
| `Timberborn.StockpilesUI.StockpileOptionsService` | class | Timberborn.StockpilesUI | service, ui | 3 |
| `Timberborn.StockpileVisualization.GoodColumnVariantsService` | class | Timberborn.StockpileVisualization | service, singleton, lifecycle | 3 |
| `Timberborn.StockpileVisualization.GoodVisualizationSpecService` | class | Timberborn.StockpileVisualization | service, singleton, lifecycle | 2 |
| `Timberborn.TemplateCollectionSystem.TemplateCollectionService` | class | Timberborn.TemplateCollectionSystem | service, singleton, lifecycle | 2 |
| `Timberborn.TemplateSystem.TemplateInstantiationOrderService` | class | Timberborn.TemplateSystem | service | 1 |
| `Timberborn.TemplateSystem.TemplateService` | class | Timberborn.TemplateSystem | service | 2 |
| `Timberborn.TerrainPhysics.ITerrainPhysicsService` | interface | Timberborn.TerrainPhysics | interface, service | 7 |
| `Timberborn.TerrainQueryingSystem.TerrainAreaService` | class | Timberborn.TerrainQueryingSystem | service | 2 |
| `Timberborn.TerrainSystem.ITerrainService` | interface | Timberborn.TerrainSystem | interface, service | 27 |
| `Timberborn.TerrainSystemRendering.TerrainHighlightingService` | class | Timberborn.TerrainSystemRendering | service, singleton, lifecycle | 4 |
| `Timberborn.TickSystem.ITickableSingletonService` | interface | Timberborn.TickSystem | interface, service, singleton | 5 |
| `Timberborn.TickSystem.ITickService` | interface | Timberborn.TickSystem | interface, service | 1 |
| `Timberborn.TickSystem.TickOnlyArrayService` | class | Timberborn.TickSystem | service, singleton, tick, lifecycle | 6 |
| `Timberborn.TimeSystem.ITickProgressService` | interface | Timberborn.TimeSystem | interface, service | 2 |
| `Timberborn.ToolButtonSystem.ToolButtonService` | class | Timberborn.ToolButtonSystem | service, singleton, lifecycle | 12 |
| `Timberborn.ToolSystem.ToolGroupService` | class | Timberborn.ToolSystem | service, singleton, lifecycle | 11 |
| `Timberborn.ToolSystem.ToolService` | class | Timberborn.ToolSystem | service, singleton, lifecycle | 10 |
| `Timberborn.ToolSystem.ToolUnlockingService` | class | Timberborn.ToolSystem | service | 5 |
| `Timberborn.TutorialSystem.ITutorialService` | interface | Timberborn.TutorialSystem | interface, service | 1 |
| `Timberborn.Versioning.VersionCompatibilityService` | class | Timberborn.Versioning | service | 3 |
| `Timberborn.WalkingSystem.WalkerService` | class | Timberborn.WalkingSystem | service | 1 |
| `Timberborn.WaterBuildings.WaterInputService` | class | Timberborn.WaterBuildings | service, singleton, tick | 3 |
| `Timberborn.WaterObjects.WaterObjectService` | class | Timberborn.WaterObjects | service, singleton, tick | 3 |
| `Timberborn.WaterSourceSystem.WaterStrengthService` | class | Timberborn.WaterSourceSystem | service, singleton, lifecycle | 4 |
| `Timberborn.WaterSystem.IFlowLimiterService` | interface | Timberborn.WaterSystem | interface, service | 2 |
| `Timberborn.WaterSystem.INonThreadSafeWaterService` | interface | Timberborn.WaterSystem | interface, service | 4 |
| `Timberborn.WaterSystem.IWaterRemovalService` | interface | Timberborn.WaterSystem | interface, service | 1 |
| `Timberborn.WaterSystem.IWaterService` | interface | Timberborn.WaterSystem | interface, service | 19 |
| `Timberborn.WaterSystemRendering.WaterOpacityService` | class | Timberborn.WaterSystemRendering | service, singleton, lifecycle | 4 |
| `Timberborn.WeatherSystem.TemperateWeatherDurationService` | class | Timberborn.WeatherSystem | service, singleton, lifecycle | 7 |
| `Timberborn.WeatherSystem.WeatherService` | class | Timberborn.WeatherSystem | service, singleton, lifecycle | 9 |
| `Timberborn.Wellbeing.IWellbeingTierService` | interface | Timberborn.Wellbeing | interface, service | 3 |
| `Timberborn.Wellbeing.WellbeingLimitService` | class | Timberborn.Wellbeing | service, singleton, lifecycle | 3 |
| `Timberborn.Wellbeing.WellbeingService` | class | Timberborn.Wellbeing | service, singleton, tick, lifecycle | 10 |
| `Timberborn.WindSystem.WindService` | class | Timberborn.WindSystem | service, singleton, tick, lifecycle | 8 |
| `Timberborn.WonderCompletion.WonderCompletionService` | class | Timberborn.WonderCompletion | service | 4 |
| `Timberborn.WorkerTypes.WorkerTypeService` | class | Timberborn.WorkerTypes | service, singleton, lifecycle | 3 |
| `Timberborn.Workshops.RecipeSpecService` | class | Timberborn.Workshops | service, singleton, lifecycle | 3 |
| `Timberborn.WorkSystem.CommunityServiceBehavior` | abstract class | Timberborn.WorkSystem | service | 0 |
| `Timberborn.WorkSystem.WorkplaceUnlockingService` | class | Timberborn.WorkSystem | service, singleton, lifecycle | 7 |
| `Timberborn.WorkSystemUI.WorkplaceUnlockingDialogService` | class | Timberborn.WorkSystemUI | service, ui | 3 |
| `Timberborn.Yielding.YieldRemovalChanceBonusService` | class | Timberborn.Yielding | service, singleton, lifecycle | 2 |
| `Timberborn.ZiplineSystem.ZiplineConnectionService` | class | Timberborn.ZiplineSystem | service, singleton, lifecycle | 10 |
| `Timberborn.ZiplineSystem.ZiplineGroupService` | class | Timberborn.ZiplineSystem | service, singleton, lifecycle | 7 |

## Public Interface API Index

This is the complete reflected set of public Timberborn interfaces. These are the best compile-time extension and injection targets.

| Interface | Assembly | Tags | Public member count |
| --- | --- | --- | --- |
| `Timberborn.AchievementSystem.IStoreAchievements` | Timberborn.AchievementSystem | interface | 3 |
| `Timberborn.ActivatorSystem.IActivableComponent` | Timberborn.ActivatorSystem | interface | 2 |
| `Timberborn.AlertPanelSystem.IAlertFragment` | Timberborn.AlertPanelSystem | interface, ui | 2 |
| `Timberborn.AssetSystem.IAssetLoader` | Timberborn.AssetSystem | interface | 4 |
| `Timberborn.AssetSystem.IAssetProvider` | Timberborn.AssetSystem | interface, provider | 4 |
| `Timberborn.Automation.IAutomatableNeeder` | Timberborn.Automation | interface | 1 |
| `Timberborn.Automation.IAutomationRunnerDebugger` | Timberborn.Automation | interface | 1 |
| `Timberborn.Automation.IAutomatorListener` | Timberborn.Automation | interface, listener | 1 |
| `Timberborn.Automation.ICombinationalTransmitter` | Timberborn.Automation | interface | 1 |
| `Timberborn.Automation.ICommittingSingleton` | Timberborn.Automation | interface, singleton | 1 |
| `Timberborn.Automation.ISamplingSingleton` | Timberborn.Automation | interface, singleton | 1 |
| `Timberborn.Automation.ISamplingTransmitter` | Timberborn.Automation | interface | 1 |
| `Timberborn.Automation.ISequentialTransmitter` | Timberborn.Automation | interface | 4 |
| `Timberborn.Automation.ITerminal` | Timberborn.Automation | interface | 1 |
| `Timberborn.Automation.ITransmitter` | Timberborn.Automation | interface | 0 |
| `Timberborn.Autosaving.IAutosaveBlocker` | Timberborn.Autosaving | interface | 1 |
| `Timberborn.BaseComponentSystem.IAwakableComponent` | Timberborn.BaseComponentSystem | interface | 1 |
| `Timberborn.BaseComponentSystem.ILateUpdatableComponent` | Timberborn.BaseComponentSystem | interface | 1 |
| `Timberborn.BaseComponentSystem.IStartableComponent` | Timberborn.BaseComponentSystem | interface | 1 |
| `Timberborn.BaseComponentSystem.IUpdatableComponent` | Timberborn.BaseComponentSystem | interface | 1 |
| `Timberborn.BatchControl.IBatchControlBox` | Timberborn.BatchControl | interface | 7 |
| `Timberborn.BatchControl.IBatchControlRowItem` | Timberborn.BatchControl | interface | 1 |
| `Timberborn.BatchControl.IClearableBatchControlRowItem` | Timberborn.BatchControl | interface | 1 |
| `Timberborn.BatchControl.IFinishableBatchControlRowItem` | Timberborn.BatchControl | interface | 1 |
| `Timberborn.BatchControl.IHideableByBatchControl` | Timberborn.BatchControl | interface | 2 |
| `Timberborn.BatchControl.IUpdatableBatchControlRowItem` | Timberborn.BatchControl | interface | 1 |
| `Timberborn.BehaviorSystem.IExecutor` | Timberborn.BehaviorSystem | interface | 3 |
| `Timberborn.BlockObjectModelSystem.IBlockObjectModel` | Timberborn.BlockObjectModelSystem | interface | 5 |
| `Timberborn.BlockObjectModelSystem.IInfiniteUndergroundModel` | Timberborn.BlockObjectModelSystem | interface | 0 |
| `Timberborn.BlockObjectModelSystem.IModelUpdater` | Timberborn.BlockObjectModelSystem | interface | 1 |
| `Timberborn.BlockObjectTools.IBlockObjectPlacer` | Timberborn.BlockObjectTools | interface | 3 |
| `Timberborn.BlockObjectTools.IBlockObjectToolDescriber` | Timberborn.BlockObjectTools | interface | 1 |
| `Timberborn.BlockSystem.IBlockObjectCustomOverriding` | Timberborn.BlockSystem | interface | 1 |
| `Timberborn.BlockSystem.IBlockObjectDeletionBlocker` | Timberborn.BlockSystem | interface | 4 |
| `Timberborn.BlockSystem.IBlockObjectValidator` | Timberborn.BlockSystem | interface | 1 |
| `Timberborn.BlockSystem.IBlockOccupancyService` | Timberborn.BlockSystem | interface, service | 1 |
| `Timberborn.BlockSystem.IBlockService` | Timberborn.BlockSystem | interface, service | 22 |
| `Timberborn.BlockSystem.IFinishedPostLoadStateListener` | Timberborn.BlockSystem | interface, listener | 2 |
| `Timberborn.BlockSystem.IFinishedStateListener` | Timberborn.BlockSystem | interface, listener | 2 |
| `Timberborn.BlockSystem.IPostPlacementChangeListener` | Timberborn.BlockSystem | interface, listener | 1 |
| `Timberborn.BlockSystem.IPrePlacementChangeListener` | Timberborn.BlockSystem | interface, listener | 1 |
| `Timberborn.BlockSystem.IPrePreviewShownListener` | Timberborn.BlockSystem | interface, listener | 1 |
| `Timberborn.BlockSystem.IPreviewSelectionListener` | Timberborn.BlockSystem | interface, listener | 2 |
| `Timberborn.BlockSystem.IPreviewServiceMember` | Timberborn.BlockSystem | interface, service | 2 |
| `Timberborn.BlockSystem.IPreviewStateListener` | Timberborn.BlockSystem | interface, listener | 1 |
| `Timberborn.BlockSystem.IPreviewValidator` | Timberborn.BlockSystem | interface | 2 |
| `Timberborn.BlockSystem.IUnfinishedStateListener` | Timberborn.BlockSystem | interface, listener | 2 |
| `Timberborn.BlockSystemNavigation.IBlockObjectNavMesh` | Timberborn.BlockSystemNavigation | interface | 2 |
| `Timberborn.BlueprintPrefabSystem.ISpecToPrefabConverter` | Timberborn.BlueprintPrefabSystem | interface | 2 |
| `Timberborn.BlueprintSystem.IBlueprintModifierProvider` | Timberborn.BlueprintSystem | interface, provider | 2 |
| `Timberborn.BlueprintSystem.IDeserializer` | Timberborn.BlueprintSystem | interface | 2 |
| `Timberborn.BlueprintSystem.ISpecService` | Timberborn.BlueprintSystem | interface, service | 3 |
| `Timberborn.BottomBarSystem.IBottomBarElementsProvider` | Timberborn.BottomBarSystem | interface, provider | 1 |
| `Timberborn.Brushes.IBrushWithDirection` | Timberborn.Brushes | interface | 3 |
| `Timberborn.Brushes.IBrushWithHeight` | Timberborn.Brushes | interface | 2 |
| `Timberborn.Brushes.IBrushWithShape` | Timberborn.Brushes | interface | 1 |
| `Timberborn.Brushes.IBrushWithSize` | Timberborn.Brushes | interface | 1 |
| `Timberborn.BuilderHubSystem.IBuilderJobProvider` | Timberborn.BuilderHubSystem | interface, provider | 2 |
| `Timberborn.BuildingRange.IBuildingWithRange` | Timberborn.BuildingRange | interface | 3 |
| `Timberborn.Buildings.IBuildingEfficiencyProvider` | Timberborn.Buildings | interface, provider | 2 |
| `Timberborn.Buildings.IFinishedPausable` | Timberborn.Buildings | interface | 0 |
| `Timberborn.Buildings.IUnfinishedPausable` | Timberborn.Buildings | interface | 0 |
| `Timberborn.BuildingsNavigation.IConstructionSiteAccessProvider` | Timberborn.BuildingsNavigation | interface, provider | 1 |
| `Timberborn.BuildingsReachability.IExpandedConstructionSiteReachability` | Timberborn.BuildingsReachability | interface | 1 |
| `Timberborn.BuildingsReachability.IUnconnectedBuildingBlocker` | Timberborn.BuildingsReachability | interface | 1 |
| `Timberborn.BuildingsReachability.IUnreachableEntity` | Timberborn.BuildingsReachability | interface | 1 |
| `Timberborn.BuildingTools.ISectionProvider` | Timberborn.BuildingTools | interface, provider | 1 |
| `Timberborn.CameraSystem.ICameraAnchorPicker` | Timberborn.CameraSystem | interface | 1 |
| `Timberborn.CharacterMovementSystem.IMovementSpeedAffector` | Timberborn.CharacterMovementSystem | interface | 1 |
| `Timberborn.Characters.IChildhoodInfluenced` | Timberborn.Characters | interface | 1 |
| `Timberborn.CommandLine.ICommandLineArguments` | Timberborn.CommandLine | interface | 3 |
| `Timberborn.Common.IFakeRandomNumberGenerator` | Timberborn.Common | interface | 2 |
| `Timberborn.Common.IFakeRandomNumberGeneratorFactory` | Timberborn.Common | interface, factory | 1 |
| `Timberborn.Common.IRandomNumberGenerator` | Timberborn.Common | interface | 9 |
| `Timberborn.Console.IConsolePanel` | Timberborn.Console | interface, ui | 1 |
| `Timberborn.ConstructionGuidelines.IBlockObjectGridTool` | Timberborn.ConstructionGuidelines | interface | 0 |
| `Timberborn.ConstructionMode.IConstructionModeEnabler` | Timberborn.ConstructionMode | interface | 0 |
| `Timberborn.ConstructionSites.IConstructionFinishBlocker` | Timberborn.ConstructionSites | interface | 1 |
| `Timberborn.ConstructionSites.IConstructionSiteValidator` | Timberborn.ConstructionSites | interface | 3 |
| `Timberborn.CoreUI.ILocalizableElement` | Timberborn.CoreUI | interface, ui | 2 |
| `Timberborn.CoreUI.IPanelBlocker` | Timberborn.CoreUI | interface, ui | 0 |
| `Timberborn.CoreUI.IPanelController` | Timberborn.CoreUI | interface, ui | 3 |
| `Timberborn.CoreUI.IVisualElementInitializer` | Timberborn.CoreUI | interface, ui | 1 |
| `Timberborn.Debugging.IDevModule` | Timberborn.Debugging | interface | 1 |
| `Timberborn.DebuggingUI.IDebuggingPanel` | Timberborn.DebuggingUI | interface, ui | 1 |
| `Timberborn.DecalSystem.IDecalService` | Timberborn.DecalSystem | interface, service | 4 |
| `Timberborn.DropdownSystem.IDropdownProvider` | Timberborn.DropdownSystem | interface, provider | 3 |
| `Timberborn.DropdownSystem.IExtendedDropdownProvider` | Timberborn.DropdownSystem | interface, provider | 3 |
| `Timberborn.DropdownSystem.IExtendedTooltipDropdownProvider` | Timberborn.DropdownSystem | interface, provider | 1 |
| `Timberborn.DuplicationSystem.IDuplicable` | Timberborn.DuplicationSystem | interface | 1 |
| `` Timberborn.DuplicationSystem.IDuplicable`1 `` | Timberborn.DuplicationSystem | interface | 1 |
| `Timberborn.EnterableSystem.IStatusHider` | Timberborn.EnterableSystem | interface | 0 |
| `Timberborn.EntityNaming.IEntityNamer` | Timberborn.EntityNaming | interface | 2 |
| `Timberborn.EntityPanelSystem.IEntityBadge` | Timberborn.EntityPanelSystem | interface | 5 |
| `Timberborn.EntityPanelSystem.IEntityDescriber` | Timberborn.EntityPanelSystem | interface | 1 |
| `Timberborn.EntityPanelSystem.IEntityPanel` | Timberborn.EntityPanelSystem | interface, ui | 1 |
| `Timberborn.EntityPanelSystem.IEntityPanelFragment` | Timberborn.EntityPanelSystem | interface, ui | 4 |
| `Timberborn.EntitySystem.IDeletableEntity` | Timberborn.EntitySystem | interface | 1 |
| `Timberborn.EntitySystem.IInitializableEntity` | Timberborn.EntitySystem | interface | 1 |
| `Timberborn.EntitySystem.IPostInitializableEntity` | Timberborn.EntitySystem | interface | 1 |
| `Timberborn.EntitySystem.IPostLoadableEntity` | Timberborn.EntitySystem | interface | 1 |
| `Timberborn.EntitySystem.IPreInitializableEntity` | Timberborn.EntitySystem | interface | 1 |
| `Timberborn.EntitySystem.IRegisteredComponent` | Timberborn.EntitySystem | interface | 0 |
| `Timberborn.ErrorReporting.ILoadingIssueService` | Timberborn.ErrorReporting | interface, service | 3 |
| `Timberborn.FileSystem.IFileService` | Timberborn.FileSystem | interface, service | 13 |
| `Timberborn.GameCycleSystem.ICycleDuration` | Timberborn.GameCycleSystem | interface | 2 |
| `Timberborn.GameDistricts.ICitizenPositionOverrider` | Timberborn.GameDistricts | interface | 1 |
| `Timberborn.GameDistrictsMigration.IDistributorTemplate` | Timberborn.GameDistrictsMigration | interface | 3 |
| `Timberborn.GameOver.IGameOverChecker` | Timberborn.GameOver | interface | 1 |
| `Timberborn.GameSaveRepositorySystemUI.IGameLoadValidator` | Timberborn.GameSaveRepositorySystemUI | interface, ui | 2 |
| `Timberborn.GameStartup.ISettlementNamePromptShower` | Timberborn.GameStartup | interface | 1 |
| `Timberborn.GoodCollectionSystem.IGoodCollectionIdsProvider` | Timberborn.GoodCollectionSystem | interface, provider | 1 |
| `Timberborn.Goods.IAllowedGoodProvider` | Timberborn.Goods | interface, provider | 1 |
| `Timberborn.Goods.IGoodFilter` | Timberborn.Goods | interface | 1 |
| `Timberborn.Goods.IGoodService` | Timberborn.Goods | interface, service | 6 |
| `Timberborn.GoodStackSystem.IGoodStackInventory` | Timberborn.GoodStackSystem | interface | 1 |
| `Timberborn.GoodStackSystem.IGoodStackService` | Timberborn.GoodStackSystem | interface, service | 1 |
| `Timberborn.Hauling.IHaulBehaviorProvider` | Timberborn.Hauling | interface, provider | 1 |
| `Timberborn.HazardousWeatherSystem.IHazardousWeather` | Timberborn.HazardousWeatherSystem | interface | 2 |
| `Timberborn.HttpApiSystem.IHttpApiEndpoint` | Timberborn.HttpApiSystem | interface | 1 |
| `Timberborn.HttpApiSystem.IHttpApiPageSection` | Timberborn.HttpApiSystem | interface | 3 |
| `Timberborn.InputSystem.IInputProcessor` | Timberborn.InputSystem | interface | 1 |
| `Timberborn.InputSystem.IInputStateResetter` | Timberborn.InputSystem | interface | 1 |
| `Timberborn.InputSystem.IPriorityInputProcessor` | Timberborn.InputSystem | interface | 1 |
| `Timberborn.InventorySystem.IAmountProvider` | Timberborn.InventorySystem | interface, provider | 2 |
| `Timberborn.InventorySystem.IGoodDisallower` | Timberborn.InventorySystem | interface | 1 |
| `Timberborn.InventorySystem.IInitializableGoodDisallower` | Timberborn.InventorySystem | interface | 1 |
| `Timberborn.InventorySystem.IInventoryValidator` | Timberborn.InventorySystem | interface | 1 |
| `Timberborn.KeyBindingSystem.IKeyBindingBlocker` | Timberborn.KeyBindingSystem | interface | 1 |
| `Timberborn.LevelVisibilitySystem.ILevelVisibilityService` | Timberborn.LevelVisibilitySystem | interface, service | 9 |
| `Timberborn.LevelVisibilitySystemUI.ILevelVisibilityPanel` | Timberborn.LevelVisibilitySystemUI | interface, ui | 1 |
| `Timberborn.LifeSystem.ILongevity` | Timberborn.LifeSystem | interface | 1 |
| `Timberborn.Localization.ILoc` | Timberborn.Localization | interface | 10 |
| `Timberborn.Localization.ILocalizationCsvValidator` | Timberborn.Localization | interface | 1 |
| `Timberborn.Localization.ILocalizationService` | Timberborn.Localization | interface, service | 3 |
| `Timberborn.MapEditorConstructionGuidelinesUI.IBrushWithGuidelines` | Timberborn.MapEditorConstructionGuidelinesUI | interface, ui | 0 |
| `Timberborn.MapItemsUI.ICustomMapItemFactory` | Timberborn.MapItemsUI | interface, factory, ui | 1 |
| `Timberborn.MapRepositorySystemUI.IMapLoadValidator` | Timberborn.MapRepositorySystemUI | interface, ui | 3 |
| `Timberborn.MechanicalSystem.IBattery` | Timberborn.MechanicalSystem | interface | 1 |
| `Timberborn.MechanicalSystemUI.IMechanicalModelUpdater` | Timberborn.MechanicalSystemUI | interface, ui | 1 |
| `Timberborn.Metrics.IMetricsService` | Timberborn.Metrics | interface, service | 4 |
| `Timberborn.Metrics.ITimerMetric` | Timberborn.Metrics | interface | 2 |
| `Timberborn.Modding.IModsProvider` | Timberborn.Modding | interface, provider | 1 |
| `Timberborn.ModdingUI.IModItemFactory` | Timberborn.ModdingUI | interface, factory, ui | 1 |
| `Timberborn.ModdingUI.IModManagerTooltipRegistrar` | Timberborn.ModdingUI | interface, ui | 4 |
| `Timberborn.ModelHiding.IModelAdder` | Timberborn.ModelHiding | interface | 2 |
| `Timberborn.ModManagerScene.IModEnvironment` | Timberborn.ModManagerScene | interface | 2 |
| `Timberborn.ModManagerScene.IModStarter` | Timberborn.ModManagerScene | interface | 1 |
| `Timberborn.MortalComponents.IDeadNeededComponent` | Timberborn.MortalComponents | interface | 0 |
| `Timberborn.Multithreading.IParallelizer` | Timberborn.Multithreading | interface | 12 |
| `Timberborn.Multithreading.IParallelizerLoopTask` | Timberborn.Multithreading | interface | 1 |
| `Timberborn.Multithreading.IParallelizerSingleTask` | Timberborn.Multithreading | interface | 1 |
| `Timberborn.Multithreading.ISnapshotCollector` | Timberborn.Multithreading | interface | 3 |
| `Timberborn.Multithreading.ITaskRunner` | Timberborn.Multithreading | interface | 2 |
| `Timberborn.NaturalResources.ISpawnValidator` | Timberborn.NaturalResources | interface | 1 |
| `Timberborn.NaturalResourcesLifecycle.IDyingProgressProvider` | Timberborn.NaturalResourcesLifecycle | interface, provider | 1 |
| `Timberborn.Navigation.IAccessibleNeeder` | Timberborn.Navigation | interface | 2 |
| `Timberborn.Navigation.IAccessibleValidator` | Timberborn.Navigation | interface | 1 |
| `Timberborn.Navigation.IBlockedAccessible` | Timberborn.Navigation | interface | 2 |
| `Timberborn.Navigation.IDistrictService` | Timberborn.Navigation | interface, service | 16 |
| `Timberborn.Navigation.IInstantNavMeshListener` | Timberborn.Navigation | interface, listener | 1 |
| `Timberborn.Navigation.INavigationCachingService` | Timberborn.Navigation | interface, service | 4 |
| `Timberborn.Navigation.INavigationDebuggingService` | Timberborn.Navigation | interface, service | 1 |
| `Timberborn.Navigation.INavigationPhase` | Timberborn.Navigation | interface | 0 |
| `Timberborn.Navigation.INavigationRangeService` | Timberborn.Navigation | interface, service | 6 |
| `Timberborn.Navigation.INavigationService` | Timberborn.Navigation | interface, service | 14 |
| `Timberborn.Navigation.INavMeshDrawer` | Timberborn.Navigation | interface | 1 |
| `Timberborn.Navigation.INavMeshListener` | Timberborn.Navigation | interface, listener | 1 |
| `Timberborn.Navigation.INavMeshListenerEntityRegistry` | Timberborn.Navigation | interface, listener | 6 |
| `Timberborn.Navigation.INavMeshObjectFactory` | Timberborn.Navigation | interface, factory | 1 |
| `Timberborn.Navigation.INavMeshService` | Timberborn.Navigation | interface, service | 12 |
| `Timberborn.Navigation.INavMeshSizeProvider` | Timberborn.Navigation | interface, provider | 1 |
| `` Timberborn.Navigation.IOrderable`1 `` | Timberborn.Navigation | interface | 1 |
| `Timberborn.Navigation.IPathHeightProvider` | Timberborn.Navigation | interface, provider | 1 |
| `Timberborn.Navigation.IPathToAccessibleModifier` | Timberborn.Navigation | interface | 1 |
| `Timberborn.Navigation.IPathTransformer` | Timberborn.Navigation | interface | 1 |
| `Timberborn.Navigation.ISingletonInstantNavMeshListener` | Timberborn.Navigation | interface, singleton, listener | 1 |
| `Timberborn.Navigation.ISingletonNavMeshListener` | Timberborn.Navigation | interface, singleton, listener | 1 |
| `Timberborn.Navigation.ISingletonPreviewNavMeshListener` | Timberborn.Navigation | interface, singleton, listener | 1 |
| `Timberborn.NeedApplication.INeedEffectsSpec` | Timberborn.NeedApplication | interface | 1 |
| `Timberborn.NeedApplication.IProbabilityGroupProvider` | Timberborn.NeedApplication | interface, provider | 1 |
| `Timberborn.NeedBehaviorSystem.INeedBehaviorPicker` | Timberborn.NeedBehaviorSystem | interface | 3 |
| `Timberborn.NeedCollectionSystem.INeedCollectionIdsProvider` | Timberborn.NeedCollectionSystem | interface, provider | 1 |
| `Timberborn.Options.IOptionsBox` | Timberborn.Options | interface | 1 |
| `Timberborn.Particles.IFastForwardableParticles` | Timberborn.Particles | interface | 1 |
| `Timberborn.Particles.IParticlesSpeedMultiplier` | Timberborn.Particles | interface | 1 |
| `Timberborn.PathSystem.IConnectionService` | Timberborn.PathSystem | interface, service | 1 |
| `Timberborn.PathSystem.IPathConnectionEnforcer` | Timberborn.PathSystem | interface | 1 |
| `Timberborn.PathSystem.IPathService` | Timberborn.PathSystem | interface, service | 1 |
| `Timberborn.Persistence.IObjectLoader` | Timberborn.Persistence | interface | 31 |
| `Timberborn.Persistence.IObjectSaver` | Timberborn.Persistence | interface | 28 |
| `Timberborn.Persistence.IValueLoader` | Timberborn.Persistence | interface | 30 |
| `Timberborn.Persistence.IValueSaver` | Timberborn.Persistence | interface | 27 |
| `` Timberborn.Persistence.IValueSerializer`1 `` | Timberborn.Persistence | interface | 2 |
| `Timberborn.Planting.IPlantableGoodIdProvider` | Timberborn.Planting | interface, provider | 1 |
| `Timberborn.Planting.IPlantingSpotValidator` | Timberborn.Planting | interface | 1 |
| `Timberborn.PlatformUtilities.IExplorerOpener` | Timberborn.PlatformUtilities | interface | 1 |
| `Timberborn.PlayerDataSystem.IPlayerDataService` | Timberborn.PlayerDataSystem | interface, service | 8 |
| `Timberborn.PopulationStatisticsSystem.IContaminationStatisticsProvider` | Timberborn.PopulationStatisticsSystem | interface, provider | 1 |
| `Timberborn.PopulationStatisticsSystem.IDwellingStatisticsProvider` | Timberborn.PopulationStatisticsSystem | interface, provider | 1 |
| `Timberborn.PopulationStatisticsSystem.IEmploymentStatisticsProvider` | Timberborn.PopulationStatisticsSystem | interface, provider | 1 |
| `Timberborn.PopulationStatisticsSystem.IWorkRefusingStatisticsProvider` | Timberborn.PopulationStatisticsSystem | interface, provider | 1 |
| `Timberborn.PopulationUI.IPopulationRow` | Timberborn.PopulationUI | interface, ui | 1 |
| `Timberborn.PrefabOptimization.IPrefabOptimizationChain` | Timberborn.PrefabOptimization | interface | 3 |
| `Timberborn.PrefabOptimization.IPrefabOptimizer` | Timberborn.PrefabOptimization | interface | 1 |
| `Timberborn.PrefabOptimization.ITransform` | Timberborn.PrefabOptimization | interface | 3 |
| `Timberborn.PrioritySystem.IPrioritizable` | Timberborn.PrioritySystem | interface | 2 |
| `Timberborn.PrioritySystemUI.IPrioritySpriteLoader` | Timberborn.PrioritySystemUI | interface, ui | 1 |
| `Timberborn.RecoverableGoodSystem.IRecoverableGoodMultiplier` | Timberborn.RecoverableGoodSystem | interface | 1 |
| `Timberborn.RecoverableGoodSystemUI.IRecoverableObjectAdder` | Timberborn.RecoverableGoodSystemUI | interface, ui | 1 |
| `Timberborn.RelationSystem.IRelationOwner` | Timberborn.RelationSystem | interface | 1 |
| `Timberborn.ResourceCountingSystem.IGoodProcessor` | Timberborn.ResourceCountingSystem | interface | 2 |
| `` Timberborn.SaveSystem.IBackwardCompatibleSaveEntryReader`1 `` | Timberborn.SaveSystem | interface | 1 |
| `Timberborn.SaveSystem.IOptionalSaveEntryWriter` | Timberborn.SaveSystem | interface | 1 |
| `` Timberborn.SaveSystem.ISaveEntryReader`1 `` | Timberborn.SaveSystem | interface | 2 |
| `Timberborn.SaveSystem.ISaveEntryWriter` | Timberborn.SaveSystem | interface | 2 |
| `Timberborn.SceneLoading.ISceneLoader` | Timberborn.SceneLoading | interface | 7 |
| `Timberborn.SceneLoading.ISceneParameters` | Timberborn.SceneLoading | interface | 1 |
| `Timberborn.SelectionSystem.ICameraTarget` | Timberborn.SelectionSystem | interface | 1 |
| `Timberborn.SelectionSystem.ISelectionListener` | Timberborn.SelectionSystem | interface, listener | 2 |
| `Timberborn.SettingsSystem.ISettings` | Timberborn.SettingsSystem | interface | 16 |
| `Timberborn.SettingsSystemUI.ISettingsController` | Timberborn.SettingsSystemUI | interface, ui | 0 |
| `Timberborn.SingletonSystem.ILateUpdatableSingleton` | Timberborn.SingletonSystem | interface, singleton | 1 |
| `Timberborn.SingletonSystem.ILoadableSingleton` | Timberborn.SingletonSystem | interface, singleton | 1 |
| `Timberborn.SingletonSystem.INonSingletonLoader` | Timberborn.SingletonSystem | interface, singleton | 1 |
| `Timberborn.SingletonSystem.INonSingletonPostLoader` | Timberborn.SingletonSystem | interface, singleton | 1 |
| `Timberborn.SingletonSystem.IPostLoadableSingleton` | Timberborn.SingletonSystem | interface, singleton | 1 |
| `Timberborn.SingletonSystem.ISingletonRepository` | Timberborn.SingletonSystem | interface, singleton, repository | 1 |
| `Timberborn.SingletonSystem.IUnloadableSingleton` | Timberborn.SingletonSystem | interface, singleton | 1 |
| `Timberborn.SingletonSystem.IUpdatableSingleton` | Timberborn.SingletonSystem | interface, singleton | 1 |
| `Timberborn.SlotSystem.ICustomSlotRetriever` | Timberborn.SlotSystem | interface | 1 |
| `Timberborn.SlotSystem.ISlot` | Timberborn.SlotSystem | interface | 6 |
| `Timberborn.SoilContaminationSystem.ISoilContaminationService` | Timberborn.SoilContaminationSystem | interface, service | 2 |
| `Timberborn.SoilMoistureSystem.ISoilMoistureService` | Timberborn.SoilMoistureSystem | interface, service | 2 |
| `Timberborn.SoundSystem.IEmitterMap` | Timberborn.SoundSystem | interface | 1 |
| `Timberborn.SoundSystem.ISoundSystem` | Timberborn.SoundSystem | interface, system | 22 |
| `Timberborn.StatusSystem.IStatusAggregator` | Timberborn.StatusSystem | interface | 1 |
| `Timberborn.StatusSystem.IStatusIconOffsetService` | Timberborn.StatusSystem | interface, service | 10 |
| `Timberborn.StatusSystem.IStatusIconOffsetter` | Timberborn.StatusSystem | interface | 8 |
| `Timberborn.SteamWorkshopUI.ISteamWorkshopUploadable` | Timberborn.SteamWorkshopUI | interface, ui | 23 |
| `Timberborn.StockpilesUI.IGoodSelectionController` | Timberborn.StockpilesUI | interface, ui | 5 |
| `Timberborn.StoreSystem.IStore` | Timberborn.StoreSystem | interface | 6 |
| `Timberborn.TemplateCollectionSystem.ITemplateCollectionIdProvider` | Timberborn.TemplateCollectionSystem | interface, provider | 1 |
| `` Timberborn.TemplateInstantiation.IDedicatedDecoratorInitializer`2 `` | Timberborn.TemplateInstantiation | interface | 1 |
| `Timberborn.TerrainLevelValidation.IBottomLevelProvider` | Timberborn.TerrainLevelValidation | interface, provider | 1 |
| `Timberborn.TerrainPhysics.INonStackPickable` | Timberborn.TerrainPhysics | interface | 0 |
| `Timberborn.TerrainPhysics.ITerrainPhysicsService` | Timberborn.TerrainPhysics | interface, service | 7 |
| `Timberborn.TerrainSystem.ICutoutTilesProvider` | Timberborn.TerrainSystem | interface, provider | 1 |
| `Timberborn.TerrainSystem.IGroundMatterBelowInvalidator` | Timberborn.TerrainSystem | interface | 0 |
| `Timberborn.TerrainSystem.ITerrainRemovingEntity` | Timberborn.TerrainSystem | interface | 1 |
| `Timberborn.TerrainSystem.ITerrainService` | Timberborn.TerrainSystem | interface, service | 27 |
| `Timberborn.TerrainSystem.IThreadSafeColumnTerrainMap` | Timberborn.TerrainSystem | interface | 8 |
| `Timberborn.ThumbnailCapturing.IThumbnailRenderingListener` | Timberborn.ThumbnailCapturing | interface, listener | 2 |
| `Timberborn.ThumbnailCapturing.IThumbnailRenderTextureProvider` | Timberborn.ThumbnailCapturing | interface, provider | 1 |
| `Timberborn.ThumbnailSystem.IThumbnailConfiguration` | Timberborn.ThumbnailSystem | interface | 4 |
| `Timberborn.TickSystem.ILateTickable` | Timberborn.TickSystem | interface | 0 |
| `Timberborn.TickSystem.IParallelTickableSingleton` | Timberborn.TickSystem | interface, singleton | 1 |
| `Timberborn.TickSystem.ITickableSingleton` | Timberborn.TickSystem | interface, singleton | 1 |
| `Timberborn.TickSystem.ITickableSingletonService` | Timberborn.TickSystem | interface, service, singleton | 5 |
| `Timberborn.TickSystem.ITickingMode` | Timberborn.TickSystem | interface | 1 |
| `Timberborn.TickSystem.ITickService` | Timberborn.TickSystem | interface, service | 1 |
| `Timberborn.Timbermesh.IMaterialRepository` | Timberborn.Timbermesh | interface, repository | 1 |
| `Timberborn.Timbermesh.IModelPostprocessor` | Timberborn.Timbermesh | interface | 1 |
| `Timberborn.TimbermeshAnimations.IAnimator` | Timberborn.TimbermeshAnimations | interface | 11 |
| `Timberborn.TimbermeshAnimations.IAnimatorController` | Timberborn.TimbermeshAnimations | interface | 6 |
| `Timberborn.TimbermeshDTO.IAnimation` | Timberborn.TimbermeshDTO | interface | 2 |
| `Timberborn.TimbermeshMaterials.IMaterialCollectionIdsProvider` | Timberborn.TimbermeshMaterials | interface, provider | 1 |
| `Timberborn.TimeSystem.IDayNightCycle` | Timberborn.TimeSystem | interface | 20 |
| `Timberborn.TimeSystem.ITickProgressService` | Timberborn.TimeSystem | interface, service | 2 |
| `Timberborn.TimeSystem.ITimeTrigger` | Timberborn.TimeSystem | interface | 8 |
| `Timberborn.TimeSystem.ITimeTriggerFactory` | Timberborn.TimeSystem | interface, factory | 1 |
| `Timberborn.ToolButtonSystem.IToolbarButton` | Timberborn.ToolButtonSystem | interface | 3 |
| `Timberborn.ToolPanelSystem.IToolFragment` | Timberborn.ToolPanelSystem | interface, ui | 1 |
| `Timberborn.ToolSystem.IDefaultToolProvider` | Timberborn.ToolSystem | interface, provider | 1 |
| `Timberborn.ToolSystem.IDevModeTool` | Timberborn.ToolSystem | interface | 1 |
| `Timberborn.ToolSystem.IGroupIgnoringTool` | Timberborn.ToolSystem | interface | 0 |
| `Timberborn.ToolSystem.ITool` | Timberborn.ToolSystem | interface | 2 |
| `Timberborn.ToolSystem.IToolDisabler` | Timberborn.ToolSystem | interface | 1 |
| `Timberborn.ToolSystem.IToolFinder` | Timberborn.ToolSystem | interface | 1 |
| `Timberborn.ToolSystem.IToolLocker` | Timberborn.ToolSystem | interface | 2 |
| `Timberborn.ToolSystemUI.IToolDescriptor` | Timberborn.ToolSystemUI | interface, ui | 1 |
| `Timberborn.ToolSystemUI.IWaterIgnoringTool` | Timberborn.ToolSystemUI | interface, ui | 0 |
| `Timberborn.TooltipSystem.ITooltipRegistrar` | Timberborn.TooltipSystem | interface | 13 |
| `Timberborn.TutorialSystem.IStepDeserializer` | Timberborn.TutorialSystem | interface | 1 |
| `Timberborn.TutorialSystem.ITutorialService` | Timberborn.TutorialSystem | interface, service | 1 |
| `Timberborn.TutorialSystem.ITutorialStep` | Timberborn.TutorialSystem | interface | 2 |
| `Timberborn.TutorialSystem.ITutorialStepWithTool` | Timberborn.TutorialSystem | interface | 1 |
| `Timberborn.TutorialSystem.ITutorialTriggers` | Timberborn.TutorialSystem | interface | 2 |
| `Timberborn.UndoSystem.IUndoable` | Timberborn.UndoSystem | interface | 2 |
| `Timberborn.UndoSystem.IUndoPostprocessor` | Timberborn.UndoSystem | interface | 1 |
| `Timberborn.UndoSystem.IUndoRegistry` | Timberborn.UndoSystem | interface | 9 |
| `Timberborn.WalkingSystem.IDestination` | Timberborn.WalkingSystem | interface | 1 |
| `Timberborn.WalkingSystem.INavMeshProximityValidator` | Timberborn.WalkingSystem | interface | 1 |
| `Timberborn.WalkingSystem.IPathStartProvider` | Timberborn.WalkingSystem | interface, provider | 1 |
| `Timberborn.WalkingSystem.IWaterPenaltyModifier` | Timberborn.WalkingSystem | interface | 1 |
| `Timberborn.WaterBuildings.IContaminatedWaterNeedingBuilding` | Timberborn.WaterBuildings | interface | 0 |
| `Timberborn.WaterBuildings.IWaterNeedingBuilding` | Timberborn.WaterBuildings | interface | 1 |
| `Timberborn.WaterObjects.IWaterObjectSpecification` | Timberborn.WaterObjects | interface | 1 |
| `Timberborn.WaterSourceSystem.IWaterStrengthModifier` | Timberborn.WaterSourceSystem | interface | 1 |
| `Timberborn.WaterSystem.IFlowLimiterService` | Timberborn.WaterSystem | interface, service | 2 |
| `Timberborn.WaterSystem.INonThreadSafeWaterService` | Timberborn.WaterSystem | interface, service | 4 |
| `Timberborn.WaterSystem.IThreadSafeWaterEvaporationMap` | Timberborn.WaterSystem | interface | 1 |
| `Timberborn.WaterSystem.IThreadSafeWaterMap` | Timberborn.WaterSystem | interface | 17 |
| `Timberborn.WaterSystem.IWaterRemovalService` | Timberborn.WaterSystem | interface, service | 1 |
| `Timberborn.WaterSystem.IWaterResistor` | Timberborn.WaterSystem | interface | 1 |
| `Timberborn.WaterSystem.IWaterService` | Timberborn.WaterSystem | interface, service | 19 |
| `Timberborn.WaterSystem.IWaterSource` | Timberborn.WaterSystem | interface | 5 |
| `Timberborn.WaterSystemRendering.IWaterMesh` | Timberborn.WaterSystemRendering | interface | 4 |
| `Timberborn.WaterSystemRendering.IWaterRenderer` | Timberborn.WaterSystemRendering | interface | 8 |
| `Timberborn.Wellbeing.IWellbeingTierService` | Timberborn.Wellbeing | interface, service | 3 |
| `Timberborn.WellbeingUI.INeedEffectDescriber` | Timberborn.WellbeingUI | interface, ui | 1 |
| `Timberborn.Wonders.IWonderBlocker` | Timberborn.Wonders | interface | 1 |
| `Timberborn.Workshops.IManufactoryLimiter` | Timberborn.Workshops | interface | 2 |
| `Timberborn.Workshops.IRecipeSelector` | Timberborn.Workshops | interface | 1 |
| `Timberborn.WorkshopsEffects.IWorkshopAnimationSpeedModifier` | Timberborn.WorkshopsEffects | interface | 1 |
| `Timberborn.WorkSystem.IJobBehavior` | Timberborn.WorkSystem | interface | 0 |
| `Timberborn.WorldPersistence.IEntityBatchLoader` | Timberborn.WorldPersistence | interface | 1 |
| `Timberborn.WorldPersistence.IEntityLoader` | Timberborn.WorldPersistence | interface | 4 |
| `Timberborn.WorldPersistence.IEntitySaver` | Timberborn.WorldPersistence | interface | 2 |
| `Timberborn.WorldPersistence.INamedComponent` | Timberborn.WorldPersistence | interface | 1 |
| `Timberborn.WorldPersistence.IPersistentEntity` | Timberborn.WorldPersistence | interface | 2 |
| `Timberborn.WorldPersistence.ISaveableSingleton` | Timberborn.WorldPersistence | interface, singleton | 1 |
| `Timberborn.WorldPersistence.ISerializedWorldSupplier` | Timberborn.WorldPersistence | interface | 1 |
| `Timberborn.WorldPersistence.ISingletonLoader` | Timberborn.WorldPersistence | interface, singleton | 2 |
| `Timberborn.WorldPersistence.ISingletonSaver` | Timberborn.WorldPersistence | interface, singleton | 1 |
| `Timberborn.Yielding.IOrderableYielder` | Timberborn.Yielding | interface | 1 |
| `Timberborn.Yielding.IRemoveYieldStrategy` | Timberborn.Yielding | interface | 3 |
| `Timberborn.Yielding.IYielderDecorable` | Timberborn.Yielding | interface | 1 |
| `Timberborn.Yielding.IYielderRetriever` | Timberborn.Yielding | interface | 1 |

## Publicish Service Index

These are internal or non-public types with public members that look service-like. They are not stable API. They can still be discovered through reflection, singleton repositories, Unity components, or type-name lookup, which makes them useful for research and sometimes necessary for native consequences.

| Type | Kind | Assembly | Tags | Public member count |
| --- | --- | --- | --- | --- |
| `Timberborn.AchievementSystem.AchievementService` | class | Timberborn.AchievementSystem | service, singleton, lifecycle | 1 |
| `Timberborn.AutomationBuildings.SamplingPopulationService` | class | Timberborn.AutomationBuildings | service, singleton | 3 |
| `Timberborn.AutomationBuildings.SamplingResourcesService` | class | Timberborn.AutomationBuildings | service, singleton, lifecycle | 3 |
| `Timberborn.AutomationBuildings.SpringReturnService` | class | Timberborn.AutomationBuildings | service, singleton | 2 |
| `Timberborn.BaseComponentSystem.ComponentCacheService` | class | Timberborn.BaseComponentSystem | service | 2 |
| `Timberborn.Beavers.BeaverNameService` | class | Timberborn.Beavers | service, singleton, lifecycle | 3 |
| `Timberborn.BlockObjectTools.PreviewTerrainCutoutService` | class | Timberborn.BlockObjectTools | service | 4 |
| `Timberborn.BlockSystem.BlockService` | class | Timberborn.BlockSystem | service, singleton, lifecycle | 25 |
| `Timberborn.BlueprintSystem.SpecService` | class | Timberborn.BlueprintSystem | service, singleton, lifecycle | 4 |
| `Timberborn.BuildingsNavigation.BoundsNavRangeDrawingService` | class | Timberborn.BuildingsNavigation | service, singleton, lifecycle | 4 |
| `Timberborn.CameraSystem.CameraServiceSpec` | class | Timberborn.CameraSystem | service | 19 |
| `Timberborn.DecalSystem.DecalService` | class | Timberborn.DecalSystem | service, singleton, lifecycle | 5 |
| `Timberborn.DecalSystem.UserDecalService` | class | Timberborn.DecalSystem | service | 1 |
| `Timberborn.DemolishingUI.DemolishableMarkerService` | class | Timberborn.DemolishingUI | service, singleton, ui, lifecycle | 4 |
| `Timberborn.DemolishingUI.DemolishableMarkerServiceSpec` | class | Timberborn.DemolishingUI | service, ui | 8 |
| `Timberborn.DwellingSystem.StaleAssignableDwellingService` | class | Timberborn.DwellingSystem | service | 4 |
| `Timberborn.EntityNaming.NumberedEntityNamerService` | class | Timberborn.EntityNaming | service, singleton, lifecycle | 3 |
| `Timberborn.ErrorReporting.LoadingIssueService` | class | Timberborn.ErrorReporting | service | 3 |
| `Timberborn.Explosions.ExplosionVisualizerService` | class | Timberborn.Explosions | service, singleton, lifecycle | 4 |
| `Timberborn.FactionValidators.FactionSpecValidationService` | class | Timberborn.FactionValidators | service, singleton, lifecycle | 1 |
| `Timberborn.FireworkSystem.FireworkLaunchService` | class | Timberborn.FireworkSystem | service, singleton | 1 |
| `Timberborn.GameDistrictsUI.DistrictConnectionDrawingService` | class | Timberborn.GameDistrictsUI | service, singleton, ui, lifecycle | 4 |
| `Timberborn.GameDistrictsUI.DistrictConnectionDrawingServiceSpec` | class | Timberborn.GameDistrictsUI | service, ui | 7 |
| `Timberborn.Goods.GoodService` | class | Timberborn.Goods | service, singleton, lifecycle | 7 |
| `Timberborn.Illumination.IlluminationServiceSpec` | class | Timberborn.Illumination | service | 9 |
| `Timberborn.LevelVisibilitySystem.LevelVisibilityService` | class | Timberborn.LevelVisibilitySystem | service, singleton, lifecycle | 11 |
| `Timberborn.LifeSystem.LifeServiceSpec` | class | Timberborn.LifeSystem | service | 8 |
| `Timberborn.Localization.LocalizationService` | class | Timberborn.Localization | service | 3 |
| `Timberborn.Localization.NewLocalizationService` | class | Timberborn.Localization | service, singleton, lifecycle | 2 |
| `Timberborn.MechanicalSystem.BatteryService` | class | Timberborn.MechanicalSystem | service, singleton, tick | 1 |
| `Timberborn.MechanicalSystemHighlighting.MechanicalGraphHighlightService` | class | Timberborn.MechanicalSystemHighlighting | service, singleton, lifecycle | 10 |
| `Timberborn.MechanicalSystemUI.ConsumerFragmentService` | class | Timberborn.MechanicalSystemUI | service, ui | 3 |
| `Timberborn.MechanicalSystemUI.GeneratorFragmentService` | class | Timberborn.MechanicalSystemUI | service, ui | 3 |
| `Timberborn.MechanicalSystemUI.NetworkFragmentService` | class | Timberborn.MechanicalSystemUI | service, ui | 3 |
| `Timberborn.Metrics.MetricsService` | class | Timberborn.Metrics | service, singleton, lifecycle | 5 |
| `Timberborn.ModularShafts.ModularShaftModelService` | class | Timberborn.ModularShafts | service, singleton, lifecycle | 3 |
| `Timberborn.Navigation.DistrictObstacleService` | class | Timberborn.Navigation | service, singleton, lifecycle | 4 |
| `Timberborn.Navigation.DistrictService` | class | Timberborn.Navigation | service | 16 |
| `Timberborn.Navigation.GlobalReachabilityService` | class | Timberborn.Navigation | service, singleton, lifecycle | 4 |
| `Timberborn.Navigation.NavigationCachingService` | class | Timberborn.Navigation | service | 4 |
| `Timberborn.Navigation.NavigationDebuggingService` | class | Timberborn.Navigation | service | 1 |
| `Timberborn.Navigation.NavigationRangeService` | class | Timberborn.Navigation | service | 6 |
| `Timberborn.Navigation.NavigationService` | class | Timberborn.Navigation | service | 14 |
| `Timberborn.Navigation.NavMeshPositionService` | class | Timberborn.Navigation | service | 1 |
| `Timberborn.Navigation.NavMeshService` | class | Timberborn.Navigation | service | 12 |
| `Timberborn.Navigation.NodeIdService` | class | Timberborn.Navigation | service, singleton, lifecycle | 9 |
| `Timberborn.Navigation.PathfindingService` | class | Timberborn.Navigation | service | 7 |
| `Timberborn.Navigation.RoadNavigationRangeService` | class | Timberborn.Navigation | service, singleton | 4 |
| `Timberborn.Navigation.RoadReachabilityService` | class | Timberborn.Navigation | service | 1 |
| `Timberborn.Navigation.RoadSpillNavigationRangeService` | class | Timberborn.Navigation | service | 2 |
| `Timberborn.Navigation.TerrainNavigationRangeService` | class | Timberborn.Navigation | service, singleton | 4 |
| `Timberborn.Navigation.TerrainReachabilityService` | class | Timberborn.Navigation | service | 1 |
| `Timberborn.PathSystem.ConnectionService` | class | Timberborn.PathSystem | service | 1 |
| `Timberborn.PathSystem.PathService` | class | Timberborn.PathSystem | service | 1 |
| `Timberborn.Physics.TransformSyncServiceUnityAdapter` | class | Timberborn.Physics | service | 1 |
| `Timberborn.Planting.PlantableReproductionBlockerService` | class | Timberborn.Planting | service, singleton, lifecycle | 3 |
| `Timberborn.PlantingUI.PlantingSelectionServiceSpec` | class | Timberborn.PlantingUI | service, ui | 9 |
| `Timberborn.PlantingUI.UnlockedPlantableService` | class | Timberborn.PlantingUI | service, singleton, ui, lifecycle | 2 |
| `Timberborn.PlayerDataSystem.PlayerDataService` | class | Timberborn.PlayerDataSystem | service, singleton, lifecycle | 9 |
| `Timberborn.PopulationUI.PopulationServiceDistrictSwitcher` | class | Timberborn.PopulationUI | service, singleton, ui, lifecycle | 3 |
| `Timberborn.RangedEffectSystem.RangedEffectService` | class | Timberborn.RangedEffectSystem | service, singleton, lifecycle | 8 |
| `Timberborn.RecoveredGoodSystem.BuildingGoodsRecoveryService` | class | Timberborn.RecoveredGoodSystem | service, singleton, lifecycle | 2 |
| `Timberborn.ScreenCapturing.ScreenshotService` | class | Timberborn.ScreenCapturing | service, singleton, lifecycle | 3 |
| `Timberborn.SingletonSystem.SingletonLifecycleService` | class | Timberborn.SingletonSystem | service, singleton | 4 |
| `Timberborn.SoakedEffects.SoakedEffectService` | class | Timberborn.SoakedEffects | service, singleton, lifecycle | 2 |
| `Timberborn.SoilContaminationSystem.SoilContaminationService` | class | Timberborn.SoilContaminationSystem | service, singleton, tick, lifecycle | 4 |
| `Timberborn.SoilMoistureSystem.SoilMoistureService` | class | Timberborn.SoilMoistureSystem | service, singleton, tick, lifecycle | 4 |
| `Timberborn.StatusSystem.StatusIconOffsetService` | class | Timberborn.StatusSystem | service, singleton, lifecycle | 11 |
| `Timberborn.StatusSystem.StatusSlotUpdateService` | class | Timberborn.StatusSystem | service | 4 |
| `Timberborn.StockpileVisualization.GoodPileVariantsService` | class | Timberborn.StockpileVisualization | service, singleton, lifecycle | 3 |
| `Timberborn.Terraforming.GroundRaisingService` | class | Timberborn.Terraforming | service, singleton, lifecycle | 2 |
| `Timberborn.TerrainPhysics.TerrainPhysicsService` | class | Timberborn.TerrainPhysics | service, singleton, lifecycle | 8 |
| `Timberborn.TerrainSystem.TerrainService` | class | Timberborn.TerrainSystem | service, singleton, lifecycle | 29 |
| `Timberborn.TerrainSystemRendering.TerrainTopMeshService` | class | Timberborn.TerrainSystemRendering | service, singleton, lifecycle | 2 |
| `Timberborn.TickSystem.ITickableBucketService` | interface | Timberborn.TickSystem | interface, service | 6 |
| `Timberborn.TickSystem.TickableBucketService` | class | Timberborn.TickSystem | service, tick | 6 |
| `Timberborn.TickSystem.TickableSingletonService` | class | Timberborn.TickSystem | service, singleton, tick, lifecycle | 6 |
| `Timberborn.TickSystem.TickService` | class | Timberborn.TickSystem | service, singleton, lifecycle | 2 |
| `Timberborn.TimeSystem.TickProgressService` | class | Timberborn.TimeSystem | service, singleton, tick, lifecycle | 5 |
| `Timberborn.TimeSystem.TimeTriggerService` | class | Timberborn.TimeSystem | service, singleton, tick | 3 |
| `Timberborn.TubeSystem.TubeConnectionService` | class | Timberborn.TubeSystem | service | 1 |
| `Timberborn.TutorialSteps.BuiltBuildingService` | class | Timberborn.TutorialSteps | service, singleton, lifecycle | 9 |
| `Timberborn.TutorialSteps.CameraMovementService` | class | Timberborn.TutorialSteps | service, singleton, lifecycle | 11 |
| `Timberborn.TutorialSteps.FirstbornService` | class | Timberborn.TutorialSteps | service, singleton, lifecycle | 3 |
| `Timberborn.TutorialSteps.VisibleLevelChangeService` | class | Timberborn.TutorialSteps | service, singleton, lifecycle | 6 |
| `Timberborn.TutorialSystem.TutorialService` | class | Timberborn.TutorialSystem | service, singleton, lifecycle | 6 |
| `Timberborn.TutorialSystem.TutorialStageService` | class | Timberborn.TutorialSystem | service, singleton, lifecycle | 2 |
| `Timberborn.WaterBuildings.PreviewWaterInputPipeBlockerService` | class | Timberborn.WaterBuildings | service | 3 |
| `Timberborn.WaterSourceRendering.WaterSourceRenderingService` | class | Timberborn.WaterSourceRendering | service, singleton, tick, lifecycle | 7 |
| `Timberborn.WaterSystem.FlowLimiterService` | class | Timberborn.WaterSystem | service, singleton, tick, lifecycle | 16 |
| `Timberborn.WaterSystem.NonThreadSafeWaterService` | class | Timberborn.WaterSystem | service | 4 |
| `Timberborn.WaterSystem.WaterChangeService` | class | Timberborn.WaterSystem | service, singleton, tick | 4 |
| `Timberborn.WaterSystem.WaterMapBoundaryService` | class | Timberborn.WaterSystem | service | 2 |
| `Timberborn.WaterSystem.WaterService` | class | Timberborn.WaterSystem | service | 19 |
| `Timberborn.Wellbeing.WellbeingTierService` | class | Timberborn.Wellbeing | service, singleton, lifecycle | 4 |
| `Timberborn.WellbeingUI.NeedEffectDescriptionService` | class | Timberborn.WellbeingUI | service, ui | 1 |
| `Timberborn.WellbeingUI.WellbeingServiceDistrictSwitcher` | class | Timberborn.WellbeingUI | service, singleton, ui, lifecycle | 3 |
| `Timberborn.WindSystem.WindServiceSpec` | class | Timberborn.WindSystem | service | 10 |
| `Timberborn.WorkerOutfitSystem.WorkerOutfitService` | class | Timberborn.WorkerOutfitSystem | service, singleton, lifecycle | 2 |
| `Timberborn.Yielding.RemoveYieldStrategySpecService` | class | Timberborn.Yielding | service, singleton, lifecycle | 2 |
| `Timberborn.ZiplineSystem.ZiplineConnectionServiceSpec` | class | Timberborn.ZiplineSystem | service | 7 |

## Reflection And Other Access Techniques

Use reflection only when the public API does not expose the native consequence. Keep the failure loud and specific so missing native support does not turn into silent no-op gameplay.

- Type-name plus public generic lookup: useful for components like `Timberborn.AutomationBuildings.Detonator` and `Timberborn.Explosions.Tunnel` when Wildfire does not directly reference the assembly/type. Resolve the type by full name, close `IBlockService.GetObjectsWithComponentAt<T>`, then invoke the public component method/property.
- Public method on internal type: `Tunnel.BottomLevel`, detonator evaluation/disarm-like behavior, and several UI/debug implementations can be reached this way, but the type itself is not a stable contract.
- Private field/method reflection: current Wildfire support requests include construction rollback state, district flushing, recoverable goods, inventory policy, natural-resource model refresh, workplace behavior ordering, and worker speed effects. Keep those calls isolated behind adapter APIs.
- Singleton repository enumeration: `ISingletonRepository.GetSingletons<T>()` can discover loaded singleton implementations for public lifecycle contracts, listeners, and extension points.
- Unity component traversal: entity components are ordinary Unity/Timberborn components. Prefer `EntityComponent`, `BaseComponent`, `BlockObject`, and native services over raw `GameObject` mutation.
- Resource and asset scanning: particles, timbermesh assets, UXML/USS, shader graph assets, and blueprint content can be discovered through loaded resources or the Modding/Blueprints archive. Use this for visuals and reference, not for simulation truth.
- HTTP and automation hooks: `IHttpApiEndpoint`, `IHttpApiPageSection`, `ISamplingSingleton`, and `ICommittingSingleton` are public-ish extension seams for diagnostics and QA-style control.

## Native Systems Relevant To Wildfire

- Fire simulation should remain host-agnostic. Timberborn owns only importing/exporting world state, rendering, player tools, and consequence adapters.
- World read model: `IBlockService`, `ITerrainService`, `IThreadSafeWaterMap`, `ISoilMoistureService`, `ISoilContaminationService`, `IGoodService`, and natural-resource components provide the best native sampling surface.
- World mutation: `EntityService.Delete`, `IWaterService`, `ITerrainService.SetTerrain/UnsetTerrain/SetField/SetCutout`, construction/building components, and native explosion services are the main mutation surfaces. Prefer these over raw Unity changes.
- Change detection: there is no single universal changed-cells API. Terrain exposes pre/post height events and field/cutout changes; water exposes `IThreadSafeWaterMap.AnyColumnChanged`; placement listeners cover block-object placement changes. Keep a Wildfire dirty-cell or dirty-chunk cache rather than expecting one native diff feed.
- Building aftermath: `BlockObject`, `ConstructionSite`, `Manufactory`, recoverable goods, inventory, district services, and building status systems are interdependent. A structure rollback is not just a visual change.
- Natural-resource aftermath: `Growable`, `Gatherable`, `Yielder`, `NaturalResourceModel`, moisture, contamination, and good stacks are separate components. Kill/delete/refresh flows should use native lifecycle where exposed.
- UI/tools: entity panel fragments, tool button services, bottom-bar providers, tool descriptors, and localizable CoreUI elements are stable enough for native-looking Wildfire UI.

## Footprint By Namespace

This table is a compact footprint map. `Publicish` means the type is not public but has public members and service/system/singleton/provider/factory/UI shape.

| Namespace | Public types | Publicish types | Public interfaces | Service-like types | Singleton-like types |
| --- | --- | --- | --- | --- | --- |
| `System.Runtime` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.AccessibilitySettingsSystem` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.AccessibleNavigation` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.Achievements` | 0 | 13 | 0 | 0 | 25 |
| `Timberborn.AchievementSystem` | 2 | 1 | 1 | 1 | 1 |
| `Timberborn.ActivatorSystem` | 4 | 0 | 1 | 0 | 0 |
| `Timberborn.ActivatorSystemUI` | 0 | 7 | 0 | 0 | 0 |
| `Timberborn.AlertPanelSystem` | 4 | 1 | 1 | 0 | 1 |
| `Timberborn.Analytics` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.AnalyticsUI` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.ApplicationLifetime` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.ApplicationSettingsSystem` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.AreaSelectionSystem` | 15 | 1 | 0 | 0 | 3 |
| `Timberborn.AreaSelectionSystemUI` | 3 | 1 | 0 | 0 | 1 |
| `Timberborn.AssetSystem` | 8 | 0 | 2 | 0 | 0 |
| `Timberborn.Attractions` | 5 | 0 | 0 | 0 | 0 |
| `Timberborn.AttractionsBatchControl` | 0 | 4 | 0 | 0 | 1 |
| `Timberborn.AttractionsUI` | 4 | 5 | 0 | 0 | 0 |
| `Timberborn.Automation` | 22 | 2 | 10 | 0 | 5 |
| `Timberborn.AutomationBuildings` | 39 | 8 | 0 | 4 | 9 |
| `Timberborn.AutomationBuildingsUI` | 2 | 29 | 0 | 0 | 7 |
| `Timberborn.AutomationUI` | 10 | 9 | 0 | 0 | 2 |
| `Timberborn.Autosaving` | 4 | 0 | 1 | 1 | 1 |
| `Timberborn.AutosavingUI` | 0 | 3 | 0 | 0 | 3 |
| `Timberborn.BaseComponentSystem` | 7 | 1 | 4 | 1 | 0 |
| `Timberborn.BatchControl` | 18 | 5 | 6 | 0 | 6 |
| `Timberborn.BeaverContaminationSystem` | 6 | 0 | 0 | 0 | 1 |
| `Timberborn.BeaverContaminationSystemUI` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.Beavers` | 10 | 1 | 0 | 1 | 3 |
| `Timberborn.BeaversUI` | 6 | 8 | 0 | 0 | 0 |
| `Timberborn.BehaviorSystem` | 11 | 0 | 1 | 0 | 0 |
| `Timberborn.BehaviorSystemUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.Benchmarking` | 2 | 5 | 0 | 0 | 5 |
| `Timberborn.BenchmarkingUI` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.BlockingSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.BlockObjectAccesses` | 8 | 0 | 0 | 0 | 0 |
| `Timberborn.BlockObjectModelSystem` | 6 | 0 | 3 | 0 | 0 |
| `Timberborn.BlockObjectPickingSystem` | 10 | 0 | 0 | 0 | 0 |
| `Timberborn.BlockObjectTools` | 17 | 1 | 2 | 4 | 5 |
| `Timberborn.BlockObjectToolsUI` | 2 | 3 | 0 | 0 | 1 |
| `Timberborn.BlockObstacles` | 4 | 0 | 0 | 0 | 1 |
| `Timberborn.BlockSystem` | 61 | 2 | 15 | 9 | 2 |
| `Timberborn.BlockSystemNavigation` | 3 | 1 | 1 | 0 | 1 |
| `Timberborn.BlockSystemUI` | 6 | 3 | 0 | 0 | 1 |
| `Timberborn.BlueprintPrefabSystem` | 2 | 0 | 1 | 0 | 0 |
| `Timberborn.BlueprintSystem` | 21 | 2 | 3 | 3 | 1 |
| `Timberborn.BlueprintUISystem` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.BonusSystem` | 6 | 0 | 0 | 1 | 2 |
| `Timberborn.BonusSystemUI` | 2 | 1 | 0 | 0 | 0 |
| `Timberborn.Bots` | 5 | 0 | 0 | 0 | 3 |
| `Timberborn.BotsUI` | 3 | 3 | 0 | 0 | 0 |
| `Timberborn.BottomBarSystem` | 4 | 1 | 1 | 0 | 1 |
| `Timberborn.BotUpkeep` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.Brushes` | 8 | 0 | 4 | 0 | 1 |
| `Timberborn.BrushesUI` | 0 | 6 | 0 | 0 | 0 |
| `Timberborn.BuilderHubSystem` | 3 | 1 | 1 | 0 | 0 |
| `Timberborn.BuilderPrioritySystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.BuilderPrioritySystemUI` | 2 | 8 | 0 | 0 | 1 |
| `Timberborn.BuildingAvailability` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.BuildingDoorsteps` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.BuildingRange` | 3 | 0 | 1 | 0 | 0 |
| `Timberborn.Buildings` | 15 | 0 | 3 | 1 | 2 |
| `Timberborn.BuildingsNavigation` | 9 | 5 | 1 | 1 | 5 |
| `Timberborn.BuildingsReachability` | 6 | 0 | 3 | 0 | 0 |
| `Timberborn.BuildingStatuses` | 1 | 1 | 0 | 0 | 1 |
| `Timberborn.BuildingsUI` | 5 | 4 | 0 | 0 | 1 |
| `Timberborn.BuildingTools` | 4 | 0 | 1 | 0 | 0 |
| `Timberborn.CameraSettingsSystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.CameraSystem` | 11 | 9 | 1 | 2 | 11 |
| `Timberborn.CameraWorldState` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.Carrying` | 8 | 1 | 0 | 0 | 0 |
| `Timberborn.CarryingUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.CharacterControlSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.CharacterControlSystemUI` | 1 | 4 | 0 | 0 | 0 |
| `Timberborn.CharacterModelSystem` | 4 | 1 | 0 | 0 | 1 |
| `Timberborn.CharacterMovementSystem` | 11 | 0 | 1 | 0 | 0 |
| `Timberborn.CharacterMovementSystemUI` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.CharacterNavigation` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.Characters` | 7 | 1 | 1 | 0 | 2 |
| `Timberborn.CharactersBatchControl` | 0 | 3 | 0 | 0 | 1 |
| `Timberborn.CharactersGame` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.CharactersUI` | 4 | 1 | 0 | 0 | 0 |
| `Timberborn.CommandLine` | 2 | 0 | 1 | 0 | 0 |
| `Timberborn.Common` | 38 | 1 | 3 | 0 | 0 |
| `Timberborn.Console` | 1 | 2 | 1 | 0 | 1 |
| `Timberborn.ConstructionGuidelines` | 4 | 2 | 1 | 1 | 2 |
| `Timberborn.ConstructionGuidelinesUI` | 1 | 1 | 0 | 0 | 2 |
| `Timberborn.ConstructionMode` | 4 | 0 | 1 | 1 | 1 |
| `Timberborn.ConstructionSites` | 17 | 1 | 2 | 0 | 1 |
| `Timberborn.ConstructionSitesUI` | 4 | 4 | 0 | 0 | 1 |
| `Timberborn.Coordinates` | 21 | 0 | 0 | 0 | 0 |
| `Timberborn.CoreSound` | 4 | 1 | 0 | 0 | 3 |
| `Timberborn.CoreUI` | 58 | 11 | 4 | 1 | 3 |
| `Timberborn.CursorToolSystem` | 6 | 3 | 0 | 0 | 3 |
| `Timberborn.Cutting` | 4 | 1 | 0 | 0 | 0 |
| `Timberborn.DeathSystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.Debugging` | 11 | 0 | 1 | 0 | 2 |
| `Timberborn.DebuggingUI` | 4 | 11 | 1 | 0 | 4 |
| `Timberborn.DecalSystem` | 6 | 2 | 1 | 3 | 2 |
| `Timberborn.DecalSystemUI` | 0 | 6 | 0 | 0 | 0 |
| `Timberborn.DeconstructionSystem` | 3 | 2 | 0 | 0 | 2 |
| `Timberborn.DeconstructionSystemUI` | 1 | 2 | 0 | 0 | 2 |
| `Timberborn.Demolishing` | 15 | 2 | 0 | 0 | 1 |
| `Timberborn.DemolishingToolGroupSystem` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.DemolishingUI` | 4 | 6 | 0 | 2 | 3 |
| `Timberborn.DeteriorationSystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.DeteriorationSystemUI` | 1 | 4 | 0 | 0 | 0 |
| `Timberborn.Diagnostics` | 5 | 0 | 0 | 0 | 2 |
| `Timberborn.DiagnosticsUI` | 2 | 4 | 0 | 0 | 2 |
| `Timberborn.DistributionSystem` | 8 | 1 | 0 | 0 | 0 |
| `Timberborn.DistributionSystemBatchControl` | 0 | 9 | 0 | 0 | 1 |
| `Timberborn.DistributionSystemUI` | 2 | 3 | 0 | 0 | 0 |
| `Timberborn.DropdownSystem` | 11 | 0 | 3 | 0 | 1 |
| `Timberborn.DuplicationSystem` | 4 | 0 | 2 | 0 | 0 |
| `Timberborn.DuplicationSystemUI` | 0 | 7 | 0 | 0 | 2 |
| `Timberborn.DwellingSystem` | 4 | 2 | 0 | 1 | 2 |
| `Timberborn.DwellingSystemUI` | 2 | 6 | 0 | 0 | 0 |
| `Timberborn.Effects` | 6 | 0 | 0 | 0 | 0 |
| `Timberborn.Emptying` | 12 | 0 | 0 | 0 | 0 |
| `Timberborn.EnterableSystem` | 12 | 0 | 1 | 0 | 0 |
| `Timberborn.EntityNaming` | 8 | 1 | 1 | 2 | 1 |
| `Timberborn.EntityNamingUI` | 1 | 1 | 0 | 0 | 0 |
| `Timberborn.EntityPanelSystem` | 15 | 5 | 4 | 2 | 2 |
| `Timberborn.EntitySystem` | 18 | 0 | 6 | 2 | 0 |
| `Timberborn.EntityUndoSystem` | 6 | 1 | 0 | 0 | 1 |
| `Timberborn.ErrorReporting` | 8 | 2 | 1 | 3 | 1 |
| `Timberborn.ErrorReportingUI` | 1 | 3 | 0 | 0 | 2 |
| `Timberborn.ExperimentalModeSystem` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.Explosions` | 12 | 1 | 0 | 2 | 2 |
| `Timberborn.ExplosionsUI` | 1 | 4 | 0 | 0 | 0 |
| `Timberborn.FactionGoalsSystem` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.FactionSystem` | 7 | 0 | 0 | 2 | 1 |
| `Timberborn.FactionValidators` | 0 | 2 | 0 | 1 | 1 |
| `Timberborn.FeatureToggleSystem` | 3 | 0 | 0 | 1 | 0 |
| `Timberborn.Fields` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.FieldsUI` | 3 | 5 | 0 | 0 | 0 |
| `Timberborn.FileBrowsing` | 6 | 0 | 0 | 0 | 3 |
| `Timberborn.FileSystem` | 4 | 0 | 1 | 2 | 0 |
| `Timberborn.FireworkSystem` | 5 | 2 | 0 | 2 | 2 |
| `Timberborn.FireworkSystemUI` | 0 | 4 | 0 | 0 | 0 |
| `Timberborn.Forestry` | 12 | 0 | 0 | 0 | 1 |
| `Timberborn.ForestryUI` | 3 | 8 | 0 | 0 | 3 |
| `Timberborn.GameBlockObjectButtonsSystem` | 1 | 1 | 0 | 0 | 0 |
| `Timberborn.GameCycleSystem` | 5 | 0 | 1 | 1 | 1 |
| `Timberborn.GameDistricts` | 28 | 2 | 1 | 1 | 5 |
| `Timberborn.GameDistrictsBatchControl` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.GameDistrictsMigration` | 14 | 1 | 1 | 1 | 3 |
| `Timberborn.GameDistrictsMigrationBatchControl` | 4 | 11 | 0 | 0 | 1 |
| `Timberborn.GameDistrictsUI` | 1 | 17 | 0 | 2 | 5 |
| `Timberborn.GameExitSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.GameFactionSystem` | 5 | 4 | 0 | 3 | 4 |
| `Timberborn.GameFactionSystemUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.GameGoods` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.GameOver` | 2 | 2 | 1 | 0 | 2 |
| `Timberborn.GameOverUI` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.GameSaveRepositorySystem` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.GameSaveRepositorySystemUI` | 12 | 3 | 1 | 1 | 3 |
| `Timberborn.GameSaveRuntimeSystem` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.GameSaveRuntimeSystemUI` | 2 | 1 | 0 | 0 | 1 |
| `Timberborn.GameScene` | 4 | 1 | 0 | 0 | 2 |
| `Timberborn.GameSceneLoading` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.GameSound` | 3 | 5 | 0 | 0 | 6 |
| `Timberborn.GameStartup` | 6 | 7 | 1 | 0 | 6 |
| `Timberborn.GameStockpiles` | 0 | 2 | 0 | 0 | 1 |
| `Timberborn.GameWaterSourceSystem` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.GameWonderCompletion` | 6 | 1 | 0 | 2 | 3 |
| `Timberborn.GameWonderCompletionUI` | 2 | 1 | 0 | 0 | 1 |
| `Timberborn.Gathering` | 5 | 0 | 0 | 0 | 0 |
| `Timberborn.GatheringUI` | 3 | 4 | 0 | 0 | 0 |
| `Timberborn.GoodCollectionSystem` | 3 | 0 | 1 | 0 | 0 |
| `Timberborn.GoodConsumingBuildingSystem` | 3 | 1 | 0 | 0 | 0 |
| `Timberborn.GoodConsumingBuildingSystemUI` | 1 | 2 | 0 | 0 | 0 |
| `Timberborn.Goods` | 19 | 1 | 3 | 3 | 2 |
| `Timberborn.GoodStackSystem` | 6 | 1 | 2 | 2 | 1 |
| `Timberborn.GoodStackSystemUI` | 1 | 1 | 0 | 0 | 0 |
| `Timberborn.GoodsUI` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.GraphicsQualitySystem` | 6 | 4 | 0 | 0 | 7 |
| `Timberborn.GridTraversing` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.Growing` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.GrowingUI` | 2 | 1 | 0 | 0 | 0 |
| `Timberborn.Hauling` | 9 | 0 | 1 | 0 | 0 |
| `Timberborn.HaulingUI` | 1 | 3 | 0 | 0 | 0 |
| `Timberborn.HazardousWeatherSystem` | 11 | 0 | 1 | 1 | 4 |
| `Timberborn.HazardousWeatherSystemUI` | 5 | 4 | 0 | 0 | 4 |
| `Timberborn.Healthcare` | 1 | 1 | 0 | 0 | 0 |
| `Timberborn.HousingBatchControl` | 0 | 3 | 0 | 0 | 1 |
| `Timberborn.HttpApiSystem` | 8 | 8 | 2 | 0 | 8 |
| `Timberborn.HttpApiSystemUI` | 0 | 4 | 0 | 0 | 0 |
| `Timberborn.Illumination` | 5 | 1 | 0 | 2 | 1 |
| `Timberborn.IlluminationUI` | 1 | 4 | 0 | 0 | 0 |
| `Timberborn.InputSystem` | 13 | 1 | 3 | 3 | 7 |
| `Timberborn.InputSystemUI` | 4 | 1 | 0 | 0 | 1 |
| `Timberborn.Intro` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.IntroSettingsSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.InventoryNeedSystem` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.InventorySystem` | 19 | 0 | 4 | 0 | 0 |
| `Timberborn.InventorySystemBatchControl` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.InventorySystemUI` | 6 | 5 | 0 | 0 | 1 |
| `Timberborn.KeyBindingSystem` | 25 | 1 | 1 | 4 | 6 |
| `Timberborn.KeyBindingSystemUI` | 12 | 0 | 0 | 1 | 2 |
| `Timberborn.LaborSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.Language` | 1 | 1 | 0 | 0 | 1 |
| `Timberborn.LanguageUI` | 1 | 1 | 0 | 0 | 1 |
| `Timberborn.LevelVisibilitySystem` | 3 | 1 | 1 | 2 | 1 |
| `Timberborn.LevelVisibilitySystemUI` | 1 | 4 | 1 | 0 | 3 |
| `Timberborn.LifeSystem` | 3 | 1 | 1 | 2 | 1 |
| `Timberborn.LinkedBuildingSystem` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.LinkedBuildingSystemUI` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.Localization` | 7 | 3 | 3 | 3 | 1 |
| `Timberborn.LocalizationSerialization` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.MainMenuModdingUI` | 5 | 3 | 0 | 0 | 3 |
| `Timberborn.MainMenuPanels` | 8 | 0 | 0 | 0 | 6 |
| `Timberborn.MainMenuScene` | 0 | 2 | 0 | 0 | 2 |
| `Timberborn.MainMenuSceneLoading` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.MapEditorBrushesUI` | 4 | 3 | 0 | 1 | 4 |
| `Timberborn.MapEditorConstructionGuidelinesUI` | 1 | 1 | 1 | 0 | 1 |
| `Timberborn.MapEditorDemolishingUI` | 0 | 3 | 0 | 0 | 0 |
| `Timberborn.MapEditorHazardousWeatherUI` | 1 | 5 | 0 | 0 | 1 |
| `Timberborn.MapEditorNaturalResources` | 3 | 0 | 0 | 1 | 0 |
| `Timberborn.MapEditorNaturalResourcesUI` | 3 | 4 | 0 | 0 | 3 |
| `Timberborn.MapEditorPersistence` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.MapEditorPersistenceUI` | 4 | 0 | 0 | 0 | 1 |
| `Timberborn.MapEditorPlacementRandomizing` | 2 | 0 | 0 | 1 | 0 |
| `Timberborn.MapEditorPlacementRandomizingUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.MapEditorScene` | 3 | 0 | 0 | 0 | 2 |
| `Timberborn.MapEditorSceneLoading` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.MapEditorSimulationSystem` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.MapEditorSimulationSystemUI` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.MapEditorStartup` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.MapEditorStockpilesUI` | 0 | 5 | 0 | 0 | 2 |
| `Timberborn.MapEditorTickSystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.MapEditorUI` | 1 | 9 | 0 | 0 | 2 |
| `Timberborn.MapEditorWaterSourceSystemUI` | 1 | 3 | 0 | 0 | 0 |
| `Timberborn.MapIndexSystem` | 7 | 0 | 0 | 1 | 1 |
| `Timberborn.MapItemsUI` | 8 | 0 | 1 | 0 | 2 |
| `Timberborn.MapMetadataSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.MapMetadataSystemUI` | 1 | 4 | 0 | 0 | 2 |
| `Timberborn.MapRepositorySystem` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.MapRepositorySystemUI` | 8 | 2 | 1 | 1 | 4 |
| `Timberborn.MapStateSystem` | 3 | 0 | 0 | 0 | 1 |
| `Timberborn.MapSystem` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.MapThumbnail` | 4 | 0 | 0 | 0 | 1 |
| `Timberborn.MapThumbnailCapturing` | 5 | 5 | 0 | 0 | 2 |
| `Timberborn.MapThumbnailCapturingUI` | 1 | 2 | 0 | 0 | 2 |
| `Timberborn.MapThumbnailOverlaySystem` | 2 | 0 | 0 | 0 | 1 |
| `Timberborn.MechanicalConnectorSystem` | 0 | 2 | 0 | 0 | 1 |
| `Timberborn.MechanicalSystem` | 16 | 2 | 1 | 1 | 3 |
| `Timberborn.MechanicalSystemHighlighting` | 0 | 1 | 0 | 1 | 1 |
| `Timberborn.MechanicalSystemUI` | 4 | 23 | 1 | 3 | 2 |
| `Timberborn.Metrics` | 3 | 2 | 2 | 2 | 1 |
| `Timberborn.Modding` | 13 | 0 | 1 | 0 | 1 |
| `Timberborn.ModdingAssets` | 2 | 3 | 0 | 0 | 3 |
| `Timberborn.ModdingUI` | 5 | 1 | 2 | 0 | 0 |
| `Timberborn.ModelHiding` | 2 | 2 | 1 | 0 | 2 |
| `Timberborn.ModManagerScene` | 3 | 0 | 2 | 0 | 0 |
| `Timberborn.ModManagerSceneUI` | 1 | 2 | 0 | 0 | 0 |
| `Timberborn.ModularShafts` | 0 | 5 | 0 | 1 | 5 |
| `Timberborn.MortalComponents` | 2 | 0 | 1 | 0 | 0 |
| `Timberborn.MortalSystem` | 8 | 0 | 0 | 1 | 1 |
| `Timberborn.MortalSystemUI` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.Multithreading` | 8 | 3 | 5 | 0 | 2 |
| `Timberborn.MultithreadingAnalysis` | 4 | 0 | 0 | 0 | 1 |
| `Timberborn.MultithreadingAnalysisUI` | 0 | 11 | 0 | 0 | 1 |
| `Timberborn.NaturalResources` | 8 | 0 | 1 | 1 | 0 |
| `Timberborn.NaturalResourcesContamination` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.NaturalResourcesContaminationUI` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.NaturalResourcesLifecycle` | 4 | 0 | 1 | 0 | 0 |
| `Timberborn.NaturalResourcesLifecycleModelSystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.NaturalResourcesLifecycleUI` | 0 | 3 | 0 | 0 | 0 |
| `Timberborn.NaturalResourcesModelSystem` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.NaturalResourcesMoisture` | 7 | 0 | 0 | 1 | 1 |
| `Timberborn.NaturalResourcesMoistureUI` | 0 | 4 | 0 | 0 | 0 |
| `Timberborn.NaturalResourcesReproduction` | 4 | 0 | 0 | 0 | 2 |
| `Timberborn.NaturalResourcesUI` | 2 | 2 | 0 | 0 | 0 |
| `Timberborn.Navigation` | 41 | 42 | 23 | 29 | 39 |
| `Timberborn.NavigationUI` | 2 | 0 | 0 | 0 | 2 |
| `Timberborn.NeedApplication` | 8 | 0 | 2 | 1 | 1 |
| `Timberborn.NeedApplicationUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.NeedBehaviorSystem` | 16 | 0 | 1 | 1 | 0 |
| `Timberborn.NeedCollectionSystem` | 3 | 0 | 1 | 0 | 0 |
| `Timberborn.NeedSpecs` | 10 | 0 | 0 | 1 | 2 |
| `Timberborn.NeedSuspending` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.NeedSystem` | 9 | 0 | 0 | 0 | 0 |
| `Timberborn.NewGameConfigurationSystem` | 4 | 0 | 0 | 1 | 1 |
| `Timberborn.NotificationSystem` | 5 | 0 | 0 | 0 | 1 |
| `Timberborn.NotificationSystemUI` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.Options` | 2 | 1 | 1 | 0 | 0 |
| `Timberborn.OptionsGame` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.Particles` | 7 | 0 | 2 | 0 | 1 |
| `Timberborn.PathSystem` | 9 | 5 | 3 | 4 | 2 |
| `Timberborn.PathSystemUI` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.Persistence` | 16 | 0 | 5 | 0 | 0 |
| `Timberborn.Physics` | 0 | 1 | 0 | 1 | 0 |
| `Timberborn.Planting` | 20 | 2 | 2 | 2 | 3 |
| `Timberborn.PlantingUI` | 11 | 12 | 0 | 5 | 7 |
| `Timberborn.PlatformUtilities` | 8 | 0 | 1 | 0 | 0 |
| `Timberborn.PlayerDataSystem` | 2 | 1 | 1 | 3 | 1 |
| `Timberborn.Pollination` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.Population` | 8 | 0 | 0 | 1 | 1 |
| `Timberborn.PopulationStatisticsSystem` | 8 | 0 | 4 | 0 | 0 |
| `Timberborn.PopulationUI` | 5 | 4 | 1 | 1 | 2 |
| `Timberborn.PopulationWorkStatistics` | 4 | 0 | 0 | 0 | 2 |
| `Timberborn.PowerBatchControl` | 0 | 3 | 0 | 0 | 1 |
| `Timberborn.PowerGeneration` | 5 | 0 | 0 | 0 | 0 |
| `Timberborn.PowerGenerationUI` | 0 | 13 | 0 | 0 | 1 |
| `Timberborn.PowerManagement` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.PowerManagementUI` | 0 | 5 | 0 | 0 | 0 |
| `Timberborn.PrefabOptimization` | 19 | 3 | 3 | 0 | 1 |
| `Timberborn.PrioritySystem` | 5 | 0 | 1 | 0 | 0 |
| `Timberborn.PrioritySystemUI` | 6 | 1 | 1 | 0 | 1 |
| `Timberborn.QuickNotificationSystem` | 2 | 1 | 0 | 1 | 1 |
| `Timberborn.RangedEffectBuildingUI` | 4 | 1 | 0 | 3 | 3 |
| `Timberborn.RangedEffectSystem` | 1 | 1 | 0 | 1 | 1 |
| `Timberborn.RecoverableGoodSystem` | 4 | 0 | 1 | 1 | 1 |
| `Timberborn.RecoverableGoodSystemUI` | 7 | 0 | 1 | 0 | 1 |
| `Timberborn.RecoveredGoodSystem` | 2 | 6 | 0 | 1 | 5 |
| `Timberborn.RecoveredGoodSystemUI` | 1 | 4 | 0 | 0 | 1 |
| `Timberborn.RelationSystem` | 1 | 0 | 1 | 0 | 0 |
| `Timberborn.RelationSystemUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.Rendering` | 15 | 4 | 0 | 1 | 6 |
| `Timberborn.Reproduction` | 5 | 1 | 0 | 1 | 0 |
| `Timberborn.ReproductionUI` | 5 | 2 | 0 | 0 | 0 |
| `Timberborn.ReservableSystem` | 5 | 0 | 0 | 0 | 0 |
| `Timberborn.ResourceCountingSystem` | 4 | 0 | 1 | 1 | 1 |
| `Timberborn.ResourceCountingSystemUI` | 1 | 0 | 0 | 1 | 1 |
| `Timberborn.RootProviders` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.Ruins` | 4 | 1 | 0 | 0 | 2 |
| `Timberborn.RuinsModelShuffling` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.RuinsUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.SaveMetadataSystem` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.SaveSystem` | 6 | 0 | 4 | 0 | 0 |
| `Timberborn.SaveThumbnail` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.SaveThumbnailCapturing` | 1 | 1 | 0 | 0 | 1 |
| `Timberborn.SceneLoading` | 4 | 1 | 2 | 0 | 2 |
| `Timberborn.ScienceSystem` | 6 | 0 | 0 | 2 | 2 |
| `Timberborn.ScienceSystemUI` | 6 | 2 | 0 | 0 | 0 |
| `Timberborn.ScreenCapturing` | 0 | 1 | 0 | 1 | 1 |
| `Timberborn.ScreenSystem` | 4 | 2 | 0 | 0 | 4 |
| `Timberborn.SelectionSystem` | 15 | 0 | 2 | 3 | 4 |
| `Timberborn.SelectionToolSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.SerializationSystem` | 6 | 0 | 0 | 0 | 0 |
| `Timberborn.SettingsSystem` | 2 | 0 | 1 | 0 | 0 |
| `Timberborn.SettingsSystemUI` | 2 | 25 | 1 | 0 | 14 |
| `Timberborn.SettlementNameSystem` | 1 | 0 | 0 | 1 | 1 |
| `Timberborn.SettlementNameSystemUI` | 1 | 1 | 0 | 0 | 0 |
| `Timberborn.SettlementStatistics` | 4 | 9 | 0 | 0 | 10 |
| `Timberborn.SimpleOutputBuildings` | 2 | 1 | 0 | 0 | 0 |
| `Timberborn.SimpleOutputBuildingsUI` | 1 | 2 | 0 | 0 | 0 |
| `Timberborn.SimulationSystem` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.SingletonSystem` | 12 | 4 | 8 | 1 | 15 |
| `Timberborn.SkySystem` | 4 | 3 | 0 | 0 | 4 |
| `Timberborn.SleepSystem` | 4 | 0 | 0 | 0 | 1 |
| `Timberborn.SliderToggleSystem` | 6 | 0 | 0 | 0 | 0 |
| `Timberborn.SlotSystem` | 15 | 0 | 2 | 0 | 0 |
| `Timberborn.SoakedEffects` | 0 | 1 | 0 | 1 | 1 |
| `Timberborn.SoilBarrierSystem` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.SoilContaminationSystem` | 2 | 3 | 1 | 2 | 3 |
| `Timberborn.SoilMoistureSystem` | 2 | 5 | 1 | 2 | 5 |
| `Timberborn.SoundSettingsSystem` | 1 | 2 | 0 | 0 | 2 |
| `Timberborn.SoundSystem` | 3 | 4 | 2 | 1 | 3 |
| `Timberborn.SpriteOperations` | 3 | 2 | 0 | 0 | 2 |
| `Timberborn.StartingLocationSystem` | 4 | 0 | 0 | 1 | 1 |
| `Timberborn.StatusSystem` | 22 | 4 | 3 | 3 | 5 |
| `Timberborn.StatusSystemUI` | 2 | 10 | 0 | 0 | 2 |
| `Timberborn.SteamOSUI` | 0 | 2 | 0 | 0 | 1 |
| `Timberborn.SteamOverlaySystem` | 1 | 1 | 0 | 0 | 1 |
| `Timberborn.SteamStoreSystem` | 2 | 0 | 0 | 0 | 1 |
| `Timberborn.SteamWorkshop` | 12 | 1 | 0 | 0 | 1 |
| `Timberborn.SteamWorkshopContent` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.SteamWorkshopMapDownloading` | 0 | 2 | 0 | 0 | 2 |
| `Timberborn.SteamWorkshopMapDownloadingUI` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.SteamWorkshopMapUploadingUI` | 1 | 4 | 0 | 1 | 2 |
| `Timberborn.SteamWorkshopModDownloading` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.SteamWorkshopModDownloadingUI` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.SteamWorkshopModUploadingUI` | 0 | 5 | 0 | 0 | 1 |
| `Timberborn.SteamWorkshopUI` | 8 | 0 | 1 | 0 | 2 |
| `Timberborn.StockpilePrioritySystem` | 6 | 1 | 0 | 0 | 0 |
| `Timberborn.StockpilePriorityUISystem` | 4 | 2 | 0 | 0 | 0 |
| `Timberborn.Stockpiles` | 5 | 1 | 0 | 0 | 0 |
| `Timberborn.StockpilesUI` | 5 | 23 | 1 | 1 | 6 |
| `Timberborn.StockpileVisualization` | 7 | 2 | 0 | 3 | 3 |
| `Timberborn.StockpileVisualizationUI` | 0 | 2 | 0 | 0 | 0 |
| `Timberborn.StorageBatchControl` | 0 | 3 | 0 | 0 | 1 |
| `Timberborn.StoreSystem` | 1 | 0 | 1 | 0 | 0 |
| `Timberborn.TailDecalSystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.TemplateAttachmentSystem` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.TemplateCollectionSystem` | 3 | 1 | 1 | 1 | 1 |
| `Timberborn.TemplateInstantiation` | 8 | 0 | 1 | 0 | 0 |
| `Timberborn.TemplateSystem` | 7 | 0 | 0 | 2 | 1 |
| `Timberborn.Terraforming` | 3 | 1 | 0 | 1 | 1 |
| `Timberborn.TerrainLevelValidation` | 3 | 0 | 1 | 0 | 0 |
| `Timberborn.TerrainNavigationSystem` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.TerrainPhysics` | 6 | 4 | 2 | 2 | 4 |
| `Timberborn.TerrainQueryingSystem` | 2 | 0 | 0 | 1 | 0 |
| `Timberborn.TerrainSystem` | 17 | 3 | 5 | 2 | 4 |
| `Timberborn.TerrainSystemRendering` | 9 | 5 | 0 | 2 | 7 |
| `Timberborn.TerrainSystemUI` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.TerrainUndoSystem` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.TextureOperations` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.ThumbnailCapturing` | 5 | 0 | 2 | 0 | 1 |
| `Timberborn.ThumbnailSystem` | 3 | 0 | 1 | 0 | 0 |
| `Timberborn.TickSystem` | 12 | 6 | 6 | 7 | 9 |
| `Timberborn.TickSystemUI` | 0 | 1 | 0 | 0 | 1 |
| `Timberborn.Timbermesh` | 9 | 0 | 2 | 0 | 0 |
| `Timberborn.TimbermeshAnimations` | 2 | 3 | 2 | 0 | 2 |
| `Timberborn.TimbermeshDTO` | 12 | 0 | 1 | 0 | 0 |
| `Timberborn.TimbermeshEditorTools` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.TimbermeshMaterials` | 2 | 2 | 1 | 0 | 1 |
| `Timberborn.TimeSpeedButtonSystem` | 3 | 0 | 0 | 0 | 1 |
| `Timberborn.TimeSystem` | 13 | 4 | 4 | 3 | 6 |
| `Timberborn.TimeSystemUI` | 4 | 2 | 0 | 0 | 5 |
| `Timberborn.TitleScreenUI` | 2 | 2 | 0 | 0 | 2 |
| `Timberborn.ToolButtonSystem` | 7 | 1 | 1 | 1 | 2 |
| `Timberborn.ToolPanelSystem` | 4 | 1 | 1 | 0 | 1 |
| `Timberborn.ToolSystem` | 19 | 0 | 7 | 3 | 2 |
| `Timberborn.ToolSystemUI` | 5 | 6 | 2 | 0 | 3 |
| `Timberborn.TooltipSystem` | 3 | 2 | 1 | 0 | 2 |
| `Timberborn.TopBarSystem` | 1 | 4 | 0 | 0 | 1 |
| `Timberborn.TransformControl` | 4 | 0 | 0 | 0 | 0 |
| `Timberborn.TubeSystem` | 2 | 3 | 0 | 1 | 2 |
| `Timberborn.TutorialSettingsSystem` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.TutorialSteps` | 2 | 15 | 0 | 4 | 13 |
| `Timberborn.TutorialSystem` | 11 | 3 | 5 | 3 | 3 |
| `Timberborn.TutorialSystemUI` | 1 | 10 | 0 | 0 | 2 |
| `Timberborn.UIFormatters` | 5 | 0 | 0 | 0 | 0 |
| `Timberborn.UILayoutSystem` | 4 | 1 | 0 | 0 | 4 |
| `Timberborn.UISound` | 1 | 0 | 0 | 0 | 1 |
| `Timberborn.UnderstructureSystem` | 2 | 0 | 0 | 0 | 0 |
| `Timberborn.UndoSystem` | 4 | 1 | 3 | 0 | 1 |
| `Timberborn.UnityEngineSpecs` | 7 | 0 | 0 | 0 | 0 |
| `Timberborn.Versioning` | 3 | 0 | 0 | 1 | 0 |
| `Timberborn.VersioningSerialization` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.WalkingSystem` | 22 | 0 | 4 | 1 | 0 |
| `Timberborn.WalkingSystemUI` | 1 | 1 | 0 | 0 | 1 |
| `Timberborn.Wandering` | 3 | 0 | 0 | 0 | 0 |
| `Timberborn.WaterBrushesUI` | 1 | 4 | 0 | 0 | 1 |
| `Timberborn.WaterBuildings` | 22 | 2 | 2 | 2 | 1 |
| `Timberborn.WaterBuildingsUI` | 0 | 26 | 0 | 0 | 1 |
| `Timberborn.WaterContaminationBuildings` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.WaterContaminationBuildingsUI` | 0 | 1 | 0 | 0 | 0 |
| `Timberborn.WaterObjects` | 7 | 0 | 1 | 1 | 1 |
| `Timberborn.WaterSourceRendering` | 0 | 1 | 0 | 1 | 1 |
| `Timberborn.WaterSourceSystem` | 12 | 0 | 1 | 1 | 1 |
| `Timberborn.WaterSourceSystemUI` | 2 | 5 | 0 | 0 | 0 |
| `Timberborn.WaterSystem` | 15 | 12 | 8 | 9 | 9 |
| `Timberborn.WaterSystemRendering` | 6 | 8 | 2 | 1 | 7 |
| `Timberborn.WaterSystemRenderingUI` | 0 | 2 | 0 | 0 | 2 |
| `Timberborn.WaterSystemUI` | 0 | 3 | 0 | 0 | 2 |
| `Timberborn.WeatherSystem` | 3 | 0 | 0 | 2 | 2 |
| `Timberborn.WeatherSystemUI` | 0 | 5 | 0 | 0 | 4 |
| `Timberborn.WebNavigation` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.Wellbeing` | 7 | 2 | 1 | 4 | 4 |
| `Timberborn.WellbeingUI` | 16 | 13 | 1 | 2 | 5 |
| `Timberborn.WindSystem` | 3 | 1 | 0 | 2 | 1 |
| `Timberborn.WindSystemUI` | 1 | 2 | 0 | 0 | 0 |
| `Timberborn.WonderCompletion` | 1 | 0 | 0 | 1 | 0 |
| `Timberborn.Wonders` | 9 | 2 | 1 | 0 | 0 |
| `Timberborn.WondersUI` | 0 | 4 | 0 | 0 | 0 |
| `Timberborn.WorkerOutfitSystem` | 1 | 1 | 0 | 1 | 1 |
| `Timberborn.WorkerTypes` | 2 | 0 | 0 | 1 | 1 |
| `Timberborn.WorkerTypesUI` | 1 | 0 | 0 | 0 | 0 |
| `Timberborn.WorkplacesBatchControl` | 0 | 3 | 0 | 0 | 1 |
| `Timberborn.Workshops` | 32 | 0 | 2 | 1 | 1 |
| `Timberborn.WorkshopsEffects` | 1 | 1 | 1 | 0 | 0 |
| `Timberborn.WorkshopsUI` | 7 | 8 | 0 | 0 | 0 |
| `Timberborn.WorkSystem` | 30 | 0 | 1 | 2 | 2 |
| `Timberborn.WorkSystemUI` | 12 | 11 | 0 | 1 | 1 |
| `Timberborn.WorldPersistence` | 18 | 3 | 9 | 0 | 7 |
| `Timberborn.WorldSerialization` | 5 | 0 | 0 | 0 | 1 |
| `Timberborn.YielderFinding` | 6 | 0 | 0 | 0 | 0 |
| `Timberborn.Yielding` | 17 | 2 | 4 | 2 | 2 |
| `Timberborn.YieldingUI` | 2 | 0 | 0 | 0 | 1 |
| `Timberborn.ZiplineMovementSystem` | 0 | 3 | 0 | 0 | 1 |
| `Timberborn.ZiplineSystem` | 15 | 1 | 0 | 3 | 5 |
| `Timberborn.ZiplineSystemUI` | 0 | 10 | 0 | 0 | 5 |

## Known Gaps And Support Requests

See `docs/reference/native-api-support-requests.md` for the focused Mechanistry-facing list of unsupported calls Wildfire currently needs. The highest-risk gap remains native building lifecycle rollback: finished structure to unfinished/repairable state with construction progress, district state, recoverable goods, inventory policy, and workshop runtime settings preserved.

Other gaps worth tracking:

- Public native explosion/tunnel/detonator consequence APIs.
- Public soil contamination mutation with documented coordinate/index semantics.
- Public natural-resource lifecycle/model refresh APIs for drying, yield removal, good-stack disabling, and current-model refresh.
- Public workplace behavior ordering or priority registration.
- Public worker status/speed modifier stack for temporary smoke/choking effects.
- Public changed-cell/diff feeds for block placement, terrain, water, moisture, contamination, and natural-resource lifecycle.
