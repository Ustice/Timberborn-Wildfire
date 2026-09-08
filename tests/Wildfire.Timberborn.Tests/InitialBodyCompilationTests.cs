using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Basis = Wildfire.Timberborn.Mapping.TimberbornInitialAccountingBasis;
using YieldUse = Wildfire.Timberborn.Mapping.TimberbornInitialYieldUse;
using InventoryUse = Wildfire.Timberborn.Mapping.TimberbornInitialInventoryUse;

namespace Wildfire.Timberborn.Tests;

public sealed class InitialBodyCompilationTests
{
    private static readonly Guid A = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = new("00000000-0000-0000-0000-000000000002");
    private static readonly FireGrid Grid = new(2, 1, 1);

    [Fact]
    public void SameSpecActualAndDeclaredUsePerOwnerAccountingWithoutChangingCapturedYield()
    {
        var actual = Tree(A, 0, 3); var declared = Tree(B, 1, 3);
        var compiled = Compile([actual, declared], [Choice(A, YieldUse.Actual), Choice(B, YieldUse.Declared)]);
        var damage = compiled.CreateDamage(Grid);
        Assert.Equal(3 * 12, damage.States[Key(A)].DamageCapacity);
        Assert.Equal(5 * 12, damage.States[Key(B)].DamageCapacity);
        Assert.All(new[] { actual, declared }, body => Assert.Equal(3, body.Yields[0].ActualAmount));
        Assert.Equal(2, compiled.Projections.Count);
        Assert.All(damage.States.Values, state => { Assert.Equal(0, state.DamageTaken); Assert.Equal(0u, state.LastDamagedTick); });
    }

    [Fact]
    public void ExplicitActualZeroDoesNotBecomeDeclaredYieldOrCatalogCapacity()
    {
        var compiled = Compile([Tree(A, 0, 0)], [Choice(A, YieldUse.Actual)]);
        Assert.Equal(0, compiled.CreateDamage(Grid).States[Key(A)].DamageCapacity);
        Assert.Equal(0, Assert.Single(compiled.Registrations[0].DescriptorOverride!.ResourceYields).Amount);
        Assert.Equal(TimberbornMaterialPart.Tree("Pine"), compiled.Projections[0].Parts[0]);
    }

    [Theory]
    [InlineData(YieldUse.Actual, true)]
    [InlineData(YieldUse.Declared, false)]
    [InlineData(YieldUse.Excluded, false)]
    internal void DisabledGetterIsUnavailableOnlyForActualAccounting(YieldUse use, bool rejects)
    {
        var body = Tree(A, 0, 0, false);
        if (rejects) Assert.Throws<NotSupportedException>(() => Compile([body], [Choice(A, use)]));
        else Assert.Single(Compile([body], [Choice(A, use)]).Projections);
    }

    [Fact]
    public void RequiredExplicitRolesRejectMissingForeignAndDuplicateSelections()
    {
        var capture = Capture([Tree(A, 0, 3)]);
        Assert.Throws<ArgumentException>(() => TimberbornInitialBodyCompiler.Compile(capture, []));
        Assert.Throws<ArgumentException>(() => TimberbornInitialBodyCompiler.Compile(capture, [Choice(B, YieldUse.Actual)]));
        Assert.Throws<ArgumentException>(() => TimberbornInitialBodyCompiler.Compile(capture, [Choice(A, YieldUse.Actual), Choice(A, YieldUse.Actual)]));
        Assert.Throws<ArgumentException>(() => TimberbornInitialBodyCompiler.Compile(capture, [new(A, Basis.NativeResourceAmounts, [], [])]));
        Assert.Throws<ArgumentException>(() => TimberbornInitialBodyCompiler.Compile(capture,
            [new(A, Basis.NativeResourceAmounts, [new("Foreign", TimberbornCapturedYieldRole.Cuttable, YieldUse.Actual)], [])]));
        Assert.Throws<ArgumentException>(() => new TimberbornInitialBodySelection(A, Basis.NativeResourceAmounts,
            [new("Cuttable", TimberbornCapturedYieldRole.Cuttable, YieldUse.Actual), new("Cuttable", TimberbornCapturedYieldRole.Cuttable, YieldUse.Declared)], []));
        Assert.Throws<ArgumentException>(() => new TimberbornInitialBodySelection(A, 0, [], []));
    }

