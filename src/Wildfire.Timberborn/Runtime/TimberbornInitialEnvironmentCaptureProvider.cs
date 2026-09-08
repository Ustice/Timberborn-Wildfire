using Timberborn.MapIndexSystem;
using Timberborn.MapStateSystem;
using Timberborn.SoilContaminationSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TerrainSystem;
using Timberborn.TickSystem;
using Timberborn.WaterSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Runtime;

/// <summary>Read on the native thread after service load/snapshot completion. Never forces a native tick.</summary>
public sealed class TimberbornInitialEnvironmentCaptureProvider
{
    private readonly int _captureThreadId = Environment.CurrentManagedThreadId;
    private readonly MapSize? _ownedMapSize;
    private readonly ITerrainService _terrain;
    private readonly MapIndexService _indices;
    private readonly IThreadSafeColumnTerrainMap _columns;
    private readonly ISoilMoistureService _moisture;
    private readonly ISoilContaminationService _contamination;
    private readonly IThreadSafeWaterMap _water;
    private readonly ITickableSingletonService _ticks;

    public TimberbornInitialEnvironmentCaptureProvider(ITerrainService terrain, MapIndexService indices,
        IThreadSafeColumnTerrainMap columns, ISoilMoistureService moisture, ISoilContaminationService contamination,
        IThreadSafeWaterMap water, ITickableSingletonService ticks)
        : this(null, terrain, indices, columns, moisture, contamination, water, ticks) { }

    public static TimberbornInitialEnvironmentCaptureProvider ForOwnedWorld(MapSize mapSize, ITerrainService terrain,
        MapIndexService indices, IThreadSafeColumnTerrainMap columns, ISoilMoistureService moisture,
        ISoilContaminationService contamination, IThreadSafeWaterMap water, ITickableSingletonService ticks) =>
        new(mapSize ?? throw new ArgumentNullException(nameof(mapSize)), terrain, indices, columns, moisture, contamination, water, ticks);

    private TimberbornInitialEnvironmentCaptureProvider(MapSize? ownedMapSize, ITerrainService terrain, MapIndexService indices,
        IThreadSafeColumnTerrainMap columns, ISoilMoistureService moisture, ISoilContaminationService contamination,
        IThreadSafeWaterMap water, ITickableSingletonService ticks)
    {
        _ownedMapSize = ownedMapSize;
        _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        _indices = indices ?? throw new ArgumentNullException(nameof(indices));
        _columns = columns ?? throw new ArgumentNullException(nameof(columns));
        _moisture = moisture ?? throw new ArgumentNullException(nameof(moisture));
        _contamination = contamination ?? throw new ArgumentNullException(nameof(contamination));
        _water = water ?? throw new ArgumentNullException(nameof(water));
        _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
    }

