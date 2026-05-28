using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornStoredGoodBurnConsequence(
    int CellIndex,
    uint Tick,
    int BurnBudget,
    int Fuel,
    int Heat,
    int Water)
{
    public bool ShouldBurnStoredGoods => BurnBudget > 0;

    public static TimberbornStoredGoodBurnConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornStoredGoodBurnConsequence(
            decision.CellIndex,
            tick,
            Math.Max(0, decision.OldFuel - decision.NewFuel),
            decision.NewFuel,
            decision.NewHeat,
            decision.NewWater);
    }
}

public sealed record TimberbornStoredGoodStack(string ResourceId, int Amount);

public sealed record TimberbornStoredGoodBurnTarget(
    string StableId,
    IReadOnlyList<TimberbornStoredGoodStack> Stacks,
    bool CanMutateInventory);

public readonly record struct TimberbornStoredGoodBurnConsequenceResult(
    bool MatchedStorageCell,
    bool AppliedConsequence,
    int BurnableStackCount,
    int DestroyedItemCount,
    int HazardousGoodCount,
    int ExplosiveGoodCount,
    int ExplosiveBlastTriggeredCount,
    int ContaminatedGoodCount,
    int ContaminationPulseCellCount,
    int UnknownResourceCount,
    int SkippedNonBurnableItemCount);

public sealed record TimberbornStoredGoodHazardStack(
    string ResourceId,
    int Amount,
    byte FuelValue,
    bool Explosive,
    bool Contaminated);

public readonly record struct TimberbornStoredGoodHazardConsequenceResult(
    int ExplosiveGoodCount,
    int ExplosiveBlastTriggeredCount,
    int NativeBlastAffectedTileCount,
    int ContaminatedGoodCount,
    int ContaminationPulseCellCount)
{
    public static readonly TimberbornStoredGoodHazardConsequenceResult Empty = new(
        ExplosiveGoodCount: 0,
        ExplosiveBlastTriggeredCount: 0,
        NativeBlastAffectedTileCount: 0,
        ContaminatedGoodCount: 0,
        ContaminationPulseCellCount: 0);
}

public readonly record struct TimberbornStoredGoodBurnConsequenceSummary(
    int ConsideredDeltaCount,
    int MatchedStorageCellCount,
    int DuplicateStorageTargetSuppressedCount,
    int BurnableStackCount,
    int DestroyedItemCount,
    int HazardousGoodCount,
    int ExplosiveGoodCount,
    int ExplosiveBlastTriggeredCount,
    int ContaminatedGoodCount,
    int ContaminationPulseCellCount,
    int UnknownResourceCount,
    int SkippedNonBurnableItemCount)
{
    public static readonly TimberbornStoredGoodBurnConsequenceSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedStorageCellCount: 0,
        DuplicateStorageTargetSuppressedCount: 0,
        BurnableStackCount: 0,
        DestroyedItemCount: 0,
        HazardousGoodCount: 0,
        ExplosiveGoodCount: 0,
        ExplosiveBlastTriggeredCount: 0,
        ContaminatedGoodCount: 0,
        ContaminationPulseCellCount: 0,
        UnknownResourceCount: 0,
        SkippedNonBurnableItemCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_stored_goods_burn_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_storage_cells={MatchedStorageCellCount} " +
            $"duplicate_storage_targets_suppressed={DuplicateStorageTargetSuppressedCount} " +
            $"burnable_stacks={BurnableStackCount} " +
            $"destroyed_items={DestroyedItemCount} " +
            $"hazardous_goods={HazardousGoodCount} " +
            $"explosive_goods={ExplosiveGoodCount} " +
            $"explosive_blasts_triggered={ExplosiveBlastTriggeredCount} " +
            $"contaminated_goods={ContaminatedGoodCount} " +
            $"contamination_pulse_cells={ContaminationPulseCellCount} " +
            $"unknown_resources={UnknownResourceCount} " +
            $"skipped_non_burnable_items={SkippedNonBurnableItemCount}";
    }
}

public interface ITimberbornStoredGoodBurnConsequenceSink
{
    TimberbornStoredGoodBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornStoredGoodBurnInventoryApi
{
    TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornStoredGoodBurnConsequence consequence);

    TimberbornStoredGoodBurnConsequenceResult BurnStoredGoods(
        TimberbornStoredGoodBurnTarget target,
        int burnBudget,
        IReadOnlyList<TimberbornStoredGoodStack> stacksToDestroy);
}

