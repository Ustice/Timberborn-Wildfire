using Bindito.Core;
using Timberborn.AssetSystem;

namespace Wildfire.Timberborn.FireResponse.Presentation;

// AssetLoader captures its provider list in Bootstrapper, before Game exists.
[Context("Bootstrapper")]
public sealed class WardenAttachmentAssetConfigurator : Configurator
{
    protected override void Configure() =>
        MultiBind<IAssetProvider>().To<WardenAttachmentAssetProvider>().AsSingleton();
}
