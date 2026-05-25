using System.Reflection;
using Timberborn.BottomBarSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wildfire.Timberborn.Tools;

public sealed class TimberbornFertilizeFieldsToolButton : TimberbornFertilizeToolButton
{
    public TimberbornFertilizeFieldsToolButton(
        TimberbornFertilizeTool tool,
        ToolButtonFactory toolButtonFactory,
        ToolButtonService toolButtonService,
        ToolGroupService toolGroupService)
        : base(
            tool,
            toolButtonFactory,
            toolButtonService,
            toolGroupService,
            "Fields",
            "FieldsPlantingToolGroupIcon",
            "WildfireFertilizeFieldsTool",
            "wildfire_fertilize_fields_tool_button_failed")
    {
    }
}

public sealed class TimberbornFertilizeForestryToolButton : TimberbornFertilizeToolButton
{
    public TimberbornFertilizeForestryToolButton(
        TimberbornFertilizeTool tool,
        ToolButtonFactory toolButtonFactory,
        ToolButtonService toolButtonService,
        ToolGroupService toolGroupService)
        : base(
            tool,
            toolButtonFactory,
            toolButtonService,
            toolGroupService,
            "Forestry",
            "ForestryPlantingToolGroupIcon",
            "WildfireFertilizeForestryTool",
            "wildfire_fertilize_forestry_tool_button_failed")
    {
    }
}

public abstract class TimberbornFertilizeToolButton : IBottomBarElementsProvider
{
    private const string FertilizeToolIconResourceName =
        "Wildfire.Timberborn.Assets.WildfireFertilizeToolIcon.png";
    private const string Tooltip = "Fertilize with fertile ash";

    private readonly TimberbornFertilizeTool _tool;
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolButtonService _toolButtonService;
    private readonly ToolGroupService _toolGroupService;
    private readonly string _toolGroupId;
    private readonly string _fallbackImageName;
    private readonly string _keyPrefix;
    private readonly string _failureLogToken;
    private static Texture2D? _fertilizeToolIcon;
    private bool _buttonAdded;

    protected TimberbornFertilizeToolButton(
        TimberbornFertilizeTool tool,
        ToolButtonFactory toolButtonFactory,
        ToolButtonService toolButtonService,
        ToolGroupService toolGroupService,
        string toolGroupId,
        string fallbackImageName,
        string keyPrefix,
        string failureLogToken)
    {
        _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        _toolButtonFactory = toolButtonFactory ?? throw new ArgumentNullException(nameof(toolButtonFactory));
        _toolButtonService = toolButtonService ?? throw new ArgumentNullException(nameof(toolButtonService));
        _toolGroupService = toolGroupService ?? throw new ArgumentNullException(nameof(toolGroupService));
        _toolGroupId = toolGroupId ?? throw new ArgumentNullException(nameof(toolGroupId));
        _fallbackImageName = fallbackImageName ?? throw new ArgumentNullException(nameof(fallbackImageName));
        _keyPrefix = keyPrefix ?? throw new ArgumentNullException(nameof(keyPrefix));
        _failureLogToken = failureLogToken ?? throw new ArgumentNullException(nameof(failureLogToken));
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
            ToolGroupSpec toolGroup = _toolGroupService.GetGroup(_toolGroupId);
            ToolGroupButton toolGroupButton = FindExistingToolGroupButton(toolGroup);
            ToolButton button = _toolButtonFactory.Create(
                _tool,
                _fallbackImageName,
                toolGroupButton.ToolButtonsElement);
            SetButtonIdentity(button);
            TimberbornFertilizeToolButtonIcons.ApplyToolIcon(button, $"{_keyPrefix}Image", GetFertilizeToolIcon());
            VisualElement? rightmostToolRoot = GetRightmostToolRoot(toolGroupButton);
            toolGroupButton.AddTool(button);
            PlaceButtonBeforeRightmost(button, rightmostToolRoot);
            _toolGroupService.AssignToGroup(toolGroup, _tool);
            _buttonAdded = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"{_failureLogToken} reason={TimberbornQaCommandBridge.FormatToken(exception.Message)}");
        }

        return Array.Empty<BottomBarElement>();
    }

    private bool HasToolButton()
    {
        string rootName = $"{_keyPrefix}Root";
        return _toolButtonService.ToolButtons.Any(button => button.Root.name == rootName);
    }

    private ToolGroupButton FindExistingToolGroupButton(ToolGroupSpec toolGroup)
    {
        ToolButton? anchorButton = _toolButtonService.ToolButtons
            .Where(button => !ReferenceEquals(button.Tool, _tool))
            .Where(button => _toolGroupService.IsAssignedToGroup(button.Tool, toolGroup))
            .Select(static button => (ToolButton?)button)
            .FirstOrDefault() ??
            throw new InvalidOperationException(
                $"Wildfire fertilize tool could not find Timberborn's {_toolGroupId} tool group button.");

        return _toolButtonService.GetToolGroupButton(anchorButton);
    }

    private void SetButtonIdentity(ToolButton button)
    {
        button.Root.name = $"{_keyPrefix}Root";
        button.Root.viewDataKey = $"ToolButton-{_keyPrefix}.Root";
        button.Root.tooltip = Tooltip;
        Button? rootButton = button.Root as Button ?? button.Root.Q<Button>();
        if (rootButton is not null)
        {
            rootButton.name = $"{_keyPrefix}Button";
            rootButton.viewDataKey = $"ToolButton-{_keyPrefix}.Title";
            rootButton.tooltip = Tooltip;
        }

        VisualElement? toolImage = button.Root.Q<VisualElement>("ToolImage");
        if (toolImage is not null)
        {
            toolImage.name = $"{_keyPrefix}Image";
            toolImage.viewDataKey = $"ToolButton-{_keyPrefix}.Image";
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

    private static Texture2D GetFertilizeToolIcon()
    {
        if (_fertilizeToolIcon is not null)
        {
            return _fertilizeToolIcon;
        }

        _fertilizeToolIcon = TimberbornFertilizeToolButtonIcons.LoadToolIcon(
            FertilizeToolIconResourceName,
            "WildfireFertilizeToolIcon");
        return _fertilizeToolIcon;
    }
}

internal static class TimberbornFertilizeToolButtonIcons
{
    public static void ApplyToolIcon(ToolButton button, string imageElementName, Texture2D icon)
    {
        VisualElement? toolImage = button.Root.Q<VisualElement>(imageElementName) ??
            button.Root.Q<VisualElement>("ToolImage");
        if (toolImage is null)
        {
            return;
        }

        toolImage.style.backgroundImage = new StyleBackground(icon);
    }

    public static Texture2D LoadToolIcon(string resourceName, string textureName)
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Could not load embedded Wildfire tool icon {resourceName}.");
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);

        Texture2D texture = new(112, 112, TextureFormat.RGBA32, mipChain: false)
        {
            name = textureName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        if (!texture.LoadImage(memoryStream.ToArray(), markNonReadable: false))
        {
            throw new InvalidOperationException($"Could not decode embedded Wildfire tool icon {resourceName}.");
        }

        return texture;
    }
}
