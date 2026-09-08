using System.Globalization;

namespace Wildfire.Timberborn.Qa;

public readonly record struct BorrowedDutyQaArmRequest(Guid DonorId, float X, float Y, float Z);
public readonly record struct BorrowedWaterQaRequest(Guid DonorId, Guid SourceId, BorrowedDutyQaPoint Shore,
    int FireCell, BorrowedDutyQaPoint Approach, Guid ReturnOwnerId, string InventoryComponent);
public readonly record struct BorrowedSourceQaRequest(int X, int Y, int Z, BorrowedDutyQaPoint Shore);
public readonly record struct BorrowedDutyQaPoint(float X, float Y, float Z);
public sealed record BorrowedDutyQaActor(Guid ActorId, string Phase, bool CancellationRequested, bool NativeExecutionOwned,
    bool WaterIntent = false, int? WaterStock = null, Guid? SourceId = null, bool ReturnAssigned = false)
{
    internal string Detail => $"{ActorId:D}:{Phase}:cancel_{CancellationRequested.ToString().ToLowerInvariant()}:owned_{NativeExecutionOwned.ToString().ToLowerInvariant()}" +
        $":water_{WaterStock?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}:source_{SourceId?.ToString("D") ?? "none"}" +
        $":water_intent_{WaterIntent.ToString().ToLowerInvariant()}:return_assigned_{ReturnAssigned.ToString().ToLowerInvariant()}";
}
public sealed record BorrowedDutyQaStatus(bool AdmissionsEnabled, string OfferState, Guid? DonorId,
    IReadOnlyList<BorrowedDutyQaActor> Actors, BorrowedDutyQaPoint? OfferPoint = null, Guid? CreatedSourceId = null)
{
    public string Detail => $"admissions_enabled={AdmissionsEnabled.ToString().ToLowerInvariant()} " +
        $"offer={OfferState} donor={DonorId?.ToString("D") ?? "none"} active_count={Actors.Count(actor => actor.Phase != "Idle")} running_count={Actors.Count(actor => actor.NativeExecutionOwned)} " +
        $"retained_count={Actors.Count(actor => actor.ReturnAssigned && actor.WaterStock > 0)} " +
        $"created_source={CreatedSourceId?.ToString("D") ?? "none"} " +
        $"offer_point={(OfferPoint is { } point ? FormattableString.Invariant($"{point.X:R},{point.Y:R},{point.Z:R}") : "none")} " +
        $"actors={(Actors.Count == 0 ? "none" : string.Join(",", Actors.Select(actor => actor.Detail)))}";
}

public interface ITimberbornQaBorrowedDuty
{
    BorrowedDutyQaStatus Arm(BorrowedDutyQaArmRequest request);
    BorrowedDutyQaStatus ArmWater(BorrowedWaterQaRequest request);
    BorrowedDutyQaStatus CreateSource(BorrowedSourceQaRequest request);
    BorrowedDutyQaStatus Cancel();
    BorrowedDutyQaStatus Status();
}

/// <summary>Exact development command grammar; native validation is owned by the borrowed-duty adapter.</summary>
public static class TimberbornQaBorrowedDutyCommands
{
    public const string Arm = "qa-borrowed-duty-arm";
    public const string Water = "qa-borrowed-duty-water";
    public const string Source = "qa-borrowed-duty-source";
    public const string Cancel = "qa-borrowed-duty-cancel";
    public const string Status = "qa-borrowed-duty-status";
    public static IReadOnlyList<string> Names { get; } = new[] { Arm, Water, Source, Cancel, Status };
    public static bool Handles(string command) => Names.Contains(command, StringComparer.OrdinalIgnoreCase);

    public static BorrowedDutyQaStatus Execute(string commandText, ITimberbornQaBorrowedDuty api)
    {
        string[] parts = commandText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) throw new ArgumentException("Borrowed duty command is required.");
        if (parts[0].Equals(Arm, StringComparison.OrdinalIgnoreCase)) return api.Arm(ParseArm(parts));
        if (parts[0].Equals(Water, StringComparison.OrdinalIgnoreCase)) return api.ArmWater(ParseWater(parts));
        if (parts[0].Equals(Source, StringComparison.OrdinalIgnoreCase)) return api.CreateSource(ParseSource(parts));
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
    public static BorrowedWaterQaRequest ParseWater(IReadOnlyList<string> parts)
    {
        if (parts.Count != 12 || !parts[0].Equals(Water, StringComparison.OrdinalIgnoreCase) ||
            !Identity(parts[1], out var donor) || !Identity(parts[2], out var source) ||
            !Point(parts, 3, out var shore) || !int.TryParse(parts[6], NumberStyles.None, CultureInfo.InvariantCulture, out var cell) ||
            !Point(parts, 7, out var approach) || !Identity(parts[10], out var destination) || string.IsNullOrWhiteSpace(parts[11]))
            throw new ArgumentException("Expected qa-borrowed-duty-water <donor-guid> <source-guid> <shore-x> <shore-y> <shore-z> <fire-cell> <approach-x> <approach-y> <approach-z> <return-owner-guid> <inventory-component>.");
        return new(donor, source, shore, cell, approach, destination, parts[11]);
    }
    public static BorrowedSourceQaRequest ParseSource(IReadOnlyList<string> parts)
    {
        if (parts.Count != 7 || !parts[0].Equals(Source, StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var z) || !Point(parts, 4, out var shore))
            throw new ArgumentException("Expected qa-borrowed-duty-source <input-x> <input-y> <input-z> <shore-x> <shore-y> <shore-z>; input z and shore y are vertical.");
        return new(x, y, z, shore);
    }
    private static bool Identity(string value, out Guid id) => Guid.TryParseExact(value, "D", out id) && id != Guid.Empty;
    private static bool Point(IReadOnlyList<string> parts, int offset, out BorrowedDutyQaPoint point)
    {
        point = default;
        if (!Coordinate(parts[offset], out var x) || !Coordinate(parts[offset + 1], out var y) || !Coordinate(parts[offset + 2], out var z)) return false;
        point = new(x, y, z); return true;
    }
    private static bool Coordinate(string value, out float coordinate) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate) && float.IsFinite(coordinate);
}
