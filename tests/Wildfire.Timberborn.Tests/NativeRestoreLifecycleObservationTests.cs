using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeRestoreLifecycleObservationTests
{
    [Theory]
    [InlineData("Tree", true)]
    [InlineData("Crop", true)]
    [InlineData("Vegetation", true)]
    [InlineData("Structure", false)]
    [InlineData("Stockpile", false)]
    public void ActualNativeCacheDistinguishesMissingLifecycleFromAnObservedLiveOrDeadBody(string shape, bool required)
    {
        using var native = new NativeManagedTestContext();
        var entity = RuntimeHelpers.GetUninitializedObject(native.LoadNative("Timberborn.EntitySystem")
            .GetType("Timberborn.EntitySystem.EntityComponent")!);
        var components = native.LoadNative("Timberborn.BaseComponentSystem");
        var cache = RuntimeHelpers.GetUninitializedObject(components.GetType("Timberborn.BaseComponentSystem.ComponentCache")!);
        var map = Activator.CreateInstance(components.GetType("Timberborn.BaseComponentSystem.TypeIndexMap")!)!;
        var indices = (IDictionary)map.GetType().GetField("_typeIndex", Flags)!.GetValue(map)!;
        var livingType = native.LoadNative("Timberborn.NaturalResourcesLifecycle")
            .GetType("Timberborn.NaturalResourcesLifecycle.LivingNaturalResource")!;
        indices.Add(livingType, null);
        var values = new List<object> { entity };
        cache.GetType().GetField("_components", Flags)!.SetValue(cache, values);
        cache.GetType().GetField("_typeIndexMap", Flags)!.SetValue(cache, map);
        components.GetType("Timberborn.BaseComponentSystem.BaseComponent")!.GetField("_componentCache", Flags)!.SetValue(entity, cache);
        var mod = native.LoadMod();
        var method = mod.GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!
            .GetMethod("ReadDeathState", BindingFlags.Static | BindingFlags.NonPublic)!;
        var bodyShape = Enum.Parse(mod.GetType("Wildfire.Timberborn.Mapping.TimberbornInitialBodyShape")!, shape);
        object? Read() => method.Invoke(null, [entity, bodyShape]);
        if (required) Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(Read).InnerException);
        else Assert.Null(Read());

        var living = Activator.CreateInstance(livingType)!;
        indices[livingType] = values.Count;
        values.Add(living);
        Assert.Equal(false, Read());
        livingType.GetProperty("IsDead")!.SetValue(living, true);
        Assert.Equal(true, Read());
        // Actual native cached lookup and native state; no GameObject/template lifecycle implied.
    }

    [Fact]
    public void InstalledNaturalShapeTemplatesCarryTheSpecThatSuppliesNativeLifecycle()
    {
        using var native = new NativeManagedTestContext();
        using var archive = ZipFile.OpenRead(Path.Combine(native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        int count = 0;
        foreach (var entry in archive.Entries.Where(entry => entry.FullName.EndsWith(".blueprint.json", StringComparison.Ordinal)))
        {
            using var stream = entry.Open();
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (!root.TryGetProperty("BlockObjectSpec", out _) ||
                !new[] { "TreeComponentSpec", "CropSpec", "BushSpec" }.Any(spec => root.TryGetProperty(spec, out _))) continue;
            Assert.True(root.TryGetProperty("NaturalResourceSpec", out _), entry.FullName);
            count++;
        }
        Assert.True(count > 0);
    }

    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
}
