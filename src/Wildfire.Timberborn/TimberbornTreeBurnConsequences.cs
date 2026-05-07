namespace Wildfire.Timberborn;

public enum TimberbornTreeBurnConsequenceKind
{
    ReduceYield,
    KillTree,
    MarkBurnedVisual,
}

public readonly record struct TimberbornTreeBurnConsequence(
    TimberbornBurnDamageTargetKey TargetKey,
    string SpecId,
    TimberbornTreeBurnConsequenceKind Kind,
    string YieldResourceId,
    int YieldLost,
    int RemainingYield,
    uint Tick,
    int SourceCellIndex,
    int DamageApplied,
    int DamageTaken,
    int DamageCapacity);

public readonly record struct TimberbornTreeBurnConsequenceResult(
    bool Applied,
    bool SafeApiUnavailable);

public readonly record struct TimberbornTreeBurnConsequenceSummary(
    uint Tick,
    int ConsideredTreeTargetCount,
    int BurnableTreeTargetCount,
    int YieldLost,
    int KilledTreeCount,
    int VisualStateUpdateCount,
    int DuplicateCellSuppressedCount,
    int UnmappedTargetCount,
    int UnknownCuttableResourceCount,
    int NonBurnableTreeTargetCount,
    int SkippedUnsafeApiCount)
{
    public static readonly TimberbornTreeBurnConsequenceSummary Empty = new(
        Tick: 0,
        ConsideredTreeTargetCount: 0,
        BurnableTreeTargetCount: 0,
        YieldLost: 0,
        KilledTreeCount: 0,
        VisualStateUpdateCount: 0,
        DuplicateCellSuppressedCount: 0,
        UnmappedTargetCount: 0,
        UnknownCuttableResourceCount: 0,
        NonBurnableTreeTargetCount: 0,
        SkippedUnsafeApiCount: 0);

    public string ToLogToken()
    {
        return "wildfire_timberborn_tree_burn_consequences_applied " +
            $"tick={Tick} " +
            $"considered_tree_targets={ConsideredTreeTargetCount} " +
            $"burnable_tree_targets={BurnableTreeTargetCount} " +
            $"yield_lost={YieldLost} " +
            $"killed_trees={KilledTreeCount} " +
            $"visual_state_updates={VisualStateUpdateCount} " +
            $"duplicate_cells_suppressed={DuplicateCellSuppressedCount} " +
            $"unmapped_targets={UnmappedTargetCount} " +
            $"unknown_cuttable_resources={UnknownCuttableResourceCount} " +
            $"non_burnable_tree_targets={NonBurnableTreeTargetCount} " +
            $"skipped_unsafe_apis={SkippedUnsafeApiCount}";
    }
}

public interface ITimberbornTreeBurnConsequenceSink
{
    TimberbornTreeBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornTreeBurnConsequenceApi
{
    TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence);
}

public sealed class TimberbornTreeBurnConsequenceSink : ITimberbornTreeBurnConsequenceSink
{
    private readonly TimberbornBurnDamageService _burnDamageService;
    private readonly ITimberbornTreeBurnConsequenceApi _consequenceApi;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<TimberbornBurnDamageTargetKey, int> _appliedYieldLossByTarget = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _killedTargets = new();

