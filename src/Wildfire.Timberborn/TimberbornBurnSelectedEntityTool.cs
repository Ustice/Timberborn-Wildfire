using Timberborn.BlockSystem;
using Timberborn.QuickNotificationSystem;
using Timberborn.SelectionSystem;
using Timberborn.ToolSystem;
using Timberborn.ToolSystemUI;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornBurnSelectedEntityTool : ITool, IToolDescriptor
{
    private const byte IgnitionHeat = TimberbornFireSystem.QaIgnitionHeat;

    private readonly TimberbornFireRuntime _fireRuntime;
    private readonly EntitySelectionService _selectionService;
    private readonly QuickNotificationService _quickNotificationService;
    private readonly ToolService _toolService;

    public TimberbornBurnSelectedEntityTool(
        TimberbornFireRuntime fireRuntime,
        EntitySelectionService selectionService,
        QuickNotificationService quickNotificationService,
        ToolService toolService)
    {
        _fireRuntime = fireRuntime ?? throw new ArgumentNullException(nameof(fireRuntime));
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        _quickNotificationService = quickNotificationService ??
            throw new ArgumentNullException(nameof(quickNotificationService));
        _toolService = toolService ?? throw new ArgumentNullException(nameof(toolService));
    }

    public void Enter()
    {
        try
        {
            TimberbornBurnSelectedEntityResult result = BurnSelectedEntity();
            _quickNotificationService.SendNotification($"Wildfire: burning {result.TargetName}.");
            Debug.Log(
                "wildfire_timberborn_burn_selected_entity_tool_queued " +
                $"target={TimberbornQaCommandBridge.FormatToken(result.TargetName)} " +
                $"cell_count={result.CellCount} " +
                $"first_cell_index={result.FirstCellIndex} " +
                $"x={result.X} " +
                $"y={result.Y} " +
                $"z={result.Z} " +
                $"set_heat={IgnitionHeat}");
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
            _toolService.SwitchToDefaultTool();
        }
    }

    public void Exit()
    {
    }

    public ToolDescription DescribeTool()
    {
        return new ToolDescription.Builder("Burn selected entity")
            .AddSection("Queues Wildfire ignition heat on the currently selected block-backed entity.")
            .Build();
    }

    private TimberbornBurnSelectedEntityResult BurnSelectedEntity()
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

        if (!_selectionService.IsAnythingSelected)
        {
            throw new InvalidOperationException("No Timberborn entity is selected.");
        }

        SelectableObject selectedObject = _selectionService.SelectedObject ??
            throw new InvalidOperationException("Timberborn selection did not expose a selected object.");
        if (!selectedObject.TryGetComponent(out BlockObject blockObject))
        {
            throw new InvalidOperationException("Selected Timberborn entity is not block-backed.");
        }

        FireGrid grid = new(state.Width.Value, state.Height.Value, state.Depth.Value);
        int[] cellIndices = SelectOccupiedCellIndices(
            grid,
            blockObject.PositionedBlocks.GetOccupiedCoordinates());
        if (cellIndices.Length == 0)
        {
            throw new InvalidOperationException("Selected Timberborn entity has no occupied cells inside the fire grid.");
        }

        foreach (int cellIndex in cellIndices)
        {
            _fireRuntime.RegisterChange(new FireSimChange(CellIndex: cellIndex, SetHeat: IgnitionHeat));
        }

        (int x, int y, int z) = grid.FromIndex(cellIndices[0]);
        return new TimberbornBurnSelectedEntityResult(
            blockObject.Name,
            cellIndices.Length,
            cellIndices[0],
            x,
            y,
            z);
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
}

public sealed record TimberbornBurnSelectedEntityResult(
    string TargetName,
    int CellCount,
    int FirstCellIndex,
    int X,
    int Y,
    int Z);
