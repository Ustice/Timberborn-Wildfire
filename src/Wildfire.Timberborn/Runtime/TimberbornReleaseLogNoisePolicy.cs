namespace Wildfire.Timberborn.Runtime;

public enum TimberbornReleaseLogClass
{
    ReleaseError,
    ReleaseWarning,
    ReleaseDiagnostic,
    QaOnly,
    TooNoisy,
}

public static class TimberbornReleaseLogNoisePolicy
{
    public static TimberbornReleaseLogClass ClassifyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return TimberbornReleaseLogClass.TooNoisy;
        }

        string normalized = token.Trim();
        if (normalized.Contains("_failed", StringComparison.Ordinal) ||
            normalized.Contains("_failure", StringComparison.Ordinal) ||
            normalized.Contains("_blocked", StringComparison.Ordinal) ||
            normalized.Contains("_invalid", StringComparison.Ordinal))
        {
            return TimberbornReleaseLogClass.ReleaseError;
        }

        if (normalized.Contains("_skipped", StringComparison.Ordinal) ||
            normalized.Contains("_missing", StringComparison.Ordinal) ||
            normalized.Contains("_unavailable", StringComparison.Ordinal) ||
            normalized.Contains("_disabled", StringComparison.Ordinal))
        {
            return TimberbornReleaseLogClass.ReleaseWarning;
        }

        if (normalized.Contains("_qa_", StringComparison.Ordinal) ||
            normalized.StartsWith("wildfire_command_", StringComparison.Ordinal))
        {
            return TimberbornReleaseLogClass.QaOnly;
        }

        if (normalized.Contains("_completed", StringComparison.Ordinal) ||
            normalized.Contains("_initialized", StringComparison.Ordinal) ||
            normalized.Contains("_configured", StringComparison.Ordinal) ||
            normalized.Contains("_bound", StringComparison.Ordinal) ||
            normalized.Contains("_registered", StringComparison.Ordinal))
        {
            return TimberbornReleaseLogClass.ReleaseDiagnostic;
        }

        return TimberbornReleaseLogClass.TooNoisy;
    }

    public static bool ShouldLogConsequenceSummary(params int[] significantCounters)
    {
        return significantCounters.Any(static counter => counter > 0);
    }
}
