using System.Runtime.CompilerServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class TimberbornBurnDamageIdentityTests
{
    [Theory]
    [InlineData(NativeBurnTargetFamily.Stockpile)]
    [InlineData(NativeBurnTargetFamily.Structure)]
    [InlineData(NativeBurnTargetFamily.Tree)]
    [InlineData(NativeBurnTargetFamily.Crop)]
    [InlineData(NativeBurnTargetFamily.SelectedCrop)]
    [InlineData(NativeBurnTargetFamily.PowerInfrastructure)]
    [InlineData(NativeBurnTargetFamily.WaterInfrastructure)]
    [InlineData(NativeBurnTargetFamily.PathInfrastructure)]
    public void NativeEntityRecreationAndChangedEnumerationRetainPersistedDamage(NativeBurnTargetFamily family)
    {
        using var native = new NativeManagedTestContext();
        var entityType = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityComponent")!;
        Guid id = Guid.Parse("83f1c90d-65f9-4fe3-9c6d-9bfe70b8674f");
        Guid replacementId = Guid.Parse("278b6c72-409a-4c7e-884c-7241b3647d6c");
        object originalEntity = Entity(id);
        object reloadedEntity = Entity(id);
        Assert.NotSame(originalEntity, reloadedEntity);
        var originalKey = Key(originalEntity);
        var reloadedKey = Key(reloadedEntity);
        var replacementKey = Key(Entity(replacementId));
        var original = Service(originalKey, replacementKey);
        original.RestoreState(original.CaptureState().Select(state => state.TargetKey == originalKey
            ? state with { DamageTaken = 9, LastDamagedTick = 77 }
            : state).ToArray());
        var saved = TimberbornWildfirePersistenceSnapshot.Empty with
        { Consequences = TimberbornWildfirePersistenceCodec.CaptureConsequences(original) };
        var decoded = TimberbornWildfirePersistenceCodec.Decode(TimberbornWildfirePersistenceCodec.Encode(saved));
        var reloaded = Service(replacementKey, reloadedKey); // Reordered objects and footprints after load.
        TimberbornWildfirePersistenceCodec.RestoreConsequences(reloaded, decoded.Consequences);

        Assert.Equal(9, reloaded.States[reloadedKey].DamageTaken);
        Assert.Equal(77u, reloaded.States[reloadedKey].LastDamagedTick);
        Assert.Equal(0, reloaded.States[replacementKey].DamageTaken);

        object Entity(Guid entityId)
        {
            object entity = RuntimeHelpers.GetUninitializedObject(entityType);
            // Actual native setter/getter, without constructing a Unity entity or game world.
            entityType.GetMethod("SetEntityId", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.Invoke(entity, [entityId]);
            return entity;
        }
        TimberbornBurnDamageTargetKey Key(object entity) => new(TimberbornBurnDamageIdentity.ForEntity(
            (Guid)entityType.GetProperty("EntityId")!.GetValue(entity)!, family));
    }

    [Fact]
    public void FamiliesDoNotAliasAndUnidentifiedEntitiesAreRejected()
    {
        Guid id = Guid.NewGuid();
        var keys = Enum.GetValues<NativeBurnTargetFamily>()
            .Select(family => TimberbornBurnDamageIdentity.ForEntity(id, family)).ToArray();
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.Throws<InvalidOperationException>(() => TimberbornBurnDamageIdentity.ForEntity(Guid.Empty, NativeBurnTargetFamily.Tree));
    }

    [Fact]
    public void LegacyHashDamageIsReportedWithoutGuessingItsReplacementEntity()
    {
        var key = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(Guid.NewGuid(), NativeBurnTargetFamily.Tree));
        var service = Service(key);
        var summary = TimberbornWildfirePersistenceCodec.RestoreConsequences(service, new(
        [
            new("tree_cuttable:123456", 9, 77),
            new("structure:654321", 0, 0),
            new("custom-target", 3, 80),
        ]));
        Assert.Equal(new TimberbornConsequenceRestoreSummary(0, 3, 1), summary);
        Assert.Equal(0, service.States[key].DamageTaken);
        Assert.False(TimberbornBurnDamageIdentity.IsLegacyRuntimeHash(key.StableId));
        Assert.False(TimberbornBurnDamageIdentity.IsLegacyRuntimeHash("custom:123456"));
    }

    private static TimberbornBurnDamageService Service(params TimberbornBurnDamageTargetKey[] keys)
    {
        var descriptor = new TimberbornBurnDamageDescriptor("Tree.Pine", TimberbornBurnDamageTargetKind.Tree,
            TimberbornBurnMaterialKind.Wood, resourceYields: [new TimberbornBurnDamageResourceStack("Log", 2)]);
        var service = new TimberbornBurnDamageService(new TimberbornBurnDamageDescriptorCatalog([descriptor]),
            new TimberbornBurnDamageCapacityCalculator());
        service.RegisterTargets(new FireGrid(keys.Length, 1, 1), keys.Select((key, index) =>
            new TimberbornBurnDamageTargetRegistration(key, "Tree.Pine", [new TimberbornCellCoordinates(index, 0, 0)])));
        return service;
    }
}
