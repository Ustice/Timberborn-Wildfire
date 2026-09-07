using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timberborn.AssetSystem;
using Timberborn.Timbermesh;
using UnityEditor;
using UnityEngine;
using Wildfire.Timberborn.FireResponse.Presentation;

[InitializeOnLoad]
public static class WardenAttachmentProbe
{
    const string Pending = "Wildfire.WardenAttachmentProbe.Pending";
    static WardenAttachmentAssetProvider provider;
    static GameObject[] old;
    static GameObject replacement;
    static int updates;
    static readonly List<GameObject> imports = new List<GameObject>();
    static readonly Materials materials = new Materials();
    static WardenAttachmentProbe() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        try
        {
            provider = new WardenAttachmentAssetProvider();
            var loader = new AssetLoader(new IAssetProvider[] { provider });
            var names = new[] { "WardenHelmet", "WardenTank", "WardenWand", "WardenHose" };
            var triangles = new[] { 1812, 3868, 88, 252 };
            old = new GameObject[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var path = "Wildfire/Characters/Attachments/" + names[i] + ".IronTeeth";
                var asset = loader.Load<GameObject>(path);
                Require(ReferenceEquals(asset, loader.Load<GameObject>(path.ToLowerInvariant())), "cache mismatch");
                Require(asset.GetComponents<Component>().Length == 2, "wrapper has unexpected components");
                var description = asset.GetComponent<TimbermeshDescription>();
                Require(description != null, "native description missing");
                Require(description.ModelName == "Equipment/FireResponse/" + names[i] + ".IronTeeth", "model path mismatch");
                Require(asset.GetComponentsInChildren<Renderer>(true).Length == 0, "wrapper creates renderer/material prematurely");
                old[i] = asset;
                // Native static importer, isolated from game material/atlas collection.
                var target = UnityEngine.Object.Instantiate(asset);
                imports.Add(target);
                var modelPath = Path.Combine(Environment.GetEnvironmentVariable("WILDFIRE_GEAR_SOURCE_ROOT"), "src/Wildfire.Timberborn/Data", description.ModelName + ".timbermesh");
                using (var stream = File.OpenRead(modelPath))
                    new TimbermeshImporter(new StaticMeshBuilder(materials), Array.Empty<IModelPostprocessor>()).Import(stream, target.transform);
                Require(target.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh.triangles.Length / 3) == triangles[i], "native import triangle mismatch: " + names[i]);
            }
            foreach (var path in new[] { "Characters/Attachments/Backpack", "Wildfire/Characters/Attachments/WardenCoat.IronTeeth", "Wildfire/Characters/Attachments/WardenHelmet.IronTeeth/extra" })
                Require(loader.LoadSafe<GameObject>(path) == null, "unrelated asset shadowed: " + path);
            Require(loader.LoadSafe<Texture2D>("Wildfire/Characters/Attachments/WardenHelmet.IronTeeth") == null, "wrong type accepted");
            Require(!provider.LoadAll<GameObject>("", Array.Empty<string>()).Any(), "broad scan created assets");
            loader.Reset();
            replacement = loader.Load<GameObject>("Wildfire/Characters/Attachments/WardenHelmet.IronTeeth");
            Require(!ReferenceEquals(replacement, old[0]), "reset reused source description");
            updates = 0;
            EditorApplication.update += AfterDestroy;
        }
        catch (Exception e) { Fail(e); }
    }
    static void AfterDestroy()
    {
        if (++updates < 4) return;
        EditorApplication.update -= AfterDestroy;
        try
        {
            Require(old.All(x => !x), "reset leaked old Unity GameObjects");
            Require(replacement, "replacement destroyed with previous cache");
            Require(imports.All(x => x), "provider reset destroyed imported clones");
            provider.Reset();
            foreach (var go in imports) UnityEngine.Object.Destroy(go);
            materials.Dispose();
            SessionState.SetBool(Pending, false);
            Debug.Log("WILDFIRE_WARDEN_ATTACHMENT_PROBE_PASS wrappers=4 native_import_triangles=6020 reset_destroyed=4 unrelated_shadowed=0 native_atlas_tested=false character_fit_tested=false");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Fail(e); }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Fail(Exception e) { SessionState.SetBool(Pending, false); Debug.LogException(e); EditorApplication.Exit(1); }
    sealed class Materials : IMaterialRepository
    {
        readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        readonly HashSet<string> allowed = new HashSet<string> { "BaseMetal.IronTeeth", "BaseWood_DarkBrown.IronTeeth", "Details.IronTeeth", "Plaster_Orange.IronTeeth" };
        public Material GetMaterial(string name)
        {
            Require(allowed.Contains(name), "unexpected material: " + name);
            if (!cache.TryGetValue(name, out var material)) cache[name] = material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            return material;
        }
        public void Dispose() { foreach (var m in cache.Values) UnityEngine.Object.Destroy(m); }
    }
}
