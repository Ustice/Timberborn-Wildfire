namespace Wildfire.Unity;

public static class ComputeGridValidation
{
    public static void RequirePositiveDimension(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Fire grid dimensions must be positive.");
        }
    }

    public static int GetCheckedCellCount(int width, int height, int depth)
    {
        try
        {
            return checked(width * height * depth);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Fire grid dimensions produce too many cells.");
        }
    }

    public static void RequireCellCount(ComputeGridDimensions dimensions, int actualCellCount, string paramName)
    {
        if (actualCellCount != dimensions.CellCount)
        {
            throw new ArgumentException(
                $"Initial cell count must match grid cell count. Expected {dimensions.CellCount}, got {actualCellCount}.",
                paramName);
        }
    }
}
