using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

public sealed class LivingTreeFreshEligibilityTests
{
    private static readonly Guid Id = new("00000000-0000-0000-0000-000000000001");
    private static readonly FireGrid Grid = new(2, 1, 2);

    [Fact]
    public void ActualNativeFirstFruitAndLaterRegrowthHaveTheSameWoodEligibility()
    {
        using var native = new NativeWoodAndResin();
        native.MatureWood();
        var first = Observe(native.Read());
        Assert.False(first.RetainedBodies[0].Yields.Single(yield => yield.Role == TimberbornCapturedYieldRole.Gatherable).YieldEnabled);
        Assert.Equal(new[] { Id }, first.CaptureFreshEligibleOwners([Id]));
        var (registry, simulator) = UnknownSlot(first);
        var plan = Plan(registry, simulator, first)!;
        Assert.Equal(FireSimMaterialHandoffMode.Fresh, Assert.Single(plan.Requests).Mode);

        native.RipenResin();
        native.HarvestResin();
        var later = Observe(native.Read());
        var gatherable = later.RetainedBodies[0].Yields.Single(yield => yield.Role == TimberbornCapturedYieldRole.Gatherable);
        Assert.True(gatherable.YieldEnabled);
        Assert.Equal(0, gatherable.ActualAmount);
        Assert.Equal(first.CaptureFreshEligibleOwners([Id]), later.CaptureFreshEligibleOwners([Id]));
        Assert.Equal(2, later.RetainedBodies[0].Yields.Single(yield => yield.Role == TimberbornCapturedYieldRole.Cuttable).ActualAmount);
        Assert.Equal(FireSimMaterialHandoffMode.Fresh, Assert.Single(Plan(registry, simulator, later)!.Requests).Mode);
        Assert.Equal(0, simulator.Uploads);
    }

    [Theory]
    [InlineData("disabled-wood")]
    [InlineData("zero-wood")]
    [InlineData("dead")]
    [InlineData("unobserved-death")]
    [InlineData("unproved-composition")]
    public void UnprovedFreshWoodRejectsUnknownSlotWithoutChangingKnownMaterial(string condition)
    {
        var yields = Yields(condition != "disabled-wood", condition == "zero-wood" ? 0 : 2, true, 2);
        var observation = Observe(yields, condition == "unobserved-death" ? null : condition == "dead",
            condition != "unproved-composition");
        Assert.Empty(observation.CaptureFreshEligibleOwners([Id]));
        var (registry, simulator) = UnknownSlot(observation);
        Assert.Throws<NotSupportedException>(() => Plan(registry, simulator, observation));
        Assert.Equal(0, simulator.Uploads);
        Assert.False(simulator.IsSlotKnown(new(1, 2)));
        Assert.Equal(0, PackedCell.Fuel(simulator.CaptureSnapshot().Cells[0]));
    }

    [Fact]
    public void NativeResinResetNeverRefillsAnExhaustedKnownArchive()
    {
        using var native = new NativeWoodAndResin();
        native.MatureWood();
        var observation = Observe(native.Read());
        var registry = Registry(observation);
        var simulator = new RetiredDetachmentSimulatorFixture(new(1, Grid, 3, FireSimParameters.Default, 7,
            new ushort[Grid.CellCount], new uint[Grid.CellCount], new uint[Grid.CellCount], new uint[Grid.CellCount], new uint[Grid.CellCount],
            new(1, [new(1, 1), new(1, 2)], [new(new(1, 1), 1, 0, 0, 0), new(new(1, 2), 1, 1, 0, 0)]), []));
        native.RipenResin();
        var plan = Plan(registry, simulator, Observe(native.Read()))!;
        Assert.All(plan.Requests, request => Assert.Equal(FireSimMaterialHandoffMode.Archived, request.Mode));
        Assert.NotNull(simulator.TryHandoffMaterial(plan, receipt => Assert.True(receipt.Accepted)));
        Assert.All(simulator.CaptureSnapshot().Cells, cell => Assert.Equal(0, PackedCell.Fuel(cell)));
        native.HarvestResin();
        native.RipenResin();
        Assert.Null(Plan(registry, simulator, Observe(native.Read())));
        Assert.Equal(1, simulator.Uploads);
    }

