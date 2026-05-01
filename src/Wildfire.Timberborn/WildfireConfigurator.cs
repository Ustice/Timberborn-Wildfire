using Bindito.Core;

namespace Wildfire.Timberborn;

[Context("Game")]
public sealed class WildfireConfigurator : Configurator
{
    protected override void Configure()
    {
        Bind<TimberbornFireRuntime>().AsSingleton();
        Bind<TimberbornQaCommandFileBridge>().AsSingleton();
    }
}
