using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornWildfirePersistenceTests
{
    [Fact]
    public void CodecRoundTripsFireSimAshAndConsequenceState()
    {
        TimberbornWildfirePersistenceSnapshot snapshot = new(
            TimberbornWildfirePersistenceSnapshot.CurrentPersistenceVersion,
            new TimberbornFireSimPersistenceSnapshot(
                Width: 2,
                Height: 1,
                Depth: 1,
                Tick: 42,
                Cells:
                [
                    PackedCell.Pack(fuel: 8, heat: 4, flammability: 3, water: 0, terrain: 1, burningLevel: 1),
                    PackedCell.Pack(fuel: 3, heat: 7, flammability: 2, water: 1, terrain: 1, burningLevel: 2),
                ],
                TransportFields: [1u, 2u]),
            new TimberbornAshFieldSnapshot(
                TimberbornAshFieldEntry.CurrentPersistenceVersion,
                [
                    new TimberbornAshFieldEntry(
                        CellIndex: 1,
                        WildfireAshQuality.Fertile,
                        Strength: 80,
                        TimberbornAshSourceKind.Tree,
                        CreatedTick: 40,
                        UpdatedTick: 42,
                        TimberbornAshFieldEntry.CurrentPersistenceVersion),
                ]),
            new TimberbornBeaverFieldBehaviorSnapshot(
                TimberbornBeaverFieldBehaviorSnapshot.CurrentPersistenceVersion,
                [
                    new TimberbornBeaverFieldBehaviorStateEntry(
                        TimberbornBeaverFieldBehaviorStateEntry.CurrentPersistenceVersion,
                        "beaver:1",
                        TimberbornBeaverFieldBehaviorVariant.FireHeat,
                        TimberbornBeaverFieldBehaviorAction.SafeNoOp,
                        LastDecisionTick: 40,
                        ConsecutiveExposedSamples: 2,
                        IsExposed: true),
                ]),
            new TimberbornConsequencePersistenceSnapshot(
                [
                    new TimberbornBurnDamagePersistenceEntry("tree:pine:1", DamageTaken: 12, LastDamagedTick: 42),
                ]));

        TimberbornWildfirePersistenceSnapshot roundTrip =
            TimberbornWildfirePersistenceCodec.Decode(TimberbornWildfirePersistenceCodec.Encode(snapshot));

        Assert.NotNull(roundTrip.FireSim);
        Assert.Equal(42u, roundTrip.FireSim.Tick);
        Assert.Equal(snapshot.FireSim!.Cells, roundTrip.FireSim.Cells);
        Assert.Equal(snapshot.FireSim.TransportFields, roundTrip.FireSim.TransportFields);
        TimberbornAshFieldEntry ashEntry = Assert.Single(roundTrip.AshField.Entries);
        Assert.Equal(WildfireAshQuality.Fertile, ashEntry.Quality);
        Assert.Equal(80, ashEntry.Strength);
        TimberbornBeaverFieldBehaviorStateEntry behaviorEntry = Assert.Single(roundTrip.BeaverBehavior.Entries);
        Assert.Equal("beaver:1", behaviorEntry.BeaverId);
        Assert.Equal(TimberbornBeaverFieldBehaviorVariant.FireHeat, behaviorEntry.LastVariant);
        Assert.True(behaviorEntry.IsExposed);
        TimberbornBurnDamagePersistenceEntry burnEntry = Assert.Single(roundTrip.Consequences.BurnDamageStates);
        Assert.Equal("tree:pine:1", burnEntry.TargetKey);
        Assert.Equal(12, burnEntry.DamageTaken);
    }

    [Fact]
    public void RestoreConsequencesAppliesDamageToMatchingRegisteredTargetsOnly()
    {
        FireGrid grid = new(2, 1, 1);
        TimberbornBurnDamageService service = CreateService(
            TreeDescriptor("Tree.Pine", "Log", amount: 2),
            TreeDescriptor("Tree.Oak", "Log", amount: 2));
        TimberbornBurnDamageTargetKey pineKey = new("tree-pine-1");
        TimberbornBurnDamageTargetKey oakKey = new("tree-oak-1");
        service.RegisterTargets(
            grid,
            [
                Registration(pineKey, "Tree.Pine", [new TimberbornCellCoordinates(0, 0, 0)]),
                Registration(oakKey, "Tree.Oak", [new TimberbornCellCoordinates(1, 0, 0)]),
            ]);

        TimberbornWildfirePersistenceCodec.RestoreConsequences(
            service,
            new TimberbornConsequencePersistenceSnapshot(
                [
                    new TimberbornBurnDamagePersistenceEntry("tree-pine-1", DamageTaken: 9, LastDamagedTick: 77),
                    new TimberbornBurnDamagePersistenceEntry("missing-target", DamageTaken: 12, LastDamagedTick: 88),
                ]));

        Assert.Equal(9, service.States[pineKey].DamageTaken);
        Assert.Equal(77u, service.States[pineKey].LastDamagedTick);
        Assert.Equal(0, service.States[oakKey].DamageTaken);
    }

    private static TimberbornBurnDamageService CreateService(params TimberbornBurnDamageDescriptor[] descriptors)
    {
        return new TimberbornBurnDamageService(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            new TimberbornBurnDamageCapacityCalculator());
    }

    private static TimberbornBurnDamageDescriptor TreeDescriptor(string specId, string resourceId, int amount)
    {
        return new TimberbornBurnDamageDescriptor(
            specId,
            TimberbornBurnDamageTargetKind.Tree,
            TimberbornBurnMaterialKind.Wood,
            resourceYields: [new TimberbornBurnDamageResourceStack(resourceId, amount)]);
    }

    private static TimberbornBurnDamageTargetRegistration Registration(
        TimberbornBurnDamageTargetKey targetKey,
        string specId,
        IReadOnlyList<TimberbornCellCoordinates> ownedCells)
    {
        return new TimberbornBurnDamageTargetRegistration(targetKey, specId, ownedCells);
    }
}
