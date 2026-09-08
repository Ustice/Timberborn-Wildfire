using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

/// <summary>Compile exact captured roles into initial material and body accounting; allocates no native/GPU state.</summary>
internal static class TimberbornInitialBodyCompiler
{
    internal static TimberbornCompiledInitialBodies Compile(TimberbornInitialWorldCapture capture,
        IEnumerable<TimberbornInitialBodySelection> selections)
    {
        if (capture is null) throw new ArgumentNullException(nameof(capture));
        var chosen = selections?.ToArray() ?? throw new ArgumentNullException(nameof(selections));
        if (chosen.Any(item => item is null) || chosen.Select(item => item.EntityId).Distinct().Count() != chosen.Length ||
            !chosen.Select(item => item.EntityId).OrderBy(id => id).SequenceEqual(capture.Bodies.Select(body => body.EntityId)))
            throw new ArgumentException("Initial selections must cover every captured body exactly once.");
        var byId = chosen.ToDictionary(item => item.EntityId);
        var projections = new List<TimberbornMaterialProjection>();
        var registrations = new List<TimberbornBurnDamageTargetRegistration>();
        foreach (var body in capture.Bodies)
        {
            var choice = byId[body.EntityId];
            var projection = TimberbornMaterialProjectionCompiler.Compile(body);
            var resources = SelectResources(body, choice);
            var costs = body.ConstructionResources ?? Array.Empty<TimberbornBurnDamageResourceStack>();
            foreach (var cost in costs) RequireGood(cost.ResourceId);
            bool constructed = body.PhysicalBodyKind == TimberbornBurnDamageTargetKind.Structure;
            if (!constructed && choice.Accounting == TimberbornInitialAccountingBasis.CatalogBodyProfile)
                throw new NotSupportedException("Natural accounting requires explicit native yield selections; catalog capacity is not supported.");
            var descriptor = new TimberbornBurnDamageDescriptor(body.SpecId, body.PhysicalBodyKind,
                constructed ? TimberbornBurnMaterialKind.Constructed : body.Shape == TimberbornInitialBodyShape.Tree
                    ? TimberbornBurnMaterialKind.Wood : TimberbornBurnMaterialKind.Organic,
                resources, costs, choice.Accounting == TimberbornInitialAccountingBasis.CatalogBodyProfile ? body.BodyProfile : null);
            var capacity = new TimberbornBurnDamageCapacityCalculator().Calculate(descriptor);
            if (capacity.MissingResourceIds.Count != 0)
                throw new InvalidOperationException("Initial body accounting has unknown resource definitions.");
            projections.Add(projection);
            var key = new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(body.EntityId, body.Family!.Value));
            var cells = body.Footprint.Select(slot =>
            {
                var coordinates = capture.Grid.FromIndex(slot.CellIndex);
                return new TimberbornCellCoordinates(coordinates.X, coordinates.Y, coordinates.Z);
            }).ToArray();
            registrations.Add(new(key, body.SpecId, cells, 0, descriptor));
        }
        return new(Array.AsReadOnly(projections.ToArray()), Array.AsReadOnly(registrations.ToArray()));
    }

    private static IReadOnlyList<TimberbornBurnDamageResourceStack> SelectResources(TimberbornInitialMaterialBody body,
        TimberbornInitialBodySelection choice)
    {
        if (choice.Yields.Count != body.Yields.Count || choice.Inventories.Count != body.Inventories.Count)
            throw new ArgumentException("Each native role needs an explicit accounting selection, including exclusions.");
        var amounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var selected in choice.Yields)
        {
            var native = body.Yields.SingleOrDefault(item => item.ComponentName == selected.ComponentName && item.Role == selected.Role)
                ?? throw new ArgumentException("Selected yield role does not belong to this exact body.");
            if (selected.Use == TimberbornInitialYieldUse.Excluded) continue;
            if (native.Role == TimberbornCapturedYieldRole.Unclassified)
                throw new NotSupportedException("Unclassified native yield cannot supply accounting.");
            RequireGood(native.DeclaredGoodId);
            if (selected.Use == TimberbornInitialYieldUse.Declared) Add(native.DeclaredGoodId, native.DeclaredAmount);
            else
            {
                if (!native.YieldEnabled) throw new NotSupportedException("Disabled native yield does not expose its actual retained quantity.");
                if (native.ActualAmount > 0) RequireGood(native.ActualGoodId);
                // Empty native GoodAmount may omit the id; retain its exact named declaration for zero accounting.
                Add(native.ActualAmount == 0 ? native.DeclaredGoodId : native.ActualGoodId, native.ActualAmount);
            }
        }
        foreach (var selected in choice.Inventories)
        {
            var native = body.Inventories.SingleOrDefault(item => item.Declaration == selected.Declaration)
                ?? throw new ArgumentException("Selected inventory role does not belong to this exact body.");
            if (selected.Accounting == TimberbornInitialInventoryUse.PhysicalStock)
                foreach (var good in native.Stock) { RequireGood(good.ResourceId); Add(good.ResourceId, good.Amount); }
        }
        return Array.AsReadOnly(amounts.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new TimberbornBurnDamageResourceStack(item.Key, item.Value)).ToArray());
        void Add(string good, int amount) => amounts[good] = checked(amounts.GetValueOrDefault(good) + amount);
    }

    private static void RequireGood(string good) => TimberbornMaterialProjectionCompiler.RequireGood(good);
}

internal sealed record TimberbornCompiledInitialBodies(IReadOnlyList<TimberbornMaterialProjection> Projections,
    IReadOnlyList<TimberbornBurnDamageTargetRegistration> Registrations)
{
    internal TimberbornBurnDamageService CreateDamage(FireGrid grid)
    {
        var damage = new TimberbornBurnDamageService(new TimberbornBurnDamageDescriptorCatalog(Array.Empty<TimberbornBurnDamageDescriptor>()));
        damage.RegisterTargets(grid, Registrations);
        return damage;
    }
}
