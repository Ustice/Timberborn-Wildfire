namespace Wildfire.Timberborn.Qa;

public enum TimberbornQaCommandAccess
{
    Diagnostics,
    Development,
}

public static class TimberbornQaCommandPolicy
{
    public const string DevelopmentArgument = "--wildfire-enable-qa-mutations";

    private static readonly string[] DiagnosticCommands =
    {
        TimberbornQaCommandBridge.HelpCommand,
        TimberbornQaCommandBridge.StatusCommand,
        TimberbornQaCommandBridge.QaReadinessCommand,
        TimberbornQaCommandBridge.QaSoilMoistureRangeCommand,
        TimberbornQaCommandBridge.QaAshCellCommand,
        TimberbornQaBorrowedDutyCommands.Status,
        TimberbornQaCommandBridge.QaSaveStatusCommand,
    };

    public static TimberbornQaCommandAccess FromProcessArguments(IEnumerable<string> arguments) =>
        arguments.Contains(DevelopmentArgument, StringComparer.Ordinal)
            ? TimberbornQaCommandAccess.Development
            : TimberbornQaCommandAccess.Diagnostics;

    public static string Mode(TimberbornQaCommandAccess access) =>
        access == TimberbornQaCommandAccess.Development ? "development" : "diagnostics";

    // An explicit diagnostic allowlist keeps future commands gated, including commands
    // named "readiness" that might prepare fixtures or alter inventories.
    public static bool CanExecute(TimberbornQaCommandAccess access, string command) =>
        access == TimberbornQaCommandAccess.Development ||
        DiagnosticCommands.Contains(command, StringComparer.OrdinalIgnoreCase);
}
