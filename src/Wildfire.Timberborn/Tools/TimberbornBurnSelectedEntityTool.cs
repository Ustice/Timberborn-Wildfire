using Timberborn.AreaSelectionSystem;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.BlockSystem;
using Timberborn.InputSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using System.Reflection;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tools;

public sealed class TimberbornBurnSelectedEntityTool : ITool, IToolDescriptor, IInputProcessor, ILoadableSingleton
{
    private const byte IgnitionHeat = TimberbornFireSystem.QaIgnitionHeat;
    private const string CursorKey = "DemolishResourcesCursor";
    private const string LargeCursorResourceName = "Wildfire.Timberborn.Assets.WildfireIgniteToolCursorLarge.png";
    private const string SmallCursorResourceName = "Wildfire.Timberborn.Assets.WildfireIgniteToolCursorSmall.png";

    private readonly TimberbornFireRuntime _fireRuntime;
    private readonly InputService _inputService;
    private readonly CursorService _cursorService;
    private readonly AreaBlockObjectPickerFactory _areaBlockObjectPickerFactory;
    private readonly BlockObjectSelectionDrawerFactory _blockObjectSelectionDrawerFactory;
    private readonly QuickNotificationService _quickNotificationService;
    private static Texture2D? _largeCursorTexture;
    private static Texture2D? _smallCursorTexture;
    private AreaBlockObjectPicker? _areaBlockObjectPicker;
    private BlockObjectSelectionDrawer? _blockObjectSelectionDrawer;

    public TimberbornBurnSelectedEntityTool(
        TimberbornFireRuntime fireRuntime,
        InputService inputService,
        CursorService cursorService,
        AreaBlockObjectPickerFactory areaBlockObjectPickerFactory,
        BlockObjectSelectionDrawerFactory blockObjectSelectionDrawerFactory,
        QuickNotificationService quickNotificationService)
    {
        _fireRuntime = fireRuntime ?? throw new ArgumentNullException(nameof(fireRuntime));
        _inputService = inputService ?? throw new ArgumentNullException(nameof(inputService));
        _cursorService = cursorService ?? throw new ArgumentNullException(nameof(cursorService));
        _areaBlockObjectPickerFactory = areaBlockObjectPickerFactory ??
            throw new ArgumentNullException(nameof(areaBlockObjectPickerFactory));
        _blockObjectSelectionDrawerFactory = blockObjectSelectionDrawerFactory ??
            throw new ArgumentNullException(nameof(blockObjectSelectionDrawerFactory));
        _quickNotificationService = quickNotificationService ??
            throw new ArgumentNullException(nameof(quickNotificationService));
    }

    public void Load()
    {
        _areaBlockObjectPicker = _areaBlockObjectPickerFactory.CreatePickingUpwards();
        _blockObjectSelectionDrawer = _blockObjectSelectionDrawerFactory.Create(
            new Color(0.95f, 0.48f, 0.16f, 0.85f),
            new Color(0.95f, 0.32f, 0.10f, 0.28f),
            new Color(0.95f, 0.32f, 0.10f, 0.55f));
    }

    public void Enter()
    {
        _inputService.AddInputProcessor(this);
        _cursorService.SetCursor(CursorKey);
        UnityEngine.Cursor.SetCursor(GetCursorTexture(), Vector2.zero, CursorMode.Auto);
    }

    public void Exit()
    {
        _blockObjectSelectionDrawer?.StopDrawing();
        _areaBlockObjectPicker?.Reset();
        _inputService.RemoveInputProcessor(this);
        _cursorService.ResetCursor();
    }

    public bool ProcessInput()
    {
        return GetAreaBlockObjectPicker().PickBlockObjects<BlockObject>(
            PreviewCallback,
            ActionCallback,
            ShowNoneCallback,
            static _ => true);
    }

    public ToolDescription DescribeTool()
    {
        return new ToolDescription.Builder("Ignite entity")
            .AddSection("Click or drag over block-backed entities to queue Wildfire ignition heat.")
            .Build();
    }

    private void PreviewCallback(
        IEnumerable<BlockObject> blockObjects,
        Vector3Int start,
        Vector3Int end,
        bool selectionStarted,
        bool selectingArea)
    {
        GetBlockObjectSelectionDrawer().Draw(blockObjects, start, end, selectingArea);
    }