    [Fact]
    public void MultipleNamedRolesAreSelectedOnceAndSameGoodAmountsCombineChecked()
    {
        var body = new TimberbornInitialMaterialBody(A, "Pine", TimberbornInitialBodyShape.Tree, [new(new(0, 0, 0), 0)],
            [new(TimberbornCapturedYieldRole.Cuttable, "Cuttable", "Log", 3, "Log", 5, false, true),
             new(TimberbornCapturedYieldRole.Gatherable, "Gatherable", "Log", 2, "Log", 4, false, true)], [], null);
        var compiled = Compile([body], [new(A, Basis.NativeResourceAmounts,
            [new("Cuttable", TimberbornCapturedYieldRole.Cuttable, YieldUse.Actual), new("Gatherable", TimberbornCapturedYieldRole.Gatherable, YieldUse.Declared)], [])]);
        Assert.Equal(7, Assert.Single(compiled.Registrations[0].DescriptorOverride!.ResourceYields).Amount);
    }

    [Fact]
    public void ExcludedInventoryAccountingCannotHidePhysicalStockAndConstructedBasisIsExplicit()
    {
        var body = Building([new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), false, [new("Log", 4)])]);
        TimberbornInitialBodySelection Choice(Basis basis) => new(A, basis, [], [new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), InventoryUse.Excluded)]);
        var resources = Compile([body], [Choice(Basis.NativeResourceAmounts)]);
        var catalog = Compile([body], [Choice(Basis.CatalogBodyProfile)]);
        Assert.Contains(resources.Projections[0].Parts, part => part == TimberbornMaterialPart.StoredGood("Log"));
        Assert.Empty(resources.Registrations[0].DescriptorOverride!.ResourceYields);
        Assert.Equal(new TimberbornBurnDamageResourceStack("Log", 3), Assert.Single(resources.Registrations[0].DescriptorOverride!.ConstructionResources));
        Assert.Null(resources.Registrations[0].DescriptorOverride!.BurnableProfile);
        Assert.Equal(body.BodyProfile, catalog.Registrations[0].DescriptorOverride!.BurnableProfile);
        Assert.Equal(3 * 12, resources.CreateDamage(Grid).States[BuildingKey()].DamageCapacity);
        Assert.Equal(body.BodyProfile.DamageCapacity, catalog.CreateDamage(Grid).States[BuildingKey()].DamageCapacity);
        Assert.False(body.Inventories[0].Enabled); Assert.Equal(4, body.Inventories[0].Stock[0].Amount);
    }

    [Fact]
    public void EmptyConstructionCostRemainsEmptyWithoutAnImplicitProfile()
    {
        var body = new TimberbornInitialMaterialBody(A, "SmallWarehouse.Folktails", TimberbornInitialBodyShape.Stockpile,
            [new(new(0, 0, 0), 0)], [], [], []);
        var compiled = Compile([body], [new(A, Basis.NativeResourceAmounts, [], [])]);
        Assert.Equal(0, compiled.CreateDamage(Grid).States[BuildingKey()].DamageCapacity);
        Assert.Empty(compiled.Registrations[0].DescriptorOverride!.ConstructionResources);
    }

    [Fact]
    public void UnknownPositiveStockCannotBeExcludedOutOfPhysicalMaterial()
    {
        var body = Building([new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), true, [new("UnreviewedGood", 1)])]);
        Assert.Throws<NotSupportedException>(() => Compile([body],
            [new(A, Basis.NativeResourceAmounts, [], [new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), InventoryUse.Excluded)])]));
    }

    [Fact]
    public void EmptySupportedInventoriesAddNoFuelAndUnsupportedBodiesStillReject()
    {
        var body = Building([new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), true, []), new(new(TimberbornNativeInventoryRole.SimpleOutput, "SimpleOutput"), true, [])]);
        var compiled = Compile([body], [new(A, Basis.NativeResourceAmounts, [],
            [new(new(TimberbornNativeInventoryRole.Stockpile, "Stockpile"), InventoryUse.Excluded), new(new(TimberbornNativeInventoryRole.SimpleOutput, "SimpleOutput"), InventoryUse.Excluded)])]);
        Assert.Single(compiled.Projections[0].Parts); // Empty supported inventories add no phantom fuel.
        var unsupported = new TimberbornInitialMaterialBody(A, "Pine", TimberbornInitialBodyShape.Unknown, [new(new(0, 0, 0), 0)], [], [], null);
        Assert.Throws<NotSupportedException>(() => Compile([unsupported], [new(A, Basis.NativeResourceAmounts, [], [])]));
    }

    [Fact]
    public void InventorySelectionRequiresExactNameAndCannotReuseAnotherRoleOrDeclaration()
    {
        var declaration = new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.Stockpile, "Exact.Store");
        var body = Building([new(declaration, true, [new("Log", 4)])]);
        TimberbornInitialBodySelection Choice(TimberbornInventoryDeclaration identity) => new(A, Basis.NativeResourceAmounts, [],
            [new(identity, InventoryUse.PhysicalStock)]);
        var compiled = Compile([body], [Choice(declaration)]);
        Assert.Equal(4, Assert.Single(compiled.Registrations[0].DescriptorOverride!.ResourceYields).Amount);
        Assert.Throws<ArgumentException>(() => Compile([body], [Choice(new(declaration.Role, "Wrong.Store"))]));
        Assert.Throws<ArgumentException>(() => Compile([body], [Choice(new(TimberbornNativeInventoryRole.SimpleOutput, declaration.ComponentName))]));
        Assert.Throws<ArgumentException>(() => new TimberbornInitialBodySelection(A, Basis.NativeResourceAmounts, [],
            [new(declaration, InventoryUse.Excluded), new(declaration, InventoryUse.PhysicalStock)]));
        var renamed = Building([new(new(declaration.Role, "Changed.Store"), true, [new("Log", 4)])]);
        Assert.False(body.SameReadings(renamed));
        var declarations = new TimberbornInventoryDeclarationCapture([new(A, [declaration])]);
        declarations.RequireSupportedMaterialBodies([body]);
        Assert.Throws<ArgumentException>(() => declarations.RequireSupportedMaterialBodies([renamed]));
    }

    [Theory]
    [InlineData(TimberbornNativeInventoryRole.GoodStack)]
    [InlineData(TimberbornNativeInventoryRole.RecoveredGoodStack)]
    [InlineData(TimberbornNativeInventoryRole.WardenStation)]
    public void CapturingAdditionalNativeRoleDoesNotAdmitItsMaterialOrEffects(TimberbornNativeInventoryRole role)
    {
        var declaration = new TimberbornInventoryDeclaration(role, "Native.Exact");
        var body = Building([new(declaration, false, [new("Log", 2)])]);
        Assert.Throws<NotSupportedException>(() => Compile([body], [new(A, Basis.NativeResourceAmounts, [],
            [new(declaration, InventoryUse.PhysicalStock)])]));
        var declarations = new TimberbornInventoryDeclarationCapture([new(A, [declaration])]);
        Assert.Throws<NotSupportedException>(() => declarations.RequireSupportedMaterialBodies([body]));
    }

    [Fact]
    public void SelectedSameGoodAcrossInventoriesUsesCheckedAccountingAndDistinctMaterial()
    {
        var first = new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.Stockpile, "Store");
        var second = new TimberbornInventoryDeclaration(TimberbornNativeInventoryRole.Manufactory, "Factory");
        TimberbornInitialBodySelection choice = new(A, Basis.NativeResourceAmounts, [],
            [new(first, InventoryUse.PhysicalStock), new(second, InventoryUse.PhysicalStock)]);
        var body = Building([new(first, false, [new("Log", 3)]), new(second, true, [new("Log", 2), new("Plank", 1)])]);
        var compiled = Compile([body], [choice]);
        Assert.Equal(new[] { TimberbornMaterialPart.StoredGood("Log"), TimberbornMaterialPart.StoredGood("Plank") },
            compiled.Projections[0].Parts.Skip(1));
        Assert.Equal(5, compiled.Registrations[0].DescriptorOverride!.ResourceYields.Single(value => value.ResourceId == "Log").Amount);
        var overflow = Building([new(first, true, [new("Log", int.MaxValue)]), new(second, true, [new("Log", 1)])]);
        Assert.Throws<OverflowException>(() => Compile([overflow], [choice]));
    }

    [Fact]
    public void OverlappingBodyContributorsBothRemainRegisteredAndSelectionsAreCopied()
    {
        var entries = new[] { new TimberbornInitialYieldSelection("Cuttable", TimberbornCapturedYieldRole.Cuttable, YieldUse.Actual) };
        var choice = new TimberbornInitialBodySelection(A, Basis.NativeResourceAmounts, entries, []);
        entries[0] = entries[0] with { Use = YieldUse.Declared };
        var compiled = Compile([Tree(A, 0, 3), Tree(B, 0, 3)], [choice, Choice(B, YieldUse.Actual)]);
        var registry = new TimberbornNativeMaterialRegistry(Grid, []);
        registry.Reconcile(compiled.Projections, []);
        Assert.Equal(2, registry.ResolveCell(0).Contributors.Count);
        Assert.Equal(2, compiled.CreateDamage(Grid).States.Count);
        Assert.Equal(3, Assert.Single(compiled.Registrations[0].DescriptorOverride!.ResourceYields).Amount);
    }

    private static TimberbornInitialBodySelection Choice(Guid id, YieldUse use) => new(id, Basis.NativeResourceAmounts,
        [new("Cuttable", TimberbornCapturedYieldRole.Cuttable, use)], []);
    private static TimberbornInitialMaterialBody Tree(Guid id, int cell, int actual, bool enabled = true) => new(id, "Pine",
        TimberbornInitialBodyShape.Tree, [new(new(0, 0, 0), cell)],
        [new(TimberbornCapturedYieldRole.Cuttable, "Cuttable", actual == 0 ? "" : "Log", actual, "Log", 5, false, enabled)], [], null);
    private static TimberbornInitialMaterialBody Building(TimberbornInventoryMaterial[] inventories) => new(A, "SmallWarehouse.Folktails",
        TimberbornInitialBodyShape.Stockpile, [new(new(0, 0, 0), 0)], [], inventories, [new("Log", 3)]);
    private static TimberbornInitialWorldCapture Capture(TimberbornInitialMaterialBody[] bodies) => new(Grid, bodies, [], [], new(Grid, [], [], []));
    private static TimberbornCompiledInitialBodies Compile(TimberbornInitialMaterialBody[] bodies, TimberbornInitialBodySelection[] choices) =>
        TimberbornInitialBodyCompiler.Compile(Capture(bodies), choices);
    private static TimberbornBurnDamageTargetKey Key(Guid id) => new(TimberbornBurnDamageIdentity.ForEntity(id, NativeBurnTargetFamily.Tree));
    private static TimberbornBurnDamageTargetKey BuildingKey() => new(TimberbornBurnDamageIdentity.ForEntity(A, NativeBurnTargetFamily.Stockpile));
}
