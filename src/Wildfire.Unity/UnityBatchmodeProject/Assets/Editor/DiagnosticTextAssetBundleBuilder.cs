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
            try
            {
                AssetBundleBuildArguments arguments = AssetBundleBuildArguments.Parse(Environment.GetCommandLineArgs());
                Debug.Log("wildfire_diagnostic_assetbundle_builder phase=start bundle=" + arguments.BundleName + " output=" + arguments.OutputDirectory);
                string assetDirectory = Path.Combine(Application.dataPath, "WildfireGenerated");
                Directory.CreateDirectory(assetDirectory);
                File.WriteAllText(Path.Combine(assetDirectory, "Diagnostic.txt"), "wildfire diagnostic asset bundle\n");
                AssetDatabase.ImportAsset(GeneratedAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();

                TextAsset diagnosticAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(GeneratedAssetPath);
                if (diagnosticAsset == null)
                {
                    throw new InvalidOperationException("Unity imported Diagnostic.txt but did not load a TextAsset.");
                }

                AssetImporter importer = AssetImporter.GetAtPath(GeneratedAssetPath);
                if (importer == null)
                {
                    throw new InvalidOperationException("Unity imported Diagnostic.txt but did not expose an AssetImporter.");
                }

                importer.assetBundleName = arguments.BundleName;
                importer.SaveAndReimport();
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

                Debug.Log("wildfire_diagnostic_assetbundle_builder phase=complete bundle=" + arguments.BundleName);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("wildfire_diagnostic_assetbundle_builder phase=failure message=\"" + Escape(exception.Message) + "\"");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
