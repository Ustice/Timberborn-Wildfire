namespace Wildfire.Timberborn.Consequences;

public sealed class TimberbornTreeBurnConsequenceSink : ITimberbornTreeBurnConsequenceSink
{
    private readonly TimberbornBurnDamageService _burnDamageService;
    private readonly ITimberbornTreeBurnConsequenceApi _consequenceApi;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<TimberbornBurnDamageTargetKey, int> _appliedYieldLossByTarget = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _driedTargets = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _killedTargets = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _burnedVisualTargets = new();
    private readonly HashSet<TimberbornBurnDamageTargetKey> _leftoverTargets = new();
    private const int BurnedDeadFuelThreshold = 12;
    private const int BurnedLeftoverFuelThreshold = 0;
    private const int DryWaterThreshold = 0;

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
        return ApplyTreeHits(tick, treeHits, _burnDamageService.LastApplySummary.DuplicateCellSuppressedCount,
            _burnDamageService.LastApplySummary.UnresolvedCellCount);
    }

    internal TimberbornTreeBurnConsequenceSummary ApplyOwnedConsequences(
        uint tick, IReadOnlyList<TimberbornOwnedBurnDecision> decisions)
    {
        var hits = decisions.Select(item => CreateTreeCandidateHit(item.Decision, item.TargetKey))
            .Where(hit => hit.HasValue).Select(hit => hit!.Value).ToArray();
        return ApplyTreeHits(tick, hits, hits.Length - hits.Select(hit => hit.State.TargetKey).Distinct().Count(), 0);
    }

    private TimberbornTreeBurnConsequenceSummary ApplyTreeHits(uint tick, TreeCandidateHit[] treeHits,
        int duplicateCells, int unmappedTargets)
    {
        TreeCandidateTarget[] consideredTreeTargets = treeHits
            .GroupBy(static hit => hit.State.TargetKey)
            .Select(static group => new TreeCandidateTarget(
                group.First().State,
                group.Min(static hit => hit.CurrentFuel),
                group.Min(static hit => hit.CurrentWater)))
            .ToArray();
        TimberbornTreeBurnTargetOutcome[] outcomes = consideredTreeTargets
            .Select(target => ApplyTreeTargetConsequence(
                tick,
                target.State,
                target.CurrentFuel,
                target.CurrentWater))
            .ToArray();
        TimberbornBurnDamageTargetState[] consideredTreeTargetStates = consideredTreeTargets
            .Select(static target => target.State)
            .ToArray();

        TimberbornTreeBurnConsequenceSummary summary = new(
            Tick: tick,
            ConsideredTreeTargetCount: consideredTreeTargets.Length,
            BurnableTreeTargetCount: consideredTreeTargetStates.Count(static state =>
                state.MaterialKind is not TimberbornBurnMaterialKind.NonBurnable &&
                state.DamageCapacity > 0 &&
                state.MissingResourceIds.Count == 0),
            YieldLost: outcomes.Sum(static outcome => outcome.YieldLost),
            KilledTreeCount: outcomes.Count(static outcome => outcome.Killed),
            VisualStateUpdateCount: outcomes.Count(static outcome => outcome.VisualUpdated),
            DuplicateCellSuppressedCount: duplicateCells,
            UnmappedTargetCount: unmappedTargets,
            UnknownCuttableResourceCount: consideredTreeTargetStates.Count(static state => state.MissingResourceIds.Count > 0),
            NonBurnableTreeTargetCount: consideredTreeTargetStates.Count(static state =>
                state.MaterialKind is TimberbornBurnMaterialKind.NonBurnable ||
                (state.DamageCapacity == 0 && state.MissingResourceIds.Count == 0)),
            FailedConsequenceCount: outcomes.Sum(static outcome => outcome.FailedConsequenceCount),
            UnavailableConsequenceCount: outcomes.Sum(static outcome => outcome.UnavailableConsequenceCount));

        if (summary.ConsideredTreeTargetCount > 0 ||
            summary.YieldLost > 0 ||
            summary.KilledTreeCount > 0 ||
            summary.VisualStateUpdateCount > 0 ||
            summary.FailedConsequenceCount > 0)
        {
            _logSink.Info(summary.ToLogToken());
        }

        return summary;
    }

    private TreeCandidateHit? CreateTreeCandidateHit(TimberbornFireCellDeltaDecision decision)
    {
        return _burnDamageService.TargetKeyByCellIndex.TryGetValue(decision.CellIndex, out var targetKey)
            ? CreateTreeCandidateHit(decision, targetKey) : null;
    }

    private TreeCandidateHit? CreateTreeCandidateHit(TimberbornFireCellDeltaDecision decision,
        TimberbornBurnDamageTargetKey targetKey)
    {
        bool fuelConsumed = decision.OldFuel > decision.NewFuel;
        bool moistureEvaporated = decision.OldWater > decision.NewWater;
        if ((!fuelConsumed && !moistureEvaporated) ||
            !_burnDamageService.States.TryGetValue(targetKey, out TimberbornBurnDamageTargetState state) ||
            !TimberbornTreeBurnTargetClassifier.IsTreeOrCuttable(state))
        {
            return null;
        }

        return new TreeCandidateHit(decision.CellIndex, state, decision.NewFuel, decision.NewWater);
    }

    private TimberbornTreeBurnTargetOutcome ApplyTreeTargetConsequence(
        uint tick,
        TimberbornBurnDamageTargetState state,
        int currentFuel,
        int currentWater)
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
        bool shouldMarkBurnedLeftover = ShouldMarkBurnedLeftover(currentFuel);
        bool shouldKillTree = shouldMarkBurnedLeftover || ShouldMarkBurnedDead(currentFuel);
        bool shouldDryTree = ShouldDryTree(currentFuel, currentWater);
        int displayedTargetYieldLost = shouldMarkBurnedLeftover ? initialYield : targetYieldLost;
        TimberbornTreeBurnTargetOutcome dryingOutcome =
            !shouldDryTree || shouldKillTree || _killedTargets.Contains(state.TargetKey)
                ? TimberbornTreeBurnTargetOutcome.BurnableNoChange
                : ApplyDryingConsequences(tick, state, initialYield, targetYieldLost);
        TimberbornTreeBurnTargetOutcome yieldOutcome = incrementalYieldLoss > 0
            ? ApplyYieldLoss(tick, state, incrementalYieldLoss, initialYield, targetYieldLost)
            : TimberbornTreeBurnTargetOutcome.BurnableNoChange;
        TimberbornTreeBurnTargetOutcome deathOutcome =
            shouldKillTree
                ? ApplyDeathConsequences(
                    tick,
                    state,
                    initialYield,
                    displayedTargetYieldLost,
                    markBurnedDeadVisual: !shouldMarkBurnedLeftover)
                : TimberbornTreeBurnTargetOutcome.BurnableNoChange;
        TimberbornTreeBurnTargetOutcome leftoverOutcome =
            shouldMarkBurnedLeftover
                ? ApplyBurnedLeftoverConsequences(tick, state, initialYield, displayedTargetYieldLost)
                : TimberbornTreeBurnTargetOutcome.BurnableNoChange;

        return TimberbornTreeBurnTargetOutcome.Combine(
            TimberbornTreeBurnTargetOutcome.Combine(
                TimberbornTreeBurnTargetOutcome.Combine(dryingOutcome, yieldOutcome),
                deathOutcome),
            leftoverOutcome);
    }

    private TimberbornTreeBurnTargetOutcome ApplyDryingConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        int initialYield,
        int targetYieldLost)
    {
        if (_driedTargets.Contains(state.TargetKey))
        {
            return TimberbornTreeBurnTargetOutcome.BurnableNoChange;
        }

        TimberbornTreeBurnConsequenceResult result = _consequenceApi.ApplyConsequence(CreateConsequence(
            tick,
            state,
            TimberbornTreeBurnConsequenceKind.DryTree,
            targetYieldLost,
            Math.Max(0, initialYield - targetYieldLost)));

        if (!result.Satisfied)
        {
            ThrowIfFailed(result, TimberbornTreeBurnConsequenceKind.DryTree, state);
            return Unapplied(result);
        }

        _driedTargets.Add(state.TargetKey);
        return TimberbornTreeBurnTargetOutcome.BurnableNoChange;
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
            ThrowIfFailed(result, TimberbornTreeBurnConsequenceKind.ReduceYield, state);
            return Unapplied(result);
        }

        _appliedYieldLossByTarget[state.TargetKey] = targetYieldLost;
        return new TimberbornTreeBurnTargetOutcome(
            Burnable: true,
            YieldLost: incrementalYieldLoss,
            Killed: false,
            VisualUpdated: false,
            IsUnknownResource: false,
            FailedConsequenceCount: 0);
    }

    private TimberbornTreeBurnTargetOutcome ApplyDeathConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        int initialYield,
        int targetYieldLost,
        bool markBurnedDeadVisual)
    {
        TimberbornTreeBurnConsequence killConsequence = CreateConsequence(
            tick,
            state,
            TimberbornTreeBurnConsequenceKind.KillTree,
            targetYieldLost,
            remainingYield: 0);
        TimberbornTreeBurnConsequenceResult killResult = _killedTargets.Contains(state.TargetKey)
            ? new(TimberbornTreeBurnConsequenceStatus.AlreadySatisfied)
            : _consequenceApi.ApplyConsequence(killConsequence);
        ThrowIfFailed(killResult, TimberbornTreeBurnConsequenceKind.KillTree, state);
        TimberbornTreeBurnConsequenceResult visualResult = markBurnedDeadVisual && !_burnedVisualTargets.Contains(state.TargetKey)
            ? _consequenceApi.ApplyConsequence(killConsequence with
            {
                Kind = TimberbornTreeBurnConsequenceKind.MarkBurnedVisual,
            })
            : new TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus.NotLive);

        if (killResult.Satisfied)
        {
            _killedTargets.Add(state.TargetKey);
        }
        ThrowIfFailed(visualResult, TimberbornTreeBurnConsequenceKind.MarkBurnedVisual, state);
        if (visualResult.Satisfied) _burnedVisualTargets.Add(state.TargetKey);

        return new TimberbornTreeBurnTargetOutcome(
            Burnable: true,
            YieldLost: 0,
            Killed: killResult.Applied,
            VisualUpdated: visualResult.Applied,
            IsUnknownResource: false,
            FailedConsequenceCount: 0,
            UnavailableConsequenceCount: (killResult.Unavailable ? 1 : 0) + (visualResult.Unavailable ? 1 : 0));
    }

    private TimberbornTreeBurnTargetOutcome ApplyBurnedLeftoverConsequences(
        uint tick,
        TimberbornBurnDamageTargetState state,
        int initialYield,
        int targetYieldLost)
    {
        if (_leftoverTargets.Contains(state.TargetKey))
        {
            return TimberbornTreeBurnTargetOutcome.BurnableNoChange;
        }

        TimberbornTreeBurnConsequenceResult result = _consequenceApi.ApplyConsequence(CreateConsequence(
            tick,
            state,
            TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover,
            targetYieldLost,
            remainingYield: 0));

        if (!result.Satisfied)
        {
            ThrowIfFailed(result, TimberbornTreeBurnConsequenceKind.MarkBurnedLeftover, state);
            return Unapplied(result);
        }

        _leftoverTargets.Add(state.TargetKey);
        return new TimberbornTreeBurnTargetOutcome(
            Burnable: true,
            YieldLost: 0,
            Killed: false,
            VisualUpdated: result.Applied,
            IsUnknownResource: false,
            FailedConsequenceCount: 0);
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
            appliedEvent.DamageCapacity,
            EntityId: TimberbornBurnDamageIdentity.TryGetEntity(state.TargetKey.StableId, NativeBurnTargetFamily.Tree,
                out Guid entityId) ? entityId : Guid.Empty);
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

    private static bool ShouldDryTree(int currentFuel, int currentWater)
    {
        return currentWater <= DryWaterThreshold && currentFuel >= BurnedDeadFuelThreshold;
    }

    private static bool ShouldMarkBurnedDead(int currentFuel)
    {
        return currentFuel < BurnedDeadFuelThreshold;
    }

    private static bool ShouldMarkBurnedLeftover(int currentFuel)
    {
        return currentFuel <= BurnedLeftoverFuelThreshold;
    }

    private static TimberbornTreeBurnTargetOutcome Unapplied(TimberbornTreeBurnConsequenceResult result) =>
        TimberbornTreeBurnTargetOutcome.BurnableNoChange with { UnavailableConsequenceCount = result.Unavailable ? 1 : 0 };

    private static void ThrowIfFailed(
        TimberbornTreeBurnConsequenceResult result,
        TimberbornTreeBurnConsequenceKind kind,
        TimberbornBurnDamageTargetState state)
    {
        if (!result.Failed)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Tree burn consequence failed for {state.TargetKey.StableId} ({state.SpecId}, {kind}).");
    }

    private readonly record struct TreeCandidateHit(
        int CellIndex,
        TimberbornBurnDamageTargetState State,
        int CurrentFuel,
        int CurrentWater);

    private readonly record struct TreeCandidateTarget(
        TimberbornBurnDamageTargetState State,
        int CurrentFuel,
        int CurrentWater);

    private readonly record struct TimberbornTreeBurnTargetOutcome(
        bool Burnable,
        int YieldLost,
        bool Killed,
        bool VisualUpdated,
        bool IsUnknownResource,
        int FailedConsequenceCount,
        int UnavailableConsequenceCount = 0)
    {
        public static readonly TimberbornTreeBurnTargetOutcome NoOp = new(
            Burnable: false,
            YieldLost: 0,
            Killed: false,
            VisualUpdated: false,
            IsUnknownResource: false,
            FailedConsequenceCount: 0);

        public static readonly TimberbornTreeBurnTargetOutcome BurnableNoChange = NoOp with
        {
            Burnable = true,
        };

        public static readonly TimberbornTreeBurnTargetOutcome UnknownResource = NoOp with
        {
            IsUnknownResource = true,
        };

        public static readonly TimberbornTreeBurnTargetOutcome FailedConsequence = BurnableNoChange with
        {
            FailedConsequenceCount = 1,
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
                IsUnknownResource: first.IsUnknownResource || second.IsUnknownResource,
                FailedConsequenceCount: first.FailedConsequenceCount + second.FailedConsequenceCount,
                UnavailableConsequenceCount: first.UnavailableConsequenceCount + second.UnavailableConsequenceCount);
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
        throw new InvalidOperationException(
            $"Tree burn consequence API is unavailable for {consequence.TargetKey.StableId}.");
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
