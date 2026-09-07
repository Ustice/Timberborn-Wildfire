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
    IReadOnlyList<string> MissingResourceIds,
    Guid EntityId = default);

public enum TimberbornCropBurnConsequenceStatus { NotLive, Applied, AlreadySatisfied, Unavailable, Failed }

public readonly record struct TimberbornCropBurnConsequenceResult(
    TimberbornCropBurnConsequenceStatus Status,
    int YieldLost = 0,
    bool KilledCrop = false,
    bool VisualStateUpdated = false,
    bool Deleted = false,
    int DestroyedGoodCount = 0)
{
    public bool MatchedCropTarget => Status != TimberbornCropBurnConsequenceStatus.NotLive;
    public bool FailedConsequence => Status == TimberbornCropBurnConsequenceStatus.Failed;
    public bool Satisfied => Status is TimberbornCropBurnConsequenceStatus.Applied or TimberbornCropBurnConsequenceStatus.AlreadySatisfied;
    public bool Unavailable => Status == TimberbornCropBurnConsequenceStatus.Unavailable;
}

public interface ITimberbornLiveCropBurnConsequenceApi : ITimberbornCropBurnConsequenceApi
{
    bool IsLive(Guid entityId);
}

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
    int FailedConsequenceCount,
    int UnavailableConsequenceCount = 0,
    int DeletedCropCount = 0,
    int DestroyedGoodCount = 0)
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
        FailedConsequenceCount: 0);

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
            $"unavailable_consequences={UnavailableConsequenceCount} deleted_crops={DeletedCropCount} " +
            $"destroyed_goods={DestroyedGoodCount}";
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

