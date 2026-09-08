using System.Collections;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeAshGathererAdmissionTests
{
    [Theory]
    [InlineData("Folktails")]
    [InlineData("IronTeeth")]
    public void NativeGathererListsSelectsAndAllowsFertileAshOutput(string faction)
    {
        var f = new Fixture(faction);
        var allowed = f.Call(f.Remover, "GetAllowedGoods");
        Assert.Contains("FertileAsh", ((IEnumerable)allowed!).Cast<string>());
        f.Call(f.Flag, "InitializeEntity");
        Assert.True((bool)f.Call(f.Flag, "CanGather", "FertileAshField")!);
        Assert.Equal(2, ((IEnumerable)f.Property(f.Flag, "AllowedGatherables")!).Cast<object>().Count());

        var dropdown = f.Dropdown();
        f.Call(dropdown, "InitializeEntity");
        Assert.Contains("Fertile ash", ((IEnumerable)f.Property(dropdown, "Items")!).Cast<string>());
        Assert.True((bool)f.Property(dropdown, "HasMultipleOptions")!);
        int changes = 0;
        f.Prioritizer.GetType().GetEvent("PrioritizedGatherableChanged")!.AddEventHandler(f.Prioritizer,
            (EventHandler)((_, _) => changes++));
        f.Call(dropdown, "SetValue", "Fertile ash");
        Assert.Same(f.Ash, f.Property(f.Prioritizer, "PrioritizedGatherable"));
        Assert.Equal(1, changes);
        Assert.Equal("Fertile ash", f.Call(dropdown, "GetValue"));
        Assert.Same(f.Ash, f.Call(f.Prioritizer, "GetGatherable", "FertileAshField")); // Native saved-name lookup.

        // Execute the native declaration policy; final Inventory.Initialize needs its engine lifecycle.
        var initializer = f.New("Timberborn.InventorySystem", "InventoryInitializer", f.Goods, null, int.MaxValue, "Inventory:SimpleOutputInventory");
        var outputType = f.T("Timberborn.SimpleOutputBuildings", "SimpleOutputInventoryInitializer");
        var output = RuntimeHelpers.GetUninitializedObject(outputType);
        f.Call(output, "AllowGoodsAsTakeable", initializer, f.Capacity, f.Remover);
        var declarations = ((IEnumerable)f.Field(initializer, "_storableGoodAmounts")!).Cast<object>().ToArray();
        var ash = Assert.Single(declarations, row => (string)f.Property(f.Property(row, "StorableGood")!, "GoodId")! == "FertileAsh");
        Assert.Equal(f.Capacity, f.Property(ash, "Amount"));
        Assert.True((bool)f.Property(f.Property(ash, "StorableGood")!, "Takeable")!);
        Assert.False((bool)f.Property(f.Property(ash, "StorableGood")!, "Givable")!);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeFilteringRequiresBothFactionGoodAndMatchingResourceGroup(bool removeGood)
    {
        var f = new Fixture("Folktails");
        if (removeGood) f.GoodMap.Remove("FertileAsh");
        else f.SetProperty(f.Property(f.Ash, "Yielder")!, "ResourceGroup", "Cuttable");
        f.Call(f.Flag, "InitializeEntity");
        Assert.False((bool)f.Call(f.Flag, "CanGather", "FertileAshField")!);
        Assert.DoesNotContain("FertileAsh", ((IEnumerable)f.Call(f.Remover, "GetAllowedGoods")!).Cast<string>());
        Assert.Null(f.Call(f.Prioritizer, "GetGatherable", "FertileAshField"));
    }

    private sealed class Fixture
    {
        // Shared context is required by native DispatchProxy contracts, but fixture services are independent.
        private readonly NativeManagedTestContext _native = NativeManagedTestContext.ProxyContracts;
        internal object Remover { get; }
        internal object Flag { get; }
        internal object Prioritizer { get; }
        internal object Ash { get; }
        internal object Goods { get; }
        internal IDictionary GoodMap { get; }
        internal int Capacity { get; }
        internal Fixture(string faction)
        {
            using var zip = ZipFile.OpenRead(Path.Combine(_native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
            using var gatherer = Read(zip, $"Buildings/Food/GathererFlag/GathererFlag.{faction}.blueprint.json");
            string group = gatherer.RootElement.GetProperty("YieldRemovingBuildingSpec").GetProperty("ResourceGroup").GetString()!;
            Capacity = gatherer.RootElement.GetProperty("SimpleOutputInventorySpec").GetProperty("Capacity").GetInt32();
            using var collection = JsonDocument.Parse(File.ReadAllText(Source($"GoodCollections/GoodCollection.{faction}.blueprint.json")));
            Assert.Contains("FertileAsh", collection.RootElement.GetProperty("GoodCollectionSpec").GetProperty("Goods#append").EnumerateArray().Select(v => v.GetString()));
            using var common = JsonDocument.Parse(File.ReadAllText(Source("TemplateCollections/TemplateCollection.NaturalResources.Common.blueprint.json")));
            Assert.Contains("NaturalResources/FertileAshField/FertileAshField.blueprint", common.RootElement.GetProperty("TemplateCollectionSpec").GetProperty("Blueprints#append").EnumerateArray().Select(v => v.GetString()));
            using var commonGoods = Read(zip, "GoodCollections/GoodCollection.Common.blueprint.json");
            Assert.Contains("Berries", commonGoods.RootElement.GetProperty("GoodCollectionSpec").GetProperty("Goods").EnumerateArray().Select(v => v.GetString()));
            using var ashDefinition = JsonDocument.Parse(File.ReadAllText(Source("NaturalResources/FertileAshField/FertileAshField.blueprint.json")));
            using var berriesDefinition = Read(zip, "NaturalResources/Bushes/Blueberry/BlueberryBush.blueprint.json");
            var ashBlueprint = Blueprint(ashDefinition.RootElement, out var ash);
            Ash = ash;
            var berriesBlueprint = Blueprint(berriesDefinition.RootElement, out _);
            var collectionService = RuntimeHelpers.GetUninitializedObject(T("Timberborn.TemplateCollectionSystem", "TemplateCollectionService"));
            SetProperty(collectionService, "AllTemplates", Immutable(ashBlueprint.GetType(), ashBlueprint, berriesBlueprint));
            var templates = New("Timberborn.TemplateSystem", "TemplateService", collectionService);
            Goods = RuntimeHelpers.GetUninitializedObject(T("Timberborn.Goods", "GoodService"));
            GoodMap = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), T("Timberborn.Goods", "GoodSpec")))!;
            string ashLabel = File.ReadLines(Source("Localizations/enUS.csv"))
                .Single(line => line.StartsWith("Good.FertileAsh.PluralDisplayName,", StringComparison.Ordinal))
                .Split(',')[1];
            GoodMap.Add("FertileAsh", Good("FertileAsh", ashLabel));
            GoodMap.Add("Berries", Good("Berries", "Berries"));
            Set(Goods, "_goodSpecsById", GoodMap);
            Remover = New("Timberborn.Yielding", "YieldRemovingBuilding", templates, Goods);
            var spec = New("Timberborn.Yielding", "YieldRemovingBuildingSpec");
            SetProperty(spec, "ResourceGroup", group);
            Set(Remover, "_yieldRemovingBuildingSpec", spec);
            Flag = New("Timberborn.Gathering", "GathererFlag");
            Set(Flag, "_yieldRemovingBuilding", Remover);
            Prioritizer = New("Timberborn.Gathering", "GatherablePrioritizer");
            Set(Prioritizer, "_yieldRemovingBuilding", Remover);
        }
        internal object Dropdown()
        {
            var describer = New("Timberborn.GoodsUI", "GoodDescriber", null, Goods);
            var loc = NativePersistenceProxy.Create(T("Timberborn.Localization", "ILoc"), (_, args) => args[0]);
            var type = T("Timberborn.GatheringUI", "GatherablePrioritizerDropdownProvider");
            var dropdown = Activator.CreateInstance(type, Flags, null, [describer, loc], null)!;
            Set(dropdown, "_gatherablePrioritizer", Prioritizer);
            Set(dropdown, "_gathererFlag", Flag);
            return dropdown;
        }
        private object Good(string id, string label)
        {
            var good = New("Timberborn.Goods", "GoodSpec");
            SetProperty(good, "Id", id);
            SetProperty(good, "PluralDisplayName", New("Timberborn.LocalizationSerialization", "LocalizedText", label));
            SetProperty(good, "IconSmall", Activator.CreateInstance(T("Timberborn.SpriteOperations", "UISprite"), new object?[] { null })!);
            return good;
        }
        private object Blueprint(JsonElement source, out object gatherable)
        {
            gatherable = New("Timberborn.Gathering", "GatherableSpec");
            var yielder = New("Timberborn.Yielding", "YielderSpec");
            var declared = source.GetProperty("GatherableSpec").GetProperty("Yielder");
            var amount = New("Timberborn.Goods", "GoodAmountSpec");
            SetProperty(amount, "Id", declared.GetProperty("Yield").GetProperty("Id").GetString());
            SetProperty(amount, "Amount", declared.GetProperty("Yield").GetProperty("Amount").GetInt32());
            SetProperty(yielder, "Yield", amount);
            SetProperty(yielder, "ResourceGroup", declared.GetProperty("ResourceGroup").GetString());
            SetProperty(yielder, "YielderComponentName", declared.GetProperty("YielderComponentName").GetString());
            SetProperty(gatherable, "Yielder", yielder);
            var template = New("Timberborn.TemplateSystem", "TemplateSpec");
            string name = source.GetProperty("TemplateSpec").GetProperty("TemplateName").GetString()!;
            SetProperty(template, "TemplateName", name);
            SetProperty(template, "BackwardCompatibleTemplateNames", System.Collections.Immutable.ImmutableArray<string>.Empty);
            var natural = New("Timberborn.NaturalResources", "NaturalResourceSpec");
            SetProperty(natural, "Order", source.GetProperty("NaturalResourceSpec").GetProperty("Order").GetInt32());
            var specs = Array.CreateInstance(T("Timberborn.BlueprintSystem", "ComponentSpec"), 3);
            specs.SetValue(gatherable, 0);
            specs.SetValue(template, 1);
            specs.SetValue(natural, 2);
            var type = T("Timberborn.BlueprintSystem", "Blueprint");
            var blueprint = Activator.CreateInstance(type, name, specs, typeof(System.Collections.Immutable.ImmutableArray<>).MakeGenericType(type).GetField("Empty")!.GetValue(null))!;
            gatherable = type.GetMethods().Single(m => m.Name == "GetSpec" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0).MakeGenericMethod(gatherable.GetType()).Invoke(blueprint, null)!;
            return blueprint;
        }
        private static object Immutable(Type type, params object[] items)
        {
            var array = Array.CreateInstance(type, items.Length); for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i);
            return typeof(System.Collections.Immutable.ImmutableArray).GetMethods().Single(m => m.Name == "Create" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsArray).MakeGenericMethod(type).Invoke(null, [array])!;
        }
        internal Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
        internal object New(string assembly, string name, params object?[] args) => Activator.CreateInstance(T(assembly, name), Flags, null, args, null)!;
        internal object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethods(Flags).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(owner, args);
        internal object? Property(object owner, string name) => owner.GetType().GetProperty(name, Flags)!.GetValue(owner);
        internal object? Field(object owner, string name) => owner.GetType().GetField(name, Flags)!.GetValue(owner);
        internal void SetProperty(object owner, string name, object? value) => owner.GetType().GetProperty(name, Flags)!.SetValue(owner, value);
        private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
        private static JsonDocument Read(ZipArchive zip, string name) { using var stream = zip.GetEntry(name)!.Open(); return JsonDocument.Parse(stream); }
        private static string Source(string relative)
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, "src/Wildfire.Timberborn/Data", relative);
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException(relative);
        }
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    }
}