    public TimberbornTreeBurnConsequenceSink(
        TimberbornBurnDamageService burnDamageService,
        ITimberbornTreeBurnConsequenceApi consequenceApi,
        ITimberbornFireLogSink? logSink = null)
    {
        _burnDamageService = burnDamageService ?? throw new ArgumentNullException(nameof(burnDamageService));
        _consequenceApi = consequenceApi ?? throw new ArgumentNullException(nameof(consequenceApi));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornTreeBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        if (decisions is null)
        {
            throw new ArgumentNullException(nameof(decisions));
        }

        TreeCandidateHit[] treeHits = decisions
            .Select(CreateTreeCandidateHit)
            .Where(static hit => hit.HasValue)
            .Select(static hit => hit!.Value)
            .ToArray();
        TimberbornBurnDamageTargetState[] consideredTreeTargets = treeHits
            .Select(static hit => hit.State)
            .GroupBy(static state => state.TargetKey)
            .Select(static group => group.First())
            .ToArray();
        TimberbornTreeBurnTargetOutcome[] outcomes = consideredTreeTargets
            .Select(state => ApplyTreeTargetConsequence(tick, state))
            .ToArray();

        TimberbornTreeBurnConsequenceSummary summary = new(
            Tick: tick,
            ConsideredTreeTargetCount: consideredTreeTargets.Length,
            BurnableTreeTargetCount: consideredTreeTargets.Count(static state =>
                state.MaterialKind is not TimberbornBurnMaterialKind.NonBurnable &&
                state.DamageCapacity > 0 &&
                state.MissingResourceIds.Count == 0),
            YieldLost: outcomes.Sum(static outcome => outcome.YieldLost),
            KilledTreeCount: outcomes.Count(static outcome => outcome.Killed),
            VisualStateUpdateCount: outcomes.Count(static outcome => outcome.VisualUpdated),
            DuplicateCellSuppressedCount: _burnDamageService.LastApplySummary.DuplicateCellSuppressedCount,
            UnmappedTargetCount: _burnDamageService.LastApplySummary.UnresolvedCellCount,
            UnknownCuttableResourceCount: consideredTreeTargets.Count(static state => state.MissingResourceIds.Count > 0),
            NonBurnableTreeTargetCount: consideredTreeTargets.Count(static state =>
                state.MaterialKind is TimberbornBurnMaterialKind.NonBurnable ||
                (state.DamageCapacity == 0 && state.MissingResourceIds.Count == 0)),
            SkippedUnsafeApiCount: outcomes.Sum(static outcome => outcome.SkippedUnsafeApiCount));

        if (summary.ConsideredTreeTargetCount > 0 ||
            summary.YieldLost > 0 ||
            summary.KilledTreeCount > 0 ||
            summary.VisualStateUpdateCount > 0 ||
            summary.SkippedUnsafeApiCount > 0)
        {
            _logSink.Info(summary.ToLogToken());
        }

        return summary;
    }

    private TreeCandidateHit? CreateTreeCandidateHit(TimberbornFireCellDeltaDecision decision)
    {
        if (decision.OldFuel <= decision.NewFuel ||
            !_burnDamageService.TargetKeyByCellIndex.TryGetValue(decision.CellIndex, out TimberbornBurnDamageTargetKey targetKey) ||
            !_burnDamageService.States.TryGetValue(targetKey, out TimberbornBurnDamageTargetState state) ||
            !TimberbornTreeBurnTargetClassifier.IsTreeOrCuttable(state))
        {
            return null;
        }

        return new TreeCandidateHit(decision.CellIndex, state);
    }

    private TimberbornTreeBurnTargetOutcome ApplyTreeTargetConsequence(
        uint tick,
        TimberbornBurnDamageTargetState state)
    {
        if (!TimberbornTreeBurnTargetClassifier.IsTreeOrCuttable(state))
        {
            return TimberbornTreeBurnTargetOutcome.NoOp;
        }

        if (state.MissingResourceIds.Count > 0)
        {
            return TimberbornTreeBurnTargetOutcome.UnknownResource;
        }

        bool burnable = state.MaterialKind is not TimberbornBurnMaterialKind.NonBurnable &&
            state.DamageCapacity > 0 &&
            state.AccountedResourceIds.Count > 0;
        if (!burnable)
        {
            return TimberbornTreeBurnTargetOutcome.NoOp;
        }

        int initialYield = CalculateInitialYield(state);
        int targetYieldLost = CalculateAcceptedYieldLoss(state, initialYield);
        int alreadyAppliedYieldLoss = _appliedYieldLossByTarget.TryGetValue(state.TargetKey, out int appliedYieldLoss)
            ? appliedYieldLoss
            : 0;
        int incrementalYieldLoss = Math.Max(0, targetYieldLost - alreadyAppliedYieldLoss);
        TimberbornTreeBurnTargetOutcome yieldOutcome = incrementalYieldLoss > 0
            ? ApplyYieldLoss(tick, state, incrementalYieldLoss, initialYield, targetYieldLost)
            : TimberbornTreeBurnTargetOutcome.BurnableNoChange;
        TimberbornTreeBurnTargetOutcome fullBurnOutcome =
            state.IsFullyDamaged || targetYieldLost >= initialYield
                ? ApplyFullBurnConsequences(tick, state, initialYield, targetYieldLost)
                : TimberbornTreeBurnTargetOutcome.BurnableNoChange;

        return TimberbornTreeBurnTargetOutcome.Combine(yieldOutcome, fullBurnOutcome);
    }

