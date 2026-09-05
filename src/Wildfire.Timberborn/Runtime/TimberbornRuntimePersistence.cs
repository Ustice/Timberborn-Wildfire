namespace Wildfire.Timberborn.Runtime;

/// <summary>
/// Retains the exact saved payload until a ready runtime can replace it. A world
/// that cannot initialize must not turn its saved Wildfire state into empty state.
/// </summary>
public sealed class TimberbornRuntimePersistence
{
    private string? _savedEncoding;

    public TimberbornWildfirePersistenceSnapshot? LoadedSnapshot { get; private set; }
    public Exception? LoadFailure { get; private set; }

    public void Load(Func<string?> readSavedEncoding)
    {
        Reset();
        try
        {
            _savedEncoding = readSavedEncoding();
            LoadedSnapshot = _savedEncoding is null ? null : TimberbornWildfirePersistenceCodec.Decode(_savedEncoding);
        }
        catch (Exception exception)
        {
            LoadFailure = exception;
        }
    }

    public string? EncodeForSave(
        TimberbornRuntimeInitializationState state,
        Func<TimberbornWildfirePersistenceSnapshot> captureReadyState)
    {
        if (LoadFailure is not null && _savedEncoding is null)
        {
            throw new InvalidOperationException(
                "Cannot save Wildfire state because the original saved payload could not be read.", LoadFailure);
        }

        if (state != TimberbornRuntimeInitializationState.Ready || LoadFailure is not null)
        {
            return _savedEncoding;
        }

        // Capture/encoding must both succeed before the original is replaced.
        string encoded = TimberbornWildfirePersistenceCodec.Encode(captureReadyState());
        _savedEncoding = encoded;
        return encoded;
    }

    public void ReleaseRestoredSnapshot() => LoadedSnapshot = null;

    public void Reset()
    {
        _savedEncoding = null;
        LoadedSnapshot = null;
        LoadFailure = null;
    }
}
