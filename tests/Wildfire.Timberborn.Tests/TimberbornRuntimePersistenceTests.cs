namespace Wildfire.Core.Tests;

public sealed class TimberbornRuntimePersistenceTests
{
    [Theory]
    [InlineData(TimberbornRuntimeInitializationState.WaitingForWorld)]
    [InlineData(TimberbornRuntimeInitializationState.Failed)]
    [InlineData(TimberbornRuntimeInitializationState.Unsupported)]
    public void NonReadyWorldPreservesOriginalPayloadWithoutCapturingPartialState(TimberbornRuntimeInitializationState state)
    {
        TimberbornRuntimePersistence persistence = new();
        string original = TimberbornWildfirePersistenceCodec.Encode(TimberbornWildfirePersistenceSnapshot.Empty);
        persistence.Load(() => original);

        string? saved = persistence.EncodeForSave(state,
            () => throw new Exception("A partial runtime must not be captured."));

        Assert.Equal(original, saved);
        Assert.NotNull(persistence.LoadedSnapshot);
        Assert.Null(persistence.LoadFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unrecognized future or damaged save payload")]
    [InlineData("WF\t2")]
    [InlineData("WF\t1\nFUTURE\tdata")]
    [InlineData("WF\t1\nBURN\ttruncated")]
    [InlineData("WF\t1\nASH\t0\t1\t5\t0\t1\t2\t3")]
    [InlineData("WF\t1\nASH\t0\t1\t5\t0\t1\t2\t1\t0")]
    [InlineData("WF\t1\nBEAVER\tYmVhdmVyOjE=\t0\t2\t3\t1\t2")]
    [InlineData("WF\t1\nFIRE\t1\t1\t1\t0\tAA==\tAAAAAA==")]
    [InlineData("WF\t1\nFIRE\t1\t1\t1\t0\tAAA=\tAA==")]
    public void CorruptPayloadIsPreservedAndItsFailurePreventsInitialization(string original)
    {
        TimberbornRuntimePersistence persistence = new();
        TimberbornRuntimeInitialization lifecycle = new();
        lifecycle.Load();
        persistence.Load(() => original);
        Assert.NotNull(persistence.LoadFailure);
        lifecycle.Fail(persistence.LoadFailure);

        lifecycle.Update(() => throw new Exception("A corrupt save must not import a replacement world."), () => true, _ => { });
        string? saved = persistence.EncodeForSave(lifecycle.State,
            () => throw new Exception("A corrupt save must not be replaced with empty state."));

        Assert.Equal(TimberbornRuntimeInitializationState.Failed, lifecycle.State);
        Assert.Equal(original, saved);
        Assert.Null(persistence.LoadedSnapshot);
    }

    [Fact]
    public void LegacyAshAndBeaverRowsRemainReadable()
    {
        TimberbornRuntimePersistence persistence = new();
        const string legacy = "WF\t1\nASH\t0\t1\t5\t0\t1\t2\t1\nBEAVER\tYmVhdmVyOjE=\t0\t2\t3\t1\t1";
        persistence.Load(() => legacy);
        Assert.Null(persistence.LoadFailure);
        Assert.NotNull(persistence.LoadedSnapshot);
        Assert.Equal(5, Assert.Single(persistence.LoadedSnapshot.AshField.Entries).Strength);
        Assert.Equal("beaver:1", Assert.Single(persistence.LoadedSnapshot.BeaverBehavior.Entries).BeaverId);
        Assert.Equal(legacy, persistence.EncodeForSave(TimberbornRuntimeInitializationState.WaitingForWorld,
            () => throw new Exception("Legacy state must be preserved until ready.")));
    }

    [Fact]
    public void UnreadableOriginalPayloadRejectsSaveInsteadOfErasingUnknownState()
    {
        TimberbornRuntimePersistence persistence = new();
        IOException failure = new("Save reader failed.");
        persistence.Load(() => throw failure);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            persistence.EncodeForSave(TimberbornRuntimeInitializationState.Failed, () => TimberbornWildfirePersistenceSnapshot.Empty));
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public void NoPreviousPayloadAndNoReadySessionDoesNotWriteNewEmptyState()
    {
        TimberbornRuntimePersistence persistence = new();
        persistence.Load(() => null);
        Assert.Null(persistence.EncodeForSave(TimberbornRuntimeInitializationState.WaitingForWorld,
            () => throw new Exception("Waiting should not capture state.")));
    }

    [Fact]
    public void ReadyCaptureReplacesOriginalOnlyAfterSuccessfulEncoding()
    {
        TimberbornRuntimePersistence persistence = new();
        string original = TimberbornWildfirePersistenceCodec.Encode(TimberbornWildfirePersistenceSnapshot.Empty);
        persistence.Load(() => original);
        persistence.ReleaseRestoredSnapshot();
        Assert.Null(persistence.LoadedSnapshot);
        Assert.Throws<InvalidOperationException>(() => persistence.EncodeForSave(
            TimberbornRuntimeInitializationState.Ready,
            () => throw new InvalidOperationException("GPU readback failed.")));
        Assert.Equal(original, persistence.EncodeForSave(TimberbornRuntimeInitializationState.Failed,
            () => throw new Exception("Failed capture must preserve the original.")));

        TimberbornWildfirePersistenceSnapshot replacement = TimberbornWildfirePersistenceSnapshot.Empty with
        {
            Consequences = new TimberbornConsequencePersistenceSnapshot(
                [new TimberbornBurnDamagePersistenceEntry("tree:1", 7, 25)]),
        };
        string? encoded = persistence.EncodeForSave(TimberbornRuntimeInitializationState.Ready, () => replacement);
        Assert.NotEqual(original, encoded);
        Assert.Equal(7, Assert.Single(TimberbornWildfirePersistenceCodec.Decode(encoded!).Consequences.BurnDamageStates).DamageTaken);
        Assert.Equal(encoded, persistence.EncodeForSave(TimberbornRuntimeInitializationState.Failed,
            () => throw new Exception("Most recent saved state must remain available.")));
    }

    [Fact]
    public void LoadingAnotherWorldClearsPreviousPayloadAndFailure()
    {
        TimberbornRuntimePersistence persistence = new();
        persistence.Load(() => "invalid");
        persistence.Load(() => null);
        Assert.Null(persistence.LoadFailure);
        Assert.Null(persistence.LoadedSnapshot);
        Assert.Null(persistence.EncodeForSave(TimberbornRuntimeInitializationState.WaitingForWorld,
            () => throw new Exception("Must not capture.")));
    }
}
