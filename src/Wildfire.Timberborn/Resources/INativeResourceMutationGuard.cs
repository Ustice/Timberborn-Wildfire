namespace Wildfire.Timberborn.Resources;

/// <summary>
/// Access to the existing runtime resource guard, not another transaction or fault ledger.
/// Runtime consumers must receive the same NativeResourceCoordinator used by world saves.
/// </summary>
public interface INativeResourceMutationGuard
{
    void TransferInventory(Action mutation);
    void ThrowIfSaveUnsafe();
}
