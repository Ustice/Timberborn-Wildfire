namespace Wildfire.Core;

public readonly record struct WildfireAtmosphericFieldState(
    byte Steam,
    byte Smoke,
    byte SmokeContamination,
    byte Ash,
    byte AshContamination,
    bool Source)
{
    public static readonly WildfireAtmosphericFieldState Empty = new(
        Steam: 0,
        Smoke: 0,
        SmokeContamination: 0,
        Ash: 0,
        AshContamination: 0,
        Source: false);

    public uint Pack()
    {
        return ((uint)Math.Clamp((int)Steam, 0, 7) << 0) |
            ((uint)Math.Clamp((int)Smoke, 0, 7) << 3) |
            ((uint)Math.Clamp((int)SmokeContamination, 0, 7) << 6) |
            ((uint)Math.Clamp((int)Ash, 0, 7) << 9) |
            ((uint)Math.Clamp((int)AshContamination, 0, 7) << 12) |
            (Source ? 1u << 15 : 0u);
    }

    public static WildfireAtmosphericFieldState Unpack(uint packed)
    {
        byte steam = (byte)((packed >> 0) & 0x7u);
        byte smoke = (byte)((packed >> 3) & 0x7u);
        byte smokeContamination = smoke == 0 ? (byte)0 : (byte)((packed >> 6) & 0x7u);
        byte ash = (byte)((packed >> 9) & 0x7u);
        byte ashContamination = ash == 0 ? (byte)0 : (byte)((packed >> 12) & 0x7u);

        return new WildfireAtmosphericFieldState(
            steam,
            smoke,
            smokeContamination,
            ash,
            ashContamination,
            ((packed >> 15) & 0x1u) != 0u);
    }
}
