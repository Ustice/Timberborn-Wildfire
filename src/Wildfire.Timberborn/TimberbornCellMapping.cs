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
    TimberbornBuildingMaterialKind Kind = TimberbornBuildingMaterialKind.WoodLike);

public readonly record struct TimberbornResourceCell(byte Fuel, byte Flammability, TimberbornResourceKind Kind);

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
    TimberbornWaterCell? Water = null,
    WildfireMaterialClass MaterialClass = WildfireMaterialClass.Empty,
    uint CompanionTargetId = 0);

public sealed class TimberbornTerrainAdapter
{
    public static byte QuantizeSoilMoisture(float soilMoisture)
    {
        if (float.IsNaN(soilMoisture) || soilMoisture <= 0f)
        {
            return 0;
        }

        if (float.IsPositiveInfinity(soilMoisture))
        {
            return 3;
        }

        return (byte)Math.Clamp((int)Math.Floor(soilMoisture) / 4, 0, 3);
    }

    public TimberbornCellSource CreateSource(int x, int y, int z, bool isSolid, byte wetness = 0)
    {
        WildfireMaterialFieldProfile profile = WildfireMaterialFieldSchema.Default.Lookup(WildfireMaterialClass.Terrain);
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Terrain: new TimberbornTerrainCell(isSolid, wetness),
            MaterialClass: isSolid ? profile.MaterialClass : WildfireMaterialClass.Empty);
    }
}

public sealed class TimberbornBuildingAdapter
{
    public const byte WoodLikeFuel = 15;
    public const byte WoodLikeFlammability = 1;
    public const byte NonBurnableFuel = 0;
    public const byte NonBurnableFlammability = 0;

    private readonly TimberbornBurnableCatalog _burnableCatalog;

    public TimberbornBuildingAdapter()
        : this(TimberbornBurnableCatalog.Default)
    {
    }

    public TimberbornBuildingAdapter(TimberbornBurnableCatalog burnableCatalog)
    {
        _burnableCatalog = burnableCatalog ?? throw new ArgumentNullException(nameof(burnableCatalog));
    }

    public TimberbornCellSource CreateSource(int x, int y, int z, byte fuel, byte flammability)
    {
        return CreateSource(x, y, z, fuel, flammability, TimberbornBuildingMaterialKind.WoodLike);
    }

