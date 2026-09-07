using Bindito.Core;
using Timberborn.Beavers;
using Timberborn.TemplateInstantiation;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.FireBell;

// Types stay registered without the process switch so copied active saves remain loadable.
[Context("Game")]
public sealed class BorrowedDutyConfigurator : Configurator
{
    protected override void Configure()
    {
        Bind<BorrowedDutyFixture>().AsSingleton();
        Bind<BorrowedDutyBehavior>().AsTransient();
        Bind<BorrowedDutyExecutor>().AsTransient();
        Bind<BorrowedDutyWorkplaceBehavior>().AsTransient();
        MultiBind<TemplateModule>().ToProvider<Module>().AsSingleton();
    }
    private sealed class Module : IProvider<TemplateModule>
    {
        public TemplateModule Get()
        {
            TemplateModule.Builder builder = new();
            builder.AddDecorator<AdultSpec, BorrowedDutyBehavior>();
            builder.AddDecorator<AdultSpec, BorrowedDutyExecutor>();
            builder.AddDecorator<Workplace, BorrowedDutyWorkplaceBehavior>();
            return builder.Build();
        }
    }
}