    private TimberbornTreeBurnTargetOutcome ApplyYieldLoss(
        uint tick,
        TimberbornBurnDamageTargetState state,
        int incrementalYieldLoss,
        int initialYield,
        int targetYieldLost)
    {
        TimberbornTreeBurnConsequenceResult result = _consequenceApi.ApplyConsequence(CreateConsequence(
            tick,
            state,
            TimberbornTreeBurnConsequenceKind.ReduceYield,
            incrementalYieldLoss,
            Math.Max(0, initialYield - targetYieldLost)));

        if (!result.Applied)
        {
            return result.SafeApiUnavailable
                ? TimberbornTreeBurnTargetOutcome.SkippedUnsafeApi
                : TimberbornTreeBurnTargetOutcome.BurnableNoChange;
        }

        _appliedYieldLossByTarget[state.TargetKey] = targetYieldLost;
        return new TimberbornTreeBurnTargetOutcome(
            Burnable: true,
            YieldLost: incrementalYieldLoss,
            Killed: false,
            VisualUpdated: false,
            SkippedUnknownResource: false,
            SkippedUnsafeApiCount: 0);
    }

    private TimberbornTreeBurnTargetOutcome ApplyFullBurnConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        int initialYield,
        int targetYieldLost)
    {
        if (_killedTargets.Contains(state.TargetKey))
        {
            return TimberbornTreeBurnTargetOutcome.BurnableNoChange;
        }

        TimberbornTreeBurnConsequence killConsequence = CreateConsequence(
            tick,
            state,
            TimberbornTreeBurnConsequenceKind.KillTree,
            targetYieldLost,
            remainingYield: 0);
        TimberbornTreeBurnConsequenceResult killResult = _consequenceApi.ApplyConsequence(killConsequence);
        TimberbornTreeBurnConsequenceResult visualResult = _consequenceApi.ApplyConsequence(killConsequence with
        {
            Kind = TimberbornTreeBurnConsequenceKind.MarkBurnedVisual,
        });

        if (killResult.Applied)
        {
            _killedTargets.Add(state.TargetKey);
            _appliedYieldLossByTarget[state.TargetKey] = initialYield;
        }

        return new TimberbornTreeBurnTargetOutcome(
            Burnable: true,
            YieldLost: 0,
            Killed: killResult.Applied,
            VisualUpdated: visualResult.Applied,
            SkippedUnknownResource: false,
            SkippedUnsafeApiCount: CountUnavailable(killResult) + CountUnavailable(visualResult));
    }

    private TimberbornTreeBurnConsequence CreateConsequence(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornTreeBurnConsequenceKind kind,
        int yieldLost,
        int remainingYield)
    {
        TimberbornBurnDamageAppliedEvent appliedEvent =
            _burnDamageService.LastAppliedEventsByTargetKey.TryGetValue(state.TargetKey, out TimberbornBurnDamageAppliedEvent found)
                ? found
                : new TimberbornBurnDamageAppliedEvent(
                    state.TargetKey,
                    state.SpecId,
                    state.OwnedCellIndices.DefaultIfEmpty(-1).Min(),
                    DamageApplied: 0,
                    state.DamageTaken,
                    state.DamageCapacity,
                    tick);

        return new TimberbornTreeBurnConsequence(
            state.TargetKey,
            state.SpecId,
            kind,
            TimberbornTreeBurnTargetClassifier.PrimaryYieldResourceId(state),
            yieldLost,
            remainingYield,
            tick,
            appliedEvent.SourceCellIndex,
            appliedEvent.DamageApplied,
            appliedEvent.DamageTaken,
            appliedEvent.DamageCapacity);
    }

    private static int CalculateInitialYield(TimberbornBurnDamageTargetState state)
    {
        int fuelValuePerYield = Math.Max(1, (int)state.FuelValue);
        return Math.Max(0, state.DamageCapacity / fuelValuePerYield);
    }

    private static int CalculateAcceptedYieldLoss(TimberbornBurnDamageTargetState state, int initialYield)
    {
        if (state.IsFullyDamaged)
        {
            return initialYield;
        }

        int fuelValuePerYield = Math.Max(1, (int)state.FuelValue);
        return Math.Clamp(state.DamageTaken / fuelValuePerYield, 0, initialYield);
    }

    private static int CountUnavailable(TimberbornTreeBurnConsequenceResult result)
    {
        return !result.Applied && result.SafeApiUnavailable ? 1 : 0;
    }

    private readonly record struct TreeCandidateHit(
        int CellIndex,
        TimberbornBurnDamageTargetState State);

    private readonly record struct TimberbornTreeBurnTargetOutcome(
        bool Burnable,
        int YieldLost,
        bool Killed,
        bool VisualUpdated,
        bool SkippedUnknownResource,
        int SkippedUnsafeApiCount)
    {
        public static readonly TimberbornTreeBurnTargetOutcome NoOp = new(
            Burnable: false,
            YieldLost: 0,
            Killed: false,
            VisualUpdated: false,
            SkippedUnknownResource: false,
            SkippedUnsafeApiCount: 0);

        public static readonly TimberbornTreeBurnTargetOutcome BurnableNoChange = NoOp with
        {
            Burnable = true,
        };

        public static readonly TimberbornTreeBurnTargetOutcome UnknownResource = NoOp with
        {
            SkippedUnknownResource = true,
        };

        public static readonly TimberbornTreeBurnTargetOutcome SkippedUnsafeApi = BurnableNoChange with
        {
            SkippedUnsafeApiCount = 1,
        };

        public static TimberbornTreeBurnTargetOutcome Combine(
            TimberbornTreeBurnTargetOutcome first,
            TimberbornTreeBurnTargetOutcome second)
        {
            return new TimberbornTreeBurnTargetOutcome(
                Burnable: first.Burnable || second.Burnable,
                YieldLost: first.YieldLost + second.YieldLost,
                Killed: first.Killed || second.Killed,
                VisualUpdated: first.VisualUpdated || second.VisualUpdated,
                SkippedUnknownResource: first.SkippedUnknownResource || second.SkippedUnknownResource,
                SkippedUnsafeApiCount: first.SkippedUnsafeApiCount + second.SkippedUnsafeApiCount);
        }
    }
}

