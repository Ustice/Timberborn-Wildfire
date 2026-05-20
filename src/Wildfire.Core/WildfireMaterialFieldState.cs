namespace Wildfire.Core;

public readonly record struct WildfireMaterialFieldState(
    WildfireMaterialClass MaterialClass,
    byte BurnCapacity,
    byte BurnHistory,
    byte AshStrength,
    WildfireAshQuality AshQuality,
    WildfireContaminationBehavior ContaminationBehavior,
    byte SoilContamination = 0)
{
    public static readonly WildfireMaterialFieldState Empty = new(
        WildfireMaterialClass.Empty,
        BurnCapacity: 0,
        BurnHistory: 0,
        AshStrength: 0,
        WildfireAshQuality.None,
        WildfireContaminationBehavior.None);

    public static readonly WildfireMaterialFieldState Unknown = new(
        WildfireMaterialClass.Unknown,
        BurnCapacity: 0,
        BurnHistory: 0,
        AshStrength: 0,
        WildfireAshQuality.None,
        WildfireContaminationBehavior.FailClosed);

    public uint Pack()
    {
        return ((uint)MaterialClass & 0xFFu) |
            ((uint)Math.Clamp((int)BurnCapacity, 0, 15) << 8) |
            ((uint)Math.Clamp((int)BurnHistory, 0, 15) << 12) |
            ((uint)Math.Clamp((int)AshStrength, 0, 15) << 16) |
            (((uint)AshQuality & 0x3u) << 20) |
            (((uint)ContaminationBehavior & 0x7u) << 22) |
            ((uint)Math.Clamp((int)SoilContamination, 0, 7) << 25);
    }

    public static WildfireMaterialFieldState FromMaterialProfile(WildfireMaterialFieldProfile profile)
    {
        return new WildfireMaterialFieldState(
            profile.MaterialClass,
            profile.BurnCapacity,
            BurnHistory: 0,
            AshStrength: 0,
            profile.AshQuality,
            profile.ContaminationBehavior,
            SoilContamination: 0);
    }

    public static WildfireMaterialFieldState Unpack(uint packed)
    {
        return new WildfireMaterialFieldState(
            (WildfireMaterialClass)(packed & 0xFFu),
            BurnCapacity: (byte)((packed >> 8) & 0xFu),
            BurnHistory: (byte)((packed >> 12) & 0xFu),
            AshStrength: (byte)((packed >> 16) & 0xFu),
            (WildfireAshQuality)((packed >> 20) & 0x3u),
            (WildfireContaminationBehavior)((packed >> 22) & 0x7u),
            SoilContamination: (byte)((packed >> 25) & 0x7u));
    }
}

public readonly record struct WildfireMaterialField(
    uint TargetId,
    WildfireMaterialFieldState State)
{
    public static readonly WildfireMaterialField Empty = new(
        TargetId: 0,
        WildfireMaterialFieldState.Empty);
}
