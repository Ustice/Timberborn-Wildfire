using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenPreviewCoordinatesTests
{
    [Fact]
    public void PackagedStationComposesNativeGroundPreviewUsingOrdinaryCenteredPivot()
    {
        using var f = new Fixture();
        using var station = JsonDocument.Parse(File.ReadAllText(StationPath()));
        var placeable = f.Spec(station.RootElement, "PlaceableBlockObjectSpec");
        var block = f.Spec(station.RootElement, "BlockObjectSpec");
        // Execute the exact native failing method, rather than merely asserting a JSON key.
        var coordinate = f.Compose(placeable, block);
        Assert.Equal((11, 14, 2), f.Coordinate(coordinate));
        var pivot = placeable.GetType().GetProperty("CustomPivot")!.GetValue(placeable)!;
        Assert.Equal(false, pivot.GetType().GetProperty("HasCustomPivot")!.GetValue(pivot));
    }

    [Theory]
    [InlineData("IronTeeth")]
    [InlineData("Folktails")]
    public void InstalledOrdinaryBuildingProvidesRequiredPivotAndOmissionReproducesNativeCrash(string faction)
    {
        using var f = new Fixture();
        using var zip = ZipFile.OpenRead(Path.Combine(f.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var stream = zip.GetEntry($"Buildings/DistrictManagement/BuildersHut/BuildersHut.{faction}.blueprint.json")!.Open();
        using var native = JsonDocument.Parse(stream);
        var placeable = f.Spec(native.RootElement, "PlaceableBlockObjectSpec");
        var block = f.Spec(native.RootElement, "BlockObjectSpec");
        Assert.NotNull(f.Compose(placeable, block));
        // Real native deserialization leaves the omitted nested reference null. No prefab,
        // mutable preview object or malformed block list is needed to cause this exception.
        var omitted = f.Deserialize("{\"ToolShape\":\"Square\",\"Layout\":\"Single\"}", "PlaceableBlockObjectSpec");
        Assert.Null(omitted.GetType().GetProperty("CustomPivot")!.GetValue(omitted));
        var error = Assert.Throws<TargetInvocationException>(() => f.Compose(omitted, block));
        Assert.IsType<NullReferenceException>(error.InnerException);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        internal string ManagedPath => _native.ManagedPath;
        internal object Spec(JsonElement root, string name) => Deserialize(root.GetProperty(name).GetRawText(), name);
        internal object Deserialize(string json, string name)
        {
            var jsonConvert = _native.LoadNative("Newtonsoft.Json").GetType("Newtonsoft.Json.JsonConvert")!;
            return jsonConvert.GetMethod("DeserializeObject", [typeof(string), typeof(Type)])!
                .Invoke(null, [json, T("Timberborn.BlockSystem", name)])!;
        }
        internal object Compose(object placeable, object block)
        {
            var vector = _native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3")!;
            var grid = _native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!;
            var candidate = Activator.CreateInstance(T("Timberborn.GridTraversing", "TraversedCoordinates"),
                Activator.CreateInstance(grid, 12, 15, 1), Activator.CreateInstance(grid, 0, 0, 1),
                Activator.CreateInstance(vector, 12.25f, 15.75f, 2f));
            var method = T("Timberborn.BlockObjectPickingSystem", "BlockObjectPreviewPicker")
                .GetMethod("ComposeCoordinates", BindingFlags.NonPublic | BindingFlags.Static)!;
            var orientation = Enum.Parse(T("Timberborn.Coordinates", "Orientation"), "Cw0");
            return method.Invoke(null, [orientation, placeable.GetType().GetProperty("CustomPivot")!.GetValue(placeable), block, candidate])!;
        }
        internal (int, int, int) Coordinate(object value) =>
            ((int)value.GetType().GetProperty("x")!.GetValue(value)!,
             (int)value.GetType().GetProperty("y")!.GetValue(value)!,
             (int)value.GetType().GetProperty("z")!.GetValue(value)!);
        private Type T(string assembly, string name) => _native.LoadNative(assembly).GetType(assembly + "." + name)!;
        public void Dispose() => _native.Dispose();
    }

    private static string StationPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "src/Wildfire.Timberborn/Data/Buildings/FireResponse/WardenStation.IronTeeth.blueprint.json");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException("Packaged Warden station blueprint was not found.");
    }
}
