namespace Wildfire.Timberborn.Consequences;

public enum TimberbornCropBurnConsequenceKind
{
    DryCrop,
    ReduceYield,
    KillCrop,
    MarkBurnedVisual,
    MarkBurnedLeftover,
}

public readonly record struct TimberbornCropBurnConsequence(
    TimberbornBurnDamageTargetKey TargetKey,
    string SpecId,
    TimberbornBurnDamageTargetKind TargetKind,
    TimberbornCropBurnConsequenceKind Kind,
    string YieldResourceId,
    int YieldLost,
    int RemainingYield,
    uint Tick,
    int SourceCellIndex,
    int DamageApplied,
    int DamageTaken,
    int DamageCapacity,
    bool IsFullyBurned,
    IReadOnlyList<string> AccountedResourceIds,
    IReadOnlyList<string> MissingResourceIds);

public readonly record struct TimberbornCropBurnConsequenceResult(
    bool MatchedCropTarget,
    int YieldLost,
    bool KilledCrop,
    bool VisualStateUpdated,
    bool SkippedUnsafeApi);

public readonly record struct TimberbornCropBurnConsequenceSummary(
    uint Tick,
    int ConsideredCropTargetCount,
    int BurnableCropTargetCount,
    int YieldLost,
    int KilledCropCount,
    int VisualStateUpdateCount,
    int DuplicateCellSuppressedCount,
    int UnmappedTargetCount,
    int UnknownHarvestResourceCount,
    int NonBurnableCropTargetCount,
    int SkippedUnsafeApiCount)
{
    public static readonly TimberbornCropBurnConsequenceSummary Empty = new(
        Tick: 0,
        ConsideredCropTargetCount: 0,
        BurnableCropTargetCount: 0,
        YieldLost: 0,
        KilledCropCount: 0,
        VisualStateUpdateCount: 0,
        DuplicateCellSuppressedCount: 0,
        UnmappedTargetCount: 0,
        UnknownHarvestResourceCount: 0,
        NonBurnableCropTargetCount: 0,
        SkippedUnsafeApiCount: 0);

    public string ToLogToken()
    {
        return "wildfire_timberborn_crop_burn_consequences_applied " +
            $"tick={Tick} " +
            $"considered_crop_targets={ConsideredCropTargetCount} " +
            $"burnable_crop_targets={BurnableCropTargetCount} " +
            $"yield_lost={YieldLost} " +
            $"killed_crops={KilledCropCount} " +
            $"visual_state_updates={VisualStateUpdateCount} " +
            $"duplicate_cells_suppressed={DuplicateCellSuppressedCount} " +
            $"unmapped_targets={UnmappedTargetCount} " +
            $"unknown_harvest_resources={UnknownHarvestResourceCount} " +
            $"non_burnable_crop_targets={NonBurnableCropTargetCount}";
    }
}

public interface ITimberbornCropBurnConsequenceSink
{
    TimberbornCropBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornCropBurnConsequenceApi
{
    TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence consequence);
}

public sealed class TimberbornCropBurnConsequenceSink : ITimberbornCropBurnConsequenceSink
{
    private readonly TimberbornBurnDamageService _burnDamageService;
    private readonly ITimberbornCropBurnConsequenceApi _consequenceApi;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<TimberbornBurnDamageTargetKey, int> _appliedYieldLossByTarget = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _driedTargets = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _killedTargets = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _leftoverTargets = new();
    private const int CropDeathDamageNumerator = 1;
    private const int CropDeathDamageDenominator = 5;