public interface ITimberbornStoredGoodBurnDamageInventoryApi
{
    TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornBurnDamageTargetState state);
}

public interface ITimberbornStoredGoodHazardConsequenceSink
{
    TimberbornStoredGoodHazardConsequenceResult ApplyHazards(
        TimberbornStoredGoodBurnTarget target,
        TimberbornStoredGoodBurnConsequence consequence,
        IReadOnlyList<TimberbornStoredGoodHazardStack> hazardStacks);
}

public sealed class NullTimberbornStoredGoodHazardConsequenceSink : ITimberbornStoredGoodHazardConsequenceSink
{
    public static readonly NullTimberbornStoredGoodHazardConsequenceSink Instance = new();

    private NullTimberbornStoredGoodHazardConsequenceSink()
    {
    }

    public TimberbornStoredGoodHazardConsequenceResult ApplyHazards(
        TimberbornStoredGoodBurnTarget target,
        TimberbornStoredGoodBurnConsequence consequence,
        IReadOnlyList<TimberbornStoredGoodHazardStack> hazardStacks)
    {
        return TimberbornStoredGoodHazardConsequenceResult.Empty;
    }
}

public interface ITimberbornStoredGoodContaminationPulseSink
{
    int EnqueueContaminationPulse(TimberbornExplosiveInfrastructureTarget target, int pulseRadius);
}

public sealed class TimberbornQueuedStoredGoodContaminationPulseSink : ITimberbornStoredGoodContaminationPulseSink
{
    private const byte MaxContamination = 7;
    private readonly FireGrid _grid;
    private TimberbornFireSystem? _fireSystem;

    public TimberbornQueuedStoredGoodContaminationPulseSink(FireGrid grid)
    {
        _grid = grid;
    }

    public void Attach(TimberbornFireSystem fireSystem)
    {
        _fireSystem = fireSystem ?? throw new ArgumentNullException(nameof(fireSystem));
    }

    public void Detach()
    {
        _fireSystem = null;
    }

    public int EnqueueContaminationPulse(TimberbornExplosiveInfrastructureTarget target, int pulseRadius)
    {
        if (_fireSystem is null || pulseRadius < 0)
        {
            return 0;
        }

        FireSimChange[] changes = TimberbornQueuedFireSimHeatPulseSink
            .CreatePulseChanges(_grid, target.CellIndex, pulseHeat: 0, pulseRadius)
            .Select(static change => change with
            {
                AddHeat = null,
                SetSmokeContamination = MaxContamination,
                SetAshContamination = MaxContamination,
            })
            .ToArray();
        changes
            .ToList()
            .ForEach(change => _fireSystem.RegisterChange(change, "stored_good_contamination_pulse", shouldLog: false));
        if (changes.Length > 0)
        {
            _fireSystem.LogRegisteredChanges("stored_good_contamination_pulse", changes.Length);
        }

        return changes.Length;
    }
}

public sealed class TimberbornStoredGoodHazardConsequenceSink : ITimberbornStoredGoodHazardConsequenceSink
{
    private readonly FireGrid _grid;
    private readonly Func<TimberbornExplosiveInfrastructureConsequenceSettings> _settingsProvider;
    private readonly ITimberbornNativeBlastRadiusApi _nativeBlastRadiusApi;
    private readonly ITimberbornExplosiveInfrastructureHeatPulseSink _heatPulseSink;
    private readonly ITimberbornStoredGoodContaminationPulseSink _contaminationPulseSink;

    public TimberbornStoredGoodHazardConsequenceSink(
        FireGrid grid,
        Func<TimberbornExplosiveInfrastructureConsequenceSettings> settingsProvider,
        ITimberbornNativeBlastRadiusApi nativeBlastRadiusApi,
        ITimberbornExplosiveInfrastructureHeatPulseSink heatPulseSink,
        ITimberbornStoredGoodContaminationPulseSink contaminationPulseSink)
    {
        _grid = grid;
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _nativeBlastRadiusApi = nativeBlastRadiusApi ?? throw new ArgumentNullException(nameof(nativeBlastRadiusApi));
        _heatPulseSink = heatPulseSink ?? throw new ArgumentNullException(nameof(heatPulseSink));
        _contaminationPulseSink = contaminationPulseSink ?? throw new ArgumentNullException(nameof(contaminationPulseSink));
    }

