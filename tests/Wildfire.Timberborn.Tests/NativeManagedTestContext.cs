using System.Reflection;
using System.Runtime.Loader;

namespace Wildfire.Timberborn.Tests;

/// <summary>Loads installed managed code for field/IL contracts, without constructing a game entity.</summary>
internal sealed class NativeManagedTestContext : AssemblyLoadContext, IDisposable
{
    // DispatchProxy's generated assembly resolves same-name native interfaces through its
    // first loaded assembly identity. All proxy fixtures must share that identity, while
    // their mutable native service instances stay independent.
    public static NativeManagedTestContext ProxyContracts { get; } = new(collectible: false);
    public string ManagedPath { get; } = typeof(NativeManagedTestContext).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == "TimberbornManagedPath").Value!;
    public NativeManagedTestContext(bool collectible = true) : base(isCollectible: collectible) { }
    public Assembly LoadMod() => LoadFromAssemblyPath(typeof(TimberbornCompatibilityReport).Assembly.Location);
    public Assembly LoadNative(string name) => LoadFromAssemblyPath(Path.Combine(ManagedPath, name + ".dll"));
    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name is null || !(name.Name.StartsWith("Timberborn.") || name.Name.StartsWith("UnityEngine.") || name.Name.StartsWith("Bindito."))) return null;
        string path = Path.Combine(ManagedPath, name.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
    public void Dispose() { if (IsCollectible) Unload(); }
}
