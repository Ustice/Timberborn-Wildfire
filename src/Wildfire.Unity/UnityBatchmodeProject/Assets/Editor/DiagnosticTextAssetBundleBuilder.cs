using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wildfire.UnityBatchmode
{
    public static class DiagnosticTextAssetBundleBuilder
    {
        private const string GeneratedAssetPath = "Assets/WildfireGenerated/Diagnostic.txt";

        public static void Build()
        {
            AssetBundleBuildArguments arguments = AssetBundleBuildArguments.Parse(Environment.GetCommandLineArgs());
            string assetDirectory = Path.Combine(Application.dataPath, "WildfireGenerated");
            Directory.CreateDirectory(assetDirectory);
            File.WriteAllText(Path.Combine(assetDirectory, "Diagnostic.txt"), "wildfire diagnostic asset bundle\n");
            AssetDatabase.ImportAsset(GeneratedAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Directory.CreateDirectory(arguments.OutputDirectory);

            AssetBundleBuild[] bundleDefinitions =
            {
                new AssetBundleBuild
                {
                    assetBundleName = arguments.BundleName,
                    assetNames = new[] { GeneratedAssetPath },
                },
            };

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                arguments.OutputDirectory,
                bundleDefinitions,
                BuildAssetBundleOptions.None,
                arguments.BuildTarget);
            if (manifest == null)
            {
                throw new InvalidOperationException("Unity did not return an AssetBundle manifest.");
            }

            EditorApplication.Exit(0);
        }
    }
}
