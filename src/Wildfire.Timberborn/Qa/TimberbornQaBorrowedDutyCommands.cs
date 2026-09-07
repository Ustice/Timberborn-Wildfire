using System.Globalization;

namespace Wildfire.Timberborn.Qa;

public readonly record struct BorrowedDutyQaArmRequest(Guid DonorId, float X, float Y, float Z);
public readonly record struct BorrowedDutyQaPoint(float X, float Y, float Z);
public sealed record BorrowedDutyQaActor(Guid ActorId, string Phase, bool CancellationRequested, bool NativeExecutionOwned);
public sealed record BorrowedDutyQaStatus(bool AdmissionsEnabled, string OfferState, Guid? DonorId,
    IReadOnlyList<BorrowedDutyQaActor> Actors, BorrowedDutyQaPoint? OfferPoint = null)
{
    public string Detail => $"admissions_enabled={AdmissionsEnabled.ToString().ToLowerInvariant()} " +
        $"offer={OfferState} donor={DonorId?.ToString("D") ?? "none"} active_count={Actors.Count} running_count={Actors.Count(actor => actor.NativeExecutionOwned)} " +
        $"offer_point={(OfferPoint is { } point ? FormattableString.Invariant($"{point.X:R},{point.Y:R},{point.Z:R}") : "none")} " +
        $"actors={(Actors.Count == 0 ? "none" : string.Join(",", Actors.Select(actor => $"{actor.ActorId:D}:{actor.Phase}:cancel_{actor.CancellationRequested.ToString().ToLowerInvariant()}:owned_{actor.NativeExecutionOwned.ToString().ToLowerInvariant()}")))}";
}

public interface ITimberbornQaBorrowedDuty
{
    BorrowedDutyQaStatus Arm(BorrowedDutyQaArmRequest request);
    BorrowedDutyQaStatus Cancel();
    BorrowedDutyQaStatus Status();
}

/// <summary>Exact development command grammar; native validation is owned by the borrowed-duty adapter.</summary>
public static class TimberbornQaBorrowedDutyCommands
{
    public const string Arm = "qa-borrowed-duty-arm";
    public const string Cancel = "qa-borrowed-duty-cancel";
    public const string Status = "qa-borrowed-duty-status";
    public static IReadOnlyList<string> Names { get; } = new[] { Arm, Cancel, Status };
    public static bool Handles(string command) => Names.Contains(command, StringComparer.OrdinalIgnoreCase);

    public static BorrowedDutyQaStatus Execute(string commandText, ITimberbornQaBorrowedDuty api)
    {
        string[] parts = commandText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("Borrowed duty command is required.");
        if (parts[0].Equals(Arm, StringComparison.OrdinalIgnoreCase)) return api.Arm(ParseArm(parts));
        if (parts.Length != 1) throw new ArgumentException("Borrowed duty cancel/status accepts no arguments.");
        if (parts[0].Equals(Cancel, StringComparison.OrdinalIgnoreCase)) return api.Cancel();
        if (parts[0].Equals(Status, StringComparison.OrdinalIgnoreCase)) return api.Status();
        throw new ArgumentException("Unknown borrowed duty command.");
    }

    public static BorrowedDutyQaArmRequest ParseArm(IReadOnlyList<string> parts)
    {
        if (parts.Count != 5 || !parts[0].Equals(Arm, StringComparison.OrdinalIgnoreCase) ||
            !Guid.TryParseExact(parts[1], "D", out var donor) || donor == Guid.Empty ||
            !Coordinate(parts[2], out var x) || !Coordinate(parts[3], out var y) || !Coordinate(parts[4], out var z))
            throw new ArgumentException("Expected qa-borrowed-duty-arm <donor-guid> <x> <y> <z>; coordinates must be finite Unity world coordinates (y is vertical).");
        return new BorrowedDutyQaArmRequest(donor, x, y, z);
    }
    private static bool Coordinate(string value, out float coordinate) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate) && float.IsFinite(coordinate);
}