    [Fact]
    public void SameScopeRereadRejectsNativeWoodChangeBeforeAPlannedFreshStep()
    {
        using var native = new NativeWoodAndResin();
        native.MatureWood();
        var first = Observe(native.Read());
        var (registry, simulator) = UnknownSlot(first);
        var probe = new DesiredMaterialAuthorityProbe(simulator)
        {
            TransformSnapshot = value => { native.RemoveWood(); return value; }
        };
        var guard = new NativeResourceTransaction();
        Assert.Throws<InvalidOperationException>(() => guard.CaptureAtRest<bool>(() =>
        {
            var observed = Observe(native.Read());
            _ = TimberbornDesiredMaterialReconciliation.Plan(registry, Owners(), probe, observed.CaptureFreshEligibleOwners([Id]));
            // This is the executor's required current-observation revalidation boundary, not an
            // extra quantity ledger or permission retained after leaving the native read scope.
            if (!observed.SameReadings(Observe(native.Read()))) throw new InvalidOperationException("Native facts changed before dispatch.");
            throw new Exception("A changed observation must not reach simulation dispatch.");
        }));
        Assert.Equal(0, simulator.Uploads);
        Assert.False(guard.IsIndeterminate);
    }

    private static IReadOnlyDictionary<Guid, OwnedBodyRetention> Owners() => new Dictionary<Guid, OwnedBodyRetention> { [Id] = OwnedBodyRetention.RetainedBody };
    private static FireSimMaterialHandoffBatch? Plan(TimberbornNativeMaterialRegistry registry, RetiredDetachmentSimulatorFixture simulator,
        TimberbornOwnedRestoreObservation observation) => TimberbornDesiredMaterialReconciliation.Plan(registry, Owners(), simulator, observation.CaptureFreshEligibleOwners([Id]));
    private static TimberbornNativeMaterialRegistry Registry(TimberbornOwnedRestoreObservation observation)
    {
        var registry = new TimberbornNativeMaterialRegistry(Grid, []);
        registry.Reconcile(observation.RetainedBodies.Select(TimberbornMaterialProjectionCompiler.Compile), []);
        return registry;
    }
    private static (TimberbornNativeMaterialRegistry, RetiredDetachmentSimulatorFixture) UnknownSlot(TimberbornOwnedRestoreObservation observation) =>
        (Registry(observation), new(new(1, Grid, 3, FireSimParameters.Default, 7, new ushort[Grid.CellCount], new uint[Grid.CellCount],
            new uint[Grid.CellCount], [1, 0, 0, 0], [1, 0, 0, 0], new(0, [new(1, 1)], []), [])));
    private static TimberbornNamedYieldMaterial[] Yields(bool woodEnabled, int wood, bool resinEnabled, int resin) =>
        [new(TimberbornCapturedYieldRole.Cuttable, "Cuttable", "Log", wood, "Log", 2, false, woodEnabled),
         new(TimberbornCapturedYieldRole.Gatherable, "Gatherable", "PineResin", resin, "PineResin", 2, false, resinEnabled)];
    private static TimberbornOwnedRestoreObservation Observe(IReadOnlyList<TimberbornNamedYieldMaterial> yields, bool? dead = false, bool supportedWood = true)
    {
        var body = new TimberbornInitialMaterialBody(Id, "Pine", TimberbornInitialBodyShape.Tree,
            [new(new(0, 0, 0), 0), new(new(1, 0, 0), 1)], yields, [], null);
        var declarations = new TimberbornInventoryDeclarationCapture([new(Id, [])]);
        var environment = TimberbornInitialEnvironmentCapture.ForOwnedDomain(new TimberbornWorldDomain(Grid, new(2, 1, 1)), [], [], []);
        return new(new(Grid, [body], [], [], environment, declarations), [body], [new(Id, null, dead, supportedWood, [])]);
    }

