using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeRetainedTreeMaterialEvidenceTests
{
    [Fact]
    public void ActualPineCuttableCompositionPreservesKnownMaterialEvidenceWithoutReadingPublicZeroAsEmpty()
    {
        using var f = new Fixture();
        Assert.True(f.Observe());
        int events = 0;
        f.Native.YielderType.GetEvent("YieldAdded")!.AddEventHandler(f.Yielder, (EventHandler)((_, _) => events++));
        f.Native.YielderType.GetEvent("YieldDecreased")!.AddEventHandler(f.Yielder, (EventHandler)((_, _) => events++));
        f.Native.EnabledField.SetValue(f.Yielder, false);
        Assert.Equal(0, f.Native.Quantity(f.Yielder));
        Assert.True(f.Observe());
        Assert.Equal(2, f.Native.RawQuantity(f.Yielder));
        Assert.Equal(0, events);
    }

    [Theory]
    [InlineData("spec-only")]
    [InlineData("component-only")]
    [InlineData("remove-on-cut")]
    [InlineData("missing-tree")]
    [InlineData("duplicate-name")]
    [InlineData("foreign-yielder")]
    [InlineData("changed-declared-yield")]
    public void ExactNativeCompositionRefusesUnprovedWoodRetention(string change)
    {
        using var f = new Fixture();
        switch (change)
        {
            case "spec-only": f.Components.Add(Activator.CreateInstance(f.Native.Type("Timberborn.Cutting", "Timberborn.Cutting.DeadCuttableYieldRemoverSpec"))!); break;
            case "component-only": f.Components.Add(RuntimeHelpers.GetUninitializedObject(f.Native.Type("Timberborn.Cutting", "Timberborn.Cutting.DeadCuttableYieldRemover"))); break;
            case "remove-on-cut": f.CuttableSpec.GetType().GetProperty("RemoveOnCut")!.SetValue(f.CuttableSpec, true); break;
            case "missing-tree": f.Indices[f.Tree.GetType()] = null; break;
            case "duplicate-name":
                f.Indices[f.Native.YielderType] = new List<int> { (int)f.Indices[f.Native.YielderType]!, f.Components.Count };
                f.Components.Add(f.Native.NewYielder("Cuttable", "Log"));
                break;
            case "foreign-yielder": NativePartialYieldFixture.Set(f.Cuttable, "<Yielder>k__BackingField", f.Native.NewYielder("Cuttable", "Log")); break;
            case "changed-declared-yield":
                var spec = f.Native.YielderType.GetProperty("YielderSpec")!.GetValue(f.Yielder)!;
                var yield = spec.GetType().GetProperty("Yield")!.GetValue(spec)!;
                yield.GetType().GetProperty("Amount")!.SetValue(yield, 3);
                break;
        }
        Assert.False(f.Observe());
        Assert.Equal(2, f.Native.RawQuantity(f.Yielder));
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly NativePartialYieldFixture Native = new();
        internal readonly object Entity, Tree, Cuttable, CuttableSpec, Yielder;
        internal readonly List<object> Components;
        internal readonly IDictionary Indices;
        private readonly object _body;
        private readonly MethodInfo _observe;

        internal Fixture()
        {
            using var archive = ZipFile.OpenRead(Path.Combine(Path.GetDirectoryName(Native.Type("Timberborn.Cutting", "Timberborn.Cutting.Cuttable").Assembly.Location)!,
                "../StreamingAssets/Modding/Blueprints.zip"));
            using var stream = archive.GetEntry("NaturalResources/Trees/Pine/Pine.blueprint.json")!.Open();
            using var blueprint = JsonDocument.Parse(stream);
            Assert.True(blueprint.RootElement.TryGetProperty("TreeComponentSpec", out _));
            Assert.False(blueprint.RootElement.TryGetProperty("DeadCuttableYieldRemoverSpec", out _));
            var cuttableDefinition = blueprint.RootElement.GetProperty("CuttableSpec");
            var named = cuttableDefinition.GetProperty("Yielder");
            string name = named.GetProperty("YielderComponentName").GetString()!;
            string good = named.GetProperty("Yield").GetProperty("Id").GetString()!;
            int amount = named.GetProperty("Yield").GetProperty("Amount").GetInt32();
            Yielder = Native.NewYielder(name, good);
            var spec = Native.YielderType.GetProperty("YielderSpec")!.GetValue(Yielder)!;
            var yield = spec.GetType().GetProperty("Yield")!.GetValue(spec)!;
            yield.GetType().GetProperty("Amount")!.SetValue(yield, amount);
            Native.YielderType.GetMethod("Initialize")!.Invoke(Yielder, [spec, Activator.CreateInstance(Native.AmountType, good, amount), null, "Gather"]);
            Entity = RuntimeHelpers.GetUninitializedObject(Native.Type("Timberborn.EntitySystem", "Timberborn.EntitySystem.EntityComponent"));
            Tree = RuntimeHelpers.GetUninitializedObject(Native.Type("Timberborn.Forestry", "Timberborn.Forestry.TreeComponent"));
            Cuttable = RuntimeHelpers.GetUninitializedObject(Native.Type("Timberborn.Cutting", "Timberborn.Cutting.Cuttable"));
            CuttableSpec = Activator.CreateInstance(Native.Type("Timberborn.Cutting", "Timberborn.Cutting.CuttableSpec"))!;
            CuttableSpec.GetType().GetProperty("RemoveOnCut")!.SetValue(CuttableSpec, cuttableDefinition.GetProperty("RemoveOnCut").GetBoolean());
            CuttableSpec.GetType().GetProperty("Yielder")!.SetValue(CuttableSpec, spec);
            NativePartialYieldFixture.Set(Cuttable, "_cuttableSpec", CuttableSpec);
            NativePartialYieldFixture.Set(Cuttable, "<Yielder>k__BackingField", Yielder);
            Components = [Entity, Tree, Cuttable, Yielder, CuttableSpec];
            var cache = RuntimeHelpers.GetUninitializedObject(Native.Type("Timberborn.BaseComponentSystem", "Timberborn.BaseComponentSystem.ComponentCache"));
            var map = Activator.CreateInstance(Native.Type("Timberborn.BaseComponentSystem", "Timberborn.BaseComponentSystem.TypeIndexMap"))!;
            Indices = (IDictionary)NativePartialYieldFixture.Get(map, "_typeIndex");
            for (int index = 0; index < Components.Count; index++) Indices.Add(Components[index].GetType(), index);
            NativePartialYieldFixture.Set(cache, "_components", Components);
            NativePartialYieldFixture.Set(cache, "_typeIndexMap", map);
            var baseType = Native.Type("Timberborn.BaseComponentSystem", "Timberborn.BaseComponentSystem.BaseComponent");
            foreach (var component in Components.Where(baseType.IsInstanceOfType))
                baseType.GetField("_componentCache", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, cache);

            var provider = Native.ModType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider");
            var role = Enum.Parse(Native.ModType("Wildfire.Timberborn.Mapping.TimberbornCapturedYieldRole"), "Cuttable");
            var captured = provider.GetMethod("CaptureNamedYield", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [Yielder, role, false])!;
            var coordinate = Activator.CreateInstance(Native.ModType("Wildfire.Timberborn.Mapping.TimberbornCellCoordinates"), 0, 0, 0)!;
            var footprint = Activator.CreateInstance(Native.ModType("Wildfire.Timberborn.Mapping.TimberbornMaterialFootprintSlot"), coordinate, 0)!;
            var shape = Enum.Parse(Native.ModType("Wildfire.Timberborn.Mapping.TimberbornInitialBodyShape"), "Tree");
            _body = Activator.CreateInstance(Native.ModType("Wildfire.Timberborn.Mapping.TimberbornInitialMaterialBody"), Guid.NewGuid(), "Pine", shape,
                ArrayOf(footprint), ArrayOf(captured), Array.CreateInstance(Native.ModType("Wildfire.Timberborn.Mapping.TimberbornInventoryMaterial"), 0), null)!;
            _observe = Native.ModType("Wildfire.Timberborn.Runtime.TimberbornRetainedTreeMaterialEvidence")
                .GetMethod("Observe", BindingFlags.Static | BindingFlags.NonPublic)!;
        }

        internal bool Observe() => (bool)_observe.Invoke(null, [Entity, _body])!;
        private static Array ArrayOf(object value) { var array = Array.CreateInstance(value.GetType(), 1); array.SetValue(value, 0); return array; }
        public void Dispose() => Native.Dispose();
    }
}
