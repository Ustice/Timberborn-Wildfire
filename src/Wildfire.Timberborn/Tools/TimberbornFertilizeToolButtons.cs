using Timberborn.BottomBarSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wildfire.Timberborn;

public sealed class TimberbornFertilizeCropsToolButton : IBottomBarElementsProvider
{
    private const string FarmingToolGroupId = "Fields";
    private const string BurnToolImageName = "DemolishResourcesTool";

    private readonly TimberbornFertilizeCropsTool _tool;
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolButtonService _toolButtonService;
    private readonly ToolGroupService _toolGroupService;
    private bool _buttonAdded;

    public TimberbornFertilizeCropsToolButton(
        TimberbornFertilizeCropsTool tool,
        ToolButtonFactory toolButtonFactory,
        ToolButtonService toolButtonService,
        ToolGroupService toolGroupService)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _toolButtonFactory = toolButtonFactory ?? throw new ArgumentNullException(nameof(toolButtonFactory));
        _toolButtonService = toolButtonService ?? throw new ArgumentNullException(nameof(toolButtonService));
        _toolGroupService = toolGroupService ?? throw new ArgumentNullException(nameof(toolGroupService));
    }

    public IEnumerable<BottomBarElement> GetElements()
    {
        if (_buttonAdded || HasToolButton())
        {
            _buttonAdded = true;
            return Array.Empty<BottomBarElement>();
        }

        try
        {
            ToolGroupSpec toolGroup = _toolGroupService.GetGroup(FarmingToolGroupId);
            ToolGroupButton toolGroupButton = FindExistingToolGroupButton(toolGroup);
            ToolButton button = _toolButtonFactory.Create(_tool, BurnToolImageName, toolGroupButton.ToolButtonsElement);
            SetButtonIdentity(button, "WildfireFertilizeCropsTool", "Fertilize crops");
            VisualElement? rightmostToolRoot = GetRightmostToolRoot(toolGroupButton);
            toolGroupButton.AddTool(button);
            PlaceButtonBeforeRightmost(button, rightmostToolRoot);
            _toolGroupService.AssignToGroup(toolGroup, _tool);
            _buttonAdded = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "wildfire_fertilize_crops_tool_button_failed " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }

        return Array.Empty<BottomBarElement>();
    }

    private bool HasToolButton()
    {
        return _toolButtonService.ToolButtons.Any(button => ReferenceEquals(button.Tool, _tool));
    }

    private ToolGroupButton FindExistingToolGroupButton(ToolGroupSpec toolGroup)
    {
        ToolButton? anchorButton = _toolButtonService.ToolButtons
            .Where(button => !ReferenceEquals(button.Tool, _tool))
            .Where(button => _toolGroupService.IsAssignedToGroup(button.Tool, toolGroup))
            .Select(static button => (ToolButton?)button)
            .FirstOrDefault() ??
            throw new InvalidOperationException(
                $"Wildfire fertilize crops tool could not find Timberborn's {FarmingToolGroupId} tool group button.");

        return _toolButtonService.GetToolGroupButton(anchorButton);
    }

    private static void SetButtonIdentity(ToolButton button, string keyPrefix, string tooltip)
    {
        button.Root.name = $"{keyPrefix}Root";
        button.Root.viewDataKey = $"ToolButton-{keyPrefix}.Root";
        button.Root.tooltip = tooltip;
        Button? rootButton = button.Root as Button ?? button.Root.Q<Button>();
        if (rootButton is not null)
        {
            rootButton.name = $"{keyPrefix}Button";
            rootButton.viewDataKey = $"ToolButton-{keyPrefix}.Title";
            rootButton.tooltip = tooltip;
        }

        VisualElement? toolImage = button.Root.Q<VisualElement>("ToolImage");
        if (toolImage is not null)
        {
            toolImage.name = $"{keyPrefix}Image";
            toolImage.viewDataKey = $"ToolButton-{keyPrefix}.Image";
        }
    }

    private static VisualElement? GetRightmostToolRoot(ToolGroupButton toolGroupButton)
    {
        HashSet<VisualElement> toolRoots = toolGroupButton.ToolButtons
            .Select(static button => button.Root)
            .ToHashSet();
        return toolGroupButton.ToolButtonsElement.Children()
            .Where(toolRoots.Contains)
            .LastOrDefault();
    }

    private static void PlaceButtonBeforeRightmost(ToolButton button, VisualElement? rightmostToolRoot)
    {
        if (rightmostToolRoot is null)
        {
            return;
        }

        VisualElement parent = rightmostToolRoot.parent;
        int rightmostIndex = parent.IndexOf(rightmostToolRoot);
        if (rightmostIndex >= 0)
        {
            parent.Remove(button.Root);
            parent.Insert(rightmostIndex, button.Root);
        }
    }
}

