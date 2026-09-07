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
    private readonly HashSet<int> _solidTerrain;
    private Dictionary<Guid, Entry> _entries = new();
    private Dictionary<int, TimberbornResolvedMaterialCell> _cells = new();
    private uint _nextTargetId = 1;

    public TimberbornNativeMaterialRegistry(FireGrid grid, IEnumerable<int> solidTerrainCells)
    {
        if (grid.Width <= 0 || grid.Height <= 0 || grid.Depth <= 0) throw new ArgumentOutOfRangeException(nameof(grid));
        _ = checked(grid.Width * grid.Height * grid.Depth);
        _grid = grid;
        _solidTerrain = solidTerrainCells.ToHashSet();
        foreach (int cell in _solidTerrain) _grid.FromIndex(cell);
    }

    /// <summary>Resolve retained origin identity, including hidden/removed projections. Never resolve through the current cell.</summary>
    public bool TryResolveOrigin(uint targetId, out Guid entityId)
    {
        foreach (var pair in _entries)
            if (pair.Value.TargetId == targetId) { entityId = pair.Key; return true; }
        entityId = default;
        return false;
    }

    public TimberbornResolvedMaterialCell ResolveCell(int cellIndex)
    {
        _grid.FromIndex(cellIndex);
        return _cells.TryGetValue(cellIndex, out var resolved) ? resolved :
            TimberbornMaterialResolver.Resolve(cellIndex, _solidTerrain.Contains(cellIndex), Array.Empty<TimberbornMaterialContributor>());
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
        _entries = staged;
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
        _entries = staged;
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
            _solidTerrain.Contains(group.Key), group.Select(value => value.Contributor)));

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
