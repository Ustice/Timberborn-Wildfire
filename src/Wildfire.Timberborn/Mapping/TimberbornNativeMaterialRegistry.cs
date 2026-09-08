using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

public sealed record TimberbornMaterialSlotBinding(TimberbornCellCoordinates LocalCoordinates, uint SlotId);
public sealed record TimberbornMaterialEntityBinding(Guid EntityId, uint TargetId, uint NextSlotId,
    IReadOnlyList<TimberbornMaterialSlotBinding> Slots);
public sealed record TimberbornMaterialBindingSnapshot(int Version, uint NextTargetId,
    IReadOnlyList<TimberbornMaterialEntityBinding> Entities);

/// <summary>
/// Desired native projections and durable token bindings only. GPU active slots, remaining fuel and
/// archives belong exclusively to the simulator coordinator. This prototype is not runtime-bound.
/// </summary>
public sealed class TimberbornNativeMaterialRegistry
{
    private readonly FireGrid _grid;
    private readonly TimberbornMaterialBaseline _baseline;
    private Dictionary<Guid, Entry> _entries = new();
    private Dictionary<uint, Guid> _origins = new();
    private HashSet<FireSimMaterialIdentity> _boundSlots = new();
    private Dictionary<int, TimberbornResolvedMaterialCell> _cells = new();
    private uint _nextTargetId = 1;

    internal FireGrid Grid => _grid;

    public TimberbornNativeMaterialRegistry(FireGrid grid, IEnumerable<int> solidTerrainCells)
        : this(new TimberbornMaterialBaseline(grid, solidTerrainCells.Distinct().Select(cell =>
            new KeyValuePair<int, FireSimBaselineDefinition>(cell, FireSimBaselineDefinition.SolidTerrain))))
    {
    }

    public TimberbornNativeMaterialRegistry(TimberbornMaterialBaseline baseline)
    {
        _baseline = baseline ?? throw new ArgumentNullException(nameof(baseline));
        _grid = baseline.Grid;
    }

    /// <summary>Resolve retained origin identity, including hidden/removed projections. Never resolve through the current cell.</summary>
    public bool TryResolveOrigin(uint targetId, out Guid entityId) => _origins.TryGetValue(targetId, out entityId);

    /// <summary>Retained footprint identity, including hidden and removed slots; independent of current location.</summary>
    public bool IsSlotBound(uint targetId, uint slotId) => targetId != 0 && slotId != 0 &&
        _boundSlots.Contains(new FireSimMaterialIdentity(targetId, slotId));

    internal void ValidateOriginCellIndex(int cellIndex) => _grid.FromIndex(cellIndex);

    public TimberbornResolvedMaterialCell ResolveCell(int cellIndex)
    {
        _grid.FromIndex(cellIndex);
        return _cells.TryGetValue(cellIndex, out var resolved) ? resolved :
            TimberbornMaterialResolver.Resolve(cellIndex, _baseline.GetCell(cellIndex), Array.Empty<TimberbornMaterialContributor>());
    }

    /// <summary>Only an uncovered cell may reveal its baseline; lower native contributors must win first.</summary>
    public FireSimMaterialHandoffRequest CreateBaselineRequest(int cellIndex, FireSimMaterialIdentity expectedGpuOwner)
    {
        if (ResolveCell(cellIndex).Owner is not null)
            throw new InvalidOperationException("A current native contributor must not be replaced with unowned baseline.");
        return FireSimMaterialHandoffRequest.SetBaseline(cellIndex, expectedGpuOwner, _baseline.GetCell(cellIndex));
    }

    /// <summary>
    /// First activation only, using the current resolved native projection. This never allocates a binding
    /// or accepts a caller-supplied incoming identity/definition. Known slots need their GPU-retained state.
    /// </summary>
    public FireSimMaterialHandoffRequest CreateFirstActivationRequest(int cellIndex,
        FireSimMaterialIdentity expectedGpuOwner, IFireSimMaterialHandoffSimulator simulator)
    {
        if (simulator is null) throw new ArgumentNullException(nameof(simulator));
        var resolved = ResolveCell(cellIndex);
        var owner = resolved.Owner ?? throw new InvalidOperationException("First activation requires a current native material projection.");
        var identity = new FireSimMaterialIdentity(owner.TargetId, owner.SlotId);
        if (simulator.IsSlotKnown(identity))
            throw new InvalidOperationException("Previously activated native slots require retained GPU material.");
        var profile = resolved.Profile;
        var definition = new FireSimMaterialDefinition(profile.MaterialClass, profile.BurnCapacity,
            profile.AshQuality, profile.ContaminationBehavior, (byte)PackedCell.Fuel(resolved.PackedDefinition),
            (byte)PackedCell.Flammability(resolved.PackedDefinition), (byte)PackedCell.Terrain(resolved.PackedDefinition));
        return FireSimMaterialHandoffRequest.Fresh(cellIndex, expectedGpuOwner, identity, definition);
    }

