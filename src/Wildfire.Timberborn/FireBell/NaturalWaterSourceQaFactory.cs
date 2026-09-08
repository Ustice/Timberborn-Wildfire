using Timberborn.BlockSystem;
using Timberborn.BlueprintSystem;
using Timberborn.Coordinates;
using Timberborn.EntitySystem;
using Timberborn.TickSystem;
using Wildfire.Timberborn.Qa;
using Wildfire.Timberborn.Resources;
using UnityEngine;

namespace Wildfire.Timberborn.FireBell;

internal sealed class NaturalWaterSourceQaFactory
{
    internal const string TemplateName = "Wildfire.NaturalWaterSource.QA";
    private readonly ISpecService _specs;
    private readonly BlockObjectFactory _factory;
    private readonly EntityRegistry _entities;
    private readonly EntityService _entityService;
    private readonly NaturalWaterSourceShore _shore;
    private readonly NativeResourceCoordinator _resources;
    private readonly Ticker _ticker;

    internal NaturalWaterSourceQaFactory(ISpecService specs, BlockObjectFactory factory, EntityRegistry entities,
        EntityService entityService, NaturalWaterSourceShore shore, NativeResourceCoordinator resources,
        Ticker ticker, TimberbornWaterCreditBoundary boundary)
    {
        _specs = specs; _factory = factory; _entities = entities; _entityService = entityService;
        _shore = shore; _resources = resources; _ticker = ticker;
        // This dependency provisions the same boundary before singleton collection, including restore PostLoad.
        _ = boundary ?? throw new ArgumentNullException(nameof(boundary));
    }

    internal TimberbornNaturalWaterSource Create(Vector3Int inputCoordinate, Vector3 shorelinePoint)
    {
        if (TimberbornQaCommandPolicy.FromProcessArguments(Environment.GetCommandLineArgs()) != TimberbornQaCommandAccess.Development)
            throw new InvalidOperationException("qa_mutations_disabled");
        _resources.ThrowIfSaveUnsafe();
        _ticker.FinishFullTick();
        _resources.ThrowIfSaveUnsafe();
        if (!_shore.IsUsable(inputCoordinate, shorelinePoint))
            throw new InvalidOperationException("No clean fixed water input beside the exact dry native shore.");
        var blueprint = _specs.GetSingleSpec<NaturalWaterSourceQaSpec>().Blueprint;
        var block = _factory.CreateFinished(new EntitySetup.Builder(blueprint), new Placement(inputCoordinate));
        var source = block.GetComponent<TimberbornNaturalWaterSource>();
        if (source.TryArmNewSource() && IsUsable(source, shorelinePoint)) return source;
        // Only this returned, newly created entity is ours to remove. Never adopt or delete an older source.
        _entityService.Delete(block);
        throw new InvalidOperationException("Native fixed source failed post-creation ownership/shore admission.");
    }

    internal bool IsUsable(TimberbornNaturalWaterSource source, Vector3 shorelinePoint) =>
        source && source.Entity && source.Entity.Initialized && !source.Entity.Deleted &&
        ReferenceEquals(_entities.GetEntity(source.Entity.EntityId), source.Entity) &&
        ReferenceEquals(source.Entity.GetComponent<TimberbornNaturalWaterSource>(), source) &&
        source.GetComponent<NaturalWaterSourceQaSpec>() is not null && source.Ready &&
        source.Input.Coordinates == source.Coordinate && _shore.IsUsable(source.Coordinate, shorelinePoint);
}
