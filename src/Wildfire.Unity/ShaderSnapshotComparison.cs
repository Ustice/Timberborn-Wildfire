namespace Wildfire.Unity;

public sealed record ShaderSnapshotComparison(bool Matches, string[] Differences)
{
    public static ShaderSnapshotComparison Create(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        int maxDifferences = 8)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDifferences);

        List<string> differences = [];
        AddHeaderDifferences(expected, actual, differences, maxDifferences);
        AddFinalCellDifferences(expected, actual, differences, maxDifferences);
        AddFinalAtmosphericFieldDifferences(expected, actual, differences, maxDifferences);
        AddFinalMaterialFieldDifferences(expected, actual, differences, maxDifferences);
        AddOptionalWords("finalTargetIds", expected.FinalTargetIds, actual.FinalTargetIds, differences, maxDifferences);
        AddOptionalWords("finalSlotIds", expected.FinalSlotIds, actual.FinalSlotIds, differences, maxDifferences);
        AddTickDifferences(expected, actual, differences, maxDifferences);
        AddVisualDifferences(expected, actual, differences, maxDifferences);

        return new ShaderSnapshotComparison(differences.Count == 0, differences.ToArray());
    }

    private static void AddHeaderDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "scenario", expected.Scenario, actual.Scenario, maxDifferences);
        AddIfDifferent(differences, "seed", expected.Seed, actual.Seed, maxDifferences);
        AddIfDifferent(differences, "tickCount", expected.TickCount, actual.TickCount, maxDifferences);
        AddIfDifferent(differences, "grid.width", expected.Grid.Width, actual.Grid.Width, maxDifferences);
        AddIfDifferent(differences, "grid.height", expected.Grid.Height, actual.Grid.Height, maxDifferences);
        AddIfDifferent(differences, "grid.depth", expected.Grid.Depth, actual.Grid.Depth, maxDifferences);
    }

    private static void AddFinalCellDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "finalPackedCells.length", expected.FinalPackedCells.Length, actual.FinalPackedCells.Length, maxDifferences);

        int compareLength = Math.Min(expected.FinalPackedCells.Length, actual.FinalPackedCells.Length);
        Enumerable.Range(0, compareLength)
            .Where(index => expected.FinalPackedCells[index] != actual.FinalPackedCells[index])
            .Take(Math.Max(0, maxDifferences - differences.Count))
            .Select(index => $"finalPackedCells[{index}] expected 0x{expected.FinalPackedCells[index]:X4}, got 0x{actual.FinalPackedCells[index]:X4}.")
            .ToList()
            .ForEach(differences.Add);
    }

    private static void AddFinalAtmosphericFieldDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        if (expected.FinalAtmosphericFields is null && actual.FinalAtmosphericFields is null)
        {
            return;
        }

        if (expected.FinalAtmosphericFields is null || actual.FinalAtmosphericFields is null)
        {
            AddIfDifferent(
                differences,
                "finalAtmosphericFields.present",
                expected.FinalAtmosphericFields is not null,
                actual.FinalAtmosphericFields is not null,
                maxDifferences);
            return;
        }

        AddIfDifferent(
            differences,
            "finalAtmosphericFields.length",
            expected.FinalAtmosphericFields.Length,
            actual.FinalAtmosphericFields.Length,
            maxDifferences);

        int compareLength = Math.Min(expected.FinalAtmosphericFields.Length, actual.FinalAtmosphericFields.Length);
        Enumerable.Range(0, compareLength)
            .Where(index => expected.FinalAtmosphericFields[index] != actual.FinalAtmosphericFields[index])
            .Take(Math.Max(0, maxDifferences - differences.Count))
            .Select(index => $"finalAtmosphericFields[{index}] expected 0x{expected.FinalAtmosphericFields[index]:X4}, got 0x{actual.FinalAtmosphericFields[index]:X4}.")
            .ToList()
            .ForEach(differences.Add);
    }

    private static void AddFinalMaterialFieldDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        if (expected.FinalCompanionFields is null && actual.FinalCompanionFields is null)
        {
            return;
        }

        if (expected.FinalCompanionFields is null || actual.FinalCompanionFields is null)
        {
            AddIfDifferent(
                differences,
                "finalCompanionFields.present",
                expected.FinalCompanionFields is not null,
                actual.FinalCompanionFields is not null,
                maxDifferences);
            return;
        }

        AddIfDifferent(
            differences,
            "finalCompanionFields.length",
            expected.FinalCompanionFields.Length,
            actual.FinalCompanionFields.Length,
            maxDifferences);

        int compareLength = Math.Min(expected.FinalCompanionFields.Length, actual.FinalCompanionFields.Length);
        Enumerable.Range(0, compareLength)
            .Where(index => expected.FinalCompanionFields[index] != actual.FinalCompanionFields[index])
            .Take(Math.Max(0, maxDifferences - differences.Count))
            .Select(index => $"finalCompanionFields[{index}] expected 0x{expected.FinalCompanionFields[index]:X8}, got 0x{actual.FinalCompanionFields[index]:X8}.")
            .ToList()
            .ForEach(differences.Add);
    }

    private static void AddTickDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "ticks.length", expected.Ticks.Length, actual.Ticks.Length, maxDifferences);

        int compareLength = Math.Min(expected.Ticks.Length, actual.Ticks.Length);
        Enumerable.Range(0, compareLength)
            .TakeWhile(_ => differences.Count < maxDifferences)
            .ToList()
            .ForEach(index => AddSingleTickDifferences(expected.Ticks[index], actual.Ticks[index], differences, maxDifferences));
    }

    private static void AddSingleTickDifferences(
        ShaderSnapshotTick expected,
        ShaderSnapshotTick actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, $"ticks[{expected.Tick}].tick", expected.Tick, actual.Tick, maxDifferences);
        AddIfDifferent(differences, $"ticks[{expected.Tick}].deltaCount", expected.DeltaCount, actual.DeltaCount, maxDifferences);
        AddIfDifferent(differences, $"ticks[{expected.Tick}].deltas.length", expected.Deltas.Length, actual.Deltas.Length, maxDifferences);

        AddOptionalWords($"ticks[{expected.Tick}].appliedChangeWords", expected.AppliedChangeWords, actual.AppliedChangeWords, differences, maxDifferences);
        AddOptionalWords($"ticks[{expected.Tick}].materialHeader", expected.MaterialHeader, actual.MaterialHeader, differences, maxDifferences);
        AddOptionalWords($"ticks[{expected.Tick}].materialReceipts", expected.MaterialReceipts, actual.MaterialReceipts, differences, maxDifferences);

        ShaderSnapshotDelta[] expectedDeltas = SortDeltas(expected.Deltas);
        ShaderSnapshotDelta[] actualDeltas = SortDeltas(actual.Deltas);
        int compareLength = Math.Min(expected.Deltas.Length, actual.Deltas.Length);
        Enumerable.Range(0, compareLength)
            .Where(index => !expectedDeltas[index].Equals(actualDeltas[index]))
            .Take(Math.Max(0, maxDifferences - differences.Count))
            .Select(index => FormatDeltaDifference(expected.Tick, index, expectedDeltas[index], actualDeltas[index]))
            .ToList()
            .ForEach(differences.Add);
    }

    private static void AddOptionalWords(string name, uint[]? expected, uint[]? actual,
        List<string> differences, int maxDifferences)
    {
        // Old captures leave later protocol evidence unspecified; supplied evidence must match exactly.
        if (expected is null) return;
        AddIfDifferent(differences, name + ".present", true, actual is not null, maxDifferences);
        if (actual is null) return;
        AddIfDifferent(differences, name + ".length", expected.Length, actual.Length, maxDifferences);
        for (int i = 0; i < Math.Min(expected.Length, actual.Length); i++)
            AddIfDifferent(differences, $"{name}[{i}]", expected[i], actual[i], maxDifferences);
    }

    private static ShaderSnapshotDelta[] SortDeltas(ShaderSnapshotDelta[] deltas)
    {
        return deltas
            .OrderBy(static delta => delta.CellIndex)
            .ThenBy(static delta => delta.OldCell)
            .ThenBy(static delta => delta.NewCell)
            .ThenBy(static delta => delta.TargetId)
            .ToArray();
    }

    private static void AddVisualDifferences(
        ShaderSnapshotCapture expected,
        ShaderSnapshotCapture actual,
        List<string> differences,
        int maxDifferences)
    {
        AddIfDifferent(differences, "visual.checksum", expected.Visual?.Checksum, actual.Visual?.Checksum, maxDifferences);
        AddIfDifferent(differences, "visual.artifactPath", expected.Visual?.ArtifactPath, actual.Visual?.ArtifactPath, maxDifferences);
    }

    private static void AddIfDifferent<T>(List<string> differences, string fieldName, T expected, T actual, int maxDifferences)
    {
        if (differences.Count >= maxDifferences || EqualityComparer<T>.Default.Equals(expected, actual))
        {
            return;
        }

        differences.Add($"{fieldName} expected {expected}, got {actual}.");
    }

    private static string FormatDeltaDifference(
        int tick,
        int deltaIndex,
        ShaderSnapshotDelta expected,
        ShaderSnapshotDelta actual)
    {
        return $"ticks[{tick}].deltas[{deltaIndex}] expected cell {expected.CellIndex} " +
            $"0x{expected.OldCell:X4}->0x{expected.NewCell:X4} owner {expected.TargetId}, got cell {actual.CellIndex} " +
            $"0x{actual.OldCell:X4}->0x{actual.NewCell:X4} owner {actual.TargetId}.";
    }
}

