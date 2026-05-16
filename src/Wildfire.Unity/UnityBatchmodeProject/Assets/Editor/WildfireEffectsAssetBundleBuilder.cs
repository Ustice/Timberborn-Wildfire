using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wildfire.UnityBatchmode
{
    public static class WildfireEffectsAssetBundleBuilder
    {
        private const string FlameAssetPath  = "Assets/WildfireGenerated/WildfireFlame.shader";
        private const string CloudAssetPath  = "Assets/WildfireGenerated/WildfireCloud.shader";

        public static void Build()
        {
            try
            {
                EffectsAssetBundleBuildArguments arguments =
                    EffectsAssetBundleBuildArguments.Parse(Environment.GetCommandLineArgs());

                Debug.Log(
                    "wildfire_effects_builder phase=start " +
                    "bundle=" + arguments.BundleName +
                    " output=" + arguments.OutputDirectory);

                ImportShader(arguments.FlameShaderPath, FlameAssetPath, arguments.BundleName);
                ImportShader(arguments.CloudShaderPath, CloudAssetPath, arguments.BundleName);

                Directory.CreateDirectory(arguments.OutputDirectory);

                AssetBundleBuild[] bundleDefinitions =
                {
                    new AssetBundleBuild
                    {
                        assetBundleName = arguments.BundleName,
                        assetNames = new[] { FlameAssetPath, CloudAssetPath },
                    },
                };

                Debug.Log(
                    "wildfire_effects_builder phase=build " +
                    "target=" + arguments.BuildTarget +
                    " assets=" + FlameAssetPath + "," + CloudAssetPath);

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
                    throw new FileNotFoundException(
                        "Expected Wildfire effects AssetBundle was not created.", bundlePath);
                }

                Debug.Log(
                    "wildfire_effects_builder phase=complete " +
                    "bundle=" + arguments.BundleName +
                    " path=" + bundlePath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "wildfire_effects_builder phase=failure message=\"" + Escape(exception.Message) + "\"");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ImportShader(string sourcePath, string assetPath, string bundleName)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException(
                    "Wildfire effects shader source was not found: " + sourcePath, sourcePath);
            }

            string assetDirectory = Path.Combine(Application.dataPath, "WildfireGenerated");
            Directory.CreateDirectory(assetDirectory);

            string fileName = Path.GetFileName(assetPath);
            string absoluteAssetPath = Path.Combine(assetDirectory, fileName);
            File.Copy(sourcePath, absoluteAssetPath, overwrite: true);

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Unity imported " + fileName + " but did not load a Shader asset.");
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Unity imported " + fileName + " but did not expose an AssetImporter.");
            }

            importer.assetBundleName = bundleName;
            importer.SaveAndReimport();
            Debug.Log(
                "wildfire_effects_builder phase=import " +
                "status=ok asset=" + assetPath +
                " bundle=" + bundleName);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal sealed class EffectsAssetBundleBuildArguments
    {
        public string FlameShaderPath;
        public string CloudShaderPath;
        public string OutputDirectory;
        public string BundleName;
        public BuildTarget BuildTarget;

        public static EffectsAssetBundleBuildArguments Parse(string[] args)
        {
            return new EffectsAssetBundleBuildArguments
            {
                FlameShaderPath = ValueAfter(args, "--flame"),
                CloudShaderPath = ValueAfter(args, "--cloud"),
                OutputDirectory = ValueAfter(args, "--output"),
                BundleName      = ValueAfter(args, "--bundle"),
                BuildTarget     = ParseBuildTarget(ValueAfter(args, "--target")),
            };
        }

        private static BuildTarget ParseBuildTarget(string value)
        {
            if (!Enum.TryParse(value, ignoreCase: true, result: out BuildTarget target))
            {
                throw new ArgumentException("--target must be a Unity BuildTarget value.");
            }

            return target;
        }

        private static string ValueAfter(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name && !string.IsNullOrEmpty(args[i + 1]))
                {
                    return args[i + 1];
                }
            }

            throw new ArgumentException("Missing required argument " + name + ".");
        }
    }
}
