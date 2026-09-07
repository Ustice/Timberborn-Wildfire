namespace Wildfire.Timberborn.Consequences;

public enum TimberbornTreeBurnConsequenceKind
{
    DryTree,
    ReduceYield,
    KillTree,
    MarkBurnedVisual,
    MarkBurnedLeftover,
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
    int DamageCapacity,
    Guid EntityId = default);

public enum TimberbornTreeBurnConsequenceStatus
{
    NotLive, Applied, AlreadySatisfied, Unavailable, Failed,
}

public readonly record struct TimberbornTreeBurnConsequenceResult(TimberbornTreeBurnConsequenceStatus Status, int YieldLost = 0)
{
    internal void ValidateReceipt()
    {
        if (Status < TimberbornTreeBurnConsequenceStatus.NotLive || Status > TimberbornTreeBurnConsequenceStatus.Failed)
            throw new InvalidOperationException("Native tree consequence returned an unknown status.");
        if (YieldLost < 0 || (Status != TimberbornTreeBurnConsequenceStatus.Applied && YieldLost != 0))
            throw new InvalidOperationException("Native tree consequence returned a contradictory yield receipt.");
    }
    public bool Applied => Status == TimberbornTreeBurnConsequenceStatus.Applied;
    public bool Failed => Status == TimberbornTreeBurnConsequenceStatus.Failed;
    public bool Satisfied => Applied || Status == TimberbornTreeBurnConsequenceStatus.AlreadySatisfied;
    public bool Unavailable => Status == TimberbornTreeBurnConsequenceStatus.Unavailable;
}

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
    int FailedConsequenceCount,
    int UnavailableConsequenceCount = 0)
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
        FailedConsequenceCount: 0);

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
            $"unavailable_consequences={UnavailableConsequenceCount}";
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

