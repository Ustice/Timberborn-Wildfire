using Timberborn.BaseComponentSystem;
using Timberborn.EntityPanelSystem;
using UnityEngine.UIElements;

namespace Wildfire.Timberborn.Beavers.Emergency;

/// <summary>Development status stays visible while an unresolved carrying job is held.</summary>
public sealed class CarryEmergencyFragment : IEntityPanelFragment
{
    private VisualElement _root = null!;
    private Label _status = null!;
    private WildfireCarryEmergencyExecutor? _executor;
    public VisualElement InitializeFragment()
    {
        _root = new VisualElement { name = "WildfireCarryEmergency" };
        _status = new Label();
        _status.style.whiteSpace = WhiteSpace.Normal;
        _root.Add(_status);
        var limitation = new Label("Development experiment: carried goods and reservations are retained. Prolonged holding can block eating and drinking; recovery is not implemented.");
        limitation.style.whiteSpace = WhiteSpace.Normal;
        _root.Add(limitation);
        ClearFragment();
        return _root;
    }
    public void ShowFragment(BaseComponent entity)
    { _executor = entity.GetComponent<WildfireCarryEmergencyExecutor>(); UpdateFragment(); }
    public void ClearFragment() { _executor = null; _root.style.display = DisplayStyle.None; }
    public void UpdateFragment()
    {
        if (_executor is null || !_executor || _executor.Phase == CarryEmergencyPhase.Inactive)
        { _root.style.display = DisplayStyle.None; return; }
        _root.style.display = DisplayStyle.Flex;
        _status.text = _executor.Status;
    }
}
