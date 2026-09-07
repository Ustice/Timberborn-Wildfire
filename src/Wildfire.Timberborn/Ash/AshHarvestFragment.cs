using Timberborn.BaseComponentSystem;
using Timberborn.EntityPanelSystem;
using Timberborn.Localization;
using UnityEngine.UIElements;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Ash;

/// <summary>Native carried-good UI supplies quantity; this row explains why the owning job is waiting.</summary>
public sealed class AshHarvestFragment : IEntityPanelFragment
{
    private readonly ILoc _loc;
    private readonly NativeResourceCoordinator _resources;
    private Label _status = null!;
    private AshHarvestExecutor? _executor;
    public AshHarvestFragment(ILoc loc, NativeResourceCoordinator resources) { _loc = loc; _resources = resources; }
    public VisualElement InitializeFragment()
    {
        _status = new Label { name = "WildfireAshHarvest" };
        _status.style.whiteSpace = WhiteSpace.Normal;
        _status.style.paddingLeft = 8; _status.style.paddingRight = 8;
        ClearFragment(); return _status;
    }
    public void ShowFragment(BaseComponent entity) { _executor = entity.GetComponent<AshHarvestExecutor>(); UpdateFragment(); }
    public void ClearFragment() { _executor = null; _status.style.display = DisplayStyle.None; }
    public void UpdateFragment()
    {
        if (_executor is null || !_executor || _executor.Phase == AshHarvestPhase.Idle) { ClearFragment(); return; }
        _status.style.display = DisplayStyle.Flex;
        _status.text = _loc.T(_resources.IsIndeterminate ? "Wildfire.Resources.Recovery" : _executor.StatusKey);
    }
}