    public TimberbornStoredGoodHazardConsequenceResult ApplyHazards(
        TimberbornStoredGoodBurnTarget target,
        TimberbornStoredGoodBurnConsequence consequence,
        IReadOnlyList<TimberbornStoredGoodHazardStack> hazardStacks)
    {
        TimberbornStoredGoodHazardStack[] stacks = hazardStacks?.ToArray() ??
            throw new ArgumentNullException(nameof(hazardStacks));
        if (stacks.Length == 0)
        {
            return TimberbornStoredGoodHazardConsequenceResult.Empty;
        }

        TimberbornExplosiveInfrastructureConsequenceSettings settings = _settingsProvider();
        TimberbornExplosiveInfrastructureTarget blastTarget = CreateBlastTarget(target, consequence);
        int explosiveGoodCount = stacks
            .Where(static stack => stack.Explosive)
            .Sum(static stack => stack.Amount);
        int contaminatedGoodCount = stacks
            .Where(static stack => stack.Contaminated)
            .Sum(static stack => stack.Amount);
        TimberbornNativeBlastRadiusResult nativeBlastResult = explosiveGoodCount > 0
            ? _nativeBlastRadiusApi.TriggerBlastRadius(BlastCenter(consequence.CellIndex), settings.PulseRadius)
            : new TimberbornNativeBlastRadiusResult(AffectedTileCount: 0);
        if (explosiveGoodCount > 0)
        {
            _heatPulseSink.EnqueueHeatPulse(blastTarget, settings.PulseHeat, settings.PulseRadius);
        }
        int contaminationPulseCellCount = contaminatedGoodCount > 0
            ? _contaminationPulseSink.EnqueueContaminationPulse(blastTarget, settings.PulseRadius)
            : 0;

        return new TimberbornStoredGoodHazardConsequenceResult(
            ExplosiveGoodCount: explosiveGoodCount,
            ExplosiveBlastTriggeredCount: explosiveGoodCount > 0 ? 1 : 0,
            NativeBlastAffectedTileCount: nativeBlastResult.AffectedTileCount,
            ContaminatedGoodCount: contaminatedGoodCount,
            ContaminationPulseCellCount: contaminationPulseCellCount);
    }

    private TimberbornExplosiveInfrastructureTarget CreateBlastTarget(
        TimberbornStoredGoodBurnTarget target,
        TimberbornStoredGoodBurnConsequence consequence)
    {
        return new TimberbornExplosiveInfrastructureTarget(
            $"stored_goods:{target.StableId}",
            TimberbornExplosiveInfrastructureKind.StoredGoods,
            consequence.CellIndex,
            Depth: 0,
            CanTriggerNative: true);
    }

    private Vector3Int BlastCenter(int cellIndex)
    {
        (int x, int y, int z) = _grid.FromIndex(cellIndex);
        return new Vector3Int(x, y, z);
    }
}

public sealed class TimberbornStoredGoodBurnConsequenceSink : ITimberbornStoredGoodBurnConsequenceSink
{
    private readonly ITimberbornStoredGoodBurnInventoryApi _inventoryApi;
    private readonly ITimberbornStoredGoodHazardConsequenceSink _hazardSink;
    private readonly TimberbornResourceFuelCatalog _resourceFuelCatalog;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly ITimberbornBurnDamageTargetStateProvider? _burnDamageTargets;
    private readonly Dictionary<string, int> _partialBurnFuelByTargetResource = new(StringComparer.Ordinal);

    public TimberbornStoredGoodBurnConsequenceSink(
        ITimberbornStoredGoodBurnInventoryApi inventoryApi,
        ITimberbornStoredGoodHazardConsequenceSink? hazardSink = null,
        TimberbornResourceFuelCatalog? resourceFuelCatalog = null,
        ITimberbornFireLogSink? logSink = null,
        ITimberbornBurnDamageTargetStateProvider? burnDamageTargets = null)
    {
        _inventoryApi = inventoryApi ?? throw new ArgumentNullException(nameof(inventoryApi));
        _hazardSink = hazardSink ?? NullTimberbornStoredGoodHazardConsequenceSink.Instance;
        _resourceFuelCatalog = resourceFuelCatalog ?? TimberbornResourceFuelCatalog.Default;
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        _burnDamageTargets = burnDamageTargets;
    }

