namespace Wildfire.Timberborn.Runtime;

// Every live consequence lane is prepared before runtime initialization starts.
// No optional/partially attached set is published to the runtime.
internal sealed record TimberbornRuntimeBindings(
    ITimberbornBuildingBurnoutConsequenceApi BuildingBurnout,
    ITimberbornQaBuildingBurnoutStimulusTargetProvider BuildingBurnoutStimulus,
    TimberbornBurnDamageService BurnDamageService,
    ITimberbornTreeBurnConsequenceApi TreeBurn,
    ITimberbornCropBurnConsequenceApi CropBurn,
    ITimberbornStructureBurnDamageRollbackTargetApi StructureRollback,
    ITimberbornStoredGoodBurnInventoryApi StoredInventory,
    ITimberbornQaInventoryAdjuster InventoryAdjuster,
    ITimberbornNativeBlastRadiusApi BlastRadius,
    ITimberbornExplosiveInfrastructureTargetApi ExplosiveInfrastructure,
    ITimberbornDetonatorFireSafetyTargetApi DetonatorSafety,
    ITimberbornTunnelFireTargetApi TunnelFire,
    ITimberbornPathInfrastructureFireTargetApi PathFire,
    ITimberbornPowerInfrastructureFireTargetApi PowerFire,
    ITimberbornWaterInfrastructureFireTargetApi WaterFire);