    public TimberbornCropBurnConsequenceSink(
        TimberbornBurnDamageService burnDamageService,
        ITimberbornCropBurnConsequenceApi consequenceApi,
        ITimberbornFireLogSink? logSink = null)
    {
        _burnDamageService = burnDamageService ?? throw new ArgumentNullException(nameof(burnDamageService));
        _consequenceApi = consequenceApi ?? throw new ArgumentNullException(nameof(consequenceApi));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornCropBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        if (decisions is null)
        {
            throw new ArgumentNullException(nameof(decisions));
        }

        CropCandidateHit[] cropHits = decisions
            .Select(CreateCropCandidateHit)
            .Where(static hit => hit.HasValue)
            .Select(static hit => hit!.Value)
            .ToArray();
        TimberbornBurnDamageTargetState[] consideredCropTargets = cropHits
            .Select(static hit => hit.State)
            .GroupBy(static state => state.TargetKey)
            .Select(static group => group.First())
            .ToArray();
        TimberbornCropBurnTargetOutcome[] outcomes = consideredCropTargets
            .Select(state => ApplyCropTargetConsequence(tick, state))
            .ToArray();

        TimberbornCropBurnConsequenceSummary summary = new(
            Tick: tick,
            ConsideredCropTargetCount: consideredCropTargets.Length,
            BurnableCropTargetCount: consideredCropTargets.Count(static state =>
                state.MaterialKind is not TimberbornBurnMaterialKind.NonBurnable &&
                state.DamageCapacity > 0 &&
                state.MissingResourceIds.Count == 0),
            YieldLost: outcomes.Sum(static outcome => outcome.YieldLost),
            KilledCropCount: outcomes.Count(static outcome => outcome.Killed),
            VisualStateUpdateCount: outcomes.Count(static outcome => outcome.VisualUpdated),
            DuplicateCellSuppressedCount: _burnDamageService.LastApplySummary.DuplicateCellSuppressedCount,
            UnmappedTargetCount: _burnDamageService.LastApplySummary.UnresolvedCellCount,
            UnknownHarvestResourceCount: consideredCropTargets.Count(static state => state.MissingResourceIds.Count > 0),
            NonBurnableCropTargetCount: consideredCropTargets.Count(static state =>
                state.MaterialKind is TimberbornBurnMaterialKind.NonBurnable ||
                (state.DamageCapacity == 0 && state.MissingResourceIds.Count == 0)),
            SkippedUnsafeApiCount: outcomes.Sum(static outcome => outcome.SkippedUnsafeApiCount));

        if (summary.ConsideredCropTargetCount > 0 ||
            summary.YieldLost > 0 ||
            summary.KilledCropCount > 0 ||
            summary.VisualStateUpdateCount > 0 ||
            summary.SkippedUnsafeApiCount > 0)
        {
            _logSink.Info(summary.ToLogToken());
        }

        return summary;
    }

    private CropCandidateHit? CreateCropCandidateHit(TimberbornFireCellDeltaDecision decision)
    {
        if (decision.OldFuel <= decision.NewFuel ||
            !_burnDamageService.TargetKeyByCellIndex.TryGetValue(decision.CellIndex, out TimberbornBurnDamageTargetKey targetKey) ||
            !_burnDamageService.States.TryGetValue(targetKey, out TimberbornBurnDamageTargetState state) ||
            !TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(state))
        {
            return null;
        }

        return new CropCandidateHit(decision.CellIndex, state);
    }

    private TimberbornCropBurnTargetOutcome ApplyCropTargetConsequence(
        uint tick,
        TimberbornBurnDamageTargetState state)
    {
        if (!TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(state))
        {
            return TimberbornCropBurnTargetOutcome.NoOp;
        }

        if (state.MissingResourceIds.Count > 0)
        {
            return TimberbornCropBurnTargetOutcome.UnknownResource;
        }

        bool burnable = state.MaterialKind is not TimberbornBurnMaterialKind.NonBurnable &&
            state.DamageCapacity > 0 &&
            state.AccountedResourceIds.Count > 0;
        if (!burnable)
        {
            return TimberbornCropBurnTargetOutcome.NoOp;
        }

        int initialYield = CalculateInitialYield(state);
        int targetYieldLost = CalculateAcceptedYieldLoss(state, initialYield);
        int alreadyAppliedYieldLoss = _appliedYieldLossByTarget.TryGetValue(state.TargetKey, out int appliedYieldLoss)
            ? appliedYieldLoss
            : 0;
        TimberbornCropBurnPlan plan = CreatePlan(state, initialYield, targetYieldLost, alreadyAppliedYieldLoss);

        return plan.DesiredState switch
        {
            TimberbornCropBurnDesiredState.BurnedLeftover => ApplyBurnedLeftoverConsequences(tick, state, plan),
            TimberbornCropBurnDesiredState.DeadBurned => ApplyDeadBurnedConsequences(tick, state, plan),
            _ => ApplyDamagedConsequences(tick, state, plan),
        };
    }

