using Timberborn.Localization;
using Timberborn.QuickNotificationSystem;
using Timberborn.SingletonSystem;
using UnityEngine;

namespace Wildfire.Timberborn.FireResponse;

public sealed class WardenRecoveryNotification : ILoadableSingleton, IUpdatableSingleton
{
    private readonly WardenDeliveryService _delivery;
    private readonly QuickNotificationService _notifications;
    private readonly ILoc _loc;
    private readonly WardenRecoveryNotice _notice = new();
    public WardenRecoveryNotification(WardenDeliveryService delivery, QuickNotificationService notifications, ILoc loc)
    { _delivery = delivery; _notifications = notifications; _loc = loc; }

    public void Load() => _notice.ResetForWorldLoad();
    public void UpdateSingleton()
    {
        // This updater remains active when the fire runtime is frozen. The transaction has already ended.
        try { _notice.ShowIfNeeded(_delivery.IsIndeterminate,
            () => _notifications.SendWarningNotification(_loc.T("Wildfire.Warden.Recovery"))); }
        catch (Exception exception)
        {
            // A broken notification subscriber must neither replace the water failure nor clear its save guard.
            Debug.LogError("Wildfire could not display the warden recovery warning. Reload the last saved world before saving.");
            Debug.LogException(exception);
        }
    }
}
