using Timberborn.InventorySystem;
namespace Wildfire.Timberborn.FireResponse;

// Observe before GoodReserver clears its record after native capacity callbacks.
internal sealed class WardenCapacityRelease : IDisposable
{
    private readonly Inventory _destination;
    private readonly GoodReserver _reserver;
    private readonly int _expected;
    private int _events;
    internal WardenCapacityRelease(Inventory destination, GoodReserver reserver)
    {
        _destination = destination; _reserver = reserver;
        _expected = destination.ReservedCapacity(WardenEquipment.WaterId) - 1;
        destination.InventoryCapacityReservationChanged += OnChanged;
    }
    private void OnChanged(object sender, InventoryReservationChangedEventArgs args)
    {
        if (++_events != 1 || !WardenEquipmentReturnStock.ExactCapacity(_destination, _reserver) ||
            _destination.ReservedCapacity(WardenEquipment.WaterId) != _expected)
            throw new InvalidOperationException("Native callback changed or reentered the Warden capacity release.");
    }
    internal void RequireComplete()
    {
        if (_events != 1 || _expected < 0 || _destination.ReservedCapacity(WardenEquipment.WaterId) != _expected)
            throw new InvalidOperationException("Native Warden capacity release did not complete exactly once.");
        WardenEquipmentReturnStock.RequireReleased(_reserver);
    }
    public void Dispose() => _destination.InventoryCapacityReservationChanged -= OnChanged;
}
