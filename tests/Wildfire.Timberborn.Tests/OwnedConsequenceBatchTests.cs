using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedConsequenceBatchTests
{
    private static readonly Guid Tree = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Crop = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Stock = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Structure = new("00000000-0000-0000-0000-000000000004");
    private static readonly Guid[] Ids = [Tree, Crop, Stock, Structure];
    private static readonly NativeBurnTargetFamily[] Families = [NativeBurnTargetFamily.Tree, NativeBurnTargetFamily.Crop,
        NativeBurnTargetFamily.Stockpile, NativeBurnTargetFamily.Structure];
    private static readonly FireGrid Grid = new(4, 2, 1);

    [Fact]
    public void MixedFamiliesShareOneBodyPassAndKeepSubsetTelemetry()
    {
        var f = new Fixture();
        var result = f.Consumer.Consume(1, [f.Delta(Tree, 3), f.Delta(Crop, 3), f.Delta(Stock, 2),
            f.Delta(Stock, 4, 6), f.Delta(Structure, 2)]);
        Assert.Equal(1, f.Native.DamagePasses);
        Assert.Equal(4, result.Damage.DamageAppliedTargetCount);
        Assert.Equal(0, result.Damage.DuplicateCellSuppressedCount);
        Assert.Equal(0, result.Trees.CoalescedCellCount);
        Assert.Equal(0, result.Crops.CoalescedCellCount);
        Assert.Equal(2, f.Damage.States[Key(Structure)].DamageTaken); // Body+inventory share one damage state.
        Assert.Equal(6, f.Damage.States[Key(Stock)].DamageTaken);
        Assert.Equal(4, result.Storage.DestroyedItems);
        Assert.NotEmpty(f.Native.TreeCalls);
        Assert.NotEmpty(f.Native.CropCalls);
        Assert.Equal(2, result.StructureRollbackUnavailableOwners);
        Assert.False(result.Capabilities.StructureRollbackSupported);
    }

    [Theory]
    [InlineData(2)] // Stockpile role retains its physical constructed Structure body.
    [InlineData(3)] // SimpleOutput belongs to the same kind of physical body.
    public void LiveConstructedOwnerWithoutInventoryStillTakesBodyDamageAndReportsUnavailableEffects(int ownerIndex)
    {
        var f = new Fixture();
        Guid owner = Ids[ownerIndex];
        f.Native.InventoryAvailable.Remove(owner);
        var result = f.Consumer.Consume(2, [f.Delta(owner, 4)]);
        Assert.Equal(TimberbornBurnDamageTargetKind.Structure, f.Damage.States[Key(owner)].TargetKind);
        Assert.Equal(4, f.Damage.States[Key(owner)].DamageTaken);
        Assert.Equal(0, result.NotLiveOwners);
        Assert.Equal(1, result.Storage.UnavailableInventories);
        Assert.Equal(0, result.Storage.DestroyedItems);
        Assert.Equal(1, result.StructureRollbackUnavailableOwners);
        Assert.Empty(f.Native.InventoryCalls);
    }

    [Fact]
    public void TrailingUnknownRejectsAllFamiliesBeforeBodyOrNativeEffects()
    {
        var f = new Fixture();
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(3,
            [f.Delta(Tree, 15), f.Delta(Crop, 15), f.Delta(Stock, 15), new CellDelta(0, 0xffff, 0, 999, 1)]));
        Assert.Equal(0, f.Native.DamagePasses);
        Assert.Empty(f.Native.TreeCalls);
        Assert.Empty(f.Native.CropCalls);
        Assert.Empty(f.Native.InventoryCalls);
        Assert.All(f.Damage.States.Values, state => Assert.Equal(0, state.DamageTaken));
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void MissingRequiredAdapterFailsConstructionBeforeAnyBodyEffect()
    {
        var f = new Fixture();
        var invalid = f.Effects with { Crops = null! };
        Assert.Throws<ArgumentException>(() => new TimberbornOwnedDeltaConsumer(f.Registry, f.Damage, invalid,
            f.Guard, f.Registrations));
        Assert.Equal(0, f.Native.DamagePasses);
    }

    [Fact]
    public void LostTrailingBodyRegistrationRejectsBeforeEarlierDamageEvenForZeroFuelLoss()
    {
        var f = new Fixture();
        f.Damage.RemoveTarget(Key(Crop));
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(4, [f.Delta(Tree, 15), f.Delta(Crop, 0)]));
        Assert.Equal(0, f.Damage.States[Key(Tree)].DamageTaken);
        Assert.Empty(f.Native.TreeCalls);
    }

    [Fact]
    public void TreeCallbackFailurePoisonsSharedGuardAndStopsOtherFamilyEffects()
    {
        var f = new Fixture();
        var failure = new ApplicationException("native callback after tree mutation");
        f.Native.AfterTree = () => throw failure;
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => f.Consumer.Consume(5,
            [f.Delta(Tree, 15), f.Delta(Crop, 15), f.Delta(Stock, 15)])));
        Assert.Equal(1, f.Native.TreeMutations);
        Assert.Contains(f.Native.TreeCalls, call => call.Kind == TimberbornTreeBurnConsequenceKind.KillTree);
        Assert.Empty(f.Native.CropCalls);
        Assert.Empty(f.Native.InventoryCalls);
        Assert.Equal(1, f.Native.DamagePasses);
        Assert.True(f.Guard.IsIndeterminate);
        Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
        Assert.Throws<InvalidOperationException>(() => f.Consumer.Consume(6, [f.Delta(Stock, 15)]));
        Assert.Equal(10, f.Native.Amounts[Stock]);
    }

    [Fact]
    public void EarlierFamilyDeletionMakesLaterNativeEffectsSkipExactOwner()
    {
        var f = new Fixture();
        f.Native.AfterTree = () => { f.Native.Live.Remove(Crop); f.Native.Live.Remove(Stock); };
        var result = f.Consumer.Consume(7, [f.Delta(Tree, 15), f.Delta(Crop, 15), f.Delta(Stock, 15)]);
        Assert.NotEmpty(f.Native.CropCalls); // Each raw crop call observes native disappearance itself.
        Assert.Equal(3, result.Damage.DamageAppliedTargetCount); // Body pass preceded the native callback.
        Assert.All(f.Native.CropResults, status => Assert.Equal(TimberbornCropBurnConsequenceStatus.NotLive, status));
        Assert.Equal(0, result.Crops.KilledCropCount);
        Assert.Equal(0, result.Crops.DestroyedGoodCount);
        Assert.Equal(1, result.Storage.NotLiveOwners);
        Assert.Empty(f.Native.InventoryCalls);
        Assert.Equal(10, f.Native.Amounts[Stock]);
    }

    [Fact]
    public void SaveAndRegistrationCannotInterleaveNativeFamilyCallback()
    {
        var f = new Fixture();
        f.Native.AfterTree = () =>
        {
            Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(() => f.Consumer.Register(f.Registrations[0]));
        };
        f.Consumer.Consume(8, [f.Delta(Tree, 15), f.Delta(Stock, 2)]);
        Assert.False(f.Guard.IsIndeterminate);
        f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void ConflictingBodyFamilyAndSelectedCropAliasRejectBeforeEffects()
    {
        var f = new Fixture();
        var second = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(Structure, NativeBurnTargetFamily.Stockpile));
        f.Damage.UpsertTarget(Grid, new(second, "Warehouse", [new(3, 0, 0)], 1));
        Assert.Throws<ArgumentException>(() => f.Consumer.Register(new(Structure, NativeBurnTargetFamily.Stockpile)));
        Assert.Throws<ArgumentException>(() => f.Consumer.Register(new(Crop, NativeBurnTargetFamily.SelectedCrop)));
        Assert.Equal(0, f.Native.DamagePasses);
    }

    private static TimberbornBurnDamageTargetKey Key(Guid id) => new(TimberbornBurnDamageIdentity.ForEntity(id,
        Families[Array.IndexOf(Ids, id)]));
    internal sealed class Fixture
    {
        internal readonly TimberbornNativeMaterialRegistry Registry = new(Grid, []);
        internal readonly NativeResourceTransaction Guard = new();
        internal readonly NativeFake Native = new();
        internal readonly TimberbornBurnDamageService Damage;
        internal readonly TimberbornOwnedNativeEffects Effects;
        internal readonly TimberbornOwnedBodyRegistration[] Registrations = Ids.Select((id, i) => new TimberbornOwnedBodyRegistration(id, Families[i])).ToArray();
        internal readonly TimberbornOwnedDeltaConsumer Consumer;
        internal Fixture()
        {
            string[] specs = ["Pine", "Carrot", "Warehouse", "Mill"];
            TimberbornMaterialPart[] parts = [TimberbornMaterialPart.Tree("Pine"), TimberbornMaterialPart.Crop("Carrot"),
                TimberbornMaterialPart.StoredGood("Log"), TimberbornMaterialPart.Building("LumberMill.Folktails")];
            Registry.Reconcile(Ids.Select((id, i) => new TimberbornMaterialProjection(id,
                [new(new(0, 0, 0), i), new(new(1, 0, 0), i + 4)], [parts[i]])), []);
            Damage = new(new TimberbornBurnDamageDescriptorCatalog([
                new("Pine", TimberbornBurnDamageTargetKind.Tree, TimberbornBurnMaterialKind.Wood, resourceYields: [new("Log", 10)]),
                new("Carrot", TimberbornBurnDamageTargetKind.Crop, TimberbornBurnMaterialKind.Organic, resourceYields: [new("Carrot", 10)]),
                new("Warehouse", TimberbornBurnDamageTargetKind.Structure, TimberbornBurnMaterialKind.Constructed, constructionResources: [new("Log", 20)]),
                new("Mill", TimberbornBurnDamageTargetKind.Structure, TimberbornBurnMaterialKind.Constructed, constructionResources: [new("Log", 20)])]), logSink: Native);
            Damage.RegisterTargets(Grid, Ids.Select((id, i) => new TimberbornBurnDamageTargetRegistration(Key(id), specs[i],
                [new(i, 0, 0), new(i, 1, 0)], 10)).ToArray());
            Effects = new(Native, Native, Native, Native, Native);
            Consumer = new(Registry, Damage, Effects, Guard, Registrations,
                new TimberbornResourceFuelCatalog([new("Log", 2, 3, false, false, true)]));
        }
        internal CellDelta Delta(Guid id, int loss, int? cell = null) => new(cell ?? Array.IndexOf(Ids, id),
            PackedCell.Pack(15, 10, 3, 0, 0, 1), PackedCell.Pack(15 - loss, 10, 3, 0, 0, 1),
            Registry.CaptureBindings().Entities.Single(binding => binding.EntityId == id).TargetId, cell >= 4 ? 2u : 1u);
    }
    internal sealed class NativeFake : ITimberbornOwnedBodyLiveness, ITimberbornLiveTreeBurnConsequenceApi,
        ITimberbornLiveCropBurnConsequenceApi, ITimberbornOwnedStorageInventoryApi, ITimberbornStoredGoodHazardConsequenceSink, ITimberbornFireLogSink
    {
        internal readonly HashSet<Guid> Live = [.. Ids];
        internal readonly HashSet<Guid> InventoryAvailable = [Stock, Structure];
        internal readonly Dictionary<Guid, int> Amounts = new() { [OwnedConsequenceBatchTests.Stock] = 10, [Structure] = 10 };
        internal readonly List<TimberbornTreeBurnConsequence> TreeCalls = [];
        internal readonly List<TimberbornCropBurnConsequence> CropCalls = [];
        internal readonly List<Guid> InventoryCalls = [];
        internal Action? AfterTree;
        internal Action? DuringIsLive;
        internal int DamagePasses;
        internal int TreeMutations;
        internal int TreeYieldReceipt;
        internal readonly List<TimberbornCropBurnConsequenceStatus> CropResults = [];
        public bool IsLive(Guid id) { DuringIsLive?.Invoke(); return Live.Contains(id); }
        public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence call)
        {
            TreeCalls.Add(call);
            if (!IsLive(call.EntityId)) return new(TimberbornTreeBurnConsequenceStatus.NotLive);
            if (call.Kind == TimberbornTreeBurnConsequenceKind.ReduceYield) return TreeYieldReceipt > 0 ?
                new(TimberbornTreeBurnConsequenceStatus.Applied, YieldLost: Math.Min(TreeYieldReceipt, call.YieldLost)) : new(TimberbornTreeBurnConsequenceStatus.Unavailable);
            TreeMutations++;
            AfterTree?.Invoke();
            return new(TimberbornTreeBurnConsequenceStatus.Applied);
        }
        public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence call)
        {
            CropCalls.Add(call);
            var status = !IsLive(call.EntityId) ? TimberbornCropBurnConsequenceStatus.NotLive :
                call.Kind == TimberbornCropBurnConsequenceKind.ReduceYield ? TimberbornCropBurnConsequenceStatus.Unavailable : TimberbornCropBurnConsequenceStatus.Applied;
            CropResults.Add(status);
            return new(status);
        }
        public TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner) => !IsLive(owner.EntityId)
            ? new(TimberbornOwnedInventoryStatus.NotLive, []) : !InventoryAvailable.Contains(owner.EntityId)
                ? new(TimberbornOwnedInventoryStatus.Unavailable, []) : new(TimberbornOwnedInventoryStatus.Available, [new("Log", Amounts[owner.EntityId])]);
        public TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner, TimberbornStoredGoodStack requested)
        {
            InventoryCalls.Add(owner.EntityId);
            int amount = Math.Min(Amounts[owner.EntityId], requested.Amount);
            Amounts[owner.EntityId] -= amount;
            return new(TimberbornOwnedInventoryStatus.Available, amount);
        }
        public TimberbornStoredGoodHazardConsequenceResult ApplyHazards(TimberbornStoredGoodBurnTarget target,
            TimberbornStoredGoodBurnConsequence consequence, IReadOnlyList<TimberbornStoredGoodHazardStack> stacks) => throw new Exception("No hazardous good is present.");
        public void Info(string message) { if (message.StartsWith("wildfire_timberborn_burn_damage_applied ")) DamagePasses++; }
        public void Warning(string message) { }
    }
}
