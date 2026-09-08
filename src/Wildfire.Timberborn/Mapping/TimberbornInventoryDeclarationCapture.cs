namespace Wildfire.Timberborn.Mapping;

/// <summary>Original or current native declaration evidence, including an explicit empty set for an owner.</summary>
public sealed class TimberbornBodyInventoryDeclarations
{
    public TimberbornBodyInventoryDeclarations(Guid entityId, IEnumerable<TimberbornInventoryDeclaration> declarations)
    {
        if (entityId == Guid.Empty) throw new ArgumentException("Native declaration evidence requires an owner.", nameof(entityId));
        var copied = declarations?.ToArray() ?? throw new ArgumentNullException(nameof(declarations));
        if (copied.Any(value => value is null) || copied.Select(value => value.Role).Distinct().Count() != copied.Length ||
            copied.Select(value => value.ComponentName).Distinct(StringComparer.Ordinal).Count() != copied.Length)
            throw new ArgumentException("Native declaration roles and component names must be unique.");
        EntityId = entityId;
        Declarations = Array.AsReadOnly(copied.OrderBy(value => value.Role).ToArray());
    }
    public Guid EntityId { get; }
    public IReadOnlyList<TimberbornInventoryDeclaration> Declarations { get; }
}

/// <summary>Copied evidence only; this allocates no native, material or inventory ownership.</summary>
public sealed class TimberbornInventoryDeclarationCapture
{
    private readonly IReadOnlyDictionary<Guid, IReadOnlyList<TimberbornInventoryDeclaration>> _byId;
    public TimberbornInventoryDeclarationCapture(IEnumerable<TimberbornBodyInventoryDeclarations> bodies)
    {
        var copied = bodies?.OrderBy(body => body.EntityId).ToArray() ?? throw new ArgumentNullException(nameof(bodies));
        if (copied.Select(body => body.EntityId).Distinct().Count() != copied.Length)
            throw new ArgumentException("Each native owner requires exactly one declaration set.");
        Bodies = Array.AsReadOnly(copied);
        _byId = copied.ToDictionary(body => body.EntityId, body => body.Declarations);
    }
    public IReadOnlyList<TimberbornBodyInventoryDeclarations> Bodies { get; }
    internal IReadOnlyList<TimberbornInventoryDeclaration> Get(Guid id) => _byId.TryGetValue(id, out var declarations) ? declarations :
        throw new ArgumentException("Captured body has no native declaration evidence.");
    internal void RequireOwners(IEnumerable<Guid> ids)
    {
        if (!ids.OrderBy(id => id).SequenceEqual(Bodies.Select(body => body.EntityId)))
            throw new ArgumentException("Declaration evidence must cover every captured owner exactly once.");
    }
    internal void RequireSupportedMaterialBodies(IReadOnlyList<TimberbornInitialMaterialBody> bodies)
    {
        RequireOwners(bodies.Select(body => body.EntityId));
        foreach (var body in bodies)
        {
            var declarations = Get(body.EntityId);
            RequireMaterialSupport(body.Shape, declarations);
            if (body.Inventories.Any(inventory => !declarations.Any(role => role == inventory.Declaration)))
                throw new ArgumentException("Physical inventory has no exact declared native role.");
            if (declarations.Any(role => role.Role != TimberbornNativeInventoryRole.GoodStack &&
                !body.Inventories.Any(inventory => inventory.Declaration == role)))
                throw new ArgumentException("Declared physical inventory was omitted from native material facts.");
        }
    }
    internal static TimberbornInitialCompositionGap? MaterialSupportGap(TimberbornInitialBodyShape shape,
        IReadOnlyList<TimberbornInventoryDeclaration> declarations)
    {
        if (declarations.Count == 0) return null;
        if ((shape is TimberbornInitialBodyShape.Structure or TimberbornInitialBodyShape.Stockpile) &&
            declarations.All(declaration => declaration.Role is TimberbornNativeInventoryRole.Stockpile or
                TimberbornNativeInventoryRole.SimpleOutput or TimberbornNativeInventoryRole.Manufactory)) return null;
        // Existing single natural harvest-stack representation; this does not create a new effect route.
        if ((shape is TimberbornInitialBodyShape.Tree or TimberbornInitialBodyShape.Crop or TimberbornInitialBodyShape.Vegetation) &&
            declarations.Count == 1 && declarations[0].Role == TimberbornNativeInventoryRole.GoodStack) return null;
        return declarations.Count > 1 ? TimberbornInitialCompositionGap.MultipleInventoryRoles :
            TimberbornInitialCompositionGap.UnsupportedInventoryRole;
    }

    internal static void RequireMaterialSupport(TimberbornInitialBodyShape shape,
        IReadOnlyList<TimberbornInventoryDeclaration> declarations)
    {
        if (MaterialSupportGap(shape, declarations) is { } gap)
            throw new NotSupportedException($"Declared inventory composition has no supported material route: {gap}.");
    }

    internal bool SameReadings(TimberbornInventoryDeclarationCapture other) => Bodies.Count == other.Bodies.Count &&
        Bodies.Zip(other.Bodies, (a, b) => a.EntityId == b.EntityId && a.Declarations.SequenceEqual(b.Declarations)).All(equal => equal);
}
