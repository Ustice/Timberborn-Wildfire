namespace Wildfire.Timberborn.Simulation;

public interface ITimberbornTransportFieldReader
{
    // Returns a synchronous snapshot of the current GPU transport field in grid index order.
    IReadOnlyList<uint> ReadTransportFields();
}
