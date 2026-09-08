using Timberborn.Characters;
using Timberborn.Common;
using Timberborn.GameDistricts;
using Timberborn.Goods;
using Timberborn.InventorySystem;
using Timberborn.ResourceCountingSystem;

namespace Wildfire.Timberborn.Resources;

/// <summary>Native district membership and teardown for one private inventory; never owns cargo or worker phase.</summary>
internal sealed class PersonalInventoryDistrictRegistration
{
    private readonly NativeResourceCoordinator _resources;
    private readonly Inventory _inventory;
    private readonly Citizen _citizen;
    private readonly Character _character;
    private readonly Func<bool> _canRegister;
    private readonly Action<Exception> _warn;
    private readonly PrivateInventoryCounter _processor;
    private DistrictInventoryRegistry? _registry;
    private DistrictResourceCounter? _counter;
    internal bool Exited { get; private set; }

    internal PersonalInventoryDistrictRegistration(Inventory inventory, NativeResourceCoordinator resources,
        Func<bool> canRegister, Action<Exception> warn)
    {
        _inventory = inventory;
        _resources = resources;
        _canRegister = canRegister;
        _warn = warn;
        _processor = new(inventory);
        _citizen = inventory.GetComponent<Citizen>();
        _character = inventory.GetComponent<Character>();
        _citizen.ChangedAssignedDistrict += OnDistrictChanged;
        _character.Died += OnDied;
    }

    private void OnDied(object sender, EventArgs args) => Exit();

    private void OnDistrictChanged(object sender, ChangeAssignedDistrictEventArgs args)
    {
        // Citizen.OnDied can unassign the district before our own Died subscriber runs.
        if (!_character.Alive) Exit();
        else UpdateRegistration(enableInventory: false);
    }

    internal void Restore() => UpdateRegistration(enableInventory: true);

    private void UpdateRegistration(bool enableInventory)
    {
        if (Exited) return;
        if (!_character.Alive)
        {
            Exit();
            return;
        }
        try
        {
            _resources.TransferInventory(() =>
            {
                if (enableInventory) _inventory.Enable();
                RegisterDistrictCore();
            });
        }
        catch
        {
            // A native district/lifecycle callback has already begun. Rejected reentry
            // cannot leave a successful enclosing capture or transfer behind.
            _resources.InvalidateAfterLifecycleFailure();
            throw;
        }
    }

    private void RegisterDistrictCore()
    {
        if (_resources.IsIndeterminate) _resources.ThrowIfSaveUnsafe();
        UnregisterDistrict();
        if (_resources.IsIndeterminate) _resources.ThrowIfSaveUnsafe();
        if (Exited || !_character.Alive || !_citizen.HasAssignedDistrict || !_canRegister()) return;
        RegisterDistrict(_citizen.AssignedDistrict);
    }

    private void RegisterDistrict(DistrictCenter district)
    {
        _registry = district.GetComponent<DistrictInventoryRegistry>();
        _counter = district.GetComponent<DistrictResourceCounter>();
        // Even private inventory registration notifies native production/consumption observers.
        _registry.Add(_inventory);
        if (_resources.IsIndeterminate || Exited || !_character.Alive || !_canRegister() ||
            !ReferenceEquals(_citizen.AssignedDistrict, district))
            throw new InvalidOperationException("Personal inventory district registration changed during its native callback.");
        _counter.Add(_processor);
    }

    internal void Exit()
    {
        if (Exited) return;
        Exited = true;
        _citizen.ChangedAssignedDistrict -= OnDistrictChanged;
        _character.Died -= OnDied;
        // Keep native death/deletion progressing without replaying an uncertain write.
        if (_resources.IsIndeterminate) return;
        try { _resources.TransferInventory(UnregisterDistrict); }
        catch (Exception exception)
        {
            _resources.InvalidateAfterLifecycleFailure();
            try
            {
                _warn(exception);
            }
            catch
            {
                // Diagnostics must not abort native teardown or retry this exited component.
            }
        }
    }

    private void UnregisterDistrict()
    {
        _counter?.Remove(_processor);
        _registry?.Remove(_inventory);
        _counter = null;
        _registry = null;
    }

    // This proxy avoids the native IGoodProcessor decorator's DistrictBuilding requirement.
    private sealed class PrivateInventoryCounter : IGoodProcessor
    {
        private static readonly List<GoodAmount> Empty = new();
        internal PrivateInventoryCounter(Inventory inventory) => Inventory = inventory;
        public Inventory Inventory { get; }
        public ReadOnlyList<GoodAmount> ProcessedGoods => Empty.AsReadOnlyList();
    }
}
