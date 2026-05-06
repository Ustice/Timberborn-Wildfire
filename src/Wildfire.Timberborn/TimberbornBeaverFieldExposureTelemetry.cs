using Timberborn.Beavers;
using Timberborn.EntitySystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornBeaverFieldExposureTelemetry
{
    public const float RespiratorySmokeThreshold = 0.18f;
    public const float BurnFireThreshold = 0.12f;
    public const float ToxicSmokeThreshold = 0.55f;
    public const float TaintedAftermathAshThreshold = 0.35f;
    public const int MaxSampleCellsPerBeaver = 9;

    private readonly ITimberbornBeaverPositionProvider _positionProvider;
    private readonly ITimberbornGpuVisualFieldSurface _visualFieldSurface;
    private readonly ITimberbornFireLogSink _logSink;
    private TimberbornBeaverFieldExposureSnapshot _lastSnapshot =
        TimberbornBeaverFieldExposureSnapshot.Unavailable("not_sampled");

    public TimberbornBeaverFieldExposureTelemetry(
        ITimberbornBeaverPositionProvider positionProvider,
        ITimberbornGpuVisualFieldSurface visualFieldSurface,
        ITimberbornFireLogSink logSink)
    {
        _positionProvider = positionProvider ?? throw new ArgumentNullException(nameof(positionProvider));
        _visualFieldSurface = visualFieldSurface ?? throw new ArgumentNullException(nameof(visualFieldSurface));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
    }

    public TimberbornBeaverFieldExposureSnapshot LastSnapshot => _lastSnapshot;

    public TimberbornBeaverFieldExposureSnapshot Sample(FireGrid grid, uint? tick)
    {
        TimberbornBeaverPositionSnapshot positions = _positionProvider.GetPositions(grid);
        if (!positions.IsAvailable)
        {
            _lastSnapshot = TimberbornBeaverFieldExposureSnapshot.Unavailable(positions.UnavailableReason);
            LogSnapshot(tick, _lastSnapshot);
            return _lastSnapshot;
        }

        if (!_visualFieldSurface.State.IsBound)
        {
            _lastSnapshot = TimberbornBeaverFieldExposureSnapshot.Unavailable(
                "visual_field_surface_unbound",
                sampledBeavers: positions.Beavers.Count);
            LogSnapshot(tick, _lastSnapshot);
            return _lastSnapshot;
        }

        TimberbornBeaverFieldExposureCandidate[] candidates = positions.Beavers
            .Select(beaver => new TimberbornBeaverFieldExposureCandidate(
                beaver,
                CandidateCellIndices(grid, beaver.X, beaver.Y, beaver.Z)))
            .Where(static candidate => candidate.CellIndices.Count > 0)
            .ToArray();

        IReadOnlyList<TimberbornGpuVisualFieldSample> samples = candidates.Length == 0
            ? Array.Empty<TimberbornGpuVisualFieldSample>()
            : _visualFieldSurface.InspectCells(candidates
                .SelectMany(static candidate => candidate.CellIndices)
                .Distinct()
                .Take(TimberbornGpuVisualFieldSurface.MaxInspectionCellCount)
                .ToArray());
        Dictionary<int, TimberbornGpuVisualFieldSample> sampleByCell = samples
            .GroupBy(static sample => sample.CellIndex)
            .ToDictionary(static group => group.Key, static group => group.First());

        TimberbornBeaverFieldExposureClassification[] classifications = candidates
            .Select(candidate => Classify(candidate.Beaver, candidate.CellIndices, sampleByCell))
            .ToArray();
        _lastSnapshot = TimberbornBeaverFieldExposureSnapshot.FromClassifications(
            sampledBeavers: positions.Beavers.Count,
            skippedNoPositionApi: positions.SkippedNoPositionApiCount,
            classifications);
        LogSnapshot(tick, _lastSnapshot);
        return _lastSnapshot;
    }

    public static IReadOnlyList<int> CandidateCellIndices(FireGrid grid, int x, int y, int z)
    {
        return Enumerable.Range(-1, 3)
            .SelectMany(dx => Enumerable.Range(-1, 3)
                .Select(dy => new { X = x + dx, Y = y + dy, Z = z }))
            .Where(candidate =>
                candidate.X >= 0 &&
                candidate.Y >= 0 &&
                candidate.Z >= 0 &&
                candidate.X < grid.Width &&
                candidate.Y < grid.Height &&
                candidate.Z < grid.Depth)
            .Select(candidate => grid.ToIndex(candidate.X, candidate.Y, candidate.Z))
            .Take(MaxSampleCellsPerBeaver)
            .ToArray();
    }

    public static TimberbornBeaverFieldExposureClassification Classify(
        TimberbornBeaverPositionSample beaver,
        IReadOnlyList<int> candidateCellIndices,
        IReadOnlyDictionary<int, TimberbornGpuVisualFieldSample> sampleByCell)
    {
        TimberbornGpuVisualFieldSample[] samples = candidateCellIndices
            .Where(sampleByCell.ContainsKey)
            .Select(cellIndex => sampleByCell[cellIndex])
            .ToArray();
        int respiratoryCells = samples.Count(static sample => sample.Smoke >= RespiratorySmokeThreshold);
        int burnCells = samples.Count(static sample => sample.Fire >= BurnFireThreshold);
        int contaminatedSmokeCells = samples.Count(static sample =>
            sample.Smoke >= RespiratorySmokeThreshold &&
            sample.SmokeContamination > 0f);
        int toxicCells = samples.Count(static sample =>
            sample.Smoke >= ToxicSmokeThreshold ||
            sample.SmokeContamination >= ToxicSmokeThreshold);
        int taintedAftermathCells = samples.Count(static sample =>
            sample.Ash >= TaintedAftermathAshThreshold &&
            sample.AshContamination > 0f &&
            sample.Fire < BurnFireThreshold);

        return new TimberbornBeaverFieldExposureClassification(
            beaver.BeaverId,
            beaver.X,
            beaver.Y,
            beaver.Z,
            candidateCellIndices.Count,
            respiratoryCells,
            burnCells,
            contaminatedSmokeCells,
            toxicCells,
            0,
            taintedAftermathCells);
    }

    private void LogSnapshot(uint? tick, TimberbornBeaverFieldExposureSnapshot snapshot)
    {
        _logSink.Info(
            "wildfire_timberborn_beaver_field_exposure_sampled " +
            $"tick={FormatNumber(tick)} " +
            $"available={snapshot.IsAvailable.ToString().ToLowerInvariant()} " +
            $"reason={TimberbornQaCommandBridge.FormatToken(snapshot.UnavailableReason)} " +
            $"sampled_beavers={snapshot.SampledBeavers} " +
            $"exposed_beavers={snapshot.ExposedBeavers} " +
            $"respiratory_cells={snapshot.RespiratoryExposureCells} " +
            $"burn_cells={snapshot.BurnExposureCells} " +
            $"toxic_cells={snapshot.ToxicExposureCells} " +
            $"tainted_aftermath_cells={snapshot.TaintedAftermathCells} " +
            $"skipped_no_position_api={snapshot.SkippedNoPositionApi}");
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private readonly record struct TimberbornBeaverFieldExposureCandidate(
        TimberbornBeaverPositionSample Beaver,
        IReadOnlyList<int> CellIndices);
}

public interface ITimberbornBeaverPositionProvider
{
    TimberbornBeaverPositionSnapshot GetPositions(FireGrid grid);
}

public sealed class TimberbornEntityRegistryBeaverPositionProvider : ITimberbornBeaverPositionProvider
{
    private readonly EntityRegistry _entityRegistry;

    public TimberbornEntityRegistryBeaverPositionProvider(EntityRegistry entityRegistry)
    {
        _entityRegistry = entityRegistry ?? throw new ArgumentNullException(nameof(entityRegistry));
    }

    public TimberbornBeaverPositionSnapshot GetPositions(FireGrid grid)
    {
        try
        {
            TimberbornBeaverPositionSample[] beavers = _entityRegistry.Entities
                .Where(static entity => entity.TryGetComponent(out Beaver _))
                .Select(entity => TryReadPosition(entity, grid))
                .Where(static sample => sample is not null)
                .Select(static sample => sample!.Value)
                .ToArray();

            return TimberbornBeaverPositionSnapshot.Available(beavers);
        }
        catch (Exception exception)
        {
            return TimberbornBeaverPositionSnapshot.Unavailable(
                "position_api_exception:" + exception.GetType().Name);
        }
    }

    private static TimberbornBeaverPositionSample? TryReadPosition(EntityComponent entity, FireGrid grid)
    {
        Vector3 position = entity.Transform.position;
        int x = Mathf.FloorToInt(position.x);
        int y = Mathf.FloorToInt(position.y);
        int z = Mathf.FloorToInt(position.z);
        if (x < 0 || y < 0 || z < 0 || x >= grid.Width || y >= grid.Height || z >= grid.Depth)
        {
            return null;
        }

        return new TimberbornBeaverPositionSample(
            TimberbornQaCommandBridge.FormatToken(entity.EntityId.ToString()),
            x,
            y,
            z);
    }
}

public sealed record TimberbornBeaverPositionSnapshot(
    bool IsAvailable,
    IReadOnlyList<TimberbornBeaverPositionSample> Beavers,
    int SkippedNoPositionApiCount,
    string UnavailableReason)
{
    public static TimberbornBeaverPositionSnapshot Available(IReadOnlyList<TimberbornBeaverPositionSample> beavers)
    {
        return new TimberbornBeaverPositionSnapshot(
            IsAvailable: true,
            Beavers: beavers ?? throw new ArgumentNullException(nameof(beavers)),
            SkippedNoPositionApiCount: 0,
            UnavailableReason: "none");
    }

    public static TimberbornBeaverPositionSnapshot Unavailable(string reason)
    {
        return new TimberbornBeaverPositionSnapshot(
            IsAvailable: false,
            Beavers: Array.Empty<TimberbornBeaverPositionSample>(),
            SkippedNoPositionApiCount: 1,
            UnavailableReason: string.IsNullOrWhiteSpace(reason) ? "position_api_unavailable" : reason);
    }
}

public readonly record struct TimberbornBeaverPositionSample(string BeaverId, int X, int Y, int Z);

public sealed record TimberbornBeaverFieldExposureSnapshot(
    bool IsAvailable,
    int SampledBeavers,
    int ExposedBeavers,
    int RespiratoryExposureCells,
    int BurnExposureCells,
    int ContaminatedSmokeCells,
    int ToxicExposureCells,
    int ToxicSteamCells,
    int TaintedAftermathCells,
    int SkippedNoPositionApi,
    string UnavailableReason)
{
    public static TimberbornBeaverFieldExposureSnapshot Unavailable(
        string reason,
        int sampledBeavers = 0)
    {
        return new TimberbornBeaverFieldExposureSnapshot(
            IsAvailable: false,
            SampledBeavers: sampledBeavers,
            ExposedBeavers: 0,
            RespiratoryExposureCells: 0,
            BurnExposureCells: 0,
            ContaminatedSmokeCells: 0,
            ToxicExposureCells: 0,
            ToxicSteamCells: 0,
            TaintedAftermathCells: 0,
            SkippedNoPositionApi: 1,
            UnavailableReason: string.IsNullOrWhiteSpace(reason) ? "unavailable" : reason);
    }

    public static TimberbornBeaverFieldExposureSnapshot FromClassifications(
        int sampledBeavers,
        int skippedNoPositionApi,
        IReadOnlyList<TimberbornBeaverFieldExposureClassification> classifications)
    {
        return new TimberbornBeaverFieldExposureSnapshot(
            IsAvailable: true,
            SampledBeavers: sampledBeavers,
            ExposedBeavers: classifications.Count(static classification => classification.HasExposure),
            RespiratoryExposureCells: classifications.Sum(static classification => classification.RespiratoryExposureCells),
            BurnExposureCells: classifications.Sum(static classification => classification.BurnExposureCells),
            ContaminatedSmokeCells: classifications.Sum(static classification => classification.ContaminatedSmokeCells),
            ToxicExposureCells: classifications.Sum(static classification => classification.ToxicExposureCells),
            ToxicSteamCells: classifications.Sum(static classification => classification.ToxicSteamCells),
            TaintedAftermathCells: classifications.Sum(static classification => classification.TaintedAftermathCells),
            SkippedNoPositionApi: skippedNoPositionApi,
            UnavailableReason: "none");
    }
}

public sealed record TimberbornBeaverFieldExposureClassification(
    string BeaverId,
    int X,
    int Y,
    int Z,
    int CandidateCellCount,
    int RespiratoryExposureCells,
    int BurnExposureCells,
    int ContaminatedSmokeCells,
    int ToxicExposureCells,
    int ToxicSteamCells,
    int TaintedAftermathCells)
{
    public bool HasExposure =>
        RespiratoryExposureCells > 0 ||
        BurnExposureCells > 0 ||
        ContaminatedSmokeCells > 0 ||
        ToxicExposureCells > 0 ||
        ToxicSteamCells > 0 ||
        TaintedAftermathCells > 0;
}