public sealed class TimberbornFertilizeTreesToolButton : IBottomBarElementsProvider
{
    private const string ForestryToolGroupId = "Forestry";
    private const string BurnToolImageName = "DemolishResourcesTool";

    private readonly TimberbornFertilizeTreesTool _tool;
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolButtonService _toolButtonService;
    private readonly ToolGroupService _toolGroupService;
    private bool _buttonAdded;

    public TimberbornFertilizeTreesToolButton(
        TimberbornFertilizeTreesTool tool,
        ToolButtonFactory toolButtonFactory,
        ToolButtonService toolButtonService,
        ToolGroupService toolGroupService)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _toolButtonFactory = toolButtonFactory ?? throw new ArgumentNullException(nameof(toolButtonFactory));
        _toolButtonService = toolButtonService ?? throw new ArgumentNullException(nameof(toolButtonService));
        _toolGroupService = toolGroupService ?? throw new ArgumentNullException(nameof(toolGroupService));
    }

    public IEnumerable<BottomBarElement> GetElements()
    {
        if (_buttonAdded || HasToolButton())
        {
            _buttonAdded = true;
            return Array.Empty<BottomBarElement>();
        }

        try
        {
            ToolGroupSpec toolGroup = _toolGroupService.GetGroup(ForestryToolGroupId);
            ToolGroupButton toolGroupButton = FindExistingToolGroupButton(toolGroup);
            ToolButton button = _toolButtonFactory.Create(_tool, BurnToolImageName, toolGroupButton.ToolButtonsElement);
            SetButtonIdentity(button, "WildfireFertilizeTreesTool", "Fertilize trees and bushes");
            VisualElement? rightmostToolRoot = GetRightmostToolRoot(toolGroupButton);
            toolGroupButton.AddTool(button);
            PlaceButtonBeforeRightmost(button, rightmostToolRoot);
            _toolGroupService.AssignToGroup(toolGroup, _tool);
            _buttonAdded = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "wildfire_fertilize_trees_tool_button_failed " +
                $"reason={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }

        return Array.Empty<BottomBarElement>();
    }

    private bool HasToolButton()
    {
        return _toolButtonService.ToolButtons.Any(button => ReferenceEquals(button.Tool, _tool));
    }

    private ToolGroupButton FindExistingToolGroupButton(ToolGroupSpec toolGroup)
    {
        ToolButton? anchorButton = _toolButtonService.ToolButtons
            .Where(button => !ReferenceEquals(button.Tool, _tool))
            .Where(button => _toolGroupService.IsAssignedToGroup(button.Tool, toolGroup))
            .Select(static button => (ToolButton?)button)
            .FirstOrDefault() ??
            throw new InvalidOperationException(
                $"Wildfire fertilize trees tool could not find Timberborn's {ForestryToolGroupId} tool group button.");

        return _toolButtonService.GetToolGroupButton(anchorButton);
    }

    private static void SetButtonIdentity(ToolButton button, string keyPrefix, string tooltip)
    {
        button.Root.name = $"{keyPrefix}Root";
        button.Root.viewDataKey = $"ToolButton-{keyPrefix}.Root";
        button.Root.tooltip = tooltip;
        Button? rootButton = button.Root as Button ?? button.Root.Q<Button>();
        if (rootButton is not null)
        {
            rootButton.name = $"{keyPrefix}Button";
            rootButton.viewDataKey = $"ToolButton-{keyPrefix}.Title";
            rootButton.tooltip = tooltip;
        }

        VisualElement? toolImage = button.Root.Q<VisualElement>("ToolImage");
        if (toolImage is not null)
        {
            toolImage.name = $"{keyPrefix}Image";
            toolImage.viewDataKey = $"ToolButton-{keyPrefix}.Image";
        }
    }

    private static VisualElement? GetRightmostToolRoot(ToolGroupButton toolGroupButton)
    {
        HashSet<VisualElement> toolRoots = toolGroupButton.ToolButtons
            .Select(static button => button.Root)
            .ToHashSet();
        return toolGroupButton.ToolButtonsElement.Children()
            .Where(toolRoots.Contains)
            .LastOrDefault();
    }

    private static void PlaceButtonBeforeRightmost(ToolButton button, VisualElement? rightmostToolRoot)
    {
        if (rightmostToolRoot is null)
        {
            return;
        }

        VisualElement parent = rightmostToolRoot.parent;
        int rightmostIndex = parent.IndexOf(rightmostToolRoot);
        if (rightmostIndex >= 0)
        {
            parent.Remove(button.Root);
            parent.Insert(rightmostIndex, button.Root);
        }
    }
}
