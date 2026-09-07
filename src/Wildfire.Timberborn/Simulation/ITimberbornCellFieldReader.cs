namespace Wildfire.Timberborn.Simulation;

public interface ITimberbornCellFieldReader
{
    // Synchronous packed-cell observation in grid index order; separate from save capture.
    IReadOnlyList<ushort> ReadFireCells();
}
