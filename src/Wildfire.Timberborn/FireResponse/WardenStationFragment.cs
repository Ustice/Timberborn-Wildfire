using Wildfire.Timberborn.Resources;
using Timberborn.BaseComponentSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine.UIElements;

namespace Wildfire.Timberborn.FireResponse;

public sealed class WardenStationFragment : IEntityPanelFragment
{
    private readonly ILoc _loc;
    private readonly NativeResourceCoordinator _delivery;
    private readonly WardenFireField _field;
    private VisualElement _root = null!;
    private Label _status = null!, _reason = null!, _reserve = null!, _worker = null!, _payload = null!;
    private WardenStation? _station;

    public WardenStationFragment(ILoc loc, NativeResourceCoordinator delivery, WardenFireField field)
    { _loc = loc; _delivery = delivery; _field = field; }

    public VisualElement InitializeFragment()
    {
        _root = new VisualElement { name = "WildfireWardenStation" };
        _root.style.paddingLeft = 8;
        _root.style.paddingRight = 8;
        _root.style.paddingTop = 6;
        _root.style.paddingBottom = 6;
        AddLabel(_loc.T("Wildfire.Warden.PanelTitle"));
        _status = AddLabel("");
        _reason = AddLabel("");
        _reserve = AddLabel("");
        _worker = AddLabel("");
        _payload = AddLabel("");
        AddLabel(_loc.T("Wildfire.Warden.Coverage", WardenFireField.ResponseRange));
        ClearFragment();
        return _root;
    }

    public void ShowFragment(BaseComponent entity)
    {
        _station = entity.GetComponent<WardenStation>();
        UpdateFragment();
    }

    public void ClearFragment()
    { _station = null; _root.style.display = DisplayStyle.None; }

    public void UpdateFragment()
    {
        if (_station is null || !_station) { ClearFragment(); return; }
        _root.style.display = DisplayStyle.Flex;
        var workplace = _station.Workplace;
        var worker = workplace.AssignedWorkers.FirstOrDefault();
        var executor = worker?.GetComponent<WardenExecutor>();
        var equipment = worker?.GetComponent<WardenEquipment>();
        var state = new WardenStationViewState(_delivery.IsIndeterminate, _station.Finished,
            _station.Operational, _field.ResponseEnabled, workplace.NumberOfAssignedWorkers,
            _station.Restocking, executor?.Phase ?? WardenPhase.Idle,
            executor?.ResponseReason ?? WardenResponseReason.None);
        _status.text = _loc.T(WardenStationPresentation.StatusKey(state));
        _reason.style.display = state.Phase == WardenPhase.Returning && state.Reason != WardenResponseReason.None && !state.UnsafeResourceState
            ? DisplayStyle.Flex : DisplayStyle.None;
        _reason.text = _loc.T(WardenStationPresentation.ReasonKey(state.Reason, "Ready"));
        _reserve.text = _loc.T("Wildfire.Warden.Reserve", _station.Inventory.AmountInStock(WardenEquipment.WaterId), WardenStation.WaterCapacity);
        _worker.text = _loc.T("Wildfire.Warden.Worker", workplace.NumberOfAssignedWorkers, workplace.MaxWorkers);
        _payload.text = _loc.T("Wildfire.Warden.Payload", equipment?.Inventory.AmountInStock(WardenEquipment.WaterId) ?? 0);
    }

    private Label AddLabel(string text)
    {
        var label = new Label(text);
        label.style.whiteSpace = WhiteSpace.Normal;
        _root.Add(label);
        return label;
    }
}
