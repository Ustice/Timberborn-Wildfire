using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeBodyClassificationTests
{
    [Theory]
    [InlineData("GearWorkshop.Folktails", "Structure")]
    [InlineData("GearWorkshop.IronTeeth", "Structure")]
    [InlineData("WaterPump.Folktails", "Structure")]
    [InlineData("DeepWaterPump.IronTeeth", "Structure")]
    [InlineData("PowerShaft.Folktails", "Structure")]
    [InlineData("PowerShaft.IronTeeth", "Structure")]
    [InlineData("SmallWarehouse.Folktails", "Stockpile")]
    [InlineData("Path", "Structure")]
    [InlineData("NaturalDam", "Infrastructure")]
    [InlineData("Slope", "Infrastructure")]
    [InlineData("BlueberryBush", "Vegetation")]
    [InlineData("Dandelion", "Vegetation")]
    public void InstalledPhysicalComponentsTakePrecedenceOverFunctionalNames(string name, string expected)
    {
        using var native = new NativeManagedTestContext();
        using var archive = ZipFile.OpenRead(Path.Combine(native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var stream = archive.Entries.Single(entry => entry.FullName.EndsWith("/" + name + ".blueprint.json", StringComparison.Ordinal)).Open();
        using var blueprint = JsonDocument.Parse(stream);
        var entity = RuntimeHelpers.GetUninitializedObject(native.LoadNative("Timberborn.EntitySystem")
            .GetType("Timberborn.EntitySystem.EntityComponent")!);
        var components = native.LoadNative("Timberborn.BaseComponentSystem");
        var cache = RuntimeHelpers.GetUninitializedObject(components.GetType("Timberborn.BaseComponentSystem.ComponentCache")!);
        var map = Activator.CreateInstance(components.GetType("Timberborn.BaseComponentSystem.TypeIndexMap")!)!;
        var indices = (IDictionary)map.GetType().GetField("_typeIndex", Flags)!.GetValue(map)!;
        var values = new List<object> { entity };
        foreach (var (assembly, component, spec) in new[]
        {
            ("Timberborn.Stockpiles", "Timberborn.Stockpiles.Stockpile", "StockpileSpec"),
            ("Timberborn.Buildings", "Timberborn.Buildings.Building", "BuildingSpec"),
            ("Timberborn.Forestry", "Timberborn.Forestry.TreeComponent", "TreeComponentSpec"),
            ("Timberborn.GoodStackSystem", "Timberborn.GoodStackSystem.GoodStack", "GoodStackSpec"),
        })
        {
            var type = native.LoadNative(assembly).GetType(component)!;
            if (blueprint.RootElement.TryGetProperty(spec, out _))
            {
                indices.Add(type, values.Count);
                values.Add(RuntimeHelpers.GetUninitializedObject(type));
            }
            else indices.Add(type, null);
        }
        cache.GetType().GetField("_components", Flags)!.SetValue(cache, values);
        cache.GetType().GetField("_typeIndexMap", Flags)!.SetValue(cache, map);
        components.GetType("Timberborn.BaseComponentSystem.BaseComponent")!.GetField("_componentCache", Flags)!.SetValue(entity, cache);
        var provider = native.LoadMod().GetType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")!;
        var shape = provider.GetMethod("Shape", Flags | BindingFlags.Static)!.Invoke(null, [entity, name]);
        Assert.Equal(expected, shape!.ToString());
        // Real native cached component lookup, populated from installed root specs. This does not
        // instantiate a GameObject/template or exercise settled-world provider liveness checks.
    }

    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
}
