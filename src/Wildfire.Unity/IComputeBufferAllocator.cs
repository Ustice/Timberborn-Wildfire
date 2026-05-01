namespace Wildfire.Unity;

public interface IComputeBufferAllocator
{
    IComputeBufferHandle Allocate(string name, int count, int strideBytes);
}

public interface IComputeBufferHandle : IDisposable
{
    string Name { get; }

    int Count { get; }

    int StrideBytes { get; }

    void Upload(ReadOnlySpan<uint> values);
}
