using Timberborn.SelectionSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tools;

public sealed class TimberbornSelectedTreeTargetProvider : ITimberbornQaSelectedTreeTargetProvider
{
    private readonly EntitySelectionService _selectionService;

    public TimberbornSelectedTreeTargetProvider(EntitySelectionService selectionService)
    {
        _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
    }

    public TimberbornImportedFieldTarget FindSelectedTreeTarget(
        FireGrid grid,
        IReadOnlyList<TimberbornImportedFieldTarget> importedTargets)
    {
        if (importedTargets is null)
        {
            throw new ArgumentNullException(nameof(importedTargets));
        }

        if (!_selectionService.IsAnythingSelected)
        {
            throw new InvalidOperationException("No Timberborn entity is selected.");
        }

        SelectableObject selectedObject = _selectionService.SelectedObject ??
            throw new InvalidOperationException("Timberborn selection did not expose a selected object.");
        HashSet<int> selectedCells = TimberbornEntityComponentCells.OccupiedCoordinates(selectedObject)
            .Where(coordinates => TimberbornEntityComponentCells.IsInsideGrid(coordinates, grid))
            .Select(coordinates => ToCellIndex(grid, coordinates))
            .ToHashSet();

        if (selectedCells.Count == 0)
        {
            throw new InvalidOperationException("Selected Timberborn entity has no occupied cells inside the fire grid.");
        }

        return importedTargets
            .Where(target => selectedCells.Contains(target.CellIndex))
            .Where(static target => target.MaterialClass == WildfireMaterialClass.Tree)
            .OrderBy(static target => target.CellIndex)
            .Select(static target => (TimberbornImportedFieldTarget?)target)
            .FirstOrDefault() ??
            throw new InvalidOperationException(
                "Selected Timberborn entity did not resolve to an imported tree cell.");
    }

    private static int ToCellIndex(FireGrid grid, Vector3Int coordinates)
    {
        return coordinates.x +
            coordinates.y * grid.Width +
            coordinates.z * grid.Width * grid.Height;
    }
}
