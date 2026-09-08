namespace Wildfire.Timberborn.Qa;

public sealed partial class TimberbornQaCommandBridge
{
    public const string QaSaveCopyCommand = "qa-save-copy";
    public const string QaSaveStatusCommand = "qa-save-status";
    public const string QaGameSpeedCommand = "qa-game-speed";
    private static readonly string[] SessionCommands = { QaSaveCopyCommand, QaSaveStatusCommand, QaGameSpeedCommand };
    private readonly ITimberbornQaSession? _session;

    private TimberbornQaCommandResult ExecuteSession(string command, string text)
    {
        if (_session is null) throw new InvalidOperationException("QA session control is unavailable.");
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string detail;
        if (command == QaGameSpeedCommand && words.Length == 2 && words[1] is "0" or "1")
            detail = _session.ChangeSpeed(words[1] == "0" ? 0 : 1);
        else if (words.Length >= 2 && Guid.TryParse(words[1], out var id) && id != Guid.Empty)
            detail = command switch
            {
                QaSaveCopyCommand when words.Length == 3 => _session.QueueSave(id, words[2]),
                QaSaveStatusCommand when words.Length == 2 => _session.SaveStatus(id),
                _ => throw new ArgumentException("Expected qa-save-copy <request-guid> <QA-name>, qa-save-status <request-guid>, or qa-game-speed <0|1>.")
            };
        else throw new ArgumentException("Invalid QA session command arguments.");
        return TimberbornQaCommandResult.CreateSuccess(command, _stateProvider.GetState(), KnownCommands, detail);
    }
}
