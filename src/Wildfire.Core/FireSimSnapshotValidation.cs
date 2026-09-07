namespace Wildfire.Core;

public static class FireSimSnapshotValidation
{
    /// <summary>Copy all mutable arrays, then validate the complete graph before any publication or allocation.</summary>
    public static FireSimSnapshot ValidateAndClone(FireSimSnapshot snapshot)
    {
        if (snapshot is null) throw Invalid("Snapshot is missing.");
        if (snapshot.Version != FireSimSnapshot.CurrentVersion) throw Invalid("Unsupported complete snapshot version.");
        int count;
        try { count = checked(snapshot.Grid.Width * snapshot.Grid.Height * snapshot.Grid.Depth); }
        catch (OverflowException) { throw Invalid("Snapshot grid overflows cell capacity."); }
        if (snapshot.Grid.Width <= 0 || snapshot.Grid.Height <= 0 || snapshot.Grid.Depth <= 0 || count <= 0)
            throw Invalid("Snapshot grid dimensions must be positive.");
        var authority = snapshot.MaterialAuthority ?? throw Invalid("Complete material authority is missing; legacy data cannot supply it.");
        var copy = snapshot with
        {
            Cells = Copy(snapshot.Cells, count), TransportFields = Copy(snapshot.TransportFields, count),
            CompanionFields = Copy(snapshot.CompanionFields, count), TargetIds = Copy(snapshot.TargetIds, count),
            SlotIds = Copy(snapshot.SlotIds, count), PendingChanges = Copy(snapshot.PendingChanges),
            MaterialAuthority = authority with { KnownSlots = Copy(authority.KnownSlots), Archives = Copy(authority.Archives) },
        };
        ValidateParameters(copy.Parameters);
        HashSet<FireSimMaterialIdentity> active = new();
        for (int cell = 0; cell < count; cell++)
        {
            ValidateCompanion(copy.CompanionFields[cell]);
            if ((copy.TransportFields[cell] & 0xffff0000u) != 0) throw Invalid("Transport field exceeds packed bits.");
            var identity = Identity(copy.TargetIds[cell], copy.SlotIds[cell]);
            if (identity.IsOwned && !active.Add(identity)) throw Invalid("A material slot is active at multiple cells.");
        }
        ValidateAuthority(copy.MaterialAuthority, active, count);
        foreach (var change in copy.PendingChanges)
        {
            if (change.MaterialHandoff is not null || change.CollectCleanAsh.HasValue)
                throw Invalid("Acknowledged inputs cannot be restored as queued ordinary changes.");
            // Out-of-grid ordinary inputs retain the existing queue's ignored-input semantics.
            FireSimGpuProtocol.EncodeChange(change with { CellIndex = 0 });
        }
        return copy;
    }

    private static void ValidateAuthority(FireSimMaterialAuthoritySnapshot authority, HashSet<FireSimMaterialIdentity> active, int count)
    {
        HashSet<FireSimMaterialIdentity> known = new();
        foreach (var identity in authority.KnownSlots)
            if (!identity.IsOwned || !known.Add(identity)) throw Invalid("Known material slots must be nonzero and unique.");
        HashSet<FireSimMaterialIdentity> partition = new(active);
        HashSet<(uint, int)> origins = new();
        foreach (var archive in authority.Archives)
        {
            if (archive is null || !archive.Identity.IsOwned || archive.CaptureToken == 0 || archive.CaptureToken > authority.LastAttemptToken ||
                archive.SourceCellIndex < 0 || archive.SourceCellIndex >= count || archive.PackedCell > ushort.MaxValue)
                throw Invalid("Archive identity, origin, token or packed material is invalid.");
            ValidateCompanion(archive.Companion);
            if (!partition.Add(archive.Identity)) throw Invalid("Archived slots must be unique and inactive.");
            if (!origins.Add((archive.CaptureToken, archive.SourceCellIndex))) throw Invalid("One GPU capture cannot authorize two archived slots.");
        }
        if (!known.SetEquals(partition)) throw Invalid("Known slots must exactly partition current active and inactive archived material.");
    }

    private static FireSimMaterialIdentity Identity(uint target, uint slot)
    {
        if ((target == 0) != (slot == 0)) throw Invalid("Unowned target and slot must both be zero.");
        return new(target, slot);
    }

    private static void ValidateCompanion(uint companion)
    {
        if ((companion & 0xf0000000u) != 0 ||
            !Enum.IsDefined(typeof(WildfireMaterialClass), (byte)(companion & 255u)) ||
            !Enum.IsDefined(typeof(WildfireContaminationBehavior), (byte)((companion >> 22) & 7u)))
            throw Invalid("Companion field has unknown material or reserved bits.");
    }

    private static void ValidateParameters(FireSimParameters parameters)
    {
        float[] values = { parameters.VisualFireBaseIntensity, parameters.VisualFireHeatWeight,
            parameters.VisualSmokeBaseIntensity, parameters.VisualSmokeFuelWeight, parameters.VisualSmokeHeatWeight,
            parameters.AshPresentationBaseIntensity, parameters.AshPresentationFuelWeight, parameters.AshPresentationHeatWeight,
            parameters.VisualVisibilityHeatWeight, parameters.VisualVisibilitySmokeWeight, parameters.AshPresentationVisibilityWeight };
        if (values.Any(static value => float.IsNaN(value) || float.IsInfinity(value)) || parameters.FireFuelBurnDownPressureDenominator == 0)
            throw Invalid("Snapshot parameters must be finite with a positive fuel pressure denominator.");
    }

    private static T[] Copy<T>(T[]? values, int? count = null)
    {
        if (values is null || count.HasValue && values.Length != count.Value) throw Invalid("Complete snapshot array is missing or has incorrect length.");
        return (T[])values.Clone();
    }
    private static ArgumentException Invalid(string message) => new(message, "snapshot");
}
