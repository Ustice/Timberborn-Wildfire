using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class InitialVegetationCompilationTests
{
    [Theory]
    [InlineData("BlueberryBush", "NaturalResources/Bushes/Blueberry/BlueberryBush.blueprint.json", 1)]
    [InlineData("Dandelion", "NaturalResources/Bushes/Dandelion/Dandelion.blueprint.json", 2)]
    public void InstalledBushYieldCaptureAndFormationKeepProfileIdentityAndNativeQuantity(
        string specId, string entry, int expectedFlammability)
    {
        using var paths = new NativeManagedTestContext();
        using var archive = ZipFile.OpenRead(Path.Combine(paths.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var stream = archive.GetEntry(entry)!.Open();
        using var document = JsonDocument.Parse(stream);
        var declaration = document.RootElement.GetProperty("GatherableSpec").GetProperty("Yielder");
        string name = declaration.GetProperty("YielderComponentName").GetString()!;
        string good = declaration.GetProperty("Yield").GetProperty("Id").GetString()!;
        int declared = declaration.GetProperty("Yield").GetProperty("Amount").GetInt32();

        using var native = new NativePartialYieldFixture();
        var yielder = native.NewYielder(name, good);
        var spec = native.YielderType.GetProperty("YielderSpec")!.GetValue(yielder)!;
        var yieldSpec = spec.GetType().GetProperty("Yield")!.GetValue(spec)!;
        yieldSpec.GetType().GetProperty("Amount")!.SetValue(yieldSpec, declared);
        native.YielderType.GetMethod("Initialize")!.Invoke(yielder,
            [spec, Activator.CreateInstance(native.AmountType, good, declared), null, "Gather"]);
        int actual = declared - 1;
        NativePartialYieldFixture.Set(yielder, "_yield", Activator.CreateInstance(native.AmountType, good, actual)!);
        int callbacks = 0;
        native.YielderType.GetEvent("YieldAdded")!.AddEventHandler(yielder, (EventHandler)((_, _) => callbacks++));
        native.YielderType.GetEvent("YieldDecreased")!.AddEventHandler(yielder, (EventHandler)((_, _) => callbacks++));
        var helper = native.ModType("Wildfire.Timberborn.Runtime.TimberbornInitialWorldProjectionProvider")
            .GetMethod("CaptureNamedYield", BindingFlags.NonPublic | BindingFlags.Static)!;
        var role = Enum.Parse(native.ModType("Wildfire.Timberborn.Mapping.TimberbornCapturedYieldRole"), "Gatherable");
        var captured = helper.Invoke(null, [yielder, role, false])!;
        T Read<T>(string property) => (T)captured.GetType().GetProperty(property)!.GetValue(captured)!;
        var named = new TimberbornNamedYieldMaterial(TimberbornCapturedYieldRole.Gatherable, Read<string>("ComponentName"),
            Read<string>("ActualGoodId"), Read<int>("ActualAmount"), Read<string>("DeclaredGoodId"),
            Read<int>("DeclaredAmount"), false, Read<bool>("YieldEnabled"));

        var id = Guid.NewGuid();
        var grid = new FireGrid(2, 1, 1);
        var footprint = new[] { new TimberbornMaterialFootprintSlot(new(0, 0, 0), 1) };
        var body = new TimberbornInitialMaterialBody(id, specId, TimberbornInitialBodyShape.Vegetation, footprint, [named], [], null);
        var capture = new TimberbornInitialWorldCapture(grid, [body], [], [], new(grid, [], [], []));
        var compiled = TimberbornInitialBodyCompiler.Compile(capture, [new(id, TimberbornInitialAccountingBasis.NativeResourceAmounts,
            [new(name, TimberbornCapturedYieldRole.Gatherable, TimberbornInitialYieldUse.Actual)], [])]);
        var projection = Assert.Single(compiled.Projections);
        Assert.Equal(footprint, projection.Footprint);
        var part = Assert.Single(projection.Parts);
        Assert.Equal(WildfireMaterialClass.Vegetation, part.MaterialClass);
        Assert.Equal(1, part.InitialFuel);
        Assert.Equal(expectedFlammability, part.Flammability);
        var descriptor = Assert.Single(compiled.Registrations).DescriptorOverride!;
        Assert.Equal(TimberbornBurnDamageTargetKind.Resource, descriptor.TargetKind);
        Assert.Equal(TimberbornBurnMaterialKind.Organic, descriptor.MaterialKind);
        Assert.Equal(actual, Assert.Single(descriptor.ResourceYields).Amount);
        Assert.Empty(descriptor.ConstructionResources);

        var registry = new TimberbornNativeMaterialRegistry(grid, []);
        registry.Reconcile(compiled.Projections, []);
        var resolved = registry.ResolveCell(1);
        Assert.Equal(WildfireMaterialClass.Vegetation, resolved.Profile.MaterialClass);
        Assert.Equal(1, PackedCell.Fuel(resolved.PackedDefinition));
        var effects = new OwnedConsequenceBatchTests.NativeFake();
        effects.Live.Add(id);
        var guard = new NativeResourceTransaction();
        var consumer = TimberbornOwnedDeltaConsumer.CreateWithNativeDefinitions(registry, compiled.CreateDamage(grid),
            new(effects, effects, effects, effects, effects), guard, [body]);
        var history = consumer.CaptureHistory();
        Assert.Equal(NativeBurnTargetFamily.Crop, Assert.Single(history.Owners).Family);
        var witness = Assert.Single(history.NativeDefinitions!.Definitions);
        Assert.Equal(TimberbornInitialBodyShape.Vegetation, witness.Shape);
        Assert.Equal(declared, Assert.Single(witness.Yields).Amount);
        Assert.Equal(body.BodyProfile, witness.BodyProfile);
        Assert.Empty(effects.CropCalls);
        Assert.Equal(actual, native.RawQuantity(yielder));
        Assert.Equal(declared, native.InitialQuantity(yielder));
        Assert.Equal(0, callbacks);
        guard.ThrowIfSaveUnsafe();
    }

    [Theory]
    [InlineData("Pine")]
    [InlineData("Carrot")]
    [InlineData("UnreviewedBush")]
    public void VegetationDoesNotGuessAProfileForAnotherPhysicalCategory(string spec) =>
        Assert.Throws<ArgumentException>(() => TimberbornMaterialPart.Vegetation(spec));

    [Fact]
    public void ExistingInfrastructurePrototypesAreNotRekeyedAsConstructedBodies()
    {
        var id = Guid.NewGuid();
        var grid = new FireGrid(1, 1, 1);
        var body = new TimberbornInitialMaterialBody(id, "PowerShaft.Folktails", TimberbornInitialBodyShape.Infrastructure,
            [new(new(0, 0, 0), 0)], [], [], [new("Log", 1)]);
        Assert.Equal(NativeBurnTargetFamily.PowerInfrastructure, body.Family);
        Assert.Throws<NotSupportedException>(() => TimberbornInitialBodyCompiler.Compile(new(grid, [body], [], [], new(grid, [], [], [])),
            [new(id, TimberbornInitialAccountingBasis.CatalogBodyProfile, [], [])]));
        Assert.Throws<ArgumentException>(() => OwnedNativeDefinitionWitness.Capture(body));
        Assert.Equal(NativeBurnTargetFamily.PowerInfrastructure, body.Family);
    }
}