    private void ActionCallback(
        IEnumerable<BlockObject> blockObjects,
        Vector3Int start,
        Vector3Int end,
        bool selectionStarted,
        bool selectingArea)
    {
        try
        {
            _blockObjectSelectionDrawer?.StopDrawing();
            TimberbornBurnSelectedEntityResult[] results = BurnBlockObjects(blockObjects, out int sustainedHeatDispatchTicks)
                .ToArray();
            if (results.Length == 0)
            {
                throw new InvalidOperationException("No block-backed entity with fire-grid cells was selected.");
            }

            int cellCount = results.Sum(static result => result.CellCount);
            _quickNotificationService.SendNotification(
                results.Length == 1
                    ? $"Wildfire: burning {results[0].TargetName}."
                    : $"Wildfire: burning {results.Length} entities.");
            Debug.Log(
                "wildfire_timberborn_burn_selected_entity_tool_queued " +
                $"target_count={results.Length} " +
                $"cell_count={cellCount} " +
                $"first_target={TimberbornQaCommandBridge.FormatToken(results[0].TargetName)} " +
                $"first_cell_index={results[0].FirstCellIndex} " +
                $"x={results[0].X} " +
                $"y={results[0].Y} " +
                $"z={results[0].Z} " +
                $"set_heat={IgnitionHeat} " +
                $"sustained_heat_dispatch_ticks={sustainedHeatDispatchTicks}");
        }
        catch (InvalidOperationException exception)
        {
            _quickNotificationService.SendWarningNotification($"Wildfire: {exception.Message}");
            Debug.LogWarning(
                "wildfire_timberborn_burn_selected_entity_tool_failed " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }
        catch (Exception exception)
        {
            _quickNotificationService.SendWarningNotification("Wildfire: could not burn selected entity.");
            Debug.LogWarning(
                "wildfire_timberborn_burn_selected_entity_tool_failed " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)} " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }
        finally
        {
            _blockObjectSelectionDrawer?.StopDrawing();
            _areaBlockObjectPicker?.Reset();
        }
    }

    private void ShowNoneCallback()
    {
        GetBlockObjectSelectionDrawer().StopDrawing();
    }

    private IEnumerable<TimberbornBurnSelectedEntityResult> BurnBlockObjects(
        IEnumerable<BlockObject> blockObjects,
        out int sustainedHeatDispatchTicks)
    {
        FireGrid grid = GetFireGrid();
        BlockObject[] distinctBlockObjects = blockObjects
            .Where(static blockObject => blockObject is not null)
            .Distinct()
            .ToArray();

        if (distinctBlockObjects.Length == 0)
        {
            throw new InvalidOperationException("No Timberborn entity was selected.");
        }

        TimberbornBurnSelectedEntityTarget[] burnTargets = distinctBlockObjects
            .Select(blockObject => CreateBurnTarget(grid, blockObject))
            .Where(static target => target is not null)
            .Cast<TimberbornBurnSelectedEntityTarget>()
            .ToArray();
        FireSimChange[] ignitionChanges = burnTargets
            .SelectMany(static target => target.CellIndices)
            .Distinct()
            .OrderBy(static cellIndex => cellIndex)
            .Select(static cellIndex => new FireSimChange(CellIndex: cellIndex, SetHeat: IgnitionHeat))
            .ToArray();
        sustainedHeatDispatchTicks = _fireRuntime.RegisterSustainedIgnitionChanges(
            ignitionChanges,
            "ignite_tool");
        return burnTargets.Select(static target => target.Result);
    }

    private TimberbornBurnSelectedEntityTarget? CreateBurnTarget(FireGrid grid, BlockObject blockObject)
    {
        int[] cellIndices = SelectOccupiedCellIndices(grid, blockObject.PositionedBlocks.GetOccupiedCoordinates());
        if (cellIndices.Length == 0)
        {
            return null;
        }

        (int x, int y, int z) = grid.FromIndex(cellIndices[0]);
        return new TimberbornBurnSelectedEntityTarget(
            cellIndices,
            new TimberbornBurnSelectedEntityResult(
                blockObject.Name,
                cellIndices.Length,
                cellIndices[0],
                x,
                y,
                z));
    }

    private FireGrid GetFireGrid()
    {
        TimberbornQaCommandState state = _fireRuntime.GetState();
        if (!state.WildfireEnabled)
        {
            throw new InvalidOperationException("Wildfire is disabled.");
        }

        if (!state.IsSimulatorIntegrated || state.Width is null || state.Height is null || state.Depth is null)
        {
            throw new InvalidOperationException("Wildfire runtime is not initialized yet.");
        }

        return new FireGrid(state.Width.Value, state.Height.Value, state.Depth.Value);
    }

    private AreaBlockObjectPicker GetAreaBlockObjectPicker()
    {
        return _areaBlockObjectPicker ??
            throw new InvalidOperationException("Wildfire ignite tool picker is not initialized.");
    }

    private BlockObjectSelectionDrawer GetBlockObjectSelectionDrawer()
    {
        return _blockObjectSelectionDrawer ??
            throw new InvalidOperationException("Wildfire ignite tool selection drawer is not initialized.");
    }

    private static int[] SelectOccupiedCellIndices(FireGrid grid, IEnumerable<Vector3Int> occupiedCoordinates)
    {
        if (occupiedCoordinates is null)
        {
            throw new ArgumentNullException(nameof(occupiedCoordinates));
        }

        return occupiedCoordinates
            .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
            .Select(coordinates => grid.ToIndex(coordinates.x, coordinates.y, coordinates.z))
            .Distinct()
            .OrderBy(static cellIndex => cellIndex)
            .ToArray();
    }

    private static Texture2D GetCursorTexture()
    {
        bool useSmallCursor = Application.platform is RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor;
        if (useSmallCursor)
        {
            return _smallCursorTexture ??= LoadCursorTexture(SmallCursorResourceName, 48, 48, "WildfireIgniteToolCursorSmall");
        }

        return _largeCursorTexture ??= LoadCursorTexture(LargeCursorResourceName, 64, 64, "WildfireIgniteToolCursorLarge");
    }

    private static Texture2D LoadCursorTexture(string resourceName, int width, int height, string textureName)
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Could not load embedded Wildfire ignite tool cursor {resourceName}.");
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);

        Texture2D texture = new(width, height, TextureFormat.RGBA32, mipChain: false)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        if (!texture.LoadImage(memoryStream.ToArray(), markNonReadable: false))
        {
            throw new InvalidOperationException($"Could not decode embedded Wildfire ignite tool cursor {resourceName}.");
        }

        return texture;
    }
}

public sealed record TimberbornBurnSelectedEntityResult(
    string TargetName,
    int CellCount,
    int FirstCellIndex,
    int X,
    int Y,
    int Z);

internal sealed record TimberbornBurnSelectedEntityTarget(
    int[] CellIndices,
    TimberbornBurnSelectedEntityResult Result);
