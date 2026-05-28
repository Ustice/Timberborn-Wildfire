using System.Reflection;
using Timberborn.BottomBarSystem;
using Timberborn.ToolButtonSystem;
using Timberborn.ToolSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Wildfire.Timberborn.Tools;

public sealed class TimberbornDemolitionBurnToolButton : IBottomBarElementsProvider
{
    private const string DemolishingToolGroupId = "Demolishing";
    private const string BurnToolImageName = "DemolishResourcesTool";
    private const string BurnToolIconResourceName = "Wildfire.Timberborn.Assets.WildfireIgniteToolIcon.png";

    private readonly TimberbornBurnSelectedEntityTool _burnSelectedEntityTool;
    private readonly ToolButtonFactory _toolButtonFactory;
    private readonly ToolButtonService _toolButtonService;
    private readonly ToolGroupService _toolGroupService;
    private static Texture2D? _burnToolIcon;
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
        SetUniqueButtonIdentity(button);
        ApplyBurnToolIcon(button);
        button.PostLoad();

        VisualElement? rightmostToolRoot = GetRightmostToolRoot(toolGroupButton);
        toolGroupButton.AddTool(button);
        PlaceButtonBeforeRightmostTool(button, rightmostToolRoot);
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

    private static void SetUniqueButtonIdentity(ToolButton button)
    {
        const string keyPrefix = "ToolButton-WildfireBurnSelectedEntityTool";
        button.Root.name = "WildfireBurnSelectedEntityToolButtonRoot";
        button.Root.viewDataKey = $"{keyPrefix}.Root";
        button.Root.tooltip = "Burn selected entity";

        Button? rootButton = button.Root as Button ?? button.Root.Q<Button>();
        if (rootButton is not null)
        {
            rootButton.name = "WildfireBurnSelectedEntityToolButton";
            rootButton.viewDataKey = $"{keyPrefix}.Title";
            rootButton.tooltip = "Burn selected entity";
        }

        VisualElement? toolImage = button.Root.Q<VisualElement>("ToolImage");
        if (toolImage is not null)
        {
            toolImage.name = "WildfireBurnSelectedEntityToolImage";
            toolImage.viewDataKey = $"{keyPrefix}.Image";
        }
    }

    private static void ApplyBurnToolIcon(ToolButton button)
    {
        VisualElement? toolImage = button.Root.Q<VisualElement>("WildfireBurnSelectedEntityToolImage") ??
            button.Root.Q<VisualElement>("ToolImage");
        if (toolImage is null)
        {
            return;
        }

        toolImage.style.backgroundImage = new StyleBackground(GetBurnToolIcon());
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

    private static void PlaceButtonBeforeRightmostTool(ToolButton button, VisualElement? rightmostToolRoot)
    {
        if (rightmostToolRoot is null)
        {
            return;
        }

        VisualElement parent = rightmostToolRoot.parent;
        int rightmostIndex = parent.IndexOf(rightmostToolRoot);
        if (rightmostIndex < 0)
        {
            return;
        }

        parent.Remove(button.Root);
        parent.Insert(rightmostIndex, button.Root);
    }

    private static Texture2D GetBurnToolIcon()
    {
        if (_burnToolIcon is not null)
        {
            return _burnToolIcon;
        }

        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(BurnToolIconResourceName) ??
            throw new InvalidOperationException($"Could not load embedded Wildfire ignite tool icon {BurnToolIconResourceName}.");
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);

        Texture2D texture = new(112, 112, TextureFormat.RGBA32, mipChain: false)
        {
            name = "WildfireIgniteToolIcon",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        if (!texture.LoadImage(memoryStream.ToArray(), markNonReadable: false))
        {
            throw new InvalidOperationException("Could not decode embedded Wildfire ignite tool icon.");
        }

        _burnToolIcon = texture;
        return texture;
    }
}