    public TimberbornStoredGoodBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornStoredGoodBurnConsequence[] consequences = decisions
            .Select(decision => TimberbornStoredGoodBurnConsequence.FromDecision(tick, decision))
            .Where(static consequence => consequence.ShouldBurnStoredGoods)
            .ToArray();
        ResolvedTarget[] resolvedTargets = consequences
            .Select(ResolveTarget)
            .ToArray();
        ResolvedTarget[] matchedTargets = resolvedTargets
            .Where(static resolvedTarget => resolvedTarget.Target is not null || resolvedTarget.HasOwnedStorageWithoutInventoryApi)
            .ToArray();
        ResolvedTarget[] uniqueTargets = matchedTargets
            .GroupBy(static resolvedTarget => resolvedTarget.StableId, StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static resolvedTarget => resolvedTarget.Consequence.BurnBudget)
                .ThenByDescending(static resolvedTarget => resolvedTarget.Consequence.Heat)
                .ThenBy(static resolvedTarget => resolvedTarget.Consequence.CellIndex)
                .First())
            .ToArray();
        TimberbornStoredGoodBurnConsequenceResult[] results = uniqueTargets
            .Select(BurnTarget)
            .ToArray();

        TimberbornStoredGoodBurnConsequenceSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedStorageCellCount: matchedTargets.Length,
            DuplicateStorageTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            BurnableStackCount: results.Sum(static result => result.BurnableStackCount),
            DestroyedItemCount: results.Sum(static result => result.DestroyedItemCount),
            HazardousGoodCount: results.Sum(static result => result.HazardousGoodCount),
            ExplosiveGoodCount: results.Sum(static result => result.ExplosiveGoodCount),
            ExplosiveBlastTriggeredCount: results.Sum(static result => result.ExplosiveBlastTriggeredCount),
            ContaminatedGoodCount: results.Sum(static result => result.ContaminatedGoodCount),
            ContaminationPulseCellCount: results.Sum(static result => result.ContaminationPulseCellCount),
            UnknownResourceCount: results.Sum(static result => result.UnknownResourceCount),
            SkippedNonBurnableItemCount: results.Sum(static result => result.SkippedNonBurnableItemCount));
        if (TimberbornReleaseLogNoisePolicy.ShouldLogConsequenceSummary(
            summary.MatchedStorageCellCount,
            summary.DestroyedItemCount,
            summary.HazardousGoodCount,
            summary.UnknownResourceCount))
        {
            _logSink.Info(summary.ToLogToken(tick));
        }

        return summary;
    }

    private TimberbornStoredGoodBurnConsequenceResult BurnTarget(ResolvedTarget resolvedTarget)
    {
        if (resolvedTarget.Target is null)
        {
            throw new InvalidOperationException(
                $"Stored good burn failed to resolve inventory target for owned storage {resolvedTarget.StableId}.");
        }

        TimberbornStoredGoodBurnTarget target = resolvedTarget.Target;
        ClassifiedStack[] classifiedStacks = target.Stacks
            .Where(static stack => stack.Amount > 0)
            .Select(stack => new ClassifiedStack(stack, _resourceFuelCatalog.Lookup(stack.ResourceId)))
            .ToArray();
        int skippedUnknownResourceCount = classifiedStacks
            .Count(static stack => !stack.Profile.Known);
        int hazardousGoodCount = classifiedStacks
            .Where(static stack => stack.Profile.Explosive || stack.Profile.Contaminated)
            .Sum(static stack => stack.Stack.Amount);
        int explosiveGoodCount = classifiedStacks
            .Where(static stack => stack.Profile.Explosive)
            .Sum(static stack => stack.Stack.Amount);
        int contaminatedGoodCount = classifiedStacks
            .Where(static stack => stack.Profile.Contaminated)
            .Sum(static stack => stack.Stack.Amount);
        int skippedNonBurnableItemCount = classifiedStacks
            .Where(static stack => stack.Profile.FuelValue == 0)
            .Where(static stack => !stack.Profile.Contaminated)
            .Sum(static stack => stack.Stack.Amount);
        ClassifiedStack[] hazardStacks = classifiedStacks
            .Where(static stack => stack.Profile.Known)
            .Where(static stack => stack.Profile.Explosive || stack.Profile.Contaminated)
            .OrderByDescending(static stack => stack.Profile.Explosive)
            .ThenByDescending(static stack => stack.Profile.Contaminated)
            .ThenByDescending(static stack => stack.Profile.Flammability)
            .ThenByDescending(static stack => stack.Profile.FuelValue)
            .ThenBy(static stack => stack.Stack.ResourceId, StringComparer.Ordinal)
            .ToArray();
        TimberbornStoredGoodStack[] hazardStacksToDestroy = SelectHazardStacksToDestroy(
            target.StableId,
            hazardStacks,
            resolvedTarget.Consequence.BurnBudget);
        TimberbornStoredGoodHazardConsequenceResult hazardResult = _hazardSink.ApplyHazards(
            target,
            resolvedTarget.Consequence,
            CreateHazardStacks(hazardStacks, hazardStacksToDestroy));
        int burnBudgetAfterHazards = Math.Max(
            0,
            resolvedTarget.Consequence.BurnBudget - CalculateFuelBudgetSpent(hazardStacks, hazardStacksToDestroy));
        ClassifiedStack[] burnableStacks = classifiedStacks
            .Where(static stack => stack.Profile.FuelValue > 0)
            .Where(static stack => stack.Profile.Known)
            .Where(static stack => !stack.Profile.Explosive)
            .Where(static stack => !stack.Profile.Contaminated)
            .OrderByDescending(static stack => stack.Profile.Flammability)
            .ThenByDescending(static stack => stack.Profile.FuelValue)
            .ThenBy(static stack => stack.Stack.ResourceId, StringComparer.Ordinal)
            .ToArray();
        TimberbornStoredGoodStack[] stacksToDestroy = SelectStacksToDestroy(
            target.StableId,
            burnableStacks,
            burnBudgetAfterHazards);

        if (!target.CanMutateInventory)
        {
            throw new InvalidOperationException(
                $"Stored good burn inventory mutation is unavailable for {target.StableId}.");
        }

        TimberbornStoredGoodStack[] stacksToDestroyIncludingHazards = hazardStacksToDestroy
            .Concat(stacksToDestroy)
            .GroupBy(static stack => stack.ResourceId, StringComparer.Ordinal)
            .Select(static group => new TimberbornStoredGoodStack(group.Key, group.Sum(static stack => stack.Amount)))
            .ToArray();
        return _inventoryApi.BurnStoredGoods(
            target,
            resolvedTarget.Consequence.BurnBudget,
            stacksToDestroyIncludingHazards) with
        {
            BurnableStackCount = burnableStacks.Length + hazardStacks.Length,
            HazardousGoodCount = hazardousGoodCount,
            ExplosiveGoodCount = explosiveGoodCount,
            ExplosiveBlastTriggeredCount = hazardResult.ExplosiveBlastTriggeredCount,
            ContaminatedGoodCount = contaminatedGoodCount,
            ContaminationPulseCellCount = hazardResult.ContaminationPulseCellCount,
            UnknownResourceCount = skippedUnknownResourceCount,
            SkippedNonBurnableItemCount = skippedNonBurnableItemCount,
        };
    }

    private TimberbornStoredGoodStack[] SelectHazardStacksToDestroy(
        string stableId,
        IReadOnlyList<ClassifiedStack> hazardStacks,
        int burnBudget)
    {
        ClassifiedStack[] fuelBearingHazards = hazardStacks
            .Where(static stack => stack.Profile.FuelValue > 0)
            .ToArray();
        TimberbornStoredGoodStack[] selectedFuelBearingHazards = SelectStacksToDestroy(
            stableId,
            fuelBearingHazards,
            burnBudget);
        TimberbornStoredGoodStack[] contaminatedNonFuelHazards = burnBudget <= 0
            ? Array.Empty<TimberbornStoredGoodStack>()
            : hazardStacks
                .Where(static stack => stack.Profile.Contaminated)
                .Where(static stack => stack.Profile.FuelValue == 0)
                .Select(static stack => stack.Stack)
                .ToArray();

        return selectedFuelBearingHazards
            .Concat(contaminatedNonFuelHazards)
            .GroupBy(static stack => stack.ResourceId, StringComparer.Ordinal)
            .Select(static group => new TimberbornStoredGoodStack(group.Key, group.Sum(static stack => stack.Amount)))
            .ToArray();
    }

    private static TimberbornStoredGoodHazardStack[] CreateHazardStacks(
        IReadOnlyList<ClassifiedStack> classifiedHazardStacks,
        IReadOnlyList<TimberbornStoredGoodStack> stacksToDestroy)
    {
        IReadOnlyDictionary<string, int> amountsByResourceId = stacksToDestroy
            .GroupBy(static stack => stack.ResourceId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Sum(static stack => stack.Amount), StringComparer.Ordinal);

        return classifiedHazardStacks
            .Where(stack => amountsByResourceId.GetValueOrDefault(stack.Stack.ResourceId) > 0)
            .Select(stack => new TimberbornStoredGoodHazardStack(
                stack.Stack.ResourceId,
                Math.Min(stack.Stack.Amount, amountsByResourceId.GetValueOrDefault(stack.Stack.ResourceId)),
                stack.Profile.FuelValue,
                stack.Profile.Explosive,
                stack.Profile.Contaminated))
            .ToArray();
    }

    private static int CalculateFuelBudgetSpent(
        IReadOnlyList<ClassifiedStack> classifiedStacks,
        IReadOnlyList<TimberbornStoredGoodStack> stacksToDestroy)
    {
        IReadOnlyDictionary<string, int> fuelValueByResourceId = classifiedStacks
            .ToDictionary(static stack => stack.Stack.ResourceId, static stack => Math.Max(0, (int)stack.Profile.FuelValue), StringComparer.Ordinal);

        return stacksToDestroy
            .Sum(stack => stack.Amount * fuelValueByResourceId.GetValueOrDefault(stack.ResourceId));
    }

    private TimberbornStoredGoodStack[] SelectStacksToDestroy(
        string stableId,
        IReadOnlyList<ClassifiedStack> burnableStacks,
        int burnBudget)
    {
        int remainingBudget = Math.Max(0, burnBudget);
        return burnableStacks
            .Select(stack =>
            {
                int fuelValue = Math.Max(1, (int)stack.Profile.FuelValue);
                string partialKey = $"{stableId}:{stack.Stack.ResourceId}";
                int partialFuel = _partialBurnFuelByTargetResource.GetValueOrDefault(partialKey);
                int spendableFuel = partialFuel + remainingBudget;
                int amount = Math.Min(stack.Stack.Amount, spendableFuel / fuelValue);
                int consumedFuel = amount * fuelValue;
                remainingBudget = Math.Max(0, remainingBudget - Math.Max(0, consumedFuel - partialFuel));

                int nextPartialFuel = spendableFuel - consumedFuel;
                if (amount >= stack.Stack.Amount)
                {
                    _partialBurnFuelByTargetResource.Remove(partialKey);
                    remainingBudget = nextPartialFuel;
                }
                else if (nextPartialFuel > 0)
                {
                    _partialBurnFuelByTargetResource[partialKey] = nextPartialFuel;
                    remainingBudget = 0;
                }
                else
                {
                    _partialBurnFuelByTargetResource.Remove(partialKey);
                }

                return new TimberbornStoredGoodStack(stack.Stack.ResourceId, amount);
            })
            .Where(static stack => stack.Amount > 0)
            .ToArray();
    }

    private ResolvedTarget ResolveTarget(TimberbornStoredGoodBurnConsequence consequence)
    {
        TimberbornStoredGoodBurnTarget? target = _inventoryApi.ResolveTarget(consequence);
        if (_burnDamageTargets is null)
        {
            return new ResolvedTarget(consequence, target, BurnDamageState: null);
        }

        if (!_burnDamageTargets.TryGetStateForCell(consequence.CellIndex, out TimberbornBurnDamageTargetState state) ||
            state.TargetKind is not TimberbornBurnDamageTargetKind.Storage and
                not TimberbornBurnDamageTargetKind.Structure)
        {
            return new ResolvedTarget(consequence, Target: null, BurnDamageState: null);
        }

        if ((target is null || !string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal)) &&
            _inventoryApi is ITimberbornStoredGoodBurnDamageInventoryApi burnDamageInventoryApi)
        {
            TimberbornStoredGoodBurnTarget? stateTarget = burnDamageInventoryApi.ResolveTarget(state);
            if (stateTarget is not null)
            {
                return new ResolvedTarget(consequence, stateTarget, state);
            }
        }

        if (target is not null &&
            string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal))
        {
            return new ResolvedTarget(consequence, target, state);
        }

        return new ResolvedTarget(consequence, Target: null, state);
    }

    private readonly record struct ResolvedTarget(
        TimberbornStoredGoodBurnConsequence Consequence,
        TimberbornStoredGoodBurnTarget? Target,
        TimberbornBurnDamageTargetState? BurnDamageState)
    {
        public string StableId => Target?.StableId ?? BurnDamageState?.TargetKey.StableId ?? string.Empty;

        public bool HasOwnedStorageWithoutInventoryApi => Target is null &&
            BurnDamageState?.TargetKind == TimberbornBurnDamageTargetKind.Storage;
    }

    private readonly record struct ClassifiedStack(
        TimberbornStoredGoodStack Stack,
        TimberbornResourceFuelProfile Profile);
}

