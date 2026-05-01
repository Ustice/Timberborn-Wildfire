using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Wildfire.UnityBatchmode
{
    public static class FireSimAssetBundleBuilder
    {
        private const string GeneratedShaderAssetPath = "Assets/WildfireGenerated/FireSim.compute";

        public static void Build()
        {
            try
            {
                AssetBundleBuildArguments arguments = AssetBundleBuildArguments.Parse(Environment.GetCommandLineArgs());
                Debug.Log("wildfire_assetbundle_builder phase=start bundle=" + arguments.BundleName + " output=" + arguments.OutputDirectory);
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
                Debug.Log("wildfire_assetbundle_builder phase=build target=" + arguments.BuildTarget + " options=" + BuildAssetBundleOptions.None);
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
                    throw new FileNotFoundException("Expected FireSim AssetBundle was not created.", bundlePath);
                }

                Debug.Log("wildfire_assetbundle_builder phase=complete bundle=" + arguments.BundleName + " path=" + bundlePath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("wildfire_assetbundle_builder phase=failure message=\"" + Escape(exception.Message) + "\"");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void ImportShader(string shaderPath, string bundleName)
        {
            if (!File.Exists(shaderPath))
            {
                throw new FileNotFoundException("FireSim.compute source was not found.", shaderPath);
            }

            string assetDirectory = Path.Combine(Application.dataPath, "WildfireGenerated");
            Directory.CreateDirectory(assetDirectory);
            string absoluteAssetPath = Path.Combine(assetDirectory, "FireSim.compute");
            File.Copy(shaderPath, absoluteAssetPath, overwrite: true);
            AssetDatabase.ImportAsset(GeneratedShaderAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(GeneratedShaderAssetPath);
            if (shader == null)
            {
                throw new InvalidOperationException("Unity imported FireSim.compute but did not load a ComputeShader asset.");
            }

            shader.FindKernel("ApplyExternalChanges");
            shader.FindKernel("SimulateFullGrid");

            AssetImporter importer = AssetImporter.GetAtPath(GeneratedShaderAssetPath);
            if (importer == null)
            {
                throw new InvalidOperationException("Unity imported FireSim.compute but did not expose an AssetImporter.");
            }

            importer.assetBundleName = bundleName;
            importer.SaveAndReimport();
            Debug.Log("wildfire_assetbundle_builder phase=import status=ok asset=" + GeneratedShaderAssetPath + " bundle=" + bundleName);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal sealed class AssetBundleBuildArguments
    {
        public string ShaderPath;
        public string OutputDirectory;
        public string BundleName;
        public BuildTarget BuildTarget;

        public static AssetBundleBuildArguments Parse(string[] args)
        {
            return new AssetBundleBuildArguments
            {
                ShaderPath = ValueAfter(args, "--shader"),
                OutputDirectory = ValueAfter(args, "--output"),
                BundleName = ValueAfter(args, "--bundle"),
                BuildTarget = ParseBuildTarget(ValueAfter(args, "--target")),
            };
        }

        private static BuildTarget ParseBuildTarget(string value)
        {
            BuildTarget target;
            if (!Enum.TryParse(value, ignoreCase: true, result: out target))
            {
                throw new ArgumentException("--target must be a Unity BuildTarget value.");
            }

            return target;
        }

        private static string ValueAfter(string[] args, string name)
        {
            for (int index = 0; index < args.Length - 1; index += 1)
            {
                if (args[index] == name && !string.IsNullOrEmpty(args[index + 1]))
                {
                    return args[index + 1];
                }
            }

            throw new ArgumentException("Missing required argument " + name + ".");
        }
    }
}
