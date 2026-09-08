namespace Wildfire.Timberborn.Consequences;

public enum NativeBurnTargetFamily
{
    Stockpile, Structure, Tree, Crop, SelectedCrop, PowerInfrastructure, WaterInfrastructure, PathInfrastructure,
}

/// <summary>Native entity identity survives reloading; the family distinguishes independently registered targets.</summary>
public static class TimberbornBurnDamageIdentity
{
    public static bool IsLegacyRuntimeHash(string key)
    {
        int separator = key.IndexOf(':');
        if (separator < 0) return false;
        string family = key.Substring(0, separator);
        return family is "stockpile" or "structure" or "tree_cuttable" or "crop_harvestable" or
            "selected_crop_harvestable" or "power_infrastructure" or "water_infrastructure" or "path_infrastructure"
            && int.TryParse(key.Substring(separator + 1), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out _);
    }

    /// <summary>Strict bridge for existing persisted native family keys; never parses legacy runtime hashes.</summary>
    public static bool TryGetEntity(string key, NativeBurnTargetFamily family, out Guid entityId)
    {
        entityId = Guid.Empty;
        string prefix = FamilyPrefix(family) + ":entity:";
        return key.StartsWith(prefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(key.Substring(prefix.Length), "D", out entityId) && entityId != Guid.Empty;
    }

    public static string ForEntity(Guid entityId, NativeBurnTargetFamily family)
    {
        if (entityId == Guid.Empty)
            throw new InvalidOperationException("Cannot register burn damage before the native entity has a persistent identity.");
        return $"{FamilyPrefix(family)}:entity:{entityId:D}";
    }

    private static string FamilyPrefix(NativeBurnTargetFamily family) => family switch
        {
            NativeBurnTargetFamily.Stockpile => "stockpile",
            NativeBurnTargetFamily.Structure => "structure",
            NativeBurnTargetFamily.Tree => "tree_cuttable",
            NativeBurnTargetFamily.Crop => "crop_harvestable",
            NativeBurnTargetFamily.SelectedCrop => "selected_crop_harvestable",
            NativeBurnTargetFamily.PowerInfrastructure => "power_infrastructure",
            NativeBurnTargetFamily.WaterInfrastructure => "water_infrastructure",
            NativeBurnTargetFamily.PathInfrastructure => "path_infrastructure",
            _ => throw new ArgumentOutOfRangeException(nameof(family)),
        };
}
