using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornRuntimeInitializationTests
{
    [Fact]
    public void UnloadedWorldDoesNotReadOrImportUntilLoad()
    {
        TimberbornRuntimeInitialization lifecycle = new();
        int imports = 0;
        lifecycle.Update(() => throw new Exception("Must not read an unloaded world."), () => true, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.Unloaded, lifecycle.State);
        Assert.Equal(0, imports);

        lifecycle.Load();
        lifecycle.Update(() => new FireGrid(8, 8, 8), () => true, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.Ready, lifecycle.State);
        Assert.Equal(1, imports);
    }

    [Theory]
    [InlineData(0, 8, 8)]
    [InlineData(8, 0, 8)]
    [InlineData(8, 8, 0)]
    public void MissingMapDimensionsWaitWithoutEnumeratingEntities(int width, int height, int depth)
    {
        TimberbornRuntimeInitialization lifecycle = new();
        lifecycle.Load();
        lifecycle.Update(() => new FireGrid(width, height, depth),
            () => throw new Exception("Entities must not be enumerated yet."),
            _ => throw new Exception("World must not be imported yet."));
        Assert.Equal(TimberbornRuntimeInitializationState.WaitingForWorld, lifecycle.State);
        Assert.Null(lifecycle.Failure);

        lifecycle.Update(() => new FireGrid(8, 8, 8), () => true, _ => { });
        Assert.Equal(TimberbornRuntimeInitializationState.Ready, lifecycle.State);
    }

    [Fact]
    public void ReadinessWaitsRetryButSuccessImportsOnlyOnce()
    {
        TimberbornRuntimeInitialization lifecycle = new();
        lifecycle.Load();
        int imports = 0;
        FireGrid grid = new(8, 8, 8);
        lifecycle.Update(() => grid, () => false, _ => imports++);
        lifecycle.Update(() => grid, () => false, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.WaitingForWorld, lifecycle.State);
        Assert.Equal(0, imports);

        lifecycle.Update(() => grid, () => true, actualGrid =>
        {
            Assert.Equal(grid, actualGrid);
            Assert.Equal(TimberbornRuntimeInitializationState.Initializing, lifecycle.State);
            imports++;
        });
        lifecycle.Update(() => throw new Exception("A ready world must not be reread."), () => true, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.Ready, lifecycle.State);
        Assert.Equal(1, imports);
    }

    [Theory]
    [InlineData(2000001, 1, 1)]
    [InlineData(1024, 1024, 23)]
    [InlineData(65536, 65536, 23)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue)]
    public void UnsupportedMapsAreRejectedBeforeEntityEnumerationOrImport(int width, int height, int depth)
    {
        TimberbornRuntimeInitialization lifecycle = new();
        lifecycle.Load();
        int imports = 0;
        lifecycle.Update(() => new FireGrid(width, height, depth),
            () => throw new Exception("Unsupported worlds must not enumerate entities."), _ => imports++);
        lifecycle.Update(() => throw new Exception("Unsupported worlds must not be retried."), () => true, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.Unsupported, lifecycle.State);
        Assert.Equal(0, imports);
        Assert.Null(lifecycle.Failure);
        Assert.Equal(new FireGrid(width, height, depth), lifecycle.Grid);
    }

    [Fact]
    public void FailedInitializationRecordsOriginalFailureAndDoesNotImportAgain()
    {
        TimberbornRuntimeInitialization lifecycle = new();
        lifecycle.Load();
        Exception failure = new InvalidOperationException("GPU setup failed after importing the world.");
        int imports = 0;
        lifecycle.Update(() => new FireGrid(8, 8, 8), () => true, _ =>
        {
            imports++;
            throw failure;
        });
        lifecycle.Update(() => new FireGrid(8, 8, 8), () => true, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.Failed, lifecycle.State);
        Assert.Same(failure, lifecycle.Failure);
        Assert.Equal(1, imports);
    }

    [Fact]
    public void ReadinessProbeFailureIsTerminalUntilAnExplicitReload()
    {
        TimberbornRuntimeInitialization lifecycle = new();
        lifecycle.Load();
        Exception failure = new InvalidOperationException("Native API changed.");
        lifecycle.Update(() => new FireGrid(8, 8, 8), () => throw failure, _ => { });
        Assert.Equal(TimberbornRuntimeInitializationState.Failed, lifecycle.State);
        Assert.Same(failure, lifecycle.Failure);

        lifecycle.Load();
        Assert.Null(lifecycle.Failure);
        Assert.Null(lifecycle.Grid);
        Assert.Equal(0, lifecycle.ReadinessChecks);
        lifecycle.Update(() => new FireGrid(8, 8, 8), () => true, _ => { });
        Assert.Equal(TimberbornRuntimeInitializationState.Ready, lifecycle.State);
    }

    [Fact]
    public void ReloadAndUnloadDoNotKeepPreviousWorldReadiness()
    {
        TimberbornRuntimeInitialization lifecycle = new();
        int imports = 0;
        lifecycle.Load();
        lifecycle.Update(() => new FireGrid(8, 8, 8), () => true, _ => imports++);
        lifecycle.Unload();
        lifecycle.Update(() => new FireGrid(8, 8, 8), () => true, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.Unloaded, lifecycle.State);
        Assert.Null(lifecycle.Grid);

        lifecycle.Load();
        lifecycle.Update(() => new FireGrid(16, 16, 8), () => true, _ => imports++);
        Assert.Equal(TimberbornRuntimeInitializationState.Ready, lifecycle.State);
        Assert.Equal(new FireGrid(16, 16, 8), lifecycle.Grid);
        Assert.Equal(2, imports);
    }
}
