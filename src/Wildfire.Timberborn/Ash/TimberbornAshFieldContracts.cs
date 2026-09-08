using Wildfire.Core;

namespace Wildfire.Timberborn.Ash;

public enum TimberbornAshSourceKind
{
    Unknown,
    Crop,
    Tree,
    Resource,
    Structure,
    Storage,
    Infrastructure,
}

public readonly record struct TimberbornAshSourceEvent(
    int CellIndex,
    uint Tick,
    TimberbornAshSourceKind SourceKind,
    TimberbornBurnMaterialKind MaterialKind,
    int Strength,
    bool IsSourceContaminated,
    bool IsAffectedCellContaminated,
    IReadOnlyList<string> AccountedResourceIds);

public readonly record struct TimberbornAshFieldEntry(
    int CellIndex,
    WildfireAshQuality Quality,
    int Strength,
    TimberbornAshSourceKind SourceKind,
    uint CreatedTick,
    uint UpdatedTick,
    int PersistenceVersion,
    int CreatedDayNumber = 0,
    int UpdatedDayNumber = 0,
    bool IsActiveSource = false)
{
    public const int CurrentPersistenceVersion = 2;

    public bool GrantsGrowth => Quality == WildfireAshQuality.Fertile && Strength > 0;

    public float GrowthMultiplier()
    {
        return GrantsGrowth
            ? 1f + (0.10f * Math.Clamp(Strength, 0, TimberbornAshFieldService.MaxStrength) /
                TimberbornAshFieldService.MaxStrength)
            : 1f;
    }
}

public readonly record struct TimberbornAshGrowthBonusRequest(
    int CellIndex,
    float GrowthMultiplier,
    WildfireAshQuality Quality,
    int Strength);

public readonly record struct TimberbornAshGrowthApplicationResult(
    int CandidateGrowableCount,
    int AppliedGrowableCount,
    int FailedConsequenceCount,
    int UnsupportedGrowableCount);

public interface ITimberbornAshGrowthAdapter
{
    TimberbornAshGrowthApplicationResult ApplyGrowthBonuses(
        uint tick,
        IReadOnlyList<TimberbornAshGrowthBonusRequest> requests);
}

public sealed class UnavailableTimberbornAshGrowthAdapter : ITimberbornAshGrowthAdapter
{
    public static readonly UnavailableTimberbornAshGrowthAdapter Instance = new();

    private UnavailableTimberbornAshGrowthAdapter()
    {
    }

    public TimberbornAshGrowthApplicationResult ApplyGrowthBonuses(
        uint tick,
        IReadOnlyList<TimberbornAshGrowthBonusRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        throw new InvalidOperationException("Ash growth adapter is unavailable.");
    }
}

public sealed record TimberbornAshFieldSnapshot(
    int PersistenceVersion,
    IReadOnlyList<TimberbornAshFieldEntry> Entries);

public readonly record struct TimberbornAshFieldCollectionRemoval(
    int CellIndex,
    int StrengthRemoved,
    bool RemovedEntry);

public readonly record struct TimberbornAshFieldSummary(
    uint Tick,
    int SourceEventCount,
    int ContaminatedBurnSourceCellCount,
    int ContaminatedAffectedCellCount,
    int NewAshCellCount,
    int FertileAshCellCount,
    int SpentAshCellCount,
    int TaintedAshCellCount,
    int DecayedAshCellCount,
    int GrowthCandidateCellCount,
    int GrowthAppliedGrowableCount,
    int GrowthSkippedTaintedCellCount,
    int GrowthFailedConsequenceCount,
    int GrowthUnsupportedGrowableCount,
    int PersistenceSaveCount,
    int PersistenceLoadCount)
{
    public static readonly TimberbornAshFieldSummary Empty = new(
        Tick: 0,
        SourceEventCount: 0,
        ContaminatedBurnSourceCellCount: 0,
        ContaminatedAffectedCellCount: 0,
        NewAshCellCount: 0,
        FertileAshCellCount: 0,
        SpentAshCellCount: 0,
        TaintedAshCellCount: 0,
        DecayedAshCellCount: 0,
        GrowthCandidateCellCount: 0,
        GrowthAppliedGrowableCount: 0,
        GrowthSkippedTaintedCellCount: 0,
        GrowthFailedConsequenceCount: 0,
        GrowthUnsupportedGrowableCount: 0,
        PersistenceSaveCount: 0,
        PersistenceLoadCount: 0);

    public string ToLogToken()
    {
        return "wildfire_timberborn_ash_field_updated " +
            $"tick={Tick} " +
            $"ash_source_events={SourceEventCount} " +
            $"ash_contaminated_burn_sources={ContaminatedBurnSourceCellCount} " +
            $"ash_contaminated_affected_cells={ContaminatedAffectedCellCount} " +
            $"new_ash_cells={NewAshCellCount} " +
            $"fertile_ash_cells={FertileAshCellCount} " +
            $"spent_ash_cells={SpentAshCellCount} " +
            $"tainted_ash_cells={TaintedAshCellCount} " +
            $"decayed_ash_cells={DecayedAshCellCount} " +
            $"ash_growth_candidate_cells={GrowthCandidateCellCount} " +
            $"ash_growth_applied_growables={GrowthAppliedGrowableCount} " +
            $"ash_growth_skipped_tainted_cells={GrowthSkippedTaintedCellCount} " +
            $"ash_growth_unsupported_growables={GrowthUnsupportedGrowableCount} " +
            $"ash_persistence_saves={PersistenceSaveCount} " +
            $"ash_persistence_loads={PersistenceLoadCount}";
    }
}

