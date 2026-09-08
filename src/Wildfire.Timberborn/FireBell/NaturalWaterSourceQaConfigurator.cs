using Bindito.Core;
using Timberborn.BlueprintSystem;
using Timberborn.TemplateInstantiation;

namespace Wildfire.Timberborn.FireBell;

public sealed record NaturalWaterSourceQaSpec : ComponentSpec;

// Always load the exact template/ownership decorators so existing QA saves can restore.
// Only explicit process-opted-in creation can introduce a new source.
[Context("Game")]
public sealed class NaturalWaterSourceQaConfigurator : Configurator
{
    protected override void Configure()
    {
        Bind<TimberbornWaterCreditBoundary>().AsSingleton();
        Bind<TimberbornNaturalWaterSource>().AsTransient();
        Bind<NaturalWaterSourceShore>().AsSingleton();
        Bind<NaturalWaterSourceQaFactory>().AsSingleton();
        MultiBind<TemplateModule>().ToProvider<Module>().AsSingleton();
    }

    private sealed class Module : IProvider<TemplateModule>
    {
        public TemplateModule Get()
        {
            TemplateModule.Builder builder = new();
            builder.AddDecorator<NaturalWaterSourceQaSpec, TimberbornNaturalWaterSource>();
            return builder.Build();
        }
    }
}
