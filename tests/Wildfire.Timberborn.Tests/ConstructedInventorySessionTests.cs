using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class ConstructedInventorySessionTests
{
    private static readonly Guid Id = new("00000000-0000-0000-0000-000000000071");
    private static readonly FireGrid Grid = new(1, 1, 2);
    private static readonly TimberbornInventoryDeclaration Output = new(TimberbornNativeInventoryRole.SimpleOutput, "Workshop.Output");
    private static readonly TimberbornInventoryDeclaration Input = new(TimberbornNativeInventoryRole.Manufactory, "Workshop.Input");
    private static readonly TimberbornInventoryDeclaration[] Declarations = [Output, Input];
    private static readonly TimberbornBurnDamageTargetKey Key = new(TimberbornBurnDamageIdentity.ForEntity(Id, NativeBurnTargetFamily.Structure));

    [Fact]
    public void CompleteInitialAndRestoreKeepExactPartsAndOriginalAccountingThenSpendOneGuardedBudget()
    {
        var f = new Fixture(); using var initial = f.Prepare();
        var body = f.Capture().Bodies.Single();
        var parts = TimberbornMaterialProjectionCompiler.Compile(body).Parts;
        Assert.Equal(2, parts.Count); Assert.Single(parts, part => part == TimberbornMaterialPart.StoredGood("Log"));
        Assert.Equal(new[] { 3, 2 }, body.Inventories.Select(inventory => inventory.Stock.Single().Amount));
        Assert.DoesNotContain(TimberbornInitialCompositionGap.MultipleInventoryRoles, body.CompositionGaps);
        Assert.Equal((3 + 5) * 12, initial.Damage.States[Key].DamageCapacity); // Existing construction plus selected-stock accounting.
        var empty = TimberbornWildfirePersistenceSnapshot.Empty;
        var saved = initial.Capture(empty.AshField, empty.BeaverBehavior);
        var encoded = TimberbornWildfirePersistenceCodec.Encode(saved);
        saved = TimberbornWildfirePersistenceCodec.Decode(encoded);
        Assert.Equal(Declarations, saved.OwnedMaterial!.History!.NativeDefinitions!.Get(Id).InventoryDeclarations);

        // Native inventory save/current state is authoritative. Restore never recomputes body capacity from these reduced goods.
        f.Inventory.Stock[Output] = 1; f.Inventory.Stock[Input] = 1;
        using var restored = f.Restore(saved);
        Assert.Equal(96, restored.Damage.States[Key].DamageCapacity);
        Assert.Equal(encoded, TimberbornWildfirePersistenceCodec.Encode(restored.Capture(empty.AshField, empty.BeaverBehavior)));
        Assert.Equal(TimberbornDesiredWorldCapability.CompleteStaged, restored.DesiredWorldCapability);
        Assert.Empty(f.Inventory.Calls);
        var sim = restored.Simulator.CaptureSnapshot();
        int mutations = 0;
        f.Inventory.BeforeConsume = () =>
        {
            Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(() => f.Guard.TransferInventory(() => mutations++));
            Assert.Throws<InvalidOperationException>(() => restored.Capture(empty.AshField, empty.BeaverBehavior));
        };
        var result = restored.Consumer.Consume(1, [new(1, PackedCell.Pack(15, 10, 3, 0, 0, 1),
            PackedCell.Pack(11, 10, 3, 0, 0, 1), sim.TargetIds[1], sim.SlotIds[1])]);
        Assert.Equal(2, result.Storage.DestroyedItems); Assert.Equal(Declarations, f.Inventory.Calls);
        Assert.All(f.Inventory.Stock.Values, amount => Assert.Equal(0, amount));
        Assert.Equal(4, restored.Damage.States[Key].DamageTaken); Assert.Equal(1, result.Damage.DamageAppliedTargetCount);
        Assert.Equal(0, mutations); Assert.False(f.Guard.IsIndeterminate); f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void CompleteRestoreRejectsCoherentCurrentRenameAgainstOriginalWitnessBeforeAllocation()
    {
        var f = new Fixture();
        using var initial = f.Prepare();
        var empty = TimberbornWildfirePersistenceSnapshot.Empty;
        var saved = initial.Capture(empty.AshField, empty.BeaverBehavior);
        int allocations = f.Allocations;
        f.Change = "renamed-current";
        Assert.Throws<ArgumentException>(() => f.Restore(saved));
        Assert.Equal(allocations, f.Allocations);
        Assert.Empty(f.Inventory.Calls);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void ExactMissingOrChangedDeclarationsAndUnknownGoodsRejectBeforeAllocation()
    {
        foreach (string change in new[] { "missing-evidence", "missing-row", "wrong-name", "unsupported-empty", "unknown-good" })
        {
            var f = new Fixture { Change = change };
            var error = Record.Exception(() => f.Prepare());
            Assert.True(error is ArgumentException or NotSupportedException, error?.ToString());
            Assert.Equal(0, f.Allocations); Assert.Empty(f.Inventory.Calls); f.Guard.ThrowIfSaveUnsafe();
        }
    }

    private sealed class Fixture
    {
        internal readonly NativeResourceTransaction Guard = new();
        internal readonly InventoryApi Inventory = new();
        private readonly OwnedConsequenceBatchTests.NativeFake _native = new();
        internal string? Change;
        internal int Allocations;
        internal Fixture() => _native.Live.Add(Id);
        private TimberbornOwnedNativeEffects Effects => new(_native, _native, _native, Inventory, _native);
        internal TimberbornInitialWorldCapture Capture()
        {
            var rows = Declarations.Select(declaration => new TimberbornInventoryMaterial(declaration, true,
                Inventory.Stock[declaration] > 0 ? [new(Change == "unknown-good" ? "Unknown.Native.Good" : "Log", Inventory.Stock[declaration])] : [])).ToArray();
            if (Change == "missing-row") rows = rows.Take(1).ToArray();
            if (Change == "wrong-name") rows[1] = new(new(Input.Role, "Changed.Input"), true, rows[1].Stock);
            var declarations = Change == "unsupported-empty" ? new[] { Output, Input, new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.WardenStation, "Station.Empty") } : Declarations;
            if (Change == "renamed-current")
            {
                var renamed = new TimberbornInventoryDeclaration(Input.Role, "Renamed.Input");
                declarations = [Output, renamed];
                rows[1] = new(renamed, rows[1].Enabled, rows[1].Stock);
            }
            var body = new TimberbornInitialMaterialBody(Id, "LumberMill.Folktails", TimberbornInitialBodyShape.Structure,
                [new(new(0, 0, 0), 1)], [], rows, [new("Log", 3)]);
            var environment = TimberbornInitialEnvironmentCapture.ForOwnedDomain(new(Grid, new(1, 1, 1)), [], [], []);
            return Change == "missing-evidence" ? new(Grid, [body], [], [], environment) :
                new(Grid, [body], [], [], environment, new([new(Id, declarations)]));
        }
        internal TimberbornOwnedWorldSession<Simulator> Prepare() => TimberbornOwnedWorldSession<Simulator>.PrepareInitial(Grid,
            _ => Capture(), _ => new([new(Id, TimberbornInitialAccountingBasis.NativeResourceAmounts, [],
                Declarations.Select(declaration => new TimberbornInitialInventorySelection(declaration, TimberbornInitialInventoryUse.PhysicalStock)))], FireSimParameters.Default, 7),
            saved => { Allocations++; return new(saved); }, Effects, Guard);
        internal TimberbornOwnedWorldSession<Simulator> Restore(TimberbornWildfirePersistenceSnapshot saved) =>
            TimberbornOwnedWorldSession<Simulator>.PrepareCompleteRestore(saved, snapshot => { Allocations++; return new(snapshot); },
                (_, ids) =>
                {
                    Assert.Equal(new[] { Id }, ids); var world = Capture();
                    return new(world, world.Bodies, [new(Id, null, null, false, world.InventoryDeclarations!.Get(Id))]);
                }, Effects, Guard);
    }

    private sealed class InventoryApi : ITimberbornOwnedStorageInventoryApi
    {
        internal readonly Dictionary<TimberbornInventoryDeclaration, int> Stock = new() { [Output] = 3, [Input] = 2 };
        internal readonly List<TimberbornInventoryDeclaration> Calls = [];
        internal Action? BeforeConsume;
        public TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner)
        {
            Assert.Equal(Id, owner.EntityId); Assert.Equal(Declarations, owner.Declarations);
            return new(TimberbornOwnedInventoryStatus.Available, Declarations.Select(declaration =>
                new TimberbornOwnedInventoryRow(declaration, TimberbornOwnedInventoryStatus.Available,
                    Stock[declaration] > 0 ? [new("Log", Stock[declaration])] : [])).ToArray());
        }
        public TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner,
            TimberbornInventoryDeclaration declaration, TimberbornStoredGoodStack requested)
        {
            Assert.Equal(Declarations, owner.Declarations); BeforeConsume?.Invoke();
            int removed = Math.Min(requested.Amount, Stock[declaration]); Stock[declaration] -= removed; Calls.Add(declaration);
            return new(TimberbornOwnedInventoryStatus.Available, removed);
        }
    }
    private sealed class Simulator(FireSimSnapshot saved) : IGpuFireSimulator, IFireSimSnapshotSimulator, IDisposable
    {
        public int Width => saved.Grid.Width; public int Height => saved.Grid.Height; public int Depth => saved.Grid.Depth;
        public FireSimSnapshotCapability SnapshotCapability => FireSimSnapshotCapability.CompleteMaterialHistory;
        public FireSimSnapshot CaptureSnapshot() => saved;
        public void Dispose() { }
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }
}
