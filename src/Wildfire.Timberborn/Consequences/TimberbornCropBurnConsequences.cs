namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornCropBurnConsequenceSink : ITimberbornCropBurnConsequenceSink
{
    private readonly TimberbornBurnDamageService _burnDamageService;
    private readonly ITimberbornCropBurnConsequenceApi _consequenceApi;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<TimberbornBurnDamageTargetKey, int> _appliedYieldLossByTarget = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _driedTargets = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _killedTargets = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _burnedVisualTargets = new();
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
        return ApplyCropHits(tick, cropHits, cropHits.Length - cropHits.Select(hit => hit.State.TargetKey).Distinct().Count(),
            _burnDamageService.LastApplySummary.UnresolvedCellCount);
    }

    internal TimberbornCropBurnConsequenceSummary ApplyOwnedConsequences(uint tick, IReadOnlyList<TimberbornOwnedBurnDecision> decisions)
    {
        if (decisions.Any(item => item.Family != NativeBurnTargetFamily.Crop))
            throw new ArgumentException("Owned crop sink accepts only canonical crop origins.");
        var hits = decisions.Select(item => CreateCropCandidateHit(item.Decision, item.TargetKey))
            .Where(item => item.HasValue).Select(item => item!.Value).ToArray();
        return ApplyCropHits(tick, hits, hits.Length - hits.Select(hit => hit.State.TargetKey).Distinct().Count(), 0);
    }

    private TimberbornCropBurnConsequenceSummary ApplyCropHits(uint tick, CropCandidateHit[] cropHits,
        int duplicateCells, int unmappedTargets)
    {
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
            DuplicateCellSuppressedCount: duplicateCells,
            UnmappedTargetCount: unmappedTargets,
            UnknownHarvestResourceCount: consideredCropTargets.Count(static state => state.MissingResourceIds.Count > 0),
            NonBurnableCropTargetCount: consideredCropTargets.Count(static state =>
                state.MaterialKind is TimberbornBurnMaterialKind.NonBurnable ||
                (state.DamageCapacity == 0 && state.MissingResourceIds.Count == 0)),
            FailedConsequenceCount: outcomes.Sum(static outcome => outcome.FailedConsequenceCount),
            UnavailableConsequenceCount: outcomes.Sum(static outcome => outcome.UnavailableConsequenceCount),
            DeletedCropCount: outcomes.Count(outcome => outcome.Deleted),
            DestroyedGoodCount: outcomes.Sum(outcome => outcome.DestroyedGoodCount));

        if (summary.ConsideredCropTargetCount > 0 ||
            summary.YieldLost > 0 ||
            summary.KilledCropCount > 0 ||
            summary.VisualStateUpdateCount > 0 ||
            summary.FailedConsequenceCount > 0)
        {
            _logSink.Info(summary.ToLogToken());
        }

        return summary;
    }

    private CropCandidateHit? CreateCropCandidateHit(TimberbornFireCellDeltaDecision decision)
    {
        return _burnDamageService.TargetKeyByCellIndex.TryGetValue(decision.CellIndex, out var key)
            ? CreateCropCandidateHit(decision, key) : null;
    }

    private CropCandidateHit? CreateCropCandidateHit(TimberbornFireCellDeltaDecision decision, TimberbornBurnDamageTargetKey targetKey)
    {
        if (decision.OldFuel <= decision.NewFuel ||
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

    private TimberbornCropBurnTargetOutcome ApplyDryingConsequences(uint tick, TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan)
    {
        if (_driedTargets.Contains(state.TargetKey)) return TimberbornCropBurnTargetOutcome.BurnableNoChange;
        var result = _consequenceApi.ApplyConsequence(CreateConsequence(tick, state, TimberbornCropBurnConsequenceKind.DryCrop,
            plan.TargetYieldLost, plan.RemainingYield));
        ThrowIfFailed(result, TimberbornCropBurnConsequenceKind.DryCrop, state);
        if (result.Satisfied) _driedTargets.Add(state.TargetKey);
        return FromResult(result);
    }

    private TimberbornCropBurnTargetOutcome ApplyYieldLoss(uint tick, TimberbornBurnDamageTargetState state, TimberbornCropBurnPlan plan)
    {
        var result = _consequenceApi.ApplyConsequence(CreateConsequence(tick, state, TimberbornCropBurnConsequenceKind.ReduceYield,
            plan.IncrementalYieldLoss, plan.RemainingYield));
        ThrowIfFailed(result, TimberbornCropBurnConsequenceKind.ReduceYield, state);
        if (result.YieldLost < 0 || result.YieldLost > plan.IncrementalYieldLoss)
            throw new InvalidOperationException("Native crop yield receipt exceeds the requested loss.");
        RecordYieldLoss(state.TargetKey, result.YieldLost);
        return FromResult(result);
    }

    private TimberbornCropBurnTargetOutcome ApplyDeathConsequences(uint tick, TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan, bool markBurnedDeadVisual)
    {
        var consequence = CreateConsequence(tick, state, TimberbornCropBurnConsequenceKind.KillCrop, plan.TargetYieldLost, 0);
        var kill = _killedTargets.Contains(state.TargetKey) ? new TimberbornCropBurnConsequenceResult(TimberbornCropBurnConsequenceStatus.AlreadySatisfied)
            : _consequenceApi.ApplyConsequence(consequence);
        ThrowIfFailed(kill, consequence.Kind, state);
        if (kill.Satisfied) _killedTargets.Add(state.TargetKey);
        RecordYieldLoss(state.TargetKey, kill.YieldLost);
        TimberbornCropBurnConsequenceResult visual = default;
        if (markBurnedDeadVisual && !_burnedVisualTargets.Contains(state.TargetKey))
        {
            visual = _consequenceApi.ApplyConsequence(consequence with { Kind = TimberbornCropBurnConsequenceKind.MarkBurnedVisual });
            ThrowIfFailed(visual, TimberbornCropBurnConsequenceKind.MarkBurnedVisual, state);
            if (visual.Satisfied) _burnedVisualTargets.Add(state.TargetKey);
        }
        return TimberbornCropBurnTargetOutcome.Combine(FromResult(kill), FromResult(visual));
    }

    private TimberbornCropBurnTargetOutcome ApplyBurnedLeftoverConsequences(uint tick, TimberbornBurnDamageTargetState state,
        TimberbornCropBurnPlan plan)
    {
        if (_leftoverTargets.Contains(state.TargetKey)) return TimberbornCropBurnTargetOutcome.BurnableNoChange;
        var result = _consequenceApi.ApplyConsequence(CreateConsequence(tick, state, TimberbornCropBurnConsequenceKind.MarkBurnedLeftover,
            plan.TargetYieldLost, 0));
        ThrowIfFailed(result, TimberbornCropBurnConsequenceKind.MarkBurnedLeftover, state);
        if (result.Satisfied) { _leftoverTargets.Add(state.TargetKey); _killedTargets.Add(state.TargetKey); }
        RecordYieldLoss(state.TargetKey, result.YieldLost);
        return FromResult(result);
    }

    private void RecordYieldLoss(TimberbornBurnDamageTargetKey key, int amount)
    {
        if (amount < 0) throw new InvalidOperationException("Negative native crop yield receipt.");
        if (amount == 0) return;
        _appliedYieldLossByTarget.TryGetValue(key, out int previous);
        _appliedYieldLossByTarget[key] = checked(previous + amount);
    }
    private static TimberbornCropBurnTargetOutcome FromResult(TimberbornCropBurnConsequenceResult result) => new(
        true, result.YieldLost, result.KilledCrop, result.VisualStateUpdated, false, 0,
        result.Unavailable ? 1 : 0, result.Deleted, result.DestroyedGoodCount);

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
            state.MissingResourceIds.ToArray(),
            EntityId: ReadCropIdentity(state.TargetKey));
    }

    private static Guid ReadCropIdentity(TimberbornBurnDamageTargetKey key)
    {
        if (TimberbornBurnDamageIdentity.TryGetEntity(key.StableId, NativeBurnTargetFamily.Crop, out Guid id) ||
            TimberbornBurnDamageIdentity.TryGetEntity(key.StableId, NativeBurnTargetFamily.SelectedCrop, out id)) return id;
        return Guid.Empty; // Portable fake targets never reach the native API, which rejects absent identity.
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

    private static void ThrowIfFailed(
        TimberbornCropBurnConsequenceResult result,
        TimberbornCropBurnConsequenceKind kind,
        TimberbornBurnDamageTargetState state)
    {
        result.ValidateReceipt();
        if (!result.FailedConsequence)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Crop burn consequence failed for {state.TargetKey.StableId} ({state.SpecId}, {kind}).");
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
        bool IsUnknownResource,
        int FailedConsequenceCount,
        int UnavailableConsequenceCount = 0,
        bool Deleted = false,
        int DestroyedGoodCount = 0)
    {
        public static readonly TimberbornCropBurnTargetOutcome NoOp = new(
            Burnable: false,
            YieldLost: 0,
            Killed: false,
            VisualUpdated: false,
            IsUnknownResource: false,
            FailedConsequenceCount: 0);

        public static readonly TimberbornCropBurnTargetOutcome BurnableNoChange = NoOp with
        {
            Burnable = true,
        };

        public static readonly TimberbornCropBurnTargetOutcome UnknownResource = NoOp with
        {
            IsUnknownResource = true,
        };

        public static readonly TimberbornCropBurnTargetOutcome FailedConsequence = BurnableNoChange with
        {
            FailedConsequenceCount = 1,
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
                IsUnknownResource: first.IsUnknownResource || second.IsUnknownResource,
                FailedConsequenceCount: first.FailedConsequenceCount + second.FailedConsequenceCount,
                UnavailableConsequenceCount: first.UnavailableConsequenceCount + second.UnavailableConsequenceCount,
                Deleted: first.Deleted || second.Deleted,
                DestroyedGoodCount: first.DestroyedGoodCount + second.DestroyedGoodCount);
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
