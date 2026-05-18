using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.Cutting;
using Timberborn.EntitySystem;
using Timberborn.Forestry;
using Timberborn.Gathering;
using Timberborn.SelectionSystem;
using Timberborn.Yielding;
using UnityEngine;
using Wildfire.Core;
using System.Reflection;

namespace Wildfire.Timberborn;

public sealed record TimberbornLiveCropBurnDamageTargets(
    TimberbornBurnDamageDescriptorCatalog DescriptorCatalog,
    IReadOnlyList<TimberbornBurnDamageDescriptor> Descriptors,
    IReadOnlyList<TimberbornBurnDamageTargetRegistration> Registrations);

public sealed record TimberbornQaSelectedCropTarget(
    int CellIndex,
    int X,
    int Y,
    int Z,
    string TargetSource);

public sealed record TimberbornSelectedCropTargetDiagnostics(
    string SelectionState,
    string SelectedObjectType,
    string SelectedObjectName,
    string BlockObjectName,
    int ComponentCount,
    string ComponentTypes,
    int OccupiedCellCount,
    int OccupiedInGridCellCount,
    string YieldDebug,
    string FailureReason)
{
    public static readonly TimberbornSelectedCropTargetDiagnostics Empty = new(
        "not_checked",
        "",
        "",
        "",
        0,
        "",
        0,
        0,
        "",
        "");

    public static TimberbornSelectedCropTargetDiagnostics DescribeSelection(
        string selectionState,
        string selectedObjectType,
        string selectedObjectName,
        string? blockObjectName,
        IEnumerable<object> components,
        IEnumerable<TimberbornCellCoordinates> occupiedCells,
        FireGrid grid,
        string? failureReason = null)
    {
        object[] componentArray = components?.ToArray() ?? Array.Empty<object>();
        TimberbornCellCoordinates[] occupiedCellArray = occupiedCells?.ToArray() ?? Array.Empty<TimberbornCellCoordinates>();
        int occupiedInGridCellCount = occupiedCellArray.Count(cell => IsInsideGrid(cell, grid));

        return new TimberbornSelectedCropTargetDiagnostics(
            selectionState,
            selectedObjectType,
            selectedObjectName,
            blockObjectName ?? "",
            componentArray.Length,
            JoinDebugTokens(componentArray
                .Select(static component => component.GetType().FullName ?? component.GetType().Name)
                .Distinct(StringComparer.Ordinal)
                .Take(16)),
            occupiedCellArray.Length,
            occupiedInGridCellCount,
            JoinDebugTokens(componentArray
                .SelectMany(DescribeYieldDebug)
                .Distinct(StringComparer.Ordinal)
                .Take(12)),
            failureReason ?? "");
    }

    private static IEnumerable<string> DescribeYieldDebug(object component)
    {
        string componentType = component.GetType().FullName ?? component.GetType().Name;

        return DescribeYieldDebug(component, componentType, depth: 0, new HashSet<object>());
    }

    private static IEnumerable<string> DescribeYieldDebug(
        object? value,
        string source,
        int depth,
        HashSet<object> visited)
    {
        if (value is null || depth > 3 || !visited.Add(value))
        {
            return Array.Empty<string>();
        }

        object? yieldValue = ReadProperty(value, "Yield");
        string[] directYieldDebug = yieldValue is null || ReferenceEquals(yieldValue, value)
            ? Array.Empty<string>()
            : DescribeYieldValue(source, yieldValue);

        object? yielderValue = ReadProperty(value, "Yielder") ?? ReadProperty(value, "YielderSpec");
        string[] decoratedYieldDebug = yielderValue is null || ReferenceEquals(yielderValue, value)
            ? Array.Empty<string>()
            : DescribeYieldDebug(yielderValue, $"{source}:{yielderValue.GetType().Name}", depth + 1, visited).ToArray();

        return directYieldDebug.Concat(decoratedYieldDebug);
    }

    private static string[] DescribeYieldValue(string source, object yieldValue)
    {
        string resourceId = Convert.ToString(ReadProperty(yieldValue, "GoodId") ?? ReadProperty(yieldValue, "Id"),
            System.Globalization.CultureInfo.InvariantCulture) ?? "";
        int amount = ConvertToNonNegativeInt(ReadProperty(yieldValue, "Amount"));
        if (string.IsNullOrWhiteSpace(resourceId) && amount == 0)
        {
            return Array.Empty<string>();
        }

        return new[] { $"{source}:{yieldValue.GetType().Name}:{SafeToken(resourceId)}:{amount}" };
    }

    private static object? ReadProperty(object value, string propertyName)
    {
        try
        {
            return value
                .GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(value);
        }
        catch
        {
            return null;
        }
    }

    private static int ConvertToNonNegativeInt(object? value)
    {
        try
        {
            return value is null
                ? 0
                : Math.Max(0, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch
        {
            return 0;
        }
    }

    private static string JoinDebugTokens(IEnumerable<string> tokens)
    {
        return string.Join("|", tokens.Where(static token => !string.IsNullOrWhiteSpace(token)));
    }

    private static string SafeToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Replace(' ', '_').Replace('"', '\'');
    }

    private static bool IsInsideGrid(TimberbornCellCoordinates coordinates, FireGrid grid)
    {
        return coordinates.X >= 0 &&
            coordinates.Y >= 0 &&
            coordinates.Z >= 0 &&
            coordinates.X < grid.Width &&
            coordinates.Y < grid.Height &&
            coordinates.Z < grid.Depth;
    }
}

public interface ITimberbornQaSelectedCropTargetProvider
{
    TimberbornSelectedCropTargetDiagnostics LastDiagnostics { get; }

    TimberbornLiveCropBurnDamageTargets CollectSelectedTargets(FireGrid grid);

    TimberbornQaSelectedCropTarget? FindSelectedTarget(
        FireGrid grid,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> burnDamageTargets);
}

public sealed class TimberbornSelectedCropTargetProvider : ITimberbornQaSelectedCropTargetProvider
{
    private readonly EntitySelectionService _selectionService;

    public TimberbornSelectedCropTargetProvider(EntitySelectionService selectionService)
    {
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
    }

    public TimberbornSelectedCropTargetDiagnostics LastDiagnostics { get; private set; } =
        TimberbornSelectedCropTargetDiagnostics.Empty;

    public TimberbornLiveCropBurnDamageTargets CollectSelectedTargets(FireGrid grid)
    {
        if (!_selectionService.IsAnythingSelected || _selectionService.SelectedObject is not { } selectedObject)
        {
            return TimberbornLiveCropBurnDamageTargetCollector.CollectCandidates(
                grid,
                Array.Empty<TimberbornLiveCropBurnDamageCandidate>());
        }

        if (!selectedObject.TryGetComponent(out BlockObject blockObject))
        {
            return TimberbornLiveCropBurnDamageTargetCollector.CollectCandidates(
                grid,
                Array.Empty<TimberbornLiveCropBurnDamageCandidate>());
        }

        return TimberbornLiveCropBurnDamageTargetCollector.CollectSelectedObject(
            grid,
            $"selected_crop_harvestable:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(blockObject)}",
            blockObject.Name,
            selectedObject.AllComponents,
            OccupiedCoordinates(blockObject)
                .Select(static coordinates => new TimberbornCellCoordinates(
                    coordinates.x,
                    coordinates.y,
                    coordinates.z))
                .ToArray());
    }

    public TimberbornQaSelectedCropTarget? FindSelectedTarget(
        FireGrid grid,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> burnDamageTargets)
    {
        if (burnDamageTargets is null)
        {
            throw new ArgumentNullException(nameof(burnDamageTargets));
        }

        if (!_selectionService.IsAnythingSelected)
        {
            LastDiagnostics = TimberbornSelectedCropTargetDiagnostics.Empty with
            {
                SelectionState = "none",
            };
            return null;
        }

        SelectableObject selectedObject = _selectionService.SelectedObject ??
            throw new InvalidOperationException("Timberborn reports a selection but no selected object was available.");
        if (!selectedObject.TryGetComponent(out BlockObject blockObject))
        {
            LastDiagnostics = DescribeSelectedObject(
                grid,
                selectedObject,
                blockObject: null,
                "missing_block_object",
                "Selected_Timberborn_object_is_not_block-backed.");
            throw new InvalidOperationException("Selected Timberborn object is not a block-backed crop or harvestable.");
        }

        HashSet<int> selectedCellIndices = OccupiedCoordinates(blockObject)
            .Where(coordinates => IsInsideGrid(coordinates, grid))
            .Select(coordinates => grid.ToIndex(coordinates.x, coordinates.y, coordinates.z))
            .ToHashSet();
        LastDiagnostics = DescribeSelectedObject(grid, selectedObject, blockObject, "selected", "");

        try
        {
            TimberbornQaSelectedCropTarget target = ResolveSelectedTarget(grid, burnDamageTargets, selectedCellIndices);
            LastDiagnostics = LastDiagnostics with
            {
                SelectionState = "resolved",
            };
            return target;
        }
        catch (InvalidOperationException exception)
        {
            LastDiagnostics = LastDiagnostics with
            {
                SelectionState = "unresolved",
                FailureReason = exception.Message,
            };
            throw;
        }
    }

    public static TimberbornQaSelectedCropTarget ResolveSelectedTarget(
        FireGrid grid,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> burnDamageTargets,
        IEnumerable<int> selectedCellIndices)
    {
        if (burnDamageTargets is null)
        {
            throw new ArgumentNullException(nameof(burnDamageTargets));
        }

        if (selectedCellIndices is null)
        {
            throw new ArgumentNullException(nameof(selectedCellIndices));
        }

        HashSet<int> selectedCells = selectedCellIndices
            .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
            .ToHashSet();
        if (selectedCells.Count == 0)
        {
            throw new InvalidOperationException("Selected Timberborn crop or harvestable has no occupied cells inside the fire grid.");
        }

        TimberbornBurnDamageTargetState selectedTarget = burnDamageTargets.Values
            .Where(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable)
            .Where(target => target.OwnedCellIndices.Any(selectedCells.Contains))
            .OrderBy(static target => target.OwnedCellIndices.Count)
            .ThenBy(static target => target.TargetKey.StableId, StringComparer.Ordinal)
            .Select(static target => (TimberbornBurnDamageTargetState?)target)
            .FirstOrDefault() ??
            throw new InvalidOperationException("Selected Timberborn object did not resolve to a registered crop or harvestable burn-damage target.");
        int cellIndex = selectedTarget.OwnedCellIndices
            .Where(selectedCells.Contains)
            .OrderBy(static index => index)
            .First();
        (int x, int y, int z) = grid.FromIndex(cellIndex);

        return new TimberbornQaSelectedCropTarget(
            cellIndex,
            x,
            y,
            z,
            "selected_crop_target");
    }

    private static IEnumerable<Vector3Int> OccupiedCoordinates(BlockObject blockObject)
    {
        return blockObject.PositionedBlocks.GetOccupiedCoordinates();
    }

    private static TimberbornSelectedCropTargetDiagnostics DescribeSelectedObject(
        FireGrid grid,
        SelectableObject selectedObject,
        BlockObject? blockObject,
        string selectionState,
        string failureReason)
    {
        object[] components = selectedObject.AllComponents.ToArray();
        TimberbornCellCoordinates[] occupiedCells = blockObject is null
            ? Array.Empty<TimberbornCellCoordinates>()
            : OccupiedCoordinates(blockObject)
                .Select(static coordinates => new TimberbornCellCoordinates(coordinates.x, coordinates.y, coordinates.z))
                .ToArray();

        return TimberbornSelectedCropTargetDiagnostics.DescribeSelection(
            selectionState,
            selectedObject.GetType().FullName ?? selectedObject.GetType().Name,
            selectedObject.Name,
            blockObject?.Name,
            components,
            occupiedCells,
            grid,
            failureReason);
    }

    private static bool IsInsideGrid(Vector3Int coordinates, FireGrid grid)
    {
        return coordinates.x >= 0 &&
            coordinates.y >= 0 &&
            coordinates.z >= 0 &&
            coordinates.x < grid.Width &&
            coordinates.y < grid.Height &&
            coordinates.z < grid.Depth;
    }
}

public static class TimberbornLiveCropBurnDamageTargetCollector
{
    private const int OwnershipPriority = 90;

    private static readonly string[] CropNameTokens =
    {
        "Canola",
        "Carrot",
        "Cassava",
        "Cattail",
        "Coffee",
        "Corn",
        "Dandelion",
        "Eggplant",
        "Kohlrabi",
        "Potato",
        "Soybean",
        "Spadderdock",
        "Sunflower",
        "Wheat",
    };

    private static readonly string[] TreeNameTokens =
    {
        "Birch",
        "ChestnutTree",
        "Mangrove",
        "Maple",
        "Oak",
        "Pine",
        "Tree",
    };

    private static readonly TimberbornResourceFuelCatalog ResourceFuelCatalog = TimberbornResourceFuelCatalog.Default;
    private static readonly TimberbornBurnableCatalog BurnableCatalog = TimberbornBurnableCatalog.Default;

    public static TimberbornLiveCropBurnDamageTargets Collect(EntityRegistry entityRegistry, FireGrid grid)
    {
        if (entityRegistry is null)
        {
            throw new ArgumentNullException(nameof(entityRegistry));
        }

        TargetBuildResult[] targets = entityRegistry.Entities
            .Select(entity => TryCreateCandidate(entity, grid))
            .Where(static target => target.HasValue)
            .Select(static target => target!.Value)
            .ToArray();
        TimberbornBurnDamageDescriptor[] descriptors = targets
            .GroupBy(static target => target.Descriptor.SpecId, StringComparer.Ordinal)
            .Select(static group => group.First().Descriptor)
            .ToArray();

        return new TimberbornLiveCropBurnDamageTargets(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            descriptors,
            targets.Select(static target => target.Registration).ToArray());
    }

    public static TimberbornLiveCropBurnDamageTargets CollectCandidates(
        FireGrid grid,
        IEnumerable<TimberbornLiveCropBurnDamageCandidate> candidates)
    {
        if (candidates is null)
        {
            throw new ArgumentNullException(nameof(candidates));
        }

        TargetBuildResult[] targets = candidates
            .Select(candidate => BuildTarget(candidate, grid))
            .Where(static target => target.HasValue)
            .Select(static target => target!.Value)
            .ToArray();
        TimberbornBurnDamageDescriptor[] descriptors = targets
            .GroupBy(static target => target.Descriptor.SpecId, StringComparer.Ordinal)
            .Select(static group => group.First().Descriptor)
            .ToArray();

        return new TimberbornLiveCropBurnDamageTargets(
            new TimberbornBurnDamageDescriptorCatalog(descriptors),
            descriptors,
            targets.Select(static target => target.Registration).ToArray());
    }

    public static TimberbornLiveCropBurnDamageTargets CollectSelectedObject(
        FireGrid grid,
        string stableId,
        string blockObjectName,
        IEnumerable<object> components,
        IReadOnlyList<TimberbornCellCoordinates> ownedCells)
    {
        if (components is null)
        {
            throw new ArgumentNullException(nameof(components));
        }

        object[] componentArray = components.ToArray();
        TimberbornLiveCropBurnDamageCandidate[] candidates =
            TryCreateCandidateFromComponentShape(stableId, blockObjectName, componentArray, ownedCells, out TimberbornLiveCropBurnDamageCandidate candidate)
                ? new[] { candidate }
                : Array.Empty<TimberbornLiveCropBurnDamageCandidate>();

        return CollectCandidates(grid, candidates);
    }

    private static TargetBuildResult? TryCreateCandidate(BaseComponent entity, FireGrid grid)
    {
        if (!entity.TryGetComponent(out BlockObject blockObject) ||
            !TrySelectLiveYield(entity, blockObject, out TimberbornLiveYieldSnapshot yield))
        {
            return null;
        }

        return BuildTarget(
            new TimberbornLiveCropBurnDamageCandidate(
                StableId: $"crop_harvestable:{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(blockObject)}",
                SpecId: blockObject.Name,
                ResourceId: yield.ResourceId,
                YieldAmount: yield.Amount,
                OwnedCells: OccupiedCoordinates(blockObject)
                    .Where(coordinates => IsInsideGrid(coordinates, grid))
                    .Select(static coordinates => new TimberbornCellCoordinates(coordinates.x, coordinates.y, coordinates.z))
                    .ToArray(),
                YieldSource: yield.Source),
            grid);
    }

    private static bool TrySelectLiveYield(BaseComponent entity, BlockObject blockObject, out TimberbornLiveYieldSnapshot yield)
    {
        if (entity.TryGetComponent(out TreeComponent _) || IsTreeName(blockObject.Name))
        {
            yield = default;
            return false;
        }

        if (entity.TryGetComponent(out Gatherable gatherable))
        {
            yield = CreateYieldSnapshot(gatherable.Yielder, TimberbornLiveYieldSource.Gatherable);
            return true;
        }

        if (entity.TryGetComponent(out Yielder directYielder) && IsCropName(blockObject.Name))
        {
            yield = CreateYieldSnapshot(directYielder, TimberbornLiveYieldSource.Crop);
            return true;
        }

        if (entity.TryGetComponent(out Cuttable cuttable) && IsCropName(blockObject.Name))
        {
            yield = CreateYieldSnapshot(cuttable.Yielder, TimberbornLiveYieldSource.Crop);
            return true;
        }

        yield = default;
        return false;
    }

    private static bool TryCreateCandidateFromComponentShape(
        string stableId,
        string blockObjectName,
        IReadOnlyList<object> components,
        IReadOnlyList<TimberbornCellCoordinates> ownedCells,
        out TimberbornLiveCropBurnDamageCandidate candidate)
    {
        candidate = default;
        if (string.IsNullOrWhiteSpace(stableId) ||
            string.IsNullOrWhiteSpace(blockObjectName) ||
            IsTreeName(blockObjectName) ||
            components.Any(IsTreeComponent))
        {
            return false;
        }

        TimberbornLiveYieldSnapshot? yield = components
            .Select(component => TryCreateYieldSnapshot(component, blockObjectName, out TimberbornLiveYieldSnapshot snapshot)
                ? (TimberbornLiveYieldSnapshot?)snapshot
                : null)
            .Where(static snapshot => snapshot.HasValue)
            .Select(static snapshot => snapshot!.Value)
            .OrderBy(static snapshot => snapshot.Source == TimberbornLiveYieldSource.Gatherable ? 0 : 1)
            .ThenByDescending(static snapshot => IsKnownHarvestableResource(snapshot.ResourceId))
            .Select(static snapshot => (TimberbornLiveYieldSnapshot?)snapshot)
            .FirstOrDefault();
        if (yield is not { } selectedYield)
        {
            return false;
        }

        candidate = new TimberbornLiveCropBurnDamageCandidate(
            stableId,
            blockObjectName,
            selectedYield.ResourceId,
            selectedYield.Amount,
            ownedCells,
            selectedYield.Source);
        return true;
    }

    private static bool TryCreateYieldSnapshot(
        object component,
        string blockObjectName,
        out TimberbornLiveYieldSnapshot snapshot)
    {
        snapshot = default;
        string componentName = component.GetType().FullName ?? component.GetType().Name;
        TimberbornLiveYieldSource source = SelectYieldSource(componentName, blockObjectName);
        if (source == TimberbornLiveYieldSource.Cuttable && !IsCropName(blockObjectName))
        {
            return false;
        }

        object? yielder = ReadProperty(component, "Yielder");
        object? yield = yielder is null
            ? ReadProperty(component, "Yield")
            : ReadProperty(yielder, "Yield") ?? ReadProperty(ReadProperty(yielder, "YielderSpec"), "Yield");
        if (yield is null)
        {
            return false;
        }

        string resourceId = Convert.ToString(
            ReadProperty(yield, "GoodId") ?? ReadProperty(yield, "Id"),
            System.Globalization.CultureInfo.InvariantCulture) ?? "";
        int amount = ConvertToNonNegativeInt(ReadProperty(yield, "Amount"));
        if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
        {
            return false;
        }

        snapshot = new TimberbornLiveYieldSnapshot(resourceId, amount, source);
        return true;
    }

    private static TargetBuildResult? BuildTarget(TimberbornLiveCropBurnDamageCandidate candidate, FireGrid grid)
    {
        string resourceId = candidate.ResourceId.Trim();
        string specId = string.IsNullOrWhiteSpace(candidate.SpecId)
            ? resourceId
            : candidate.SpecId.Trim();
        int amount = Math.Max(0, candidate.YieldAmount);
        TimberbornBurnableProfile burnableProfile = BurnableCatalog.Lookup(specId);
        bool isEntityBurnable = burnableProfile.Known && burnableProfile.IsBurnable;
        TimberbornCellCoordinates[] ownedCells = candidate.OwnedCells
            .Where(cell => IsInsideGrid(cell, grid))
            .Distinct()
            .ToArray();

        if (resourceId.Length == 0 ||
            ownedCells.Length == 0 ||
            !IsAcceptedCropBurnYield(candidate.YieldSource, specId, resourceId) ||
            (burnableProfile.Known && !burnableProfile.IsBurnable) ||
            (!isEntityBurnable && (
                amount <= 0 ||
                !ResourceFuelCatalog.Contains(resourceId) ||
                ResourceFuelCatalog.Lookup(resourceId).FuelValue == 0)))
        {
            return null;
        }

        if (isEntityBurnable)
        {
            amount = Math.Max(1, amount);
        }

        TimberbornBurnDamageTargetKind targetKind = IsCropName(specId) || IsCropName(resourceId)
            ? TimberbornBurnDamageTargetKind.Crop
            : TimberbornBurnDamageTargetKind.Resource;
        TimberbornBurnDamageDescriptor descriptor = new(
            specId,
            targetKind,
            TimberbornBurnMaterialKind.Organic,
            resourceYields: new[] { new TimberbornBurnDamageResourceStack(resourceId, amount) },
            burnableProfile: burnableProfile.Known ? burnableProfile : null);
        TimberbornBurnDamageTargetRegistration registration = new(
            new TimberbornBurnDamageTargetKey(candidate.StableId),
            specId,
            ownedCells,
            OwnershipPriority);

        return new TargetBuildResult(descriptor, registration);
    }

    private static string SelectResourceId(Yielder yielder)
    {
        string liveResourceId = yielder.Yield.GoodId;
        if (!string.IsNullOrWhiteSpace(liveResourceId))
        {
            return liveResourceId;
        }

        return yielder.YielderSpec?.Yield?.Id ?? "";
    }

    private static int SelectYieldAmount(Yielder yielder)
    {
        int liveAmount = yielder.Yield.Amount;
        if (liveAmount > 0)
        {
            return liveAmount;
        }

        return Math.Max(0, yielder.YielderSpec?.Yield?.Amount ?? 0);
    }

    private static object? ReadProperty(object? value, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return value
                .GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(value);
        }
        catch
        {
            return null;
        }
    }

    private static int ConvertToNonNegativeInt(object? value)
    {
        try
        {
            return value is null
                ? 0
                : Math.Max(0, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch
        {
            return 0;
        }
    }

    private static TimberbornLiveYieldSnapshot CreateYieldSnapshot(
        Yielder yielder,
        TimberbornLiveYieldSource source)
    {
        return new TimberbornLiveYieldSnapshot(SelectResourceId(yielder), SelectYieldAmount(yielder), source);
    }

    private static bool IsAcceptedCropBurnYield(
        TimberbornLiveYieldSource source,
        string specId,
        string resourceId)
    {
        bool isCrop = IsCropName(specId) || IsCropName(resourceId);
        if (IsTreeName(specId))
        {
            return false;
        }

        return source switch
        {
            TimberbornLiveYieldSource.Crop => isCrop,
            TimberbornLiveYieldSource.Gatherable => !TimberbornCropBurnTargetClassifier.IsTreeOrWoodResource(resourceId),
            TimberbornLiveYieldSource.Cuttable => isCrop,
            _ => isCrop ||
                (IsKnownHarvestableResource(resourceId) &&
                    !TimberbornCropBurnTargetClassifier.IsTreeOrWoodResource(resourceId)),
        };
    }

    private static bool IsKnownHarvestableResource(string resourceId)
    {
        return resourceId.Equals("Berries", StringComparison.OrdinalIgnoreCase);
    }

    private static TimberbornLiveYieldSource SelectYieldSource(string componentName, string blockObjectName)
    {
        if (componentName.Contains("Gatherable", StringComparison.OrdinalIgnoreCase))
        {
            return TimberbornLiveYieldSource.Gatherable;
        }

        if (componentName.Contains("Cuttable", StringComparison.OrdinalIgnoreCase))
        {
            return TimberbornLiveYieldSource.Cuttable;
        }

        return IsCropName(blockObjectName)
            ? TimberbornLiveYieldSource.Crop
            : TimberbornLiveYieldSource.CropOrHarvestable;
    }

    private static bool IsTreeComponent(object component)
    {
        string componentName = component.GetType().FullName ?? component.GetType().Name;
        return componentName.Contains("TreeComponent", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCropName(string name)
    {
        return CropNameTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTreeName(string name)
    {
        return TreeNameTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<Vector3Int> OccupiedCoordinates(BlockObject blockObject)
    {
        return blockObject.PositionedBlocks.GetOccupiedCoordinates();
    }

    private static bool IsInsideGrid(Vector3Int coordinates, FireGrid grid)
    {
        return coordinates.x >= 0 &&
            coordinates.y >= 0 &&
            coordinates.z >= 0 &&
            coordinates.x < grid.Width &&
            coordinates.y < grid.Height &&
            coordinates.z < grid.Depth;
    }

    private static bool IsInsideGrid(TimberbornCellCoordinates coordinates, FireGrid grid)
    {
        return coordinates.X >= 0 &&
            coordinates.Y >= 0 &&
            coordinates.Z >= 0 &&
            coordinates.X < grid.Width &&
            coordinates.Y < grid.Height &&
            coordinates.Z < grid.Depth;
    }

    private readonly record struct TargetBuildResult(
        TimberbornBurnDamageDescriptor Descriptor,
        TimberbornBurnDamageTargetRegistration Registration);

    private readonly record struct TimberbornLiveYieldSnapshot(
        string ResourceId,
        int Amount,
        TimberbornLiveYieldSource Source);
}

public enum TimberbornLiveYieldSource
{
    CropOrHarvestable = 0,
    Crop = 1,
    Gatherable = 2,
    Cuttable = 3,
}

public readonly record struct TimberbornLiveCropBurnDamageCandidate(
    string StableId,
    string SpecId,
    string ResourceId,
    int YieldAmount,
    IReadOnlyList<TimberbornCellCoordinates> OwnedCells,
    TimberbornLiveYieldSource YieldSource = TimberbornLiveYieldSource.CropOrHarvestable);

public static class TimberbornCropBurnTargetClassifier
{
    public static TimberbornCropBurnTargetRegistrationSummary SummarizeRegisteredTargets(
        IEnumerable<TimberbornBurnDamageTargetState> states)
    {
        TimberbornBurnDamageTargetState[] cropTargets = states
            .Where(IsCropOrHarvestable)
            .ToArray();

        return new TimberbornCropBurnTargetRegistrationSummary(
            cropTargets.Length,
            cropTargets
                .SelectMany(static state => state.OwnedCellIndices)
                .Distinct()
                .Count());
    }

    public static bool IsCropOrHarvestable(TimberbornBurnDamageTargetState state)
    {
        return state.TargetKind == TimberbornBurnDamageTargetKind.Crop ||
            (state.TargetKind == TimberbornBurnDamageTargetKind.Resource &&
                state.MaterialKind == TimberbornBurnMaterialKind.Organic &&
                state.AccountedResourceIds.All(resourceId => !IsTreeOrWoodResource(resourceId)));
    }

    public static bool IsTreeOrWoodResource(string resourceId)
    {
        return resourceId.Equals("Log", StringComparison.OrdinalIgnoreCase);
    }
}

public readonly record struct TimberbornCropBurnTargetRegistrationSummary(
    int TargetCount,
    int OwnedCellCount);
