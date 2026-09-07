using System.Security.Cryptography;
using Bindito.Core;
using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.Gathering;
using Timberborn.TemplateInstantiation;
using Timberborn.WorldPersistence;
using Timberborn.Yielding;

namespace Wildfire.Timberborn.Compatibility;

/// <summary>
/// Native ripe grower loading refills yield after Yielder.Load. Restore the original native saved
/// named yield before native initialization, model subscriptions and loaded-reservation reconciliation.
/// This retains a loader only during native loading; it stores no quantity or fire-loss history.
/// </summary>
internal sealed class TimberbornGatherableYieldLoadCorrection : BaseComponent,
    IAwakableComponent, IPersistentEntity, IPreInitializableEntity
{
    private Gatherable _gatherable = null!;
    private IEntityLoader? _loader;

    public void Awake() => _gatherable = GetComponent<Gatherable>();
    public void Save(IEntitySaver saver) { }
    public void Load(IEntityLoader loader)
    {
        if (_loader is not null)
            throw new InvalidOperationException("Native gatherable load was staged twice.");
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }
    public void PreInitializeEntity()
    {
        var loader = _loader;
        _loader = null; // Even malformed saved data must never retain an implicit retry.
        if (loader is null) return; // Newly created entity has no native saved yield to restore.
        var yielder = _gatherable.Yielder;
        if (yielder is null || !ReferenceEquals(yielder.YielderSpec, _gatherable.YielderSpec) ||
            yielder.ComponentName != _gatherable.YielderSpec.YielderComponentName)
            throw new InvalidOperationException("Native gatherable load lost its exact named yielder.");
        yielder.Load(loader); // Missing native saved component retains the native initialization value.
    }
}

[Context("Game")]
internal sealed class TimberbornGatherableYieldLoadConfigurator : Configurator
{
    protected override void Configure()
    {
        Bind<TimberbornGatherableYieldLoadCorrection>().AsTransient();
        MultiBind<TemplateModule>().ToProvider<ModuleProvider>().AsSingleton();
    }
    private sealed class ModuleProvider : IProvider<TemplateModule>
    {
        public TemplateModule Get()
        {
            TimberbornGatherableYieldLoadContract.Verify(); // Before creating/loading affected entities.
            var builder = new TemplateModule.Builder();
            builder.AddDecorator<Gatherable, TimberbornGatherableYieldLoadCorrection>();
            return builder.Build();
        }
    }
}

internal static class TimberbornGatherableYieldLoadContract
{
    private static readonly Lazy<bool> Supported = new(VerifyAssemblies);
    internal static void Verify() => _ = Supported.Value;
    private static bool VerifyAssemblies()
    {
        string directory = Path.GetDirectoryName(typeof(Yielder).Assembly.Location)!;
        foreach (var pair in Fingerprints)
        {
            using var stream = File.OpenRead(Path.Combine(directory, pair.Key));
            using var sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (actual != pair.Value)
                throw new InvalidOperationException($"Native gatherable load order is not reviewed for {pair.Key} ({actual}).");
        }
        return true;
    }
    private static readonly IReadOnlyDictionary<string, string> Fingerprints = new Dictionary<string, string>
    {
        ["Timberborn.Yielding.dll"] = "de35358244b4b6c3ee880596ee1fc09b0d7a5928c5f481a360e2a3b6db3916d5",
        ["Timberborn.Gathering.dll"] = "fa592667f30dd8e206030d954cdf2f68e73bce1ed20bbf995fd5b04971aeadfa",
        ["Timberborn.WorldPersistence.dll"] = "831546b1f90af169d6d85076ca26bd885574daf56e0ad27f3b90bc5b1fdb8bb0",
        ["Timberborn.NaturalResourcesModelSystem.dll"] = "de06f91172601e1e6930c75d026a75bb0aa324639c33d64085bffab25abe539a",
        ["Timberborn.TimeSystem.dll"] = "e4d1f60b5a4bca37927659b28412f6fcd9b1ef02f85a6329c2a84bd8d2a4d20e",
    };
}
