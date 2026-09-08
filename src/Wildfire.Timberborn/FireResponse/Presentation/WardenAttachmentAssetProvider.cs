using Timberborn.AssetSystem;
using Timberborn.Timbermesh;
using UnityEngine;

namespace Wildfire.Timberborn.FireResponse.Presentation;

/// <summary>Inert native model descriptions, owned by the bootstrap asset-loader session.</summary>
public sealed class WardenAttachmentAssetProvider : IAssetProvider
{
    private readonly Dictionary<string, GameObject> _prefabs = new(StringComparer.Ordinal);
    public bool IsBuiltIn => false;

    // AssetLoader normalizes paths to lowercase before querying providers. Keep the
    // whitelist exact: broad prefix matching could shadow another mod's resources.
    public static string? GetModelName(string normalizedPath) => normalizedPath switch
    {
        "wildfire/characters/attachments/wardenhelmet.ironteeth" => "Equipment/FireResponse/WardenHelmet.IronTeeth",
        "wildfire/characters/attachments/wardentank.ironteeth" => "Equipment/FireResponse/WardenTank.IronTeeth",
        "wildfire/characters/attachments/wardenwand.ironteeth" => "Equipment/FireResponse/WardenWand.IronTeeth",
        "wildfire/characters/attachments/wardenhose.ironteeth" => "Equipment/FireResponse/WardenHose.IronTeeth",
        _ => null,
    };

    public bool TryLoad<T>(string path, out OrderedAsset asset) where T : UnityEngine.Object
    {
        asset = default;
        if (typeof(T) != typeof(GameObject) || GetModelName(path) is not { } modelName) return false;
        if (!_prefabs.TryGetValue(path, out var prefab) || !prefab)
        {
            prefab = new GameObject(path) { hideFlags = HideFlags.HideAndDontSave };
            prefab.AddComponent<TimbermeshDescription>().SetModelName(modelName);
            // Helmet instances remain hidden even if native material registration fails
            // before our presentation component can acquire its visibility toggle.
            if (modelName == "Equipment/FireResponse/WardenHelmet.IronTeeth") prefab.SetActive(false);
            _prefabs[path] = prefab;
        }
        // Namespace ownership, not a high override priority, selects these assets.
        asset = new OrderedAsset(0, prefab);
        return true;
    }

    // Attachments use exact lookups. Do not create gear as a side effect of broad scans.
    public IEnumerable<OrderedAsset> LoadAll<T>(string path, IEnumerable<string> resourceAssets)
        where T : UnityEngine.Object => Array.Empty<OrderedAsset>();

    public void Reset()
    {
        // Like native asset providers, calls are synchronous on Unity's main thread.
        // These source descriptions have no materials/meshes; the native optimizer
        // owns its separate imported clones. Never destroy those or loaded assets.
        foreach (var prefab in _prefabs.Values)
            if (prefab) UnityEngine.Object.Destroy(prefab);
        _prefabs.Clear();
    }
}
