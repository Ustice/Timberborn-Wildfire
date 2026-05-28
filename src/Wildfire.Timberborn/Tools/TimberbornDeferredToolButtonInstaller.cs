using Timberborn.SingletonSystem;
using UnityEngine;

namespace Wildfire.Timberborn.Tools;

public sealed class TimberbornDeferredToolButtonInstaller : ILoadableSingleton, IUpdatableSingleton
{
    private const int InstallAfterUpdates = 2;

    private readonly TimberbornDemolitionBurnToolButton _burnToolButton;
    private readonly TimberbornFertilizeFieldsToolButton _fertilizeFieldsToolButton;
    private readonly TimberbornFertilizeForestryToolButton _fertilizeForestryToolButton;
    private int _updates;
    private bool _installed;

    public TimberbornDeferredToolButtonInstaller(
        TimberbornDemolitionBurnToolButton burnToolButton,
        TimberbornFertilizeFieldsToolButton fertilizeFieldsToolButton,
        TimberbornFertilizeForestryToolButton fertilizeForestryToolButton)
    {
        _burnToolButton = burnToolButton ?? throw new ArgumentNullException(nameof(burnToolButton));
        _fertilizeFieldsToolButton = fertilizeFieldsToolButton ??
            throw new ArgumentNullException(nameof(fertilizeFieldsToolButton));
        _fertilizeForestryToolButton = fertilizeForestryToolButton ??
            throw new ArgumentNullException(nameof(fertilizeForestryToolButton));
    }

    public void Load()
    {
        _updates = 0;
        _installed = false;
    }

    public void UpdateSingleton()
    {
        if (_installed)
        {
            return;
        }

        _updates++;
        if (_updates < InstallAfterUpdates)
        {
            return;
        }

        try
        {
            _burnToolButton.GetElements().ToArray();
            _fertilizeFieldsToolButton.GetElements().ToArray();
            _fertilizeForestryToolButton.GetElements().ToArray();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Wildfire failed to install its Timberborn tool buttons.", exception);
        }

        _installed = true;
        Debug.Log($"wildfire_tool_buttons_installed mode=deferred_update updates={_updates}");
    }
}
