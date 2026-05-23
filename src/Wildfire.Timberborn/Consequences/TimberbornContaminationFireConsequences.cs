using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornContaminationFireConsequenceSummary(
    int ContaminatedAffectedMapCellCount,
    int BadwaterWaterLikeMapCellCount,
    int ContaminatedWaterLikeMapCellCount,
    int BadwaterSuppressionInputCellCount,
    int ContaminatedWaterSuppressionInputCellCount,
    int WaterSuppressionInputSafeUnavailableCount,
    int NativeDecontaminationAttemptCount,
    int SkippedUnsafeContaminationApiCount)
{
    public static readonly TimberbornContaminationFireConsequenceSummary Empty = new(
        ContaminatedAffectedMapCellCount: 0,
        BadwaterWaterLikeMapCellCount: 0,
        ContaminatedWaterLikeMapCellCount: 0,
        BadwaterSuppressionInputCellCount: 0,
        ContaminatedWaterSuppressionInputCellCount: 0,
        WaterSuppressionInputSafeUnavailableCount: 0,
        NativeDecontaminationAttemptCount: 0,
        SkippedUnsafeContaminationApiCount: 0);

    public string ToLogToken()
    {
        return "wildfire_timberborn_contamination_fire_consequences " +
            $"contaminated_affected_map_cells={ContaminatedAffectedMapCellCount} " +
            $"badwater_water_like_map_cells={BadwaterWaterLikeMapCellCount} " +
            $"contaminated_water_like_map_cells={ContaminatedWaterLikeMapCellCount} " +
            $"badwater_suppression_inputs={BadwaterSuppressionInputCellCount} " +
            $"contaminated_water_suppression_inputs={ContaminatedWaterSuppressionInputCellCount} " +
            $"water_suppression_input_safe_unavailable={WaterSuppressionInputSafeUnavailableCount} " +
            $"native_decontamination_attempts={NativeDecontaminationAttemptCount} " +
            $"skipped_unsafe_contamination_apis={SkippedUnsafeContaminationApiCount}";
    }
}

public static class TimberbornContaminationFireConsequenceTelemetry
{
    public static TimberbornContaminationFireConsequenceSummary Summarize(
        FireGrid grid,
        IEnumerable<TimberbornCellSource> sources,
        IReadOnlyList<TimberbornImportedFieldTarget> importedTargets)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        if (importedTargets is null)
        {
            throw new ArgumentNullException(nameof(importedTargets));
        }

        TimberbornCellSource[] sourceValues = sources.ToArray();
        WaterSuppressionInput[] waterInputs = sourceValues
            .Where(static source => source.Water is { Water: > 0 })
            .Select(source => new WaterSuppressionInput(
                ToIndex(grid, source),
                source.Water!.Value.IsContaminated,
                source.Water.Value.IsBadwater))
            .GroupBy(static input => input.CellIndex)
            .Select(static group => new WaterSuppressionInput(
                group.Key,
                group.Any(static input => input.IsContaminated),
                group.Any(static input => input.IsBadwater)))
            .ToArray();
        TimberbornImportedFieldTarget[] burnableTargets = importedTargets
            .Where(IsBurnableImportedTarget)
            .ToArray();

        int badwaterWaterLikeMapCells = waterInputs.Count(static input => input.IsBadwater);
        int contaminatedWaterLikeMapCells = waterInputs.Count(static input => input.IsContaminated);
        int safeUnavailableInputs = Math.Max(badwaterWaterLikeMapCells, contaminatedWaterLikeMapCells);

        return new TimberbornContaminationFireConsequenceSummary(
            ContaminatedAffectedMapCellCount: burnableTargets.Count(static target => target.SoilContamination > 0),
            BadwaterWaterLikeMapCellCount: badwaterWaterLikeMapCells,
            ContaminatedWaterLikeMapCellCount: contaminatedWaterLikeMapCells,
            BadwaterSuppressionInputCellCount: 0,
            ContaminatedWaterSuppressionInputCellCount: 0,
            WaterSuppressionInputSafeUnavailableCount: safeUnavailableInputs,
            NativeDecontaminationAttemptCount: 0,
            SkippedUnsafeContaminationApiCount: safeUnavailableInputs);
    }

    private static int ToIndex(FireGrid grid, TimberbornCellSource source)
    {
        return grid.ToIndex(source.Coordinates.X, source.Coordinates.Y, source.Coordinates.Z);
    }

    private static bool IsBurnableImportedTarget(TimberbornImportedFieldTarget target)
    {
        return PackedCell.Terrain(target.InitialCell) == 1 &&
            PackedCell.Fuel(target.InitialCell) > 0 &&
            PackedCell.Flammability(target.InitialCell) > 0 &&
            target.MaterialClass is WildfireMaterialClass.Tree or
                WildfireMaterialClass.Vegetation or
                WildfireMaterialClass.Crop or
                WildfireMaterialClass.Building or
                WildfireMaterialClass.Storage;
    }

    private readonly record struct WaterSuppressionInput(
        int CellIndex,
        bool IsContaminated,
        bool IsBadwater);
}