    private static TimberbornCropBurnPlan CreatePlan(
        TimberbornBurnDamageTargetState state,
        int initialYield,
        int targetYieldLost,
        int alreadyAppliedYieldLoss)
    {
        bool shouldMarkBurnedLeftover = ShouldMarkBurnedLeftover(state, initialYield, targetYieldLost);
        bool shouldKillCrop = shouldMarkBurnedLeftover || ShouldKillCrop(state);
        TimberbornCropBurnDesiredState desiredState = shouldMarkBurnedLeftover
            ? TimberbornCropBurnDesiredState.BurnedLeftover
            : shouldKillCrop
                ? TimberbornCropBurnDesiredState.DeadBurned
                : TimberbornCropBurnDesiredState.Damaged;

        return new TimberbornCropBurnPlan(
            desiredState,
            initialYield,
            targetYieldLost,
            Math.Max(0, targetYieldLost - alreadyAppliedYieldLoss));
    }

    private TimberbornCropBurnTargetOutcome ApplyDamagedConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan)
    {
        TimberbornCropBurnTargetOutcome dryingOutcome = ApplyDryingConsequences(tick, state, plan);
        TimberbornCropBurnTargetOutcome yieldOutcome = plan.IncrementalYieldLoss > 0
            ? ApplyYieldLoss(tick, state, plan)
            : TimberbornCropBurnTargetOutcome.BurnableNoChange;

        return TimberbornCropBurnTargetOutcome.Combine(dryingOutcome, yieldOutcome);
    }

    private TimberbornCropBurnTargetOutcome ApplyDeadBurnedConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan)
    {
        TimberbornCropBurnTargetOutcome yieldOutcome = plan.IncrementalYieldLoss > 0
            ? ApplyYieldLoss(tick, state, plan)
            : TimberbornCropBurnTargetOutcome.BurnableNoChange;
        TimberbornCropBurnTargetOutcome deathOutcome = ApplyDeathConsequences(
            tick,
            state,
            plan,
            markBurnedDeadVisual: true);

        return TimberbornCropBurnTargetOutcome.Combine(yieldOutcome, deathOutcome);
    }

    private TimberbornCropBurnTargetOutcome ApplyDryingConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan)
    {
        if (_driedTargets.Contains(state.TargetKey))
        {
            return TimberbornCropBurnTargetOutcome.BurnableNoChange;
        }

        TimberbornCropBurnConsequenceResult result = _consequenceApi.ApplyConsequence(CreateConsequence(
            tick,
            state,
            TimberbornCropBurnConsequenceKind.DryCrop,
            plan.TargetYieldLost,
            plan.RemainingYield));
        if (!result.MatchedCropTarget)
        {
            return result.SkippedUnsafeApi
                ? TimberbornCropBurnTargetOutcome.SkippedUnsafeApi
                : TimberbornCropBurnTargetOutcome.BurnableNoChange;
        }

        _driedTargets.Add(state.TargetKey);
        return TimberbornCropBurnTargetOutcome.BurnableNoChange;
    }

    private TimberbornCropBurnTargetOutcome ApplyYieldLoss(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan)
    {
        TimberbornCropBurnConsequenceResult result = _consequenceApi.ApplyConsequence(CreateConsequence(
            tick,
            state,
            TimberbornCropBurnConsequenceKind.ReduceYield,
            plan.IncrementalYieldLoss,
            plan.RemainingYield));
        if (!result.MatchedCropTarget)
        {
            return result.SkippedUnsafeApi
                ? TimberbornCropBurnTargetOutcome.SkippedUnsafeApi
                : TimberbornCropBurnTargetOutcome.BurnableNoChange;
        }

        _appliedYieldLossByTarget[state.TargetKey] = plan.TargetYieldLost;
        return new TimberbornCropBurnTargetOutcome(
            Burnable: true,
            YieldLost: result.YieldLost,
            Killed: false,
            VisualUpdated: false,
            SkippedUnknownResource: false,
            SkippedUnsafeApiCount: 0);
    }

    private TimberbornCropBurnTargetOutcome ApplyDeathConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan,
        bool markBurnedDeadVisual)
    {
        if (_killedTargets.Contains(state.TargetKey))
        {
            return TimberbornCropBurnTargetOutcome.BurnableNoChange;
        }

        TimberbornCropBurnConsequence killConsequence = CreateConsequence(
            tick,
            state,
            TimberbornCropBurnConsequenceKind.KillCrop,
            plan.TargetYieldLost,
            remainingYield: 0);
        TimberbornCropBurnConsequenceResult killResult = _consequenceApi.ApplyConsequence(killConsequence);
        TimberbornCropBurnConsequenceResult visualResult = markBurnedDeadVisual
            ? _consequenceApi.ApplyConsequence(killConsequence with
            {
                Kind = TimberbornCropBurnConsequenceKind.MarkBurnedVisual,
            })
            : new TimberbornCropBurnConsequenceResult(
                MatchedCropTarget: false,
                YieldLost: 0,
                KilledCrop: false,
                VisualStateUpdated: false,
                SkippedUnsafeApi: false);

        if (killResult.KilledCrop)
        {
            _killedTargets.Add(state.TargetKey);
        }

        return new TimberbornCropBurnTargetOutcome(
            Burnable: true,
            YieldLost: 0,
            Killed: killResult.KilledCrop,
            VisualUpdated: visualResult.VisualStateUpdated,
            SkippedUnknownResource: false,
            SkippedUnsafeApiCount: CountUnavailable(killResult) + CountUnavailable(visualResult));
    }

    private TimberbornCropBurnTargetOutcome ApplyBurnedLeftoverConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan)
    {
        if (_leftoverTargets.Contains(state.TargetKey))
        {
            return TimberbornCropBurnTargetOutcome.BurnableNoChange;
        }

        TimberbornCropBurnConsequenceResult result = _consequenceApi.ApplyConsequence(CreateConsequence(
            tick,
            state,
            TimberbornCropBurnConsequenceKind.MarkBurnedLeftover,
            plan.TargetYieldLost,
            remainingYield: 0));
        if (!result.VisualStateUpdated)
        {
            return result.SkippedUnsafeApi
                ? TimberbornCropBurnTargetOutcome.SkippedUnsafeApi
                : TimberbornCropBurnTargetOutcome.BurnableNoChange;
        }

        _leftoverTargets.Add(state.TargetKey);
        _killedTargets.Add(state.TargetKey);
        _appliedYieldLossByTarget[state.TargetKey] = plan.InitialYield;
        return new TimberbornCropBurnTargetOutcome(
            Burnable: true,
            YieldLost: plan.IncrementalYieldLoss,
            Killed: result.KilledCrop,
            VisualUpdated: true,
            SkippedUnknownResource: false,
            SkippedUnsafeApiCount: 0);
    }

    private TimberbornCropBurnConsequence CreateConsequence(
        uint tick,
        TimberbornBurnDamageTargetState state,
        TimberbornCropBurnConsequenceKind kind,
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

        return new TimberbornCropBurnConsequence(
            state.TargetKey,
            state.SpecId,
            state.TargetKind,
            kind,
            PrimaryYieldResourceId(state),
            yieldLost,
            remainingYield,
            tick,
            appliedEvent.SourceCellIndex,
            appliedEvent.DamageApplied,
            appliedEvent.DamageTaken,
            appliedEvent.DamageCapacity,
            state.IsFullyDamaged,
            state.AccountedResourceIds.ToArray(),
            state.MissingResourceIds.ToArray());
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

    private static bool ShouldKillCrop(TimberbornBurnDamageTargetState state)
    {
        return state.DamageCapacity > 0 &&
            state.DamageTaken * CropDeathDamageDenominator > state.DamageCapacity * CropDeathDamageNumerator;
    }

    private static bool ShouldMarkBurnedLeftover(
        TimberbornBurnDamageTargetState state,
        int initialYield,
        int targetYieldLost)
    {
        return initialYield > 0 &&
            (state.IsFullyDamaged || targetYieldLost >= initialYield);
    }

    private static int CountUnavailable(TimberbornCropBurnConsequenceResult result)
    {
        return !result.MatchedCropTarget && result.SkippedUnsafeApi ? 1 : 0;
    }

    private static string PrimaryYieldResourceId(TimberbornBurnDamageTargetState state)
    {
        return state.AccountedResourceIds
            .OrderBy(static resourceId => resourceId, StringComparer.Ordinal)
            .FirstOrDefault() ??
            "unknown";
    }

    private readonly record struct CropCandidateHit(
        int CellIndex,
        TimberbornBurnDamageTargetState State);

    private enum TimberbornCropBurnDesiredState
    {
        Damaged,
        DeadBurned,
        BurnedLeftover,
    }

    private readonly record struct TimberbornCropBurnPlan(
        TimberbornCropBurnDesiredState DesiredState,
        int InitialYield,
        int TargetYieldLost,
        int IncrementalYieldLoss)
    {
        public int RemainingYield => Math.Max(0, InitialYield - TargetYieldLost);
    }

    private readonly record struct TimberbornCropBurnTargetOutcome(
        bool Burnable,
        int YieldLost,
        bool Killed,
        bool VisualUpdated,
        bool SkippedUnknownResource,
        int SkippedUnsafeApiCount)
    {
        public static readonly TimberbornCropBurnTargetOutcome NoOp = new(
            Burnable: false,
            YieldLost: 0,
            Killed: false,
            VisualUpdated: false,
            SkippedUnknownResource: false,
            SkippedUnsafeApiCount: 0);

        public static readonly TimberbornCropBurnTargetOutcome BurnableNoChange = NoOp with
        {
            Burnable = true,
        };

        public static readonly TimberbornCropBurnTargetOutcome UnknownResource = NoOp with
        {
            SkippedUnknownResource = true,
        };

        public static readonly TimberbornCropBurnTargetOutcome SkippedUnsafeApi = BurnableNoChange with
        {
            SkippedUnsafeApiCount = 1,
        };

        public static TimberbornCropBurnTargetOutcome Combine(
            TimberbornCropBurnTargetOutcome first,
            TimberbornCropBurnTargetOutcome second)
        {
            return new TimberbornCropBurnTargetOutcome(
                Burnable: first.Burnable || second.Burnable,
                YieldLost: first.YieldLost + second.YieldLost,
                Killed: first.Killed || second.Killed,
                VisualUpdated: first.VisualUpdated || second.VisualUpdated,
                SkippedUnknownResource: first.SkippedUnknownResource || second.SkippedUnknownResource,
                SkippedUnsafeApiCount: first.SkippedUnsafeApiCount + second.SkippedUnsafeApiCount);
        }
    }
}

public sealed class UnavailableTimberbornCropBurnConsequenceApi : ITimberbornCropBurnConsequenceApi
{
    public static readonly UnavailableTimberbornCropBurnConsequenceApi Instance = new();

    private UnavailableTimberbornCropBurnConsequenceApi()
    {
    }

    public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence consequence)
    {
        throw new InvalidOperationException(
            $"Crop burn consequence API is unavailable for {consequence.TargetKey.StableId}.");
    }
}

public sealed class NullTimberbornCropBurnConsequenceSink : ITimberbornCropBurnConsequenceSink
{
    public static readonly NullTimberbornCropBurnConsequenceSink Instance = new();

    private NullTimberbornCropBurnConsequenceSink()
    {
    }

    public TimberbornCropBurnConsequenceSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornCropBurnConsequenceSummary.Empty with { Tick = tick };
    }
}
