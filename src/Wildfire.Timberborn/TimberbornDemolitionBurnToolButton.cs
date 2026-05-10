using Timberborn.BottomBarSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;

namespace Wildfire.Timberborn;

public sealed class TimberbornDemolitionBurnToolButton : IBottomBarElementsProvider
{
    private const string DemolishingToolGroupId = "Demolishing";
    private const string BurnToolImageName = "DemolishResourcesTool";

    private readonly TimberbornBurnSelectedEntityTool _burnSelectedEntityTool;
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolButtonService _toolButtonService;
    private readonly ToolGroupService _toolGroupService;
    private bool _buttonAdded;

    public TimberbornDemolitionBurnToolButton(
        TimberbornBurnSelectedEntityTool burnSelectedEntityTool,
        ToolButtonFactory toolButtonFactory,
        ToolButtonService toolButtonService,
        ToolGroupService toolGroupService)
    {
        _burnSelectedEntityTool = burnSelectedEntityTool ??
            throw new ArgumentNullException(nameof(burnSelectedEntityTool));
        _toolButtonFactory = toolButtonFactory ?? throw new ArgumentNullException(nameof(toolButtonFactory));
        _toolButtonService = toolButtonService ?? throw new ArgumentNullException(nameof(toolButtonService));
        _toolGroupService = toolGroupService ?? throw new ArgumentNullException(nameof(toolGroupService));
    }

    public IEnumerable<BottomBarElement> GetElements()
    {
        if (_buttonAdded || HasBurnToolButton())
        {
            _buttonAdded = true;
            return Array.Empty<BottomBarElement>();
        }

        ToolGroupSpec toolGroup = _toolGroupService.GetGroup(DemolishingToolGroupId);
        ToolGroupButton toolGroupButton = FindExistingToolGroupButton(toolGroup);
        ToolButton button = _toolButtonFactory.Create(
            _burnSelectedEntityTool,
            BurnToolImageName,
            toolGroupButton.ToolButtonsElement);

        toolGroupButton.AddTool(button);
        _toolGroupService.AssignToGroup(toolGroup, _burnSelectedEntityTool);
        _buttonAdded = true;
        return Array.Empty<BottomBarElement>();
    }

    private bool HasBurnToolButton()
    {
        return _toolButtonService.ToolButtons
            .Any(button => ReferenceEquals(button.Tool, _burnSelectedEntityTool));
    }

    private ToolGroupButton FindExistingToolGroupButton(ToolGroupSpec toolGroup)
    {
        ToolButton? anchorButton = _toolButtonService.ToolButtons
            .Where(button => !ReferenceEquals(button.Tool, _burnSelectedEntityTool))
            .Where(button => _toolGroupService.IsAssignedToGroup(button.Tool, toolGroup))
            .Select(static button => (ToolButton?)button)
            .FirstOrDefault();

        if (anchorButton is null)
        {
            throw new InvalidOperationException(
                "Wildfire burn selected entity tool could not find Timberborn's Demolishing tool group button.");
        }

        return _toolButtonService.GetToolGroupButton(anchorButton);
    }
}
