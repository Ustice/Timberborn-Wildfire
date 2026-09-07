namespace Wildfire.Core;

/// <summary>
/// Canonical unowned material only. Ambient heat, wetness, ash and soil contamination are not part of
/// this definition and must never be replayed when a previously covered baseline is revealed.
/// </summary>
public readonly record struct FireSimBaselineDefinition
{
    public static readonly FireSimBaselineDefinition Empty = new(0u, 0u);
    public static readonly FireSimBaselineDefinition SolidTerrain = new(0x1000u, 1u);
    public static readonly FireSimBaselineDefinition OpenSoil = new(0u, 1u);
    public static readonly FireSimBaselineDefinition Water = new(0u, 8u | (3u << 22));
    public static readonly FireSimBaselineDefinition Badwater = new(0u, 9u | (3u << 20) | (2u << 22));

    public FireSimBaselineDefinition(FireSimMaterialDefinition definition)
    {
        if (!IsCanonical(definition.PackedMaterial, definition.CompanionMaterial))
            throw new ArgumentException("Baseline must be an exact canonical zero-fuel unowned material.", nameof(definition));
        PackedMaterial = definition.PackedMaterial;
        CompanionMaterial = definition.CompanionMaterial;
    }

    private FireSimBaselineDefinition(uint material, uint companion)
    {
        PackedMaterial = material;
        CompanionMaterial = companion;
    }

    public uint PackedMaterial { get; }
    public uint CompanionMaterial { get; }
    public WildfireMaterialClass MaterialClass => (WildfireMaterialClass)(CompanionMaterial & 255u);

    internal static bool IsCanonical(uint material, uint companion) =>
        (material == 0u && (companion == Empty.CompanionMaterial || companion == OpenSoil.CompanionMaterial ||
            companion == Water.CompanionMaterial || companion == Badwater.CompanionMaterial)) ||
        (material == SolidTerrain.PackedMaterial && companion == SolidTerrain.CompanionMaterial);
}
