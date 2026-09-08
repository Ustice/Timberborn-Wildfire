using Wildfire.Timberborn.Persistence;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedMaterialNativePersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ActualNativeSingletonSaverAndObjectLoaderPreserveTheSinglePairedPayload(bool completeHistory)
    {
        using var native = new NativeManagedTestContext();
        Type Type(string assembly, string name) => native.LoadNative(assembly).GetType(assembly + "." + name)!;
        object New(string assembly, string name, params object[] args) => Activator.CreateInstance(Type(assembly, name), args)!;
        var version = Type("Timberborn.Versioning", "Version").GetMethod("Create")!.Invoke(null, new object[] { "1.0.0.0" })!;
        var world = New("Timberborn.WorldSerialization", "SerializedWorld", version);
        var singletonSaver = New("Timberborn.WorldPersistence", "SingletonSaver", world);
        var singletonKey = New("Timberborn.WorldPersistence", "SingletonKey", "WildfireRuntime");
        var saver = NativeInjuryFixture.Call(singletonSaver, "GetSingleton", singletonKey)!;
        var propertyType = Type("Timberborn.Persistence", "PropertyKey`1").MakeGenericType(typeof(string));
        var property = Activator.CreateInstance(propertyType, "Snapshot")!;
        string encoded = TimberbornWildfirePersistenceCodec.Encode(completeHistory ? OwnedConsequenceHistoryCodecTests.Fixture() : OwnedMaterialPersistenceTests.Fixture());
        NativeInjuryFixture.Call(saver, "Set", property, encoded);
        var serialized = NativeInjuryFixture.Call(world, "GetSingleton", "WildfireRuntime")!;
        var loader = New("Timberborn.Persistence", "ObjectLoader", serialized);
        string loaded = (string)NativeInjuryFixture.Call(loader, "Get", property)!;
        Assert.Equal(encoded, loaded);
        var restored = TimberbornWildfirePersistenceCodec.Decode(loaded).OwnedMaterial!;
        Assert.Equal((uint)7, restored.CaptureSimulation().MaterialAuthority.Archives.Single().Identity.TargetId);
        Assert.Equal(3, restored.Bindings.Entities.Single().Slots.Count);
        Assert.Equal(completeHistory, restored.HistoryCapability == OwnedConsequenceHistoryCapability.Complete);
        if (completeHistory) Assert.Equal(1, restored.History!.StorageCredits.Single().FractionalBudget);
        // This executes native managed serialization, not world file creation or GPU/native world publication.
    }
}
