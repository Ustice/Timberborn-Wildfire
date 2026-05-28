using Bindito.Core;
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
