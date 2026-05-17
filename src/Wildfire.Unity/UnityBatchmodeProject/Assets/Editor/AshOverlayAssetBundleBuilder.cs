using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wildfire.UnityBatchmode
{
    public static class AshOverlayAssetBundleBuilder
    {
        private const string GeneratedShaderAssetPath = "Assets/WildfireGenerated/AshOverlay.shader";

        public static void Build()
        {
            try
            {
                AssetBundleBuildArguments arguments = AssetBundleBuildArguments.Parse(Environment.GetCommandLineArgs());
                Debug.Log("wildfire_ash_overlay_assetbundle_builder phase=start bundle=" + arguments.BundleName + " output=" + arguments.OutputDirectory);
                ImportShader(arguments.ShaderPath, arguments.BundleName);
                Directory.CreateDirectory(arguments.OutputDirectory);

                AssetBundleBuild[] bundleDefinitions =
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = arguments.BundleName,
                        assetNames = new[] { GeneratedShaderAssetPath },
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

                string bundlePath = Path.Combine(arguments.OutputDirectory, arguments.BundleName);
                if (!File.Exists(bundlePath))
                {
                    throw new FileNotFoundException("Expected ash overlay AssetBundle was not created.", bundlePath);
                }

                Debug.Log("wildfire_ash_overlay_assetbundle_builder phase=complete bundle=" + arguments.BundleName + " path=" + bundlePath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("wildfire_ash_overlay_assetbundle_builder phase=failure message=\"" + Escape(exception.Message) + "\"");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ImportShader(string shaderPath, string bundleName)
        {
            if (!File.Exists(shaderPath))
            {
                throw new FileNotFoundException("AshOverlay.shader source was not found.", shaderPath);
            }

            string assetDirectory = Path.Combine(Application.dataPath, "WildfireGenerated");
            Directory.CreateDirectory(assetDirectory);
            string absoluteAssetPath = Path.Combine(assetDirectory, "AshOverlay.shader");
            File.Copy(shaderPath, absoluteAssetPath, overwrite: true);
            AssetDatabase.ImportAsset(GeneratedShaderAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GeneratedShaderAssetPath);
            if (shader == null)
            {
                throw new InvalidOperationException("Unity imported AshOverlay.shader but did not load a Shader asset.");
            }

            AssetImporter importer = AssetImporter.GetAtPath(GeneratedShaderAssetPath);
            if (importer == null)
            {
                throw new InvalidOperationException("Unity imported AshOverlay.shader but did not expose an AssetImporter.");
            }

            importer.assetBundleName = bundleName;
            importer.SaveAndReimport();
            Debug.Log("wildfire_ash_overlay_assetbundle_builder phase=import status=ok asset=" + GeneratedShaderAssetPath + " bundle=" + bundleName);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
