using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Wildfire.Unity;

public sealed record UnityBatchmodeShaderSnapshotExecutorOptions(
    string UnityExecutablePath,
    string ProjectPath,
    string ComputeShaderPath,
    TimeSpan? Timeout = null)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromMinutes(3);
}

public sealed class UnityBatchmodeShaderSnapshotExecutor(UnityBatchmodeShaderSnapshotExecutorOptions options) : IShaderSnapshotExecutor
{
    private const string ExecuteMethod = "Wildfire.UnityBatchmode.FireSimBatchmodeRunner.Capture";

    public ShaderSnapshotCapture Capture(ShaderSnapshotFixture fixture, int tickCount)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tickCount);
        ValidateFile(options.UnityExecutablePath, "Unity executable");
        ValidateDirectory(options.ProjectPath, "Unity batchmode project");
        ValidateFile(options.ComputeShaderPath, "compute shader");

        string runDirectory = Path.Combine(Path.GetTempPath(), "wildfire-shader-harness", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDirectory);
        string fixturePath = Path.Combine(runDirectory, "fixture.json");
        string outputPath = Path.Combine(runDirectory, "capture.json");
        string logPath = Path.Combine(runDirectory, "unity.log");
        ShaderSnapshotJson.WriteFixtureFile(fixturePath, fixture);

        using Process process = StartUnity(fixturePath, outputPath, logPath, tickCount);
        if (!process.WaitForExit(options.EffectiveTimeout))
        {
            TryKill(process);
            throw CreateFailure("unity-timeout", $"Unity batchmode did not exit within {options.EffectiveTimeout}.", logPath);
        }

        if (process.ExitCode != 0)
        {
            throw CreateFailure("unity-exit", $"Unity batchmode exited with code {process.ExitCode}.", logPath);
        }

        if (!File.Exists(outputPath))
        {
            throw CreateFailure("readback", "Unity batchmode completed without writing a shader snapshot capture.", logPath);
        }

        try
        {
            return ShaderSnapshotJson.LoadFile(outputPath);
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException)
        {
            throw CreateFailure("readback", $"Unity wrote an unreadable shader snapshot capture: {exception.Message}", logPath);
        }
    }

    private Process StartUnity(string fixturePath, string outputPath, string logPath, int tickCount)
    {
        ProcessStartInfo startInfo = new(options.UnityExecutablePath)
        {
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-batchmode");
        startInfo.ArgumentList.Add("-quit");
        startInfo.ArgumentList.Add("-projectPath");
        startInfo.ArgumentList.Add(Path.GetFullPath(options.ProjectPath));
        startInfo.ArgumentList.Add("-executeMethod");
        startInfo.ArgumentList.Add(ExecuteMethod);
        startInfo.ArgumentList.Add("-logFile");
        startInfo.ArgumentList.Add(logPath);
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--fixture");
        startInfo.ArgumentList.Add(fixturePath);
        startInfo.ArgumentList.Add("--shader");
        startInfo.ArgumentList.Add(Path.GetFullPath(options.ComputeShaderPath));
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("--ticks");
        startInfo.ArgumentList.Add(tickCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Unity batchmode process did not start.");
    }

    private static ShaderSnapshotExecutionFailedException CreateFailure(string phase, string message, string logPath)
    {
        return new ShaderSnapshotExecutionFailedException(phase, message, logPath, ReadLogTail(logPath));
    }

    private static string ReadLogTail(string logPath)
    {
        if (!File.Exists(logPath))
        {
            return "<Unity log file was not created.>";
        }

        string[] lines = File.ReadAllLines(logPath, Encoding.UTF8);
        return string.Join(Environment.NewLine, lines.TakeLast(80));
    }

    private static void ValidateFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new ShaderSnapshotExecutionFailedException(
                "environment",
                $"{label} was not found at '{path}'.",
                LogPath: null,
                LogTail: "Set WILDFIRE_UNITY_EXECUTABLE or pass UnityBatchmodeShaderSnapshotExecutorOptions with the Unity Editor executable path.");
        }
    }

    private static void ValidateDirectory(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new ShaderSnapshotExecutionFailedException(
                "environment",
                $"{label} was not found at '{path}'.",
                LogPath: null,
                LogTail: "Use src/Wildfire.Unity/UnityBatchmodeProject or pass a project path containing the Wildfire batchmode runner.");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }
}

public sealed class ShaderSnapshotExecutionFailedException(
    string phase,
    string message,
    string? LogPath,
    string LogTail)
    : InvalidOperationException($"{message}{Environment.NewLine}phase={phase}{Environment.NewLine}log={LogPath ?? "<none>"}{Environment.NewLine}{LogTail}")
{
    public string Phase { get; } = phase;

    public string? UnityLogPath { get; } = LogPath;

    public string UnityLogTail { get; } = LogTail;
}
