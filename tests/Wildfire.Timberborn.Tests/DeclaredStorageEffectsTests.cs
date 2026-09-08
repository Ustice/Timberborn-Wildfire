using System.Reflection;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class DeclaredStorageEffectsTests
{
    private static readonly Guid Id = Guid.Parse("00000000-0000-0000-0000-000000000031");
    private static readonly TimberbornInventoryDeclaration Output = new(TimberbornNativeInventoryRole.SimpleOutput, "Output");
    private static readonly TimberbornInventoryDeclaration Input = new(TimberbornNativeInventoryRole.Manufactory, "Inputs");
    private static readonly TimberbornOwnedStorageRegistration Owner = new(Id, NativeBurnTargetFamily.Structure, [Output, Input]);
    private static readonly TimberbornResourceFuelCatalog Catalog = new([new("Log", 2, 3, false, false, true), new("Dynamite", 2, 3, true, false, true)]);

    [Theory]
    [InlineData(1, 0, 1, 2)]
    [InlineData(2, 1, 0, 2)]
    [InlineData(4, 2, 0, 1)]
    [InlineData(6, 3, 0, 0)]
    public void SameGoodAcrossNamedInventoriesSpendsOneOwnerBudget(int budget, int removed, int output, int input)
    {
        var api = new Api(); var sink = Sink(api);
        var result = sink.ApplyOwnedConsequences(1, [Decision(budget)]);
        Assert.Equal(removed, result.Removed); Assert.Equal(output, api.Stock[Output]); Assert.Equal(input, api.Stock[Input]);
        Assert.Equal(removed == 0 ? [] : removed == 1 ? [Output] : new[] { Output, Input }, api.Calls.Select(call => call.Declaration));
    }

    [Fact]
    public void FractionalOwnerCreditSurvivesHistoryAndIsNotDuplicatedPerRole()
    {
        var api = new Api(); var sink = Sink(api); sink.ApplyOwnedConsequences(1, [Decision(1)]);
        var fixture = new OwnedConsequenceBatchTests.Fixture();
        var profile = fixture.Consumer.CaptureHistory().Owners.First(owner => owner.Family == NativeBurnTargetFamily.Structure).Profile!;
        var owner = new OwnedConsequenceOwner(Id, NativeBurnTargetFamily.Structure, OwnedBodyRetention.RetainedBody, profile);
        var credit = Assert.Single(sink.CaptureCredit([owner])); Assert.Equal(1, credit.FractionalBudget);
        var restored = Sink(api); restored.RestoreCredit(new([owner], [], [credit]));
        var result = restored.ApplyOwnedConsequences(2, [Decision(1)]);
        Assert.Equal(1, result.Removed); Assert.Equal(0, api.Stock[Output]); Assert.Equal(2, api.Stock[Input]);
        Assert.Empty(restored.CaptureCredit([owner]));
    }

    [Fact]
    public void DisabledInventoryCannotDonateQuantityToEnabledRoleBudget()
    {
        var api = new Api(); api.Disabled.Add(Output);
        var result = Sink(api).ApplyOwnedConsequences(1, [Decision(2)]);
        Assert.Equal(1, result.Unavailable); Assert.Equal(1, result.Removed);
        Assert.Equal(1, api.Stock[Output]); Assert.Equal(1, api.Stock[Input]); Assert.Equal(Input, Assert.Single(api.Calls).Declaration);
    }

    [Fact]
    public void CallbackChangesSecondRoleAndOnlyCompletedReceiptsReachHazards()
    {
        var api = new Api { Good = "Dynamite" }; api.After = () => api.Stock[Input] = 0;
        var result = Sink(api).ApplyOwnedConsequences(1, [Decision(6)]);
        Assert.Equal(1, result.Removed); Assert.Equal(1, result.Hazardous);
        Assert.Equal(1, Assert.Single(api.Hazards).Amount);
        Assert.Equal(new[] { Output, Input }, api.Calls.Select(call => call.Declaration));
    }

    [Fact]
    public void MissingWitnessAndExplicitEmptyDeclarationsAreDifferent()
    {
        var api = new Api(); var missing = new TimberbornOwnedStorageBurnSink(api, api, Catalog, _ => null);
        Assert.Equal(1, missing.ApplyOwnedConsequences(1, [Decision(2)]).Unavailable); Assert.Equal(0, api.Reads);
        var empty = new TimberbornOwnedStorageRegistration(Id, NativeBurnTargetFamily.Structure, []);
        var explicitNone = new TimberbornOwnedStorageBurnSink(api, api, Catalog, _ => empty);
        explicitNone.Preflight([Decision(2)]);
        Assert.Equal(0, explicitNone.ApplyOwnedConsequences(1, [Decision(2)]).Unavailable);
        Assert.Equal(2, api.Reads); Assert.Empty(api.Calls);
    }

    [Fact]
    public void ChangedWholeTopologyRejectsBeforeAnyWithdrawal()
    {
        var api = new Api { WrongTopology = true }; var sink = Sink(api);
        Assert.Throws<InvalidOperationException>(() => sink.Preflight([Decision(2)]));
        Assert.Empty(api.Calls); Assert.False(sink.HasTransientFuelCredit);
        Assert.Throws<InvalidOperationException>(() => TimberbornOwnedStorageInventoryApi.RequireOriginalDeclarations(Owner, [Output]));
        Assert.Throws<InvalidOperationException>(() => TimberbornOwnedStorageInventoryApi.RequireOriginalDeclarations(Owner,
            [Output, new(TimberbornNativeInventoryRole.Manufactory, "OtherName")]));
    }

    [Fact]
    public void AggregateMissingOriginalWitnessStillAppliesBodyDamageButReportsUnavailableStorage()
    {
        var f = new OwnedConsequenceBatchTests.Fixture(inventoryWitness: false); var id = f.Registrations[2].EntityId;
        var result = f.Consumer.Consume(1, [f.Delta(id, 2)]);
        Assert.Equal(1, result.Storage.UnavailableInventories); Assert.Empty(f.Native.InventoryCalls);
        Assert.Contains(f.Damage.States.Values, state => state.DamageTaken > 0);
    }

    [Fact]
    public void ProductionRawAdapterReadsPrivateNativeInputsAndConsumesOnlyUnreservedStock()
    {
        using var f = new NativeInventoryRoleFixture(input: true); f.Role("Manufactory"); f.Stock.Reserve(capacity: false); f.Give();
        var api = f.Stock.Resources.GetType().Assembly.GetType("Wildfire.Timberborn.Consequences.TimberbornOwnedStorageInventoryApi")!;
        var rows = (System.Collections.IEnumerable)api.GetMethod("ReadStock", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [f.Inventory])!;
        Assert.Equal(1, (int)f.Property(Assert.Single(rows.Cast<object>()), "Amount")!); Assert.Equal(0, f.Listed("UnreservedTakeableStock"));
        var request = Activator.CreateInstance(api.Assembly.GetType("Wildfire.Timberborn.Consequences.TimberbornStoredGoodStack")!, "Log", 99)!;
        int receipt = -1; f.Stock.Transfer(() => receipt = (int)api.GetMethod("ConsumeUnreserved", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [f.Inventory, request])!);
        Assert.Equal(1, receipt); Assert.Equal(1, f.Physical); Assert.Equal(1, f.Consumption); Assert.Equal(1, f.Stock.Reserved(stock: true));
    }

    [Fact]
    public void InvalidNativeReceiptCannotBecomeUnavailableOrPublishHazards()
    {
        var api = new Api { ReceiptOverride = new((TimberbornOwnedInventoryStatus)99, 0) };
        var guard = new Wildfire.Timberborn.Resources.NativeResourceTransaction();
        Assert.Throws<InvalidOperationException>(() => guard.TransferInventory(() => Sink(api).ApplyOwnedConsequences(1, [Decision(2)])));
        Assert.True(guard.IsIndeterminate);
        Assert.Empty(api.Hazards);
        Assert.Throws<InvalidOperationException>(guard.ThrowIfSaveUnsafe);
    }

    private static TimberbornOwnedStorageBurnSink Sink(Api api) => new(api, api, Catalog, _ => Owner);
    private static TimberbornOwnedBurnDecision Decision(int budget) => new(TimberbornFireCellDeltaDecision.FromDelta(new CellDelta(0,
        PackedCell.Pack(15, 10, 3, 0, 0, 1), PackedCell.Pack(15 - budget, 10, 3, 0, 0, 1), 1, 1)), Id, Owner.TargetKey, Owner.Family);
    private sealed class Api : ITimberbornOwnedStorageInventoryApi, ITimberbornStoredGoodHazardConsequenceSink
    {
        internal readonly Dictionary<TimberbornInventoryDeclaration, int> Stock = new() { [Output] = 1, [Input] = 2 };
        internal readonly HashSet<TimberbornInventoryDeclaration> Disabled = [];
        internal readonly List<(TimberbornInventoryDeclaration Declaration, int Amount)> Calls = [];
        internal IReadOnlyList<TimberbornStoredGoodHazardStack> Hazards = [];
        internal TimberbornOwnedInventoryRemoval? ReceiptOverride;
        internal int Reads; internal string Good = "Log"; internal Action? After; internal bool WrongTopology;
        public TimberbornOwnedInventorySnapshot Read(TimberbornOwnedStorageRegistration owner)
        {
            Reads++;
            return new(TimberbornOwnedInventoryStatus.Available, (WrongTopology ? owner.Declarations.Take(1) : owner.Declarations)
                .Select(d => new TimberbornOwnedInventoryRow(d, Disabled.Contains(d) ? TimberbornOwnedInventoryStatus.Unavailable : TimberbornOwnedInventoryStatus.Available,
                    !Disabled.Contains(d) && Stock[d] > 0 ? [new(Good, Stock[d])] : [])).ToArray());
        }
        public TimberbornOwnedInventoryRemoval Consume(TimberbornOwnedStorageRegistration owner, TimberbornInventoryDeclaration declaration, TimberbornStoredGoodStack requested)
        {
            int amount = Math.Min(requested.Amount, Stock[declaration]); Stock[declaration] -= amount; Calls.Add((declaration, amount)); After?.Invoke();
            return ReceiptOverride ?? new(TimberbornOwnedInventoryStatus.Available, amount);
        }
        public TimberbornStoredGoodHazardConsequenceResult ApplyHazards(TimberbornStoredGoodBurnTarget target, TimberbornStoredGoodBurnConsequence consequence, IReadOnlyList<TimberbornStoredGoodHazardStack> stacks)
        { Hazards = stacks; return TimberbornStoredGoodHazardConsequenceResult.Empty; }
    }
}