    private sealed class NativeWoodAndResin : IDisposable
    {
        private readonly NativePartialYieldFixture _native = new();
        private readonly object _wood, _resin, _cuttable, _grower;
        internal NativeWoodAndResin()
        {
            using var archive = ZipFile.OpenRead(Path.Combine(Path.GetDirectoryName(_native.YielderType.Assembly.Location)!, "../StreamingAssets/Modding/Blueprints.zip"));
            using var stream = archive.GetEntry("NaturalResources/Trees/Pine/Pine.blueprint.json")!.Open();
            using var doc = JsonDocument.Parse(stream);
            _wood = Create(doc.RootElement.GetProperty("CuttableSpec").GetProperty("Yielder"));
            _resin = Create(doc.RootElement.GetProperty("GatherableSpec").GetProperty("Yielder"));
            _cuttable = RuntimeHelpers.GetUninitializedObject(_native.Type("Timberborn.Cutting", "Timberborn.Cutting.Cuttable"));
            NativePartialYieldFixture.Set(_cuttable, "<Yielder>k__BackingField", _wood);
            var gatherable = RuntimeHelpers.GetUninitializedObject(_native.Type("Timberborn.Gathering", "Timberborn.Gathering.Gatherable"));
            NativePartialYieldFixture.Set(gatherable, "<Yielder>k__BackingField", _resin);
            _grower = RuntimeHelpers.GetUninitializedObject(_native.Type("Timberborn.Gathering", "Timberborn.Gathering.GatherableYieldGrower"));
            NativePartialYieldFixture.Set(_grower, "_gatherable", gatherable);
        }
        private object Create(JsonElement json)
        {
            string name = json.GetProperty("YielderComponentName").GetString()!;
            string good = json.GetProperty("Yield").GetProperty("Id").GetString()!;
            int amount = json.GetProperty("Yield").GetProperty("Amount").GetInt32();
            var yielder = _native.NewYielder(name, good);
            var spec = _native.YielderType.GetProperty("YielderSpec")!.GetValue(yielder)!;
            var yield = spec.GetType().GetProperty("Yield")!.GetValue(spec)!;
            yield.GetType().GetProperty("Amount")!.SetValue(yield, amount);
            _native.YielderType.GetMethod("Initialize")!.Invoke(yielder, [spec, Activator.CreateInstance(_native.AmountType, good, amount), null, "Gather"]);
            _native.YielderType.BaseType!.GetField("_componentCache", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(yielder, RuntimeHelpers.GetUninitializedObject(_native.Type("Timberborn.BaseComponentSystem", "Timberborn.BaseComponentSystem.ComponentCache")));
            _native.EnabledField.SetValue(yielder, false);
            return yielder;
        }
        internal TimberbornNamedYieldMaterial[] Read() => Yields((bool)_native.EnabledField.GetValue(_wood)!, _native.Quantity(_wood),
            (bool)_native.EnabledField.GetValue(_resin)!, _native.Quantity(_resin));
        internal void MatureWood() => _cuttable.GetType().GetMethod("<Awake>b__22_1", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_cuttable, [null, EventArgs.Empty]);
        internal void RipenResin() => _grower.GetType().GetMethod("<Awake>b__12_0", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_grower, null);
        internal void HarvestResin() => _native.YielderType.GetMethod("DecreaseYield")!.Invoke(_resin, [Activator.CreateInstance(_native.AmountType, "PineResin", 2)]);
        internal void RemoveWood() => _native.YielderType.GetMethod("RemoveRemainingYield")!.Invoke(_wood, null);
        public void Dispose() => _native.Dispose();
    }
}
