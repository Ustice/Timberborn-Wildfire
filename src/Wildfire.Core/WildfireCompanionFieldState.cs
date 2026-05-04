namespace Wildfire.Core;

public readonly record struct WildfireCompanionFieldState(
    WildfireMaterialClass MaterialClass,
    byte BurnCapacity,
    byte BurnHistory,
    byte AshStrength,
    WildfireAshQuality AshQuality,
    WildfireContaminationBehavior ContaminationBehavior)
{
    public static readonly WildfireCompanionFieldState Empty = new(
        WildfireMaterialClass.Empty,
        BurnCapacity: 0,
        BurnHistory: 0,
        AshStrength: 0,
        WildfireAshQuality.None,
        WildfireContaminationBehavior.None);

    public static readonly WildfireCompanionFieldState Unknown = new(
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
            (((uint)ContaminationBehavior & 0x7u) << 22);
    }

    public static WildfireCompanionFieldState FromMaterialProfile(WildfireMaterialFieldProfile profile)
    {
        return new WildfireCompanionFieldState(
            profile.MaterialClass,
            profile.BurnCapacity,
            BurnHistory: 0,
            AshStrength: 0,
            profile.AshQuality,
            profile.ContaminationBehavior);
    }

    public static WildfireCompanionFieldState Unpack(uint packed)
    {
        return new WildfireCompanionFieldState(
            (WildfireMaterialClass)(packed & 0xFFu),
            BurnCapacity: (byte)((packed >> 8) & 0xFu),
            BurnHistory: (byte)((packed >> 12) & 0xFu),
            AshStrength: (byte)((packed >> 16) & 0xFu),
            (WildfireAshQuality)((packed >> 20) & 0x3u),
            (WildfireContaminationBehavior)((packed >> 22) & 0x7u));
    }
}

public readonly record struct WildfireCompanionField(
    uint TargetId,
    WildfireCompanionFieldState State)
{
    public static readonly WildfireCompanionField Empty = new(
        TargetId: 0,
        WildfireCompanionFieldState.Empty);
}