public sealed class UnavailableTimberbornTreeBurnConsequenceApi : ITimberbornTreeBurnConsequenceApi
{
    public static readonly UnavailableTimberbornTreeBurnConsequenceApi Instance = new();

    private UnavailableTimberbornTreeBurnConsequenceApi()
    {
    }

    public TimberbornTreeBurnConsequenceResult ApplyConsequence(TimberbornTreeBurnConsequence consequence)
    {
        return new TimberbornTreeBurnConsequenceResult(Applied: false, SafeApiUnavailable: true);
    }
}

public sealed class NullTimberbornTreeBurnConsequenceSink : ITimberbornTreeBurnConsequenceSink
{
    public static readonly NullTimberbornTreeBurnConsequenceSink Instance = new();

    private NullTimberbornTreeBurnConsequenceSink()
    {
    }

    public TimberbornTreeBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornTreeBurnConsequenceSummary.Empty with { Tick = tick };
    }
}

public static class TimberbornTreeBurnTargetClassifier
{
    public static TimberbornTreeBurnTargetRegistrationSummary SummarizeRegisteredTargets(
        IEnumerable<TimberbornBurnDamageTargetState> states)
    {
        TimberbornBurnDamageTargetState[] treeTargets = states
            .Where(IsTreeOrCuttable)
            .ToArray();

        return new TimberbornTreeBurnTargetRegistrationSummary(
            treeTargets.Length,
            treeTargets
                .SelectMany(static state => state.OwnedCellIndices)
                .Distinct()
                .Count());
    }

    public static bool IsTreeOrCuttable(TimberbornBurnDamageTargetState state)
    {
        return state.TargetKind == TimberbornBurnDamageTargetKind.Tree ||
            (state.TargetKind == TimberbornBurnDamageTargetKind.Resource &&
                state.MaterialKind == TimberbornBurnMaterialKind.Wood &&
                state.AccountedResourceIds.Any(IsTreeOrWoodResource));
    }

    public static string PrimaryYieldResourceId(TimberbornBurnDamageTargetState state)
    {
        return state.AccountedResourceIds
            .Where(IsTreeOrWoodResource)
            .OrderBy(static resourceId => resourceId, StringComparer.Ordinal)
            .FirstOrDefault() ??
            state.AccountedResourceIds
                .OrderBy(static resourceId => resourceId, StringComparer.Ordinal)
                .FirstOrDefault() ??
            "unknown";
    }

    public static bool IsTreeOrWoodResource(string resourceId)
    {
        return resourceId.Equals("Log", StringComparison.OrdinalIgnoreCase);
    }
}

public readonly record struct TimberbornTreeBurnTargetRegistrationSummary(
    int TargetCount,
    int OwnedCellCount);