public sealed class NullTimberbornStoredGoodBurnConsequenceSink : ITimberbornStoredGoodBurnConsequenceSink
{
    public static readonly NullTimberbornStoredGoodBurnConsequenceSink Instance = new();

    private NullTimberbornStoredGoodBurnConsequenceSink()
    {
    }

    public TimberbornStoredGoodBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornStoredGoodBurnConsequenceSummary.Empty;
    }
}

public sealed class TimberbornStockpileStoredGoodBurnInventoryApi :
    ITimberbornStoredGoodBurnInventoryApi,
    ITimberbornStoredGoodBurnDamageInventoryApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;
    private readonly EntityRegistry? _entityRegistry;
    private readonly Dictionary<string, Stockpile> _stockpilesByStableId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Inventory> _inventoriesByStableId = new(StringComparer.Ordinal);

    public TimberbornStockpileStoredGoodBurnInventoryApi(
        FireGrid grid,
        IBlockService blockService,
        EntityRegistry? entityRegistry = null)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _entityRegistry = entityRegistry;
    }

    public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornStoredGoodBurnConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        Stockpile? stockpile = _blockService
            .GetObjectsWithComponentAt<Stockpile>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();
        if (stockpile is not null)
        {
            return CreateTarget(stockpile);
        }

        SimpleOutputInventory? outputInventory = _blockService
            .GetObjectsWithComponentAt<SimpleOutputInventory>(coordinates)
            .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
            .FirstOrDefault();

        return outputInventory is null ? null : CreateTarget(outputInventory);
    }

    public TimberbornStoredGoodBurnTarget? ResolveTarget(TimberbornBurnDamageTargetState state)
    {
        if (state is null ||
            state.TargetKind is not TimberbornBurnDamageTargetKind.Storage and
                not TimberbornBurnDamageTargetKind.Structure)
        {
            return null;
        }

        TimberbornStoredGoodBurnTarget? indexedTarget = ResolveIndexedStockpileTarget(state);
        if (indexedTarget is not null)
        {
            return indexedTarget;
        }

        TimberbornStoredGoodBurnTarget? outputInventoryTarget = ResolveIndexedOutputInventoryTarget(state);
        if (outputInventoryTarget is not null)
        {
            return outputInventoryTarget;
        }

        return state.OwnedCellIndices
            .Select(_grid.FromIndex)
            .Select(coordinates => new Vector3Int(coordinates.X, coordinates.Y, coordinates.Z))
            .SelectMany(coordinates => _blockService.GetObjectsWithComponentAt<Stockpile>(coordinates))
            .GroupBy(static stockpile => RuntimeHelpers.GetHashCode(stockpile))
            .Select(static group => group.First())
            .Select(CreateTarget)
            .FirstOrDefault(target => string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal));
    }

    public TimberbornStoredGoodBurnConsequenceResult BurnStoredGoods(
        TimberbornStoredGoodBurnTarget target,
        int burnBudget,
        IReadOnlyList<TimberbornStoredGoodStack> stacksToDestroy)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        int destroyedItemCount = 0;
        Inventory? inventory = FindInventory(target.StableId);
        if (inventory is null)
        {
            throw new InvalidOperationException(
                $"Stored good burn inventory target disappeared before mutation for {target.StableId}.");
        }

        stacksToDestroy
            .ToList()
            .ForEach(stack =>
            {
                int availableAmount = inventory.AmountInStock(stack.ResourceId);
                int amountToDestroy = Math.Min(availableAmount, stack.Amount);
                if (amountToDestroy > 0)
                {
                    inventory.Take(new GoodAmount(stack.ResourceId, amountToDestroy));
                    destroyedItemCount += amountToDestroy;
                }
            });

        return new TimberbornStoredGoodBurnConsequenceResult(
            MatchedStorageCell: true,
            AppliedConsequence: destroyedItemCount > 0,
            BurnableStackCount: 0,
            DestroyedItemCount: destroyedItemCount,
            HazardousGoodCount: 0,
            ExplosiveGoodCount: 0,
            ExplosiveBlastTriggeredCount: 0,
            ContaminatedGoodCount: 0,
            ContaminationPulseCellCount: 0,
            UnknownResourceCount: 0,
            SkippedNonBurnableItemCount: 0);
    }

    private Stockpile? FindStockpile(string stableId)
    {
        return _stockpilesByStableId.TryGetValue(stableId, out Stockpile stockpile)
            ? stockpile
            : null;
    }

    private Inventory? FindInventory(string stableId)
    {
        return _inventoriesByStableId.TryGetValue(stableId, out Inventory inventory)
            ? inventory
            : FindStockpile(stableId)?.Inventory;
    }

    private TimberbornStoredGoodBurnTarget? ResolveIndexedStockpileTarget(TimberbornBurnDamageTargetState state)
    {
        if (_entityRegistry is null)
        {
            return null;
        }

        TimberbornEntityComponentCells.TimberbornEntityComponentBlockObject<Stockpile>[] stockpiles =
            TimberbornEntityComponentCells.ComponentBlockObjects<Stockpile>(_entityRegistry).ToArray();
        TimberbornStoredGoodBurnTarget? exactTarget = stockpiles
            .Select(static item => item.Component)
            .Select(CreateTarget)
            .FirstOrDefault(target => string.Equals(target.StableId, state.TargetKey.StableId, StringComparison.Ordinal));
        if (exactTarget is not null)
        {
            return exactTarget;
        }

        HashSet<int> ownedCellIndices = state.OwnedCellIndices.ToHashSet();
        return stockpiles
            .Where(item => TimberbornEntityComponentCells.OccupiedCoordinates(item.BlockObject)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, _grid))
                .Select(coordinates => _grid.ToIndex(coordinates.x, coordinates.y, coordinates.z))
                .Any(ownedCellIndices.Contains))
            .Select(static item => item.Component)
            .Select(CreateTarget)
            .FirstOrDefault();
    }

    private TimberbornStoredGoodBurnTarget? ResolveIndexedOutputInventoryTarget(TimberbornBurnDamageTargetState state)
    {
        if (_entityRegistry is null)
        {
            return null;
        }

        HashSet<int> ownedCellIndices = state.OwnedCellIndices.ToHashSet();
        return TimberbornEntityComponentCells.ComponentBlockObjects<SimpleOutputInventory>(_entityRegistry)
            .Where(item => TimberbornEntityComponentCells.OccupiedCoordinates(item.BlockObject)
                .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, _grid))
                .Select(coordinates => _grid.ToIndex(coordinates.x, coordinates.y, coordinates.z))
                .Any(ownedCellIndices.Contains))
            .Select(static item => item.Component)
            .Select(CreateTarget)
            .Where(static target => target.Stacks.Count > 0)
            .FirstOrDefault();
    }

    private TimberbornStoredGoodBurnTarget CreateTarget(Stockpile stockpile)
    {
        Inventory? inventory = stockpile.Inventory;
        TimberbornStoredGoodStack[] stacks = inventory is null
            ? Array.Empty<TimberbornStoredGoodStack>()
            : inventory.Stock
                .Select(static good => new TimberbornStoredGoodStack(good.GoodId, good.Amount))
                .ToArray();

        string stableId = $"stockpile:{RuntimeHelpers.GetHashCode(stockpile)}";
        _stockpilesByStableId[stableId] = stockpile;
        if (inventory is not null)
        {
            _inventoriesByStableId[stableId] = inventory;
        }

        return new TimberbornStoredGoodBurnTarget(
            StableId: stableId,
            Stacks: stacks,
            CanMutateInventory: inventory is not null);
    }

    private TimberbornStoredGoodBurnTarget CreateTarget(SimpleOutputInventory outputInventory)
    {
        Inventory inventory = outputInventory.Inventory;
        TimberbornStoredGoodStack[] stacks = inventory.Stock
            .Select(static good => new TimberbornStoredGoodStack(good.GoodId, good.Amount))
            .ToArray();

        string stableId = $"simple_output:{RuntimeHelpers.GetHashCode(outputInventory)}";
        _inventoriesByStableId[stableId] = inventory;

        return new TimberbornStoredGoodBurnTarget(
            StableId: stableId,
            Stacks: stacks,
            CanMutateInventory: true);
    }
}
