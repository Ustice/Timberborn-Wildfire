using System.Globalization;
using UnityEngine;
using Timberborn.MapIndexSystem;
using Timberborn.MapStateSystem;
using Timberborn.SingletonSystem;
using Timberborn.SoilMoistureSystem;
using Timberborn.TerrainSystem;

namespace Wildfire.Timberborn.Qa;

public sealed class TimberbornQaCommandFileBridge : ILoadableSingleton, IUnloadableSingleton, IUpdatableSingleton
{
    private const string QaDirectoryName = "WildfireQA";
    private const string InboxFileName = "command-inbox.txt";
    private const string OutboxFileName = "command-outbox.txt";

    private readonly TimberbornQaCommandBridge _commandBridge;
    private readonly string _inboxPath;
    private readonly string _outboxPath;

    public TimberbornQaCommandFileBridge(
        TimberbornFireRuntime fireRuntime,
        MapSize mapSize,
        ITerrainService terrainService,
        ISoilMoistureService soilMoistureService,
        MapIndexService mapIndexService)
    {
        if (fireRuntime is null)
        {
            throw new ArgumentNullException(nameof(fireRuntime));
        }

        string qaDirectory = Path.Combine(Application.persistentDataPath, QaDirectoryName);
        _inboxPath = Path.Combine(qaDirectory, InboxFileName);
        _outboxPath = Path.Combine(qaDirectory, OutboxFileName);
        _commandBridge = new TimberbornQaCommandBridge(
            fireRuntime,
            fireRuntime,
            fireRuntime,
            fireRuntime,
            fireRuntime,
            fireRuntime,
            new TimberbornQaSoilMoistureMapProbe(
                mapSize,
                terrainService,
                soilMoistureService,
                mapIndexService),
            new UnityTimberbornQaCommandLogSink(),
            fireRuntime,
            fireRuntime,
            fireRuntime,
            fireRuntime);
    }

    public void Load()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_inboxPath) ?? Application.persistentDataPath);
        Debug.Log(
            "wildfire_command_bridge_ready " +
            $"inbox={TimberbornQaCommandBridge.FormatToken(_inboxPath)} " +
            $"outbox={TimberbornQaCommandBridge.FormatToken(_outboxPath)} " +
            $"known_commands={TimberbornQaCommandBridge.FormatToken(string.Join(",", _commandBridge.KnownCommands))}");
    }

    public void Unload()
    {
    }

    public void UpdateSingleton()
    {
        if (!File.Exists(_inboxPath))
        {
            return;
        }

        try
        {
            string commandText = File.ReadAllText(_inboxPath).Trim();
            File.Delete(_inboxPath);
            TimberbornQaCommandResult result = _commandBridge.Execute(commandText);
            WriteOutbox(result);
        }
        catch (Exception exception)
        {
            TimberbornQaCommandResult failure = TimberbornQaCommandResult.CreateFailure(
                "file_bridge",
                exception.Message,
                TimberbornQaCommandState.Placeholder,
                _commandBridge.KnownCommands);
            Debug.LogWarning(failure.ResultToken);
            WriteOutbox(failure);
        }
    }

    private void WriteOutbox(TimberbornQaCommandResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_outboxPath) ?? Application.persistentDataPath);
        string tempPath = $"{_outboxPath}.tmp";
        string payload =
            result.ResultToken +
            Environment.NewLine +
            $"updated_at_utc={DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}" +
            Environment.NewLine;

        File.WriteAllText(tempPath, payload);
        if (File.Exists(_outboxPath))
        {
            File.Delete(_outboxPath);
        }

        File.Move(tempPath, _outboxPath);
    }
}
