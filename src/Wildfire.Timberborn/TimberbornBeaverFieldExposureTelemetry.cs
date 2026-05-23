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

        TimberbornBeaverFieldExposureCandidate[] candidates = CreateCandidates(grid, positions.Beavers);
        TimberbornBeaverFieldExposureCandidate[] sampledCandidates = SelectSampledCandidates(
            candidates,
            out int[] inspectedCellIndices,
            out int skippedBoundedSampling);

        IReadOnlyList<TimberbornGpuVisualFieldSample> samples = inspectedCellIndices.Length == 0
            ? Array.Empty<TimberbornGpuVisualFieldSample>()
            : _visualFieldSurface.InspectCells(inspectedCellIndices);
        Dictionary<int, TimberbornGpuVisualFieldSample> sampleByCell = samples
            .GroupBy(static sample => sample.CellIndex)
            .ToDictionary(static group => group.Key, static group => group.First());

        TimberbornBeaverFieldExposureClassification[] classifications = sampledCandidates
            .Select(candidate => Classify(candidate.Beaver, candidate.CellIndices, sampleByCell))
            .ToArray();
        _lastSnapshot = TimberbornBeaverFieldExposureSnapshot.FromClassifications(
            sampledBeavers: classifications.Length,
            skippedNoPositionApi: positions.SkippedNoPositionApiCount,
            skippedBoundedSampling: skippedBoundedSampling,
            classifications);
        LogSnapshot(tick, _lastSnapshot);
        return _lastSnapshot;
    }

    public TimberbornBeaverFieldExposureQaTarget SelectQaStimulusTarget(FireGrid grid)
    {
        TimberbornBeaverPositionSnapshot positions = _positionProvider.GetPositions(grid);
        if (!positions.IsAvailable)
        {
            return TimberbornBeaverFieldExposureQaTarget.Unavailable(
                positions.UnavailableReason,
                positions.SkippedNoPositionApiCount);
        }

        TimberbornBeaverFieldExposureCandidate[] candidates = CreateCandidates(grid, positions.Beavers);
        TimberbornBeaverFieldExposureCandidate[] sampledCandidates = SelectSampledCandidates(
            candidates,
            out _,
            out int skippedBoundedSampling);
        TimberbornBeaverFieldExposureCandidate? selectedCandidate = sampledCandidates
            .OrderBy(static candidate => candidate.Beaver.BeaverId, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.CellIndices.Min())
            .Select(static candidate => (TimberbornBeaverFieldExposureCandidate?)candidate)
            .FirstOrDefault();
        if (selectedCandidate is not { } targetCandidate)
        {
            return TimberbornBeaverFieldExposureQaTarget.Unavailable(
                "no_sampled_beaver_candidate_cells",
                positions.SkippedNoPositionApiCount,
                sampledCandidates.Length,
                skippedBoundedSampling);
        }

        int beaverCellIndex = grid.ToIndex(
            targetCandidate.Beaver.X,
            targetCandidate.Beaver.Y,
            targetCandidate.Beaver.Z);
        int targetCellIndex = targetCandidate.CellIndices.Contains(beaverCellIndex)
            ? beaverCellIndex
            : targetCandidate.CellIndices.OrderBy(static cellIndex => cellIndex).First();
        (int targetX, int targetY, int targetZ) = grid.FromIndex(targetCellIndex);

        return TimberbornBeaverFieldExposureQaTarget.Available(
            targetCandidate.Beaver.BeaverId,
            targetCandidate.Beaver.X,
            targetCandidate.Beaver.Y,
            targetCandidate.Beaver.Z,
            targetCellIndex,
            targetX,
            targetY,
            targetZ,
            targetCandidate.CellIndices.Count,
            sampledCandidates.Length,
            skippedBoundedSampling,
            targetCandidate.CellIndices);
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
        int steamCells = samples.Count(static sample => sample.Steam >= RespiratorySmokeThreshold);
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
            steamCells,
            taintedAftermathCells,
            candidateCellIndices.ToArray(),
            samples.Length,
            samples.Select(static sample => sample.Fire).DefaultIfEmpty(0f).Max(),
            samples.Select(static sample => sample.Smoke).DefaultIfEmpty(0f).Max(),
            samples.Select(static sample => sample.SmokeContamination).DefaultIfEmpty(0f).Max(),
            samples.Select(static sample => sample.Steam).DefaultIfEmpty(0f).Max(),
            samples.Select(static sample => sample.Ash).DefaultIfEmpty(0f).Max(),
            samples.Select(static sample => sample.AshContamination).DefaultIfEmpty(0f).Max(),
            beaver.WorldX,
            beaver.WorldY,
            beaver.WorldZ);
    }

    private static TimberbornBeaverFieldExposureCandidate[] CreateCandidates(
        FireGrid grid,
        IReadOnlyList<TimberbornBeaverPositionSample> beavers)
    {
        return beavers
            .Select(beaver => new TimberbornBeaverFieldExposureCandidate(
                beaver,
                CandidateCellIndices(grid, beaver.X, beaver.Y, beaver.Z)))
            .Where(static candidate => candidate.CellIndices.Count > 0)
            .ToArray();
    }

    private static TimberbornBeaverFieldExposureCandidate[] SelectSampledCandidates(
        TimberbornBeaverFieldExposureCandidate[] candidates,
        out int[] inspectedCellIndices,
        out int skippedBoundedSampling)
    {
        inspectedCellIndices = candidates
            .SelectMany(static candidate => candidate.CellIndices)
            .Distinct()
            .Take(TimberbornGpuVisualFieldSurface.MaxInspectionCellCount)
            .ToArray();
        HashSet<int> inspectedCellIndexSet = inspectedCellIndices.ToHashSet();
        TimberbornBeaverFieldExposureCandidate[] sampledCandidates = candidates
            .Where(candidate => candidate.CellIndices.All(inspectedCellIndexSet.Contains))
            .ToArray();
        skippedBoundedSampling = candidates.Length - sampledCandidates.Length;
        return sampledCandidates;
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
            $"steam_cells={snapshot.SteamCells} " +
            $"tainted_aftermath_cells={snapshot.TaintedAftermathCells} " +
            $"skipped_no_position_api={snapshot.SkippedNoPositionApi} " +
            $"skipped_bounded_sampling={snapshot.SkippedBoundedSampling}");
        snapshot.Classifications
            .ToList()
            .ForEach(classification => _logSink.Info(
                "wildfire_timberborn_beaver_field_exposure_beaver_sampled " +
                $"tick={FormatNumber(tick)} " +
                $"beaver_id={TimberbornQaCommandBridge.FormatToken(classification.BeaverId)} " +
                $"x={classification.X} " +
                $"y={classification.Y} " +
                $"z={classification.Z} " +
                $"firegrid_x={classification.X} " +
                $"firegrid_y={classification.Y} " +
                $"firegrid_z={classification.Z} " +
                $"world_x={FormatFloat(classification.WorldX)} " +
                $"world_y={FormatFloat(classification.WorldY)} " +
                $"world_z={FormatFloat(classification.WorldZ)} " +
                $"candidate_cells={classification.CandidateCellCount} " +
                $"sampled_cells={classification.SampledCellCount} " +
                $"cell_indices={FormatCellIndices(classification.CellIndices)} " +
                $"has_exposure={classification.HasExposure.ToString().ToLowerInvariant()} " +
                $"respiratory_cells={classification.RespiratoryExposureCells} " +
                $"burn_cells={classification.BurnExposureCells} " +
                $"contaminated_smoke_cells={classification.ContaminatedSmokeCells} " +
                $"toxic_cells={classification.ToxicExposureCells} " +
                $"steam_cells={classification.SteamCells} " +
                $"tainted_aftermath_cells={classification.TaintedAftermathCells} " +
                $"max_fire={FormatFloat(classification.MaxFire)} " +
                $"max_smoke={FormatFloat(classification.MaxSmoke)} " +
                $"max_smoke_contamination={FormatFloat(classification.MaxSmokeContamination)} " +
                $"max_steam={FormatFloat(classification.MaxSteam)} " +
                $"max_ash={FormatFloat(classification.MaxAsh)} " +
                $"max_ash_contamination={FormatFloat(classification.MaxAshContamination)}"));
    }

    private static string FormatNumber(uint? value)
    {
        return value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "placeholder";
    }

    private static string FormatFloat(float value)
    {
        return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatCellIndices(IReadOnlyList<int> cellIndices)
    {
        return cellIndices.Count == 0
            ? "none"
            : string.Join(",", cellIndices.Select(static cellIndex =>
                cellIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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
        Vector3Int? coordinates = WorldToFireGridCoordinates(position, grid);
        if (coordinates is null)
        {
            return null;
        }

        return new TimberbornBeaverPositionSample(
            TimberbornQaCommandBridge.FormatToken(entity.EntityId.ToString()),
            coordinates.Value.x,
            coordinates.Value.y,
            coordinates.Value.z,
            position.x,
            position.y,
            position.z);
    }

    public static Vector3Int? WorldToFireGridCoordinates(Vector3 position, FireGrid grid)
    {
        TimberbornBeaverFireGridCoordinates? coordinates = WorldToFireGridCoordinates(
            position.x,
            position.y,
            position.z,
            grid);
        return coordinates is null
            ? null
            : new Vector3Int(coordinates.Value.X, coordinates.Value.Y, coordinates.Value.Z);
    }

    public static TimberbornBeaverFireGridCoordinates? WorldToFireGridCoordinates(
        float worldX,
        float worldY,
        float worldZ,
        FireGrid grid)
    {
        int x = Mathf.FloorToInt(worldX);
        int y = Mathf.FloorToInt(worldZ);
        int z = Mathf.FloorToInt(worldY);
        if (x < 0 || y < 0 || z < 0 || x >= grid.Width || y >= grid.Height || z >= grid.Depth)
        {
            return null;
        }

        return new TimberbornBeaverFireGridCoordinates(x, y, z);
    }
}

public readonly record struct TimberbornBeaverFireGridCoordinates(int X, int Y, int Z);

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

public readonly record struct TimberbornBeaverPositionSample(
    string BeaverId,
    int X,
    int Y,
    int Z,
    float WorldX = 0f,
    float WorldY = 0f,
    float WorldZ = 0f);

public sealed record TimberbornBeaverFieldExposureQaTarget(
    bool IsAvailable,
    string UnavailableReason,
    string? BeaverId,
    int? BeaverX,
    int? BeaverY,
    int? BeaverZ,
    int? CellIndex,
    int? X,
    int? Y,
    int? Z,
    IReadOnlyList<int> CellIndices,
    int CandidateCellCount,
    int SampledBeaverCount,
    int SkippedNoPositionApiCount,
    int SkippedBoundedSamplingCount)
{
    public static TimberbornBeaverFieldExposureQaTarget Available(
        string beaverId,
        int beaverX,
        int beaverY,
        int beaverZ,
        int cellIndex,
        int x,
        int y,
        int z,
        int candidateCellCount,
        int sampledBeaverCount,
        int skippedBoundedSamplingCount,
        IReadOnlyList<int>? cellIndices = null)
    {
        return new TimberbornBeaverFieldExposureQaTarget(
            true,
            "none",
            beaverId,
            beaverX,
            beaverY,
            beaverZ,
            cellIndex,
            x,
            y,
            z,
            cellIndices ?? new[] { cellIndex },
            candidateCellCount,
            sampledBeaverCount,
            SkippedNoPositionApiCount: 0,
            SkippedBoundedSamplingCount: skippedBoundedSamplingCount);
    }

    public static TimberbornBeaverFieldExposureQaTarget Unavailable(
        string reason,
        int skippedNoPositionApiCount,
        int sampledBeaverCount = 0,
        int skippedBoundedSamplingCount = 0)
    {
        return new TimberbornBeaverFieldExposureQaTarget(
            false,
            string.IsNullOrWhiteSpace(reason) ? "beaver_position_unavailable" : reason,
            BeaverId: null,
            BeaverX: null,
            BeaverY: null,
            BeaverZ: null,
            CellIndex: null,
            X: null,
            Y: null,
            Z: null,
            CellIndices: Array.Empty<int>(),
            CandidateCellCount: 0,
            sampledBeaverCount,
            skippedNoPositionApiCount,
            skippedBoundedSamplingCount);
    }
}

public sealed record TimberbornBeaverFieldExposureSnapshot(
    bool IsAvailable,
    int SampledBeavers,
    int ExposedBeavers,
    int RespiratoryExposureCells,
    int BurnExposureCells,
    int ContaminatedSmokeCells,
    int ToxicExposureCells,
    int SteamCells,
    int TaintedAftermathCells,
    int SkippedNoPositionApi,
    int SkippedBoundedSampling,
    string UnavailableReason,
    IReadOnlyList<TimberbornBeaverFieldExposureClassification> Classifications)
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
            SteamCells: 0,
            TaintedAftermathCells: 0,
            SkippedNoPositionApi: 1,
            SkippedBoundedSampling: 0,
            UnavailableReason: string.IsNullOrWhiteSpace(reason) ? "unavailable" : reason,
            Classifications: Array.Empty<TimberbornBeaverFieldExposureClassification>());
    }

    public static TimberbornBeaverFieldExposureSnapshot FromClassifications(
        int sampledBeavers,
        int skippedNoPositionApi,
        int skippedBoundedSampling,
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
            SteamCells: classifications.Sum(static classification => classification.SteamCells),
            TaintedAftermathCells: classifications.Sum(static classification => classification.TaintedAftermathCells),
            SkippedNoPositionApi: skippedNoPositionApi,
            SkippedBoundedSampling: skippedBoundedSampling,
            UnavailableReason: "none",
            Classifications: classifications.ToArray());
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
    int SteamCells,
    int TaintedAftermathCells,
    IReadOnlyList<int>? CellIndices = null,
    int SampledCellCount = 0,
    float MaxFire = 0f,
    float MaxSmoke = 0f,
    float MaxSmokeContamination = 0f,
    float MaxSteam = 0f,
    float MaxAsh = 0f,
    float MaxAshContamination = 0f,
    float WorldX = 0f,
    float WorldY = 0f,
    float WorldZ = 0f)
{
    public IReadOnlyList<int> CellIndices { get; } = CellIndices ?? Array.Empty<int>();

    public bool HasExposure =>
        RespiratoryExposureCells > 0 ||
        BurnExposureCells > 0 ||
        ContaminatedSmokeCells > 0 ||
        ToxicExposureCells > 0 ||
        SteamCells > 0 ||
        TaintedAftermathCells > 0;
}
