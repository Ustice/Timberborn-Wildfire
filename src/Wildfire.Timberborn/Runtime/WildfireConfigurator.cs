using Bindito.Core;
using Timberborn.BottomBarSystem;
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
        MultiBind<BottomBarModule>().ToProvider<DemolitionBurnToolBottomBarModuleProvider>().AsSingleton();
        Bind<TimberbornFertilizeDesignationService>().AsSingleton();
        Bind<TimberbornFertilizeTool>().AsSingleton();
        Bind<TimberbornFertilizeFieldsToolButton>().AsSingleton();
        Bind<TimberbornFertilizeForestryToolButton>().AsSingleton();
        MultiBind<BottomBarModule>().ToProvider<FertilizeFieldsToolBottomBarModuleProvider>().AsSingleton();
        MultiBind<BottomBarModule>().ToProvider<FertilizeForestryToolBottomBarModuleProvider>().AsSingleton();
        Bind<TimberbornFertileAshFieldWorkplaceBehavior>().AsTransient();
        MultiBind<TemplateModule>().ToProvider<FertileAshFieldGatheringTemplateModuleProvider>().AsSingleton();
        Bind<TimberbornQaCommandFileBridge>().AsSingleton();
    }

    private sealed class DemolitionBurnToolBottomBarModuleProvider : IProvider<BottomBarModule>
    {
        private readonly TimberbornDemolitionBurnToolButton _button;

        public DemolitionBurnToolBottomBarModuleProvider(TimberbornDemolitionBurnToolButton button)
        {
            _button = button ?? throw new ArgumentNullException(nameof(button));
        }

        public BottomBarModule Get()
        {
            BottomBarModule.Builder builder = new();
            builder.AddLeftSectionElement(_button, 51);
            return builder.Build();
        }
    }

    private sealed class FertilizeFieldsToolBottomBarModuleProvider : IProvider<BottomBarModule>
    {
        private readonly TimberbornFertilizeFieldsToolButton _button;

        public FertilizeFieldsToolBottomBarModuleProvider(TimberbornFertilizeFieldsToolButton button)
        {
            _button = button ?? throw new ArgumentNullException(nameof(button));
        }

        public BottomBarModule Get()
        {
            BottomBarModule.Builder builder = new();
            builder.AddLeftSectionElement(_button, 52);
            return builder.Build();
        }
    }

    private sealed class FertilizeForestryToolBottomBarModuleProvider : IProvider<BottomBarModule>
    {
        private readonly TimberbornFertilizeForestryToolButton _button;

        public FertilizeForestryToolBottomBarModuleProvider(TimberbornFertilizeForestryToolButton button)
        {
            _button = button ?? throw new ArgumentNullException(nameof(button));
        }

        public BottomBarModule Get()
        {
            BottomBarModule.Builder builder = new();
            builder.AddLeftSectionElement(_button, 53);
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
