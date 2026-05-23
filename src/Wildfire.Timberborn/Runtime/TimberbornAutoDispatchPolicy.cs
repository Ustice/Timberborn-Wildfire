namespace Wildfire.Timberborn.Runtime;

public static class TimberbornAutoDispatchPolicy
{
    public const int CellLimit = 2_000_000;

    public static bool IsAllowedCellCount(int cellCount)
    {
        if (cellCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellCount), cellCount, "Cell count cannot be negative.");
        }

        return cellCount <= CellLimit;
    }
}
