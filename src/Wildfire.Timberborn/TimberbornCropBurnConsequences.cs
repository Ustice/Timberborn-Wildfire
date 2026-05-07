namespace Wildfire.Timberborn;

public readonly record struct TimberbornCropBurnConsequence(
    TimberbornBurnDamageTargetKey TargetKey,
    string SpecId,
    TimberbornBurnDamageTargetKind TargetKind,
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
            $"non_burnable_crop_targets={NonBurnableCropTargetCount} " +
            $"skipped_unsafe_apis={SkippedUnsafeApiCount}";
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
        TimberbornCropBurnConsequenceResult[] results = _burnDamageService.LastAppliedEventsByTargetKey.Values
            .Where(appliedEvent => appliedEvent.Tick == tick)
            .Select(CreateConsequence)
            .Where(consequence => consequence.HasValue)
            .Select(consequence => _consequenceApi.ApplyConsequence(consequence!.Value))
            .ToArray();

        TimberbornCropBurnConsequenceSummary summary = new(
            Tick: tick,
            ConsideredCropTargetCount: consideredCropTargets.Length,
            BurnableCropTargetCount: consideredCropTargets.Count(static state => state.DamageCapacity > 0),
            YieldLost: results.Sum(static result => result.YieldLost),
            KilledCropCount: results.Count(static result => result.KilledCrop),
            VisualStateUpdateCount: results.Count(static result => result.VisualStateUpdated),
            DuplicateCellSuppressedCount: _burnDamageService.LastApplySummary.DuplicateCellSuppressedCount,
            UnmappedTargetCount: _burnDamageService.LastApplySummary.UnresolvedCellCount,
            UnknownHarvestResourceCount: consideredCropTargets.Count(static state => state.MissingResourceIds.Count > 0),
            NonBurnableCropTargetCount: consideredCropTargets.Count(static state =>
                state.DamageCapacity == 0 && state.MissingResourceIds.Count == 0),
            SkippedUnsafeApiCount: results.Count(static result => result.SkippedUnsafeApi));
        _logSink.Info(summary.ToLogToken());

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

    private TimberbornCropBurnConsequence? CreateConsequence(TimberbornBurnDamageAppliedEvent appliedEvent)
    {
        if (!_burnDamageService.States.TryGetValue(appliedEvent.TargetKey, out TimberbornBurnDamageTargetState state) ||
            !TimberbornCropBurnTargetClassifier.IsCropOrHarvestable(state))
        {
            return null;
        }

        return new TimberbornCropBurnConsequence(
            appliedEvent.TargetKey,
            appliedEvent.SpecId,
            state.TargetKind,
            appliedEvent.Tick,
            appliedEvent.SourceCellIndex,
            appliedEvent.DamageApplied,
            appliedEvent.DamageTaken,
            appliedEvent.DamageCapacity,
            state.IsFullyDamaged,
            state.AccountedResourceIds.ToArray(),
            state.MissingResourceIds.ToArray());
    }

    private readonly record struct CropCandidateHit(
        int CellIndex,
        TimberbornBurnDamageTargetState State);
}

public sealed class UnavailableTimberbornCropBurnConsequenceApi : ITimberbornCropBurnConsequenceApi
{
    public static readonly UnavailableTimberbornCropBurnConsequenceApi Instance = new();

    private UnavailableTimberbornCropBurnConsequenceApi()
    {
    }

    public TimberbornCropBurnConsequenceResult ApplyConsequence(TimberbornCropBurnConsequence consequence)
    {
        return new TimberbornCropBurnConsequenceResult(
            MatchedCropTarget: true,
            YieldLost: 0,
            KilledCrop: false,
            VisualStateUpdated: false,
            SkippedUnsafeApi: true);
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