    public TimberbornCellSource CreateSource(
        int x,
        int y,
        int z,
        byte fuel,
        byte flammability,
        TimberbornBuildingMaterialKind kind,
        uint companionTargetId = 0)
    {
        WildfireMaterialClass materialClass = kind == TimberbornBuildingMaterialKind.NonBurnable
            ? WildfireMaterialClass.Infrastructure
            : WildfireMaterialClass.Building;
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Building: new TimberbornBuildingCell(fuel, flammability, kind),
            MaterialClass: materialClass,
            CompanionTargetId: companionTargetId);
    }

    public TimberbornCellSource CreateWoodLikeSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            WoodLikeFuel,
            WoodLikeFlammability,
            TimberbornBuildingMaterialKind.WoodLike);
    }

    public TimberbornCellSource CreateBuildingSource(int x, int y, int z, string specId, uint companionTargetId = 0)
    {
        TimberbornBurnableProfile profile = _burnableCatalog.Lookup(specId);
        if (!profile.Known)
        {
            return CreateWoodLikeSource(x, y, z) with { CompanionTargetId = companionTargetId };
        }

        TimberbornBuildingMaterialKind kind = profile.IsBurnable
            ? TimberbornBuildingMaterialKind.WoodLike
            : TimberbornBuildingMaterialKind.NonBurnable;
        return CreateSource(
            x,
            y,
            z,
            profile.FuelValue,
            profile.Flammability,
            kind,
            companionTargetId);
    }

    public TimberbornCellSource CreateNonBurnableSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            NonBurnableFuel,
            NonBurnableFlammability,
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
    public const byte VegetationFuel = 10;
    public const byte VegetationFlammability = 3;

    private readonly TimberbornResourceFuelCatalog _resourceFuelCatalog;
    private readonly TimberbornBurnableCatalog _burnableCatalog;

    public TimberbornResourceAdapter()
        : this(TimberbornResourceFuelCatalog.Default, TimberbornBurnableCatalog.Default)
    {
    }

    public TimberbornResourceAdapter(TimberbornResourceFuelCatalog resourceFuelCatalog)
        : this(resourceFuelCatalog, TimberbornBurnableCatalog.Default)
    {
    }

    public TimberbornResourceAdapter(
        TimberbornResourceFuelCatalog resourceFuelCatalog,
        TimberbornBurnableCatalog burnableCatalog)
    {
        _resourceFuelCatalog = resourceFuelCatalog ?? throw new ArgumentNullException(nameof(resourceFuelCatalog));
        _burnableCatalog = burnableCatalog ?? throw new ArgumentNullException(nameof(burnableCatalog));
    }

    public TimberbornCellSource CreateSource(
        int x,
        int y,
        int z,
        byte fuel,
        byte flammability,
        TimberbornResourceKind kind)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Resource: new TimberbornResourceCell(fuel, flammability, kind));
    }

    public TimberbornCellSource CreateStockpileResourceSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            StockpileResourceFuel,
            StockpileResourceFlammability,
            TimberbornResourceKind.StockpileResource,
            WildfireMaterialClass.Storage);
    }

    public TimberbornCellSource CreateStockpileResourceSource(int x, int y, int z, string resourceId, uint companionTargetId = 0)
    {
        TimberbornResourceFuelProfile profile = _resourceFuelCatalog.Lookup(resourceId);
        return CreateSource(
            x,
            y,
            z,
            profile.FuelValue,
            profile.Flammability,
            TimberbornResourceKind.StockpileResource,
            WildfireMaterialClass.Storage,
            companionTargetId);
    }

    public TimberbornResourceFuelProfile LookupFuelProfile(string resourceId)
    {
        return _resourceFuelCatalog.Lookup(resourceId);
    }

    public TimberbornBurnableProfile LookupBurnableProfile(string specId)
    {
        return _burnableCatalog.Lookup(specId);
    }

    public TimberbornCellSource CreateVegetationSource(int x, int y, int z)
    {
        return CreateSource(
            x,
            y,
            z,
            VegetationFuel,
            VegetationFlammability,
            TimberbornResourceKind.Vegetation,
            WildfireMaterialClass.Vegetation);
    }

    public TimberbornCellSource CreateTreeSource(int x, int y, int z, uint companionTargetId = 0)
    {
        WildfireMaterialFieldProfile profile = WildfireMaterialFieldSchema.Default.Lookup(WildfireMaterialClass.Tree);
        return CreateSource(
            x,
            y,
            z,
            profile.Fuel,
            profile.Flammability,
            TimberbornResourceKind.Vegetation,
            WildfireMaterialClass.Tree,
            companionTargetId);
    }

    public TimberbornCellSource CreateTreeSource(int x, int y, int z, string specId, uint companionTargetId = 0)
    {
        TimberbornBurnableProfile profile = _burnableCatalog.Lookup(specId);
        return profile.Known
            ? CreateSource(
                x,
                y,
                z,
                profile.FuelValue,
                profile.Flammability,
                TimberbornResourceKind.Vegetation,
                WildfireMaterialClass.Tree,
                companionTargetId)
            : CreateTreeSource(x, y, z, companionTargetId);
    }

    public TimberbornCellSource CreateCropSource(int x, int y, int z, uint companionTargetId = 0)
    {
        WildfireMaterialFieldProfile profile = WildfireMaterialFieldSchema.Default.Lookup(WildfireMaterialClass.Crop);
        return CreateSource(
            x,
            y,
            z,
            profile.Fuel,
            profile.Flammability,
            TimberbornResourceKind.Vegetation,
            WildfireMaterialClass.Crop,
            companionTargetId);
    }

    public TimberbornCellSource CreateCropSource(int x, int y, int z, string specId, uint companionTargetId = 0)
    {
        TimberbornBurnableProfile profile = _burnableCatalog.Lookup(specId);
        return profile.Known
            ? CreateSource(
                x,
                y,
                z,
                profile.FuelValue,
                profile.Flammability,
                TimberbornResourceKind.Vegetation,
                WildfireMaterialClass.Crop,
                companionTargetId)
            : CreateCropSource(x, y, z, companionTargetId);
    }

    public TimberbornCellSource CreateSource(
        int x,
        int y,
        int z,
        byte fuel,
        byte flammability,
        TimberbornResourceKind kind,
        WildfireMaterialClass materialClass,
        uint companionTargetId = 0)
    {
        return new TimberbornCellSource(
            new TimberbornCellCoordinates(x, y, z),
            Resource: new TimberbornResourceCell(fuel, flammability, kind),
            MaterialClass: materialClass,
            CompanionTargetId: companionTargetId);
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
            Water: new TimberbornWaterCell(water),
            MaterialClass: WildfireMaterialClass.Water);
    }
}