    public TimberbornInitialEnvironmentCapture Capture(FireGrid grid)
    {
        var domain = ReadSettledDomain(grid);
        int terrainDepth = domain?.TerrainGrid.Depth ?? grid.Depth;
        var solid = new List<int>();
        var soil = new List<TimberbornSurfaceSoilSample>();
        var water = new List<TimberbornWaterColumnSample>();
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
        {
            var xy = new Vector2Int(x, y);
            int horizontal = _indices.CellToIndex(xy);
            for (int z = 0; z < terrainDepth; z++)
                if (_terrain.UnsafeCellIsTerrain(_indices.CoordinatesToIndex3D(new Vector3Int(x, y, z))))
                    solid.Add(grid.ToIndex(x, y, z));
            if (domain is not null) ReadOwnedSurfaces(domain, xy, horizontal, soil);
            else
                foreach (var surface in _terrain.GetAllHeightsInCell(xy))
                {
                    if (surface.x != x || surface.y != y) throw new InvalidOperationException("Native surface escaped its terrain column.");
                    int cell = grid.ToIndex(surface.x, surface.y, surface.z);
                    soil.Add(ReadSurface(cell, horizontal, surface));
                }
            int count = _water.ColumnCount(horizontal);
            if (count < 0 || count > _water.MaxColumnCount) throw new InvalidOperationException("Native water columns are not settled.");
            var values = _water.WaterColumns;
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                int columnIndex = checked(ordinal * _indices.VerticalStride + horizontal);
                var column = values[columnIndex];
                water.Add(new(x, y, column.Floor, column.Ceiling, column.WaterDepth, column.Contamination, column.Overflow));
            }
        }
        if (ReadSettledDomain(grid) != domain) throw new InvalidOperationException("Native world domain changed during environment capture.");
        return domain is null ? new(grid, solid, soil, water) : TimberbornInitialEnvironmentCapture.ForOwnedDomain(domain, solid, soil, water);
    }

    // The native height iterator stops below TerrainSize.z and omits a ceiling at that boundary.
    // Owned worlds have native air above it; enumerate the actual columns, never infer soil values.
    private void ReadOwnedSurfaces(TimberbornWorldDomain domain, Vector2Int xy, int horizontal,
        List<TimberbornSurfaceSoilSample> soil)
    {
        if ((uint)horizontal >= (uint)_columns.ColumnCounts.AsSpan.Length)
            throw new InvalidOperationException("Native terrain column coordinate is outside its storage.");
        int count = _columns.GetColumnCount(horizontal);
        if (count < 0 || count > _columns.MaxColumnCount)
            throw new InvalidOperationException("Native terrain column count is not settled.");
        int previousCeiling = 0;
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            int index = checked(ordinal * _indices.VerticalStride + horizontal);
            if ((uint)index >= (uint)_columns.TerrainColumns.AsSpan.Length)
                throw new InvalidOperationException("Native terrain column index is outside its storage.");
            int floor = _columns.GetColumnFloor(index), ceiling = _columns.GetColumnCeiling(index);
            if (floor < previousCeiling || ceiling <= floor || ceiling > domain.TerrainGrid.Depth || ceiling >= domain.WorldGrid.Depth)
                throw new InvalidOperationException("Native terrain column ceiling is outside the owned soil domain.");
            if (!_columns.TryGetIndexAtCeiling(horizontal, ceiling, out int resolved) || resolved != index)
                throw new InvalidOperationException("Native terrain column ceiling does not resolve its original index.");
            var surface = new Vector3Int(xy.x, xy.y, ceiling);
            soil.Add(ReadSurface(domain.WorldGrid.ToIndex(xy.x, xy.y, ceiling), horizontal, surface));
            previousCeiling = ceiling;
        }
    }

    public void RequireUnchanged(TimberbornInitialEnvironmentCapture captured)
    {
        if (!captured.SameReadings(Capture(captured.Grid)))
            throw new InvalidOperationException("Native environment changed during initial world capture; no projection was published.");
    }

    private TimberbornSurfaceSoilSample ReadSurface(int cell, int horizontal, Vector3Int surface)
    {
        if (!_columns.TryGetIndexAtCeiling(horizontal, surface.z, out int columnIndex))
            throw new InvalidOperationException("Native soil surface has no settled terrain-column index.");
        return new(cell, _moisture.SoilMoisture(columnIndex), _contamination.Contamination(columnIndex),
            _moisture.SoilIsMoist(surface), _contamination.SoilIsContaminated(surface));
    }

    internal void RequireSettled(FireGrid grid) => ReadSettledDomain(grid);

    private TimberbornWorldDomain? ReadSettledDomain(FireGrid grid)
    {
        if (grid.Width <= 0 || grid.Height <= 0 || grid.Depth <= 0) throw new ArgumentOutOfRangeException(nameof(grid));
        _ = checked(grid.Width * grid.Height * grid.Depth);
        var domain = _ownedMapSize is null ? null : TimberbornNativeWorldDomain.Read(_ownedMapSize);
        if (domain is not null && domain.WorldGrid != grid)
            throw new NotSupportedException("Saved or requested owned grid does not match the native total world domain.");
        var terrain = domain?.TerrainGrid ?? grid;
        var size = new Vector3Int(terrain.Width, terrain.Height, terrain.Depth);
        if (Environment.CurrentManagedThreadId != _captureThreadId || _ticks.IsStartingParallelTick || !_ticks.ParalleTicklIsFinished || _terrain.Size != size ||
            _indices.TerrainSize != size || _indices.VerticalStride <= 0)
            throw new InvalidOperationException("Native environment must be captured after service load and parallel tick completion.");
        return domain;
    }
}
