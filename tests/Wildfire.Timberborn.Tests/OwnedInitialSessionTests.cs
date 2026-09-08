using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class OwnedInitialSessionTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = new("00000000-0000-0000-0000-000000000002");
    private static readonly FireGrid Grid = new(2, 1, 2);

    [Fact]
    public void InitialSessionBuildsOneCompleteSnapshotAndRoundTripsChosenAccountingWithoutNativeEffects()
    {
        var f = new Fixture(); using var session = f.Prepare();
        Assert.Equal(2, f.CaptureCount); Assert.Equal(1, f.Created);
        var snapshot = session.Simulator.CaptureSnapshot();
        Assert.Equal(0u, snapshot.Tick); Assert.Equal(17u, snapshot.Seed);
        Assert.Equal(2, snapshot.MaterialAuthority.KnownSlots.Length);
        Assert.Empty(snapshot.MaterialAuthority.Archives); Assert.Empty(snapshot.PendingChanges);
        Assert.All(snapshot.TransportFields, value => Assert.Equal(0u, value));
        Assert.All(snapshot.CompanionFields, value => Assert.Equal(0, WildfireMaterialFieldState.Unpack(value).BurnHistory));
        Assert.Equal(3, PackedCell.Water(snapshot.Cells[2])); // Actual liquid overlaps the first plant.
        Assert.Equal(PackedCell.Terrain(session.Registry.ResolveCell(2).PackedDefinition), PackedCell.Terrain(snapshot.Cells[2]));
        Assert.Equal(1, PackedCell.Terrain(snapshot.Cells[0]));
        Assert.Equal(3 * 12, session.Damage.States[Key(A)].DamageCapacity);
        Assert.Equal(5 * 12, session.Damage.States[Key(B)].DamageCapacity);
        var empty = TimberbornWildfirePersistenceSnapshot.Empty;
        var saved = session.Capture(empty.AshField, empty.BeaverBehavior);
        Assert.NotNull(saved.OwnedMaterial!.History!.NativeDefinitions);
        Assert.All(saved.OwnedMaterial.History.Natural, entry => Assert.Equal(0, entry.AppliedYieldLoss));
        using var restored = TimberbornOwnedWorldSession<Simulator>.PrepareCompleteRestore(saved, value => new(value),
            (_, _) => new(f.Current, f.Current.Bodies, f.Current.Bodies.Select(body => new TimberbornRetainedBodyObservation(body.EntityId, null, false, []))), f.Effects, f.Guard);
        Assert.Equal(TimberbornWildfirePersistenceCodec.Encode(saved),
            TimberbornWildfirePersistenceCodec.Encode(restored.Capture(empty.AshField, empty.BeaverBehavior)));
        Assert.Empty(f.Native.TreeCalls); Assert.Empty(f.Native.CropCalls); Assert.Empty(f.Native.InventoryCalls);
        Assert.All(f.Current.Bodies, body => Assert.Equal(3, body.Yields[0].ActualAmount));
    }

    [Fact]
    public void HiddenContributorGetsBodyBindingAndWitnessButNoInventedKnownSlotOrArchive()
    {
        var f = new Fixture { Current = Capture(overlap: true) }; using var session = f.Prepare();
        var cell = session.Registry.ResolveCell(2); Assert.Equal(2, cell.Contributors.Count);
        Assert.Equal(2, session.Damage.States.Count); Assert.Equal(2, session.Consumer.CaptureHistory().Owners.Count);
        Assert.Equal(2, session.Registry.CaptureBindings().Entities.Count);
        var simulation = session.Simulator.CaptureSnapshot();
        Assert.Single(simulation.MaterialAuthority.KnownSlots); Assert.Empty(simulation.MaterialAuthority.Archives);
        var hidden = cell.Contributors.Single(item => item.Owner != cell.Owner).Owner;
        Assert.DoesNotContain(new FireSimMaterialIdentity(hidden.TargetId, hidden.SlotId), simulation.MaterialAuthority.KnownSlots);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("placement")]
    [InlineData("membership")]
    [InlineData("environment")]
    [InlineData("excluded")]
    public void ChangedWorldAfterBackendAllocationDisposesUnpublishedSession(string mutation)
    {
        var f = new Fixture(); f.AfterCreate = () => f.Current = Capture(mutation: mutation);
        Assert.Throws<ArgumentException>(() => f.Prepare());
        Assert.Equal(1, f.Backend!.Disposals); Assert.Equal(2, f.CaptureCount);
        Assert.False(f.Guard.IsIndeterminate); f.Guard.ThrowIfSaveUnsafe();
        Assert.Empty(f.Native.TreeCalls);
    }

    [Fact]
    public void LivenessCallbackAfterBackendReadCannotBypassFinalQuantityCheck()
    {
        var f = new Fixture(); f.AfterCreate = () => f.Native.DuringIsLive = () => f.Current = Capture(mutation: "quantity");
        Assert.Throws<ArgumentException>(() => f.Prepare());
        Assert.Equal(1, f.Backend!.Disposals); Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void EveryProducerRunsInsideSameReadScopeWithoutNativeMutationAdmission()
    {
        var f = new Fixture();
        void CheckExcluded()
        {
            Assert.Throws<InvalidOperationException>(f.Guard.ThrowIfSaveUnsafe);
            Assert.Throws<InvalidOperationException>(() => f.Guard.TransferInventory(() => throw new Exception("must not run")));
            Assert.Throws<InvalidOperationException>(() => f.Guard.CaptureAtRest(() => 0));
        }
        f.CheckProducer = CheckExcluded; f.Native.DuringIsLive = CheckExcluded;
        using var session = f.Prepare();
        Assert.False(f.Guard.IsIndeterminate); f.Guard.ThrowIfSaveUnsafe();
    }

    [Fact]
    public void InvalidCompleteBodySelectionRejectsBeforeBackendCreation()
    {
        var f = new Fixture(); f.Compose = _ => new([], FireSimParameters.Default, 17);
        Assert.Throws<ArgumentException>(() => f.Prepare());
        Assert.Equal(0, f.Created); Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void BackendCannotRewriteFactoryInputAndPassInitialHistoryValidation()
    {
        var f = new Fixture(); f.EditFactoryInput = snapshot => snapshot.Cells[2] = PackedCell.SetWater(snapshot.Cells[2], 0);
        Assert.Throws<ArgumentException>(() => f.Prepare());
        Assert.Equal(1, f.Backend!.Disposals); Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void ThrowingFactoryLeavesNoNativeEffectsOrPoisonAndCanRetryFreshPreparation()
    {
        var f = new Fixture(); var failure = new ApplicationException("allocation failed");
        f.BeforeCreate = () => throw failure;
        Assert.Same(failure, Assert.Throws<ApplicationException>(() => f.Prepare()));
        Assert.False(f.Guard.IsIndeterminate); Assert.Empty(f.Native.TreeCalls);
        f.BeforeCreate = null; using var session = f.Prepare();
        Assert.Equal(2, session.Damage.States.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalBackendMustStillMatchAfterLastNativeAndCaptureCallbacks(bool nativeObserver)
    {
        var f = new Fixture();
        void ChangeField()
        {
            if (f.Backend is not null) f.Backend.CaptureSnapshot().Cells[2] = PackedCell.SetWater(f.Backend.CaptureSnapshot().Cells[2], 0);
        }
        f.AfterCreate = () =>
        {
            if (nativeObserver) f.Native.DuringIsLive = ChangeField;
            else f.CheckProducer = ChangeField;
        };
        Assert.Throws<ArgumentException>(() => f.Prepare());
        Assert.Equal(1, f.Backend!.Disposals);
        Assert.False(f.Guard.IsIndeterminate);
    }

    [Fact]
    public void UnavailableOriginalInventoryEvidenceIsNotAnEmptyInventoryDeclaration()
    {
        var f = new Fixture();
        var current = f.Current;
        f.Current = new(Grid, current.Bodies, current.Excluded, current.WaterSources, current.Environment);
        Assert.Null(f.Current.InventoryDeclarations);
        Assert.False(current.SameReadings(f.Current));
        Assert.Throws<NotSupportedException>(() => f.Prepare());
        Assert.Equal(0, f.Created);
        Assert.False(f.Guard.IsIndeterminate);
    }

    private sealed class Fixture
    {
        internal readonly NativeResourceTransaction Guard = new();
        internal readonly OwnedConsequenceBatchTests.NativeFake Native = new();
        internal TimberbornInitialWorldCapture Current = Capture();
        internal TimberbornOwnedNativeEffects Effects => new(Native, Native, Native, Native, Native);
        internal Func<TimberbornInitialWorldCapture, TimberbornInitialOwnedPlan> Compose = Plan;
        internal Action? BeforeCreate, AfterCreate, CheckProducer;
        internal Action<FireSimSnapshot>? EditFactoryInput;
        internal int Created, CaptureCount;
        internal Simulator? Backend;
        internal TimberbornOwnedWorldSession<Simulator> Prepare() => TimberbornOwnedWorldSession<Simulator>.PrepareInitial(Grid,
            _ => { CheckProducer?.Invoke(); CaptureCount++; return Current; },
            capture => { CheckProducer?.Invoke(); return Compose(capture); },
            snapshot =>
            {
                CheckProducer?.Invoke(); BeforeCreate?.Invoke(); Created++; EditFactoryInput?.Invoke(snapshot);
                Backend = new(snapshot); AfterCreate?.Invoke(); return Backend;
            }, Effects, Guard);
    }

    private static TimberbornInitialOwnedPlan Plan(TimberbornInitialWorldCapture capture) => new(capture.Bodies.Select(body =>
        new TimberbornInitialBodySelection(body.EntityId, TimberbornInitialAccountingBasis.NativeResourceAmounts,
            [new("Cuttable", TimberbornCapturedYieldRole.Cuttable, body.EntityId == A ? TimberbornInitialYieldUse.Actual : TimberbornInitialYieldUse.Declared)], [])),
        FireSimParameters.Default, 17);
    private static TimberbornInitialWorldCapture Capture(bool overlap = false, string? mutation = null)
    {
        TimberbornInitialMaterialBody Body(Guid id, int cell) => new(id, "Pine", TimberbornInitialBodyShape.Tree,
            [new(new(0, 0, 0), cell)], [new(TimberbornCapturedYieldRole.Cuttable, "Cuttable", "Log", mutation == "quantity" ? 2 : 3, "Log", 5, false, true)], [], null);
        TimberbornInitialMaterialBody[] bodies = mutation == "membership" ? [Body(A, 2)] : [Body(A, mutation == "placement" ? 3 : 2), Body(B, overlap ? 2 : 3)];
        return new(Grid, bodies,
            mutation == "excluded" ? [new(Guid.NewGuid(), "Preview", TimberbornInitialCaptureExclusion.Preview)] : [], [],
            new(Grid, [0, 1], [new(2, .1f, 0, true, false), new(3, .2f, 0, true, false)],
                [new(0, 0, 1, 2, mutation == "environment" ? .8f : .5f, 0, 0)]),
            new(bodies.Select(body => new TimberbornBodyInventoryDeclarations(body.EntityId, []))));
    }
    private static TimberbornBurnDamageTargetKey Key(Guid id) => new(TimberbornBurnDamageIdentity.ForEntity(id, NativeBurnTargetFamily.Tree));
    private sealed class Simulator(FireSimSnapshot snapshot) : IGpuFireSimulator, IFireSimSnapshotSimulator, IDisposable
    {
        internal int Disposals;
        public int Width => snapshot.Grid.Width; public int Height => snapshot.Grid.Height; public int Depth => snapshot.Grid.Depth;
        public FireSimSnapshotCapability SnapshotCapability => FireSimSnapshotCapability.CompleteMaterialHistory;
        public FireSimSnapshot CaptureSnapshot() => snapshot;
        public void Dispose() => Disposals++;
        public void RegisterChange(FireSimChange change) => throw new NotSupportedException();
        public GpuFireStepResult Tick() => throw new NotSupportedException();
        public IDisposable Subscribe(IFireSimListener listener) => throw new NotSupportedException();
    }
}
