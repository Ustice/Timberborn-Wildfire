using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

internal sealed class TimberbornBurnDamageTestStateProvider : ITimberbornBurnDamageTargetStateProvider
{
    private readonly Dictionary<int, TimberbornBurnDamageTargetState> _statesByCellIndex;
    private readonly Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> _statesByTargetKey;
    private readonly Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageAppliedEvent> _appliedEventsByTargetKey;

    public TimberbornBurnDamageTestStateProvider(
        IEnumerable<TimberbornBurnDamageTargetState> states,
        IEnumerable<TimberbornBurnDamageAppliedEvent>? appliedEvents = null)
    {
        TimberbornBurnDamageTargetState[] stateArray = states.ToArray();
        _statesByCellIndex = stateArray
            .SelectMany(static state => state.OwnedCellIndices.Select(cellIndex => new { cellIndex, state }))
            .ToDictionary(static item => item.cellIndex, static item => item.state);
        _statesByTargetKey = stateArray.ToDictionary(static state => state.TargetKey);
        _appliedEventsByTargetKey = (appliedEvents ?? Array.Empty<TimberbornBurnDamageAppliedEvent>())
            .ToDictionary(static appliedEvent => appliedEvent.TargetKey);
    }

    public bool TryGetStateForCell(int cellIndex, out TimberbornBurnDamageTargetState state)
    {
        return _statesByCellIndex.TryGetValue(cellIndex, out state!);
    }

    public bool TryGetState(TimberbornBurnDamageTargetKey targetKey, out TimberbornBurnDamageTargetState state)
    {
        return _statesByTargetKey.TryGetValue(targetKey, out state!);
    }

    public bool TryGetAppliedEvent(
        TimberbornBurnDamageTargetKey targetKey,
        out TimberbornBurnDamageAppliedEvent appliedEvent)
    {
        return _appliedEventsByTargetKey.TryGetValue(targetKey, out appliedEvent!);
    }

    public static TimberbornBurnDamageTargetState State(
        string stableId,
        string specId,
        TimberbornBurnDamageTargetKind targetKind,
        int damageCapacity,
        int damageTaken,
        uint lastDamagedTick = 1,
        params int[] ownedCellIndices)
    {
        return new TimberbornBurnDamageTargetState(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            targetKind,
            damageCapacity == 0 ? TimberbornBurnMaterialKind.NonBurnable : TimberbornBurnMaterialKind.Constructed,
            DamageCapacity: damageCapacity,
            FuelValue: 1,
            Flammability: 1,
            DamageTaken: damageTaken,
            LastDamagedTick: lastDamagedTick,
            OwnedCellIndices: ownedCellIndices,
            MissingResourceIds: [],
            AccountedResourceIds: []);
    }

    public static TimberbornBurnDamageAppliedEvent AppliedEvent(
        string stableId,
        string specId,
        int sourceCellIndex,
        int damageApplied,
        int damageTaken,
        int damageCapacity,
        uint tick)
    {
        return new TimberbornBurnDamageAppliedEvent(
            new TimberbornBurnDamageTargetKey(stableId),
            specId,
            sourceCellIndex,
            damageApplied,
            damageTaken,
            damageCapacity,
            tick);
    }
}
