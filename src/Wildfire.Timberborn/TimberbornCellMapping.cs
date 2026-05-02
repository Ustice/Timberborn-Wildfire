using Wildfire.Core;

namespace Wildfire.Timberborn;

public readonly record struct TimberbornCellCoordinates(int X, int Y, int Z);

public readonly record struct TimberbornCellFootprint(int X, int Y, int Z, int Width, int Height, int Depth)
{
    public IEnumerable<TimberbornCellCoordinates> EnumerateCoordinates()
    {
        if (Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Footprint width must be positive.");
        }

        if (Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), "Footprint height must be positive.");
        }

        if (Depth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Depth), "Footprint depth must be positive.");
        }

        int xStart = X;
        int yStart = Y;
        int zStart = Z;
        int width = Width;
        int height = Height;
        int depth = Depth;

        return Enumerable.Range(xStart, width)
            .SelectMany(x => Enumerable.Range(yStart, height)
                .SelectMany(y => Enumerable.Range(zStart, depth)
                    .Select(z => new TimberbornCellCoordinates(x, y, z))));
    }
}

public readonly record struct TimberbornTerrainCell(bool IsSolid, byte Wetness = 0);

public readonly record struct TimberbornBuildingCell(
    byte Fuel,
    byte Flammability,
    byte HeatLoss,
    TimberbornBuildingMaterialKind Kind = TimberbornBuildingMaterialKind.WoodLike);

public readonly record struct TimberbornResourceCell(byte Fuel, byte Flammability, byte HeatLoss, TimberbornResourceKind Kind);

public readonly record struct TimberbornWaterCell(byte Water);

public enum TimberbornBuildingMaterialKind
{
    WoodLike,
    NonBurnable,
}

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
    public const byte SolidTerrainHeatLoss = 6;

    public TimberbornCellSource CreateSource(int x, int y, int z, bool isSolid, byte wetness = 0)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Terrain: new TimberbornTerrainCell(isSolid, wetness));
    }
}

public sealed class TimberbornBuildingAdapter
{
    public const byte WoodLikeFuel = 15;
    public const byte WoodLikeFlammability = 1;
    public const byte WoodLikeHeatLoss = 3;
    public const byte NonBurnableFuel = 0;
    public const byte NonBurnableFlammability = 0;
    public const byte NonBurnableHeatLoss = 7;

    public TimberbornCellSource CreateSource(int x, int y, int z, byte fuel, byte flammability, byte heatLoss)
    {
        return CreateSource(x, y, z, fuel, flammability, heatLoss, TimberbornBuildingMaterialKind.WoodLike);
    }

    public TimberbornCellSource CreateSource(
        int x,
        int y,
        int z,
        byte fuel,
        byte flammability,
        byte heatLoss,
        TimberbornBuildingMaterialKind kind)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Building: new TimberbornBuildingCell(fuel, flammability, heatLoss, kind));
    }

    public TimberbornCellSource CreateWoodLikeSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            WoodLikeFuel,
            WoodLikeFlammability,
            WoodLikeHeatLoss,
            TimberbornBuildingMaterialKind.WoodLike);
    }

    public TimberbornCellSource CreateNonBurnableSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            NonBurnableFuel,
            NonBurnableFlammability,
            NonBurnableHeatLoss,
            TimberbornBuildingMaterialKind.NonBurnable);
    }

    public IEnumerable<TimberbornCellSource> CreateWoodLikeFootprintSources(TimberbornCellFootprint footprint)
    {
        return footprint.EnumerateCoordinates()
            .Select(coordinates => CreateWoodLikeSource(coordinates.X, coordinates.Y, coordinates.Z));
    }

    public IEnumerable<TimberbornCellSource> CreateNonBurnableFootprintSources(TimberbornCellFootprint footprint)
    {
        return footprint.EnumerateCoordinates()
            .Select(coordinates => CreateNonBurnableSource(coordinates.X, coordinates.Y, coordinates.Z));
    }
}

public sealed class TimberbornResourceAdapter
{
    public const byte StockpileResourceFuel = 8;
    public const byte StockpileResourceFlammability = 2;
    public const byte StockpileResourceHeatLoss = 3;
    public const byte VegetationFuel = 10;
    public const byte VegetationFlammability = 3;
    public const byte VegetationHeatLoss = 1;

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

    public TimberbornCellSource CreateStockpileResourceSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            StockpileResourceFuel,
            StockpileResourceFlammability,
            StockpileResourceHeatLoss,
            TimberbornResourceKind.StockpileResource);
    }

    public TimberbornCellSource CreateVegetationSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            VegetationFuel,
            VegetationFlammability,
            VegetationHeatLoss,
            TimberbornResourceKind.Vegetation);
    }

    public IEnumerable<TimberbornCellSource> CreateStockpileResourceSources(TimberbornCellFootprint footprint)
    {
        return footprint.EnumerateCoordinates()
            .Select(coordinates => CreateStockpileResourceSource(coordinates.X, coordinates.Y, coordinates.Z));
    }

    public IEnumerable<TimberbornCellSource> CreateVegetationSources(TimberbornCellFootprint footprint)
    {
        return footprint.EnumerateCoordinates()
            .Select(coordinates => CreateVegetationSource(coordinates.X, coordinates.Y, coordinates.Z));
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
        if (source.Terrain is { IsSolid: true })
        {
            yield return new MaterialContribution(
                Priority: 1,
                Fuel: 0,
                Flammability: 0,
                Terrain: 1,
                HeatLoss: TimberbornTerrainAdapter.SolidTerrainHeatLoss);
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
            if (building.Kind == TimberbornBuildingMaterialKind.NonBurnable)
            {
                yield return new MaterialContribution(
                    Priority: 3,
                    Fuel: TimberbornBuildingAdapter.NonBurnableFuel,
                    Flammability: TimberbornBuildingAdapter.NonBurnableFlammability,
                    Terrain: 1,
                    HeatLoss: TimberbornBuildingAdapter.NonBurnableHeatLoss);

                yield break;
            }

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
