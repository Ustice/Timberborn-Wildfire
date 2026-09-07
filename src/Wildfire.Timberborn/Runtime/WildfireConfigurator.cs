using Wildfire.Timberborn.Beavers.Emergency;
using Wildfire.Timberborn.Resources;
using Bindito.Core;
using Timberborn.EntityPanelSystem;
using Wildfire.Timberborn.FireResponse;
using Timberborn.WorkSystem;
using Timberborn.Beavers;
using Timberborn.InventorySystem;
using Timberborn.Gathering;
using Timberborn.TemplateInstantiation;

namespace Wildfire.Timberborn.Runtime;

[Context("Game")]
public sealed class WildfireConfigurator : Configurator
{
    protected override void Configure()
    {
        Bind<IWildfireReleaseSettingsStore>()
            .To<TimberbornSettingsSystemWildfireReleaseSettingsStore>()
            .AsSingleton();
        Bind<WildfireReleaseSettings>().AsSingleton();
        Bind<WildfireReleaseSettingsInitializer>().AsSingleton();
        Bind<TimberbornFireSimParameterPresetState>().AsSingleton();
        Bind<ITimberbornWindProvider>().To<TimberbornWindProvider>().AsSingleton();
        Bind<ITimberbornGpuVisualFieldSurface>().To<TimberbornLiveGpuVisualFieldSurface>().AsSingleton();
        Bind<ITimberbornFireSimulatorFactory>().To<TimberbornComputeFireSimulatorFactory>().AsSingleton();
        Bind<TimberbornPlayerFireAlertCameraFocus>().AsSingleton();
        Bind<TimberbornFireRuntime>().AsSingleton();
        Bind<TimberbornFireRuntimeInitializer>().AsSingleton();
        Bind<TimberbornBurnSelectedEntityTool>().AsSingleton();
        Bind<TimberbornDemolitionBurnToolButton>().AsSingleton();
        Bind<TimberbornFertilizeDesignationService>().AsSingleton();
        Bind<TimberbornFertilizeTool>().AsSingleton();
        Bind<TimberbornFertilizeFieldsToolButton>().AsSingleton();
        Bind<TimberbornFertilizeForestryToolButton>().AsSingleton();
        Bind<TimberbornDeferredToolButtonInstaller>().AsSingleton();
        Bind<TimberbornFertileAshFieldWorkplaceBehavior>().AsTransient();
        Bind<AshHarvestExecutor>().AsTransient();
        Bind<AshHarvestBehavior>().AsTransient();
        Bind<AshHarvestFragment>().AsSingleton();
        MultiBind<TemplateModule>().ToProvider<FertileAshFieldGatheringTemplateModuleProvider>().AsSingleton();
        Bind<TimberbornQaCommandFileBridge>().AsSingleton();
        Bind<WardenStationFragment>().AsSingleton();
        Bind<NativeResourceRecoveryNotification>().AsSingleton();
        MultiBind<EntityPanelModule>().ToProvider<WardenPanelModuleProvider>().AsSingleton();
        Bind<CarryEmergencyFragment>().AsSingleton();
        MultiBind<EntityPanelModule>().ToProvider<CarryEmergencyPanelModuleProvider>().AsSingleton();
        Bind<CarryEmergencySession>().AsSingleton();
        Bind<WildfireCarryEmergencyExecutor>().AsTransient();
        Bind<CarryEmergencyInterrupter>().AsTransient();
        Bind<NativeResourceCoordinator>().AsSingleton();
        Bind<WardenFireField>().AsSingleton();
        Bind<WardenStation>().AsTransient();
        Bind<WardenEquipment>().AsTransient();
        Bind<WardenExecutor>().AsTransient();
        Bind<WardenStationInventoryInitializer>().AsSingleton();
        Bind<WardenEquipmentInventoryInitializer>().AsSingleton();
        MultiBind<TemplateModule>().ToProvider<WardenTemplateModuleProvider>().AsSingleton();
    }

    private sealed class CarryEmergencyPanelModuleProvider : IProvider<EntityPanelModule>
    {
        private readonly CarryEmergencyFragment _fragment;
        public CarryEmergencyPanelModuleProvider(CarryEmergencyFragment fragment) => _fragment = fragment;
        public EntityPanelModule Get()
        {
            EntityPanelModule.Builder builder = new();
            builder.AddMiddleFragment(_fragment, 1);
            return builder.Build();
        }
    }

    private sealed class WardenPanelModuleProvider : IProvider<EntityPanelModule>
    {
        private readonly WardenStationFragment _fragment;
        private readonly AshHarvestFragment _ash;
        public WardenPanelModuleProvider(WardenStationFragment fragment, AshHarvestFragment ash) { _fragment = fragment; _ash = ash; }
        public EntityPanelModule Get()
        {
            EntityPanelModule.Builder builder = new();
            builder.AddMiddleFragment(_fragment, 0);
            builder.AddMiddleFragment(_ash, 0);
            return builder.Build();
        }
    }

    private sealed class WardenTemplateModuleProvider : IProvider<TemplateModule>
    {
        private readonly WardenStationInventoryInitializer _station;
        private readonly WardenEquipmentInventoryInitializer _equipment;
        public WardenTemplateModuleProvider(WardenStationInventoryInitializer station, WardenEquipmentInventoryInitializer equipment)
        { _station = station; _equipment = equipment; }
        public TemplateModule Get()
        {
            TemplateModule.Builder builder = new();
            builder.AddDecorator<WildfireWardenStationSpec, WardenStation>();
            builder.AddDedicatedDecorator<WardenStation, Inventory>(_station);
            builder.AddDecorator<AdultSpec, WardenEquipment>();
            builder.AddDedicatedDecorator<WardenEquipment, Inventory>(_equipment);
            builder.AddDecorator<AdultSpec, WardenExecutor>();
            builder.AddDecorator<AdultSpec, WildfireCarryEmergencyExecutor>();
            builder.AddDecorator<AdultSpec, CarryEmergencyInterrupter>();
            return builder.Build();
        }
    }

    private sealed class FertileAshFieldGatheringTemplateModuleProvider : IProvider<TemplateModule>
    {
        public TemplateModule Get()
        {
            TemplateModule.Builder builder = new();
            builder.AddDecorator<GathererFlag, TimberbornFertileAshFieldWorkplaceBehavior>();
            builder.AddDecorator<AdultSpec, AshHarvestExecutor>();
            builder.AddDecorator<AdultSpec, AshHarvestBehavior>();
            return builder.Build();
        }
    }
}
