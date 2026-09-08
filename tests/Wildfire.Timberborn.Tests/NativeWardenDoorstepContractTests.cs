using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenDoorstepContractTests
{
    [Fact]
    public void StationSuppliesDistinctUnfinishedParentForNativeDoorstepCreation()
    {
        using var native = new NativeManagedTestContext();
        using var station = JsonDocument.Parse(File.ReadAllText(StationPath()));
        // The actual native admission predicate ignores PlaceFinished. An entrance at
        // BaseZ requires a doorstep parent even for this immediately finished QA station.
        Assert.True(CanSpawnDoorstep(native, station.RootElement));
        var model = Deserialize(native, station.RootElement.GetProperty("BuildingModelSpec"),
            "Timberborn.Buildings", "BuildingModelSpec");
        string unfinished = (string)model.GetType().GetProperty("UnfinishedModelName")!.GetValue(model)!;
        string finished = (string)model.GetType().GetProperty("FinishedModelName")!.GetValue(model)!;
        Assert.False(string.IsNullOrEmpty(unfinished), "Native SpawnDoorstep dereferences the unfinished model's transform.");
        Assert.NotEqual(finished, unfinished); // Native visibility updates toggle these independently.
        var children = station.RootElement.GetProperty("Children");
        Assert.True(children.TryGetProperty(finished, out _));
        var construction = children.GetProperty(unfinished).GetProperty("Children");
        var nested = Assert.Single(construction.EnumerateObject()).Value.GetProperty("BlueprintPath").GetString()!;
        using var zip = ZipFile.OpenRead(Path.Combine(native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var stream = zip.GetEntry(nested + ".json")!.Open();
        using var foundation = JsonDocument.Parse(stream);
        var size = foundation.RootElement.GetProperty("CollidersSpec").GetProperty("BoxColliders")[0].GetProperty("Size");
        var footprint = station.RootElement.GetProperty("BlockObjectSpec").GetProperty("Size");
        Assert.Equal(footprint.GetProperty("X").GetInt32(), size.GetProperty("X").GetDouble());
        Assert.Equal(footprint.GetProperty("Y").GetInt32(), size.GetProperty("Z").GetDouble());
    }

    [Theory]
    [InlineData("IronTeeth")]
    [InlineData("Folktails")]
    public void NativeOrdinaryBuildingsProvideTheSameRequiredParent(string faction)
    {
        using var native = new NativeManagedTestContext();
        using var zip = ZipFile.OpenRead(Path.Combine(native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var stream = zip.GetEntry($"Buildings/DistrictManagement/BuildersHut/BuildersHut.{faction}.blueprint.json")!.Open();
        using var building = JsonDocument.Parse(stream);
        Assert.True(CanSpawnDoorstep(native, building.RootElement));
        var model = Deserialize(native, building.RootElement.GetProperty("BuildingModelSpec"), "Timberborn.Buildings", "BuildingModelSpec");
        string name = (string)model.GetType().GetProperty("UnfinishedModelName")!.GetValue(model)!;
        Assert.True(building.RootElement.GetProperty("Children").TryGetProperty(name, out _));
    }

    private static bool CanSpawnDoorstep(NativeManagedTestContext native, JsonElement definition)
    {
        Type T(string assembly, string name) => native.LoadNative(assembly).GetType(assembly + "." + name)!;
        var blockType = T("Timberborn.BlockSystem", "BlockObject");
        var block = RuntimeHelpers.GetUninitializedObject(blockType);
        blockType.GetField("_blockObjectSpec", Private)!.SetValue(block,
            Deserialize(native, definition.GetProperty("BlockObjectSpec"), "Timberborn.BlockSystem", "BlockObjectSpec"));
        // Supply the already-positioned entrance presence from entity placement. The native
        // predicate only tests its presence; coordinates below come from the real block spec.
        if (definition.GetProperty("BlockObjectSpec").GetProperty("Entrance").GetProperty("HasEntrance").GetBoolean())
            blockType.GetField("<PositionedEntrance>k__BackingField", Private)!.SetValue(block,
                RuntimeHelpers.GetUninitializedObject(T("Timberborn.BlockSystem", "PositionedEntrance")));
        var components = new List<object>();
        var disabler = T("Timberborn.BuildingDoorsteps", "DoorstepSpawnDisablerSpec");
        if (definition.TryGetProperty("DoorstepSpawnDisablerSpec", out _)) components.Add(Activator.CreateInstance(disabler)!);
        var readOnlyType = T("Timberborn.Common", "ReadOnlyList`1").MakeGenericType(typeof(object));
        var readOnly = Activator.CreateInstance(readOnlyType, Private, null, [components], null)!;
        var mapType = T("Timberborn.BaseComponentSystem", "TypeIndexMap");
        var map = Activator.CreateInstance(mapType)!;
        mapType.GetMethod("CacheType")!.MakeGenericMethod(disabler).Invoke(map, [readOnly]);
        var cacheType = T("Timberborn.BaseComponentSystem", "ComponentCache");
        var cache = RuntimeHelpers.GetUninitializedObject(cacheType);
        cacheType.GetField("_components", Private)!.SetValue(cache, components);
        cacheType.GetField("_typeIndexMap", Private)!.SetValue(cache, map);
        T("Timberborn.BaseComponentSystem", "BaseComponent").GetField("_componentCache", Private)!.SetValue(block, cache);
        return (bool)T("Timberborn.BuildingDoorsteps", "BuildingDoorstepSpawner")
            .GetMethod("CanSpawnDoorstep", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [block])!;
    }

    private static object Deserialize(NativeManagedTestContext native, JsonElement json, string assembly, string name) =>
        native.LoadNative("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert")!
            .GetMethod("DeserializeObject", [typeof(string), typeof(Type)])!
            .Invoke(null, [json.GetRawText(), native.LoadNative(assembly).GetType(assembly + "." + name)!])!;

    private static string StationPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "src/Wildfire.Timberborn/Data/Buildings/FireResponse/WardenStation.IronTeeth.blueprint.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Packaged Warden station blueprint was not found.");
    }

    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
}
