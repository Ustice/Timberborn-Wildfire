namespace Wildfire.Timberborn.FireResponse;

/// <summary>Pending jobs remain in their native executor; this registry owns no stock or saved queue.</summary>
public sealed class WardenDeliveryService
{
    private readonly List<WardenExecutor> _wardens = new();
    public bool IsIndeterminate { get; private set; }
    public void Register(WardenExecutor executor) => _wardens.Add(executor);
    public void Unregister(WardenExecutor executor) => _wardens.Remove(executor);
    public void ThrowIfSaveUnsafe()
    {
        if (IsIndeterminate) throw new InvalidOperationException("Warden water delivery is indeterminate; reload the last saved world before saving.");
    }
}
