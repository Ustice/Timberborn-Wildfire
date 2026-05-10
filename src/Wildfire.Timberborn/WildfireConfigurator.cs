using Bindito.Core;
using Timberborn.BottomBarSystem;

namespace Wildfire.Timberborn;

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
}