public sealed class TimberbornFireCellMapper
{
    public const ushort EmptyCell = 0x0000;

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

    public WildfireCompanionField[] CreateCompanionFields(FireGrid grid, IEnumerable<TimberbornCellSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        WildfireCompanionField[] fields = Enumerable.Repeat(WildfireCompanionField.Empty, grid.CellCount).ToArray();
        sources
            .Select(source => new IndexedSource(ToIndex(grid, source), source))
            .GroupBy(static indexedSource => indexedSource.CellIndex)
            .ToList()
            .ForEach(group =>
            {
                TimberbornCellSource[] cellSources = group.Select(static indexedSource => indexedSource.Source).ToArray();
                WildfireMaterialClass materialClass = SelectMaterialClass(cellSources);
                WildfireMaterialFieldProfile profile = WildfireMaterialFieldSchema.Default.Lookup(materialClass);
                uint targetId = cellSources
                    .Where(static source => source.CompanionTargetId != 0u)
                    .OrderByDescending(static source => SourcePriority(source))
                    .Select(static source => source.CompanionTargetId)
                    .DefaultIfEmpty(0u)
                    .First();
                fields[group.Key] = new WildfireCompanionField(
                    targetId,
                    WildfireCompanionFieldState.FromMaterialProfile(profile));
            });

        return fields;
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
            burningLevel: 0);
    }

    private static MaterialContribution SelectMaterial(IReadOnlyList<TimberbornCellSource> sources)
    {
        return sources
            .SelectMany(static source => EnumerateMaterialContributions(source))
            .DefaultIfEmpty(MaterialContribution.Empty)
            .OrderByDescending(static contribution => contribution.Priority)
            .ThenByDescending(static contribution => contribution.Fuel)
            .ThenByDescending(static contribution => contribution.Flammability)
            .First();
    }

    private static WildfireMaterialClass SelectMaterialClass(IReadOnlyList<TimberbornCellSource> sources)
    {
        return sources
            .Where(static source => source.MaterialClass != WildfireMaterialClass.Empty)
            .OrderByDescending(static source => SourcePriority(source))
            .ThenByDescending(static source => source.CompanionTargetId)
            .Select(static source => source.MaterialClass)
            .DefaultIfEmpty(WildfireMaterialClass.Empty)
            .First();
    }

    private static int SourcePriority(TimberbornCellSource source)
    {
        if (source.Building.HasValue)
        {
            return 3;
        }

        if (source.Resource.HasValue)
        {
            return 2;
        }

        if (source.Water.HasValue)
        {
            return 2;
        }

        return source.Terrain is { IsSolid: true } ? 1 : 0;
    }

    private static IEnumerable<MaterialContribution> EnumerateMaterialContributions(TimberbornCellSource source)
    {
        if (source.Terrain is { IsSolid: true })
        {
            yield return new MaterialContribution(
                Priority: 1,
                Fuel: 0,
                Flammability: 0,
                Terrain: 1);
        }

        if (source.Resource is { } resource)
        {
            yield return new MaterialContribution(
                Priority: 2,
                Fuel: ClampFuel(resource.Fuel),
                Flammability: ClampFlammability(resource.Flammability),
                Terrain: 1);
        }

        if (source.Building is { } building)
        {
            if (building.Kind == TimberbornBuildingMaterialKind.NonBurnable)
            {
                yield return new MaterialContribution(
                    Priority: 3,
                    Fuel: TimberbornBuildingAdapter.NonBurnableFuel,
                    Flammability: TimberbornBuildingAdapter.NonBurnableFlammability,
                    Terrain: 1);

                yield break;
            }

            yield return new MaterialContribution(
                Priority: 3,
                Fuel: ClampFuel(building.Fuel),
                Flammability: ClampFlammability(building.Flammability),
                Terrain: 1);
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

    private readonly record struct IndexedSource(int CellIndex, TimberbornCellSource Source);

    private readonly record struct MaterialContribution(
        int Priority,
        int Fuel,
        int Flammability,
        int Terrain)
    {
        public static readonly MaterialContribution Empty = new(
            Priority: 0,
            Fuel: 0,
            Flammability: 0,
            Terrain: 0);
    }
}

public readonly record struct TimberbornMappedCell(int CellIndex, ushort PackedCell);
