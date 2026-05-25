using Timberborn.AreaSelectionSystem;
using Timberborn.AreaSelectionSystemUI;
using Timberborn.BlockSystem;
using Timberborn.InputSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tools;

public sealed class TimberbornFertilizeTool : ITool, IToolDescriptor, IInputProcessor, ILoadableSingleton
{
    private const string CursorKey = "DemolishResourcesCursor";

    private readonly TimberbornFireRuntime _fireRuntime;
    private readonly TimberbornFertilizeDesignationService _designationService;
    private readonly InputService _inputService;
    private readonly CursorService _cursorService;
    private readonly AreaBlockObjectPickerFactory _areaBlockObjectPickerFactory;
    private readonly BlockObjectSelectionDrawerFactory _blockObjectSelectionDrawerFactory;
    private readonly QuickNotificationService _quickNotificationService;
    private AreaBlockObjectPicker? _areaBlockObjectPicker;
    private BlockObjectSelectionDrawer? _blockObjectSelectionDrawer;

    public TimberbornFertilizeTool(
        TimberbornFireRuntime fireRuntime,
        TimberbornFertilizeDesignationService designationService,
        InputService inputService,
        CursorService cursorService,
        AreaBlockObjectPickerFactory areaBlockObjectPickerFactory,
        BlockObjectSelectionDrawerFactory blockObjectSelectionDrawerFactory,
        QuickNotificationService quickNotificationService)
    {
        _fireRuntime = fireRuntime ?? throw new ArgumentNullException(nameof(fireRuntime));
        _designationService = designationService ?? throw new ArgumentNullException(nameof(designationService));
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
            new Color(0.34f, 0.64f, 0.22f, 0.85f),
            new Color(0.24f, 0.52f, 0.16f, 0.28f),
            new Color(0.24f, 0.52f, 0.16f, 0.55f));
    }

    public void Enter()
    {
        _inputService.AddInputProcessor(this);
        _cursorService.SetCursor(CursorKey);
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
        return new ToolDescription.Builder("Fertilize with fertile ash")
            .AddSection("Drag over soil to designate cells for fertile ash application beneath crops, trees, and bushes. Nearby storage with FertileAsh will be consumed by workers.")
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
        _blockObjectSelectionDrawer?.StopDrawing();
        try
        {
            FireGrid grid = GetFireGrid();
            bool remove = _inputService.IsKeyHeld("Cancel");
            int count = ApplyDesignations(grid, start, end, remove);

            if (count > 0)
            {
                _quickNotificationService.SendNotification(
                    remove
                        ? $"Wildfire: removed {count} fertile ash designations."
                        : $"Wildfire: designated {count} cells for fertile ash application.");
                Debug.Log(
                    "wildfire_fertilize_tool_applied " +
                    $"cell_count={count} " +
                    $"remove={remove.ToString().ToLowerInvariant()} " +
                    $"total_fertilize_designations={_designationService.FertilizeDesignationCount}");
            }
        }
        catch (InvalidOperationException exception)
        {
            _quickNotificationService.SendWarningNotification($"Wildfire: {exception.Message}");
            Debug.LogWarning(
                "wildfire_fertilize_tool_failed " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }
    }

    private void ShowNoneCallback()
    {
        GetBlockObjectSelectionDrawer().StopDrawing();
    }

    private int ApplyDesignations(FireGrid grid, Vector3Int start, Vector3Int end, bool remove)
    {
        int minX = Math.Min(start.x, end.x);
        int maxX = Math.Max(start.x, end.x);
        int minY = Math.Min(start.y, end.y);
        int maxY = Math.Max(start.y, end.y);
        int z = start.z;

        int count = 0;
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                if (x < 0 || x >= grid.Width || y < 0 || y >= grid.Height || z < 0 || z >= grid.Depth)
                {
                    continue;
                }

                int cellIndex = grid.ToIndex(x, y, z);
                if (remove)
                {
                    _designationService.RemoveFertilizeDesignation(cellIndex);
                }
                else
                {
                    _designationService.AddFertilizeDesignation(cellIndex);
                }

                count++;
            }
        }

        return count;
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
            throw new InvalidOperationException("Wildfire fertilize tool picker is not initialized.");
    }

    private BlockObjectSelectionDrawer GetBlockObjectSelectionDrawer()
    {
        return _blockObjectSelectionDrawer ??
            throw new InvalidOperationException("Wildfire fertilize tool selection drawer is not initialized.");
    }
}