    public void Reconcile(IEnumerable<TimberbornMaterialProjection> projections, IEnumerable<Guid> removals)
    {
        var incoming = projections.ToArray();
        var removed = removals.ToHashSet();
        if (incoming.Select(projection => projection.EntityId).Distinct().Count() != incoming.Length ||
            incoming.Any(projection => removed.Contains(projection.EntityId)))
            throw new ArgumentException("Each native entity must have exactly one operation in a reconciliation.");
        var staged = _entries.ToDictionary(pair => pair.Key, pair => pair.Value.Clone());
        uint nextTarget = _nextTargetId;
        foreach (Guid id in removed)
            if (staged.TryGetValue(id, out var old)) old.Projection = null; // Retain token/slot bindings for archived material.
        foreach (var projection in incoming.OrderBy(projection => projection.EntityId))
        {
            if (!staged.TryGetValue(projection.EntityId, out var entry))
            {
                uint target = nextTarget;
                nextTarget = checked(nextTarget + 1);
                entry = new Entry(target);
                staged.Add(projection.EntityId, entry);
            }
            foreach (var slot in projection.Footprint.OrderBy(slot => slot.LocalCoordinates.Z)
                         .ThenBy(slot => slot.LocalCoordinates.Y).ThenBy(slot => slot.LocalCoordinates.X))
            {
                _grid.FromIndex(slot.CellIndex);
                if (slot.LocalCoordinates.X < 0 || slot.LocalCoordinates.Y < 0 || slot.LocalCoordinates.Z < 0)
                    throw new ArgumentException("Native local footprint coordinates cannot be negative.");
                if (!entry.Slots.ContainsKey(slot.LocalCoordinates))
                {
                    uint id = entry.NextSlotId;
                    entry.NextSlotId = checked(id + 1);
                    entry.Slots.Add(slot.LocalCoordinates, id);
                }
            }
            entry.Projection = projection;
        }
        var cells = ResolveAll(staged); // Validate all overlaps before any token, removal or movement is published.
        var origins = staged.ToDictionary(pair => pair.Value.TargetId, pair => pair.Key);
        var boundSlots = staged.Values.SelectMany(entry => entry.Slots.Values.Select(slot =>
            new FireSimMaterialIdentity(entry.TargetId, slot))).ToHashSet();
        _entries = staged;
        _boundSlots = boundSlots;
        _origins = origins;
        _cells = cells;
        _nextTargetId = nextTarget;
    }

    public TimberbornMaterialBindingSnapshot CaptureBindings() => new(1, _nextTargetId,
        Array.AsReadOnly(_entries.OrderBy(pair => pair.Key).Select(pair => new TimberbornMaterialEntityBinding(
            pair.Key, pair.Value.TargetId, pair.Value.NextSlotId,
            Array.AsReadOnly(pair.Value.Slots.OrderBy(slot => slot.Value)
                .Select(slot => new TimberbornMaterialSlotBinding(slot.Key, slot.Value)).ToArray()))).ToArray()));

    /// <summary>Restore before world projection. This is not permission to synthesize GPU archives or fresh fuel.</summary>
    public void RestoreBindings(TimberbornMaterialBindingSnapshot snapshot)
    {
        if (_entries.Count != 0) throw new InvalidOperationException("Bindings restore requires a fresh registry.");
        if (snapshot.Version != 1 || snapshot.NextTargetId == 0) throw new ArgumentException("Unsupported material binding snapshot.");
        var staged = new Dictionary<Guid, Entry>();
        var targetIds = new HashSet<uint>();
        foreach (var binding in snapshot.Entities)
        {
            if (binding.EntityId == Guid.Empty || binding.TargetId == 0 || binding.TargetId >= snapshot.NextTargetId ||
                binding.NextSlotId == 0 || !targetIds.Add(binding.TargetId))
                throw new ArgumentException("Material target bindings must be unique, nonzero and below the allocation cursor.");
            var entry = new Entry(binding.TargetId) { NextSlotId = binding.NextSlotId };
            var slots = new HashSet<uint>();
            foreach (var slot in binding.Slots)
            {
                if (slot.SlotId == 0 || slot.SlotId >= binding.NextSlotId || !slots.Add(slot.SlotId) ||
                    slot.LocalCoordinates.X < 0 || slot.LocalCoordinates.Y < 0 || slot.LocalCoordinates.Z < 0 ||
                    !entry.Slots.TryAdd(slot.LocalCoordinates, slot.SlotId))
                    throw new ArgumentException("Local slot bindings must be bijective and below the allocation cursor.");
            }
            if (!staged.TryAdd(binding.EntityId, entry)) throw new ArgumentException("Duplicate native entity binding.");
        }
        var origins = staged.ToDictionary(pair => pair.Value.TargetId, pair => pair.Key);
        var boundSlots = staged.Values.SelectMany(entry => entry.Slots.Values.Select(slot =>
            new FireSimMaterialIdentity(entry.TargetId, slot))).ToHashSet();
        _entries = staged;
        _boundSlots = boundSlots;
        _origins = origins;
        _nextTargetId = snapshot.NextTargetId;
    }

    private Dictionary<int, TimberbornResolvedMaterialCell> ResolveAll(Dictionary<Guid, Entry> entries) => entries
        .Where(pair => pair.Value.Projection is not null)
        .SelectMany(pair => pair.Value.Projection!.Footprint.Select(slot => new
        {
            slot.CellIndex,
            Contributor = new TimberbornMaterialContributor(new TimberbornMaterialOwner(pair.Key, pair.Value.TargetId,
                pair.Value.Slots[slot.LocalCoordinates]), pair.Value.Projection.Parts),
        }))
        .GroupBy(value => value.CellIndex)
        .ToDictionary(group => group.Key, group => TimberbornMaterialResolver.Resolve(group.Key,
            _baseline.GetCell(group.Key), group.Select(value => value.Contributor)));

    private sealed class Entry
    {
        internal Entry(uint targetId) { TargetId = targetId; }
        internal uint TargetId { get; }
        internal uint NextSlotId { get; set; } = 1;
        internal Dictionary<TimberbornCellCoordinates, uint> Slots { get; } = new();
        internal TimberbornMaterialProjection? Projection { get; set; }
        internal Entry Clone()
        {
            var clone = new Entry(TargetId) { NextSlotId = NextSlotId, Projection = Projection };
            foreach (var pair in Slots) clone.Slots.Add(pair.Key, pair.Value);
            return clone;
        }
    }
}
