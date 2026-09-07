using Bindito.Core;
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
        MultiBind<TemplateModule>().ToProvider<FertileAshFieldGatheringTemplateModuleProvider>().AsSingleton();
        Bind<TimberbornQaCommandFileBridge>().AsSingleton();
        Bind<WardenDeliveryService>().AsSingleton();
        Bind<WardenFireField>().AsSingleton();
        Bind<WardenStation>().AsTransient();
        Bind<WardenEquipment>().AsTransient();
        Bind<WardenExecutor>().AsTransient();
        Bind<WardenStationInventoryInitializer>().AsSingleton();
        Bind<WardenEquipmentInventoryInitializer>().AsSingleton();
        MultiBind<TemplateModule>().ToProvider<WardenTemplateModuleProvider>().AsSingleton();
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
            return builder.Build();
        }
    }

    private sealed class FertileAshFieldGatheringTemplateModuleProvider : IProvider<TemplateModule>
    {
        public TemplateModule Get()
        {
            TemplateModule.Builder builder = new();
            builder.AddDecorator<GathererFlag, TimberbornFertileAshFieldWorkplaceBehavior>();
            return builder.Build();
        }
    }
}
