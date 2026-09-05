using Wildfire.Core;

namespace Wildfire.Timberborn.Qa;

internal static class TimberbornQaStimulusValues
{
    internal const byte QaIgnitionHeat = TimberbornSustainedIgnitionScheduler.IgnitionHeat;
    internal const byte QaIgnitionFuel = 15;
    internal const byte QaIgnitionFlammability = 3;
    internal const byte QaIgnitionTerrain = 1;
    internal const byte QaIgnitionWater = 0;
    internal const int QaDeltaStimulusSustainedHeatCycleCount = 12;
    internal static readonly ushort QaDeltaStimulusCell = PackedCell.Pack(
        fuel: QaIgnitionFuel,
        heat: QaIgnitionHeat,
        flammability: QaIgnitionFlammability,
        water: QaIgnitionWater,
        terrain: QaIgnitionTerrain,
        burningLevel: 0);
    internal static readonly ushort QaBeaverSmokeExposureStimulusCell = PackedCell.Pack(
        fuel: QaIgnitionFuel,
        heat: QaIgnitionHeat,
        flammability: 0,
        water: QaIgnitionWater,
        terrain: QaIgnitionTerrain,
        burningLevel: 0);
    internal const byte QaBeaverToxicSmokeExposureSmoke = 5;
    internal const byte QaBeaverToxicSmokeExposureContamination = 7;
    internal const byte QaSpentFuel = 0;
    internal const byte QaWaterSuppressionWater = 3;
    internal const byte QaTaintedAshAmount = 3;
    internal const byte QaTaintedAshContamination = 7;
    internal const int QaStructureBurnDamageAcceptanceDamage = 3;

}
