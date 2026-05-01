using Wildfire.Core;

namespace Wildfire.Timberborn;

public readonly record struct TimberbornCellCoordinates(int X, int Y, int Z);

public readonly record struct TimberbornTerrainCell(bool IsSolid, byte Wetness = 0);

public readonly record struct TimberbornBuildingCell(byte Fuel, byte Flammability, byte HeatLoss);

public readonly record struct TimberbornResourceCell(byte Fuel, byte Flammability, byte HeatLoss, TimberbornResourceKind Kind);

public readonly record struct TimberbornWaterCell(byte Water);

public enum TimberbornResourceKind
{
    StockpileResource,
    Vegetation,
}

public readonly record struct TimberbornCellSource(
    TimberbornCellCoordinates Coordinates,
    TimberbornTerrainCell? Terrain = null,
    TimberbornBuildingCell? Building = null,
    TimberbornResourceCell? Resource = null,
    TimberbornWaterCell? Water = null);

public sealed class TimberbornTerrainAdapter
{
    public TimberbornCellSource CreateSource(int x, int y, int z, bool isSolid, byte wetness = 0)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Terrain: new TimberbornTerrainCell(isSolid, wetness));
    }
}

public sealed class TimberbornBuildingAdapter
{
    public TimberbornCellSource CreateSource(int x, int y, int z, byte fuel, byte flammability, byte heatLoss)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Building: new TimberbornBuildingCell(fuel, flammability, heatLoss));
    }
}

public sealed class TimberbornResourceAdapter
{
    public TimberbornCellSource CreateSource(
        int x,
        int y,
        int z,
        byte fuel,
        byte flammability,
        byte heatLoss,
        TimberbornResourceKind kind)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Resource: new TimberbornResourceCell(fuel, flammability, heatLoss, kind));
    }
}

public sealed class TimberbornWaterAdapter
{
    public TimberbornCellSource CreateSource(int x, int y, int z, byte water)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Water: new TimberbornWaterCell(water));
    }
}

public sealed class TimberbornFireCellMapper
{
    public const ushort EmptyCell = 0xE000;

    public ushort[] CreateInitialCells(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        ushort[] cells = Enumerable.Repeat(EmptyCell, grid.CellCount).ToArray();

        CreateMappedCells(grid, sources)
            .ToList()
            .ForEach(mappedCell => cells[mappedCell.CellIndex] = mappedCell.PackedCell);

        return cells;
    }

    public IReadOnlyList<FireSimChange> CreateSetCellChanges(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        return CreateMappedCells(grid, sources)
            .Select(static mappedCell => new FireSimChange(mappedCell.CellIndex, SetCell: mappedCell.PackedCell))
            .ToArray();
    }

    public IReadOnlyList<TimberbornMappedCell> CreateMappedCells(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        return sources
            .Select(source => new IndexedSource(ToIndex(grid, source), source))
            .GroupBy(static indexedSource => indexedSource.CellIndex)
            .OrderBy(static group => group.Key)
            .Select(static group => new TimberbornMappedCell(group.Key, PackCell(group.Select(static value => value.Source))))
            .ToArray();
    }

    private static int ToIndex(FireGrid grid, TimberbornCellSource source)
    {
        return grid.ToIndex(source.Coordinates.X, source.Coordinates.Y, source.Coordinates.Z);
    }

    private static ushort PackCell(IEnumerable<TimberbornCellSource> sources)
    {
        TimberbornCellSource[] cells = sources.ToArray();
        MaterialContribution material = SelectMaterial(cells);
        int water = SelectWater(cells);

        return PackedCell.Pack(
            material.Fuel,
            heat: 0,
            material.Flammability,
            water,
            material.Terrain,
            material.HeatLoss);
    }

    private static MaterialContribution SelectMaterial(IReadOnlyList<TimberbornCellSource> sources)
    {
        return sources
            .SelectMany(static source => EnumerateMaterialContributions(source))
            .DefaultIfEmpty(MaterialContribution.Empty)
            .OrderByDescending(static contribution => contribution.Priority)
            .ThenByDescending(static contribution => contribution.Fuel)
            .ThenByDescending(static contribution => contribution.Flammability)
            .ThenBy(static contribution => contribution.HeatLoss)
            .First();
    }

    private static IEnumerable<MaterialContribution> EnumerateMaterialContributions(TimberbornCellSource source)
    {
        if (source.Terrain is { IsSolid: true } terrain)
        {
            yield return new MaterialContribution(
                Priority: 1,
                Fuel: 0,
                Flammability: 0,
                Terrain: 1,
                HeatLoss: 6);
        }

        if (source.Resource is { } resource)
        {
            int heatLoss = resource.Kind == TimberbornResourceKind.Vegetation
                ? Math.Min(ClampHeatLoss(resource.HeatLoss), 3)
                : ClampHeatLoss(resource.HeatLoss);

            yield return new MaterialContribution(
                Priority: 2,
                Fuel: ClampFuel(resource.Fuel),
                Flammability: ClampFlammability(resource.Flammability),
                Terrain: 1,
                HeatLoss: heatLoss);
        }

        if (source.Building is { } building)
        {
            yield return new MaterialContribution(
                Priority: 3,
                Fuel: ClampFuel(building.Fuel),
                Flammability: ClampFlammability(building.Flammability),
                Terrain: 1,
                HeatLoss: ClampHeatLoss(building.HeatLoss));
        }
    }

    private static int SelectWater(IEnumerable<TimberbornCellSource> sources)
    {
        return sources
            .Select(static source => Math.Max((int)(source.Terrain?.Wetness ?? 0), source.Water?.Water ?? 0))
            .DefaultIfEmpty(0)
            .Max(static water => ClampWater(water));
    }

    private static int ClampFuel(byte fuel) => Math.Clamp((int)fuel, 0, 15);

    private static int ClampFlammability(byte flammability) => Math.Clamp((int)flammability, 0, 3);

    private static int ClampWater(byte water) => Math.Clamp((int)water, 0, 3);

    private static int ClampWater(int water) => Math.Clamp(water, 0, 3);

    private static int ClampHeatLoss(byte heatLoss) => Math.Clamp((int)heatLoss, 0, 7);

    private readonly record struct IndexedSource(int CellIndex, TimberbornCellSource Source);

    private readonly record struct MaterialContribution(
        int Priority,
        int Fuel,
        int Flammability,
        int Terrain,
        int HeatLoss)
    {
        public static readonly MaterialContribution Empty = new(
            Priority: 0,
            Fuel: 0,
            Flammability: 0,
            Terrain: 0,
            HeatLoss: 7);
    }
}

public readonly record struct TimberbornMappedCell(int CellIndex, ushort PackedCell);
