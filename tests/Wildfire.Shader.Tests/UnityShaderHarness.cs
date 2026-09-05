using Wildfire.Unity;

namespace Wildfire.Shader.Tests;

internal static class UnityShaderHarness
{
    public static ShaderSnapshotCapture Capture(ShaderSnapshotFixture fixture, int tickCount = 1)
    {
        string repoRoot = FindRepoRoot();
        string unityExecutable = Environment.GetEnvironmentVariable("WILDFIRE_UNITY_EXECUTABLE")
            ?? "/Applications/Unity/Hub/Editor/6000.3.6f1/Unity.app/Contents/MacOS/Unity";
        ShaderSnapshotHarness harness = new(new UnityBatchmodeShaderSnapshotExecutor(new UnityBatchmodeShaderSnapshotExecutorOptions(
            UnityExecutablePath: unityExecutable,
            ProjectPath: Path.Combine(repoRoot, "src/Wildfire.Unity/UnityBatchmodeProject"),
            ComputeShaderPath: Path.Combine(repoRoot, "src/Wildfire.Unity/FireSim.compute"),
            Timeout: TimeSpan.FromMinutes(5))));

        return harness.Capture(fixture, tickCount);
    }

    public static string FindRepoRoot()
    {
        DirectoryInfo? directory = new(Environment.CurrentDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Wildfire.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Wildfire.slnx from test working directory.");
    }
}
