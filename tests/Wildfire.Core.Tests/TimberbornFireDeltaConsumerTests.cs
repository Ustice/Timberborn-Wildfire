using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class TimberbornFireDeltaConsumerTests
{
    [Fact]
    public void FromDeltaClassifiesBurningTransitionsAndFuelDepletion()
    {
        TimberbornFireCellDeltaDecision started = TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(4, Cell(fuel: 3, heat: 8), Cell(fuel: 3, heat: 10, burningLevel: 1)));
        TimberbornFireCellDeltaDecision spent = TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(5, Cell(fuel: 2, heat: 10, burningLevel: 1), Cell(fuel: 0, heat: 10)));

        Assert.True(started.StartedBurning);
        Assert.False(started.FuelDepleted);
        Assert.True(started.ShouldApplyGameplayConsequence);
        Assert.True(started.ShouldEmitAlert);

        Assert.True(spent.StoppedBurning);
        Assert.True(spent.FuelDepleted);
        Assert.True(spent.ShouldApplyGameplayConsequence);
        Assert.True(spent.ShouldEmitAlert);
    }

    [Fact]
    public void DebugVisualSpentFuelReflectsCurrentCellState()
    {
        ushort packedCell = Cell(fuel: 0, heat: 2);
        TimberbornFireCellDeltaDecision decision = TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(7, Cell(fuel: 0, heat: 1), packedCell));

        TimberbornFireDebugVisualCellState state = TimberbornFireDebugVisualCellState.FromDecision(12, decision);

        Assert.False(decision.FuelDepleted);
        Assert.Equal(packedCell, state.PackedCellValue);
        Assert.True(state.IsSpentFuel);
        Assert.Equal(0, state.Fuel);
        Assert.Equal(2, state.Heat);
    }

    [Fact]
    public void VisualEffectKindDistinguishesFuelChangesFromHeatChanges()
    {
        TimberbornFireCellDeltaDecision decision = TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(8, Cell(fuel: 5, heat: 1), Cell(fuel: 4, heat: 1)));

        TimberbornFireVisualEffectEvent effectEvent = TimberbornFireVisualEffectEvent.FromDecision(13, decision);

        Assert.True(decision.FuelChanged);
        Assert.False(decision.HeatChanged);
        Assert.Equal(TimberbornFireVisualEffectKind.FuelChanged, effectEvent.Kind);
        Assert.Equal(decision.OldWater, effectEvent.OldWater);
        Assert.Equal(decision.NewWater, effectEvent.Water);
    }

    [Fact]
    public void ConsumeRoutesChangedCellsToAdapterSinksAndSummarizesTelemetry()
    {
        RecordingFireDeltaSinks recordingSinks = new();
        RecordingFireLogSink logSink = new();
        TimberbornFireDeltaConsumer consumer = new(
            logSink,
            new TimberbornFireDeltaConsumerSinks(
                debugVisualSink: recordingSinks,
                visualEffectSink: recordingSinks,
                gameplayConsequenceSink: recordingSinks,
                alertSink: recordingSinks));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            22,
            [
                new CellDelta(1, Cell(fuel: 3, heat: 8), Cell(fuel: 3, heat: 10, burningLevel: 1)),
                new CellDelta(2, Cell(fuel: 4, heat: 2, water: 0), Cell(fuel: 4, heat: 2, water: 2)),
                new CellDelta(3, Cell(fuel: 2, heat: 10, burningLevel: 1), Cell(fuel: 0, heat: 10)),
            ]);

        Assert.Equal(3, summary.ChangedCellCount);
        Assert.Equal(3, summary.DebugVisualUpdatedCellCount);
        Assert.Equal(3, summary.DebugVisualCellCount);
        Assert.Equal(1, summary.StartedBurningCount);
        Assert.Equal(1, summary.StoppedBurningCount);
        Assert.Equal(1, summary.FuelDepletedCount);
        Assert.Equal(1, summary.HeatChangedCount);
        Assert.Equal(1, summary.WaterChangedCount);
        Assert.Equal(3, summary.VisualEffectEventCount);
        Assert.Equal(0, summary.VisualEffectFailureCount);
        Assert.Equal(2, summary.GameplayConsequenceCount);
        Assert.Equal(0, summary.BuildingBurnoutConsideredDeltaCount);
        Assert.Equal(0, summary.BuildingBurnoutMatchedCellCount);
        Assert.Equal(0, summary.BuildingBurnoutAppliedConsequenceCount);
        Assert.Equal(2, summary.AlertCount);
        Assert.Equal(10, summary.MaxHeat);

        Assert.Equal([1, 2, 3], recordingSinks.DebugVisualStates.Select(static state => state.CellIndex).ToArray());
        Assert.Equal(
            [
                TimberbornFireVisualEffectKind.BurningStarted,
                TimberbornFireVisualEffectKind.WaterChanged,
                TimberbornFireVisualEffectKind.FuelSpent,
            ],
            recordingSinks.VisualEffectEvents.Select(static effectEvent => effectEvent.Kind).ToArray());
        Assert.Equal(
            [TimberbornFireGameplayConsequenceKind.FireStarted, TimberbornFireGameplayConsequenceKind.FuelSpent],
            recordingSinks.GameplayConsequences.Select(static consequence => consequence.Kind).ToArray());
        Assert.Equal(
            [TimberbornFireAlertKind.FireStarted, TimberbornFireAlertKind.FuelSpent],
            recordingSinks.AlertEvents.Select(static alertEvent => alertEvent.Kind).ToArray());
        Assert.True(recordingSinks.DebugVisualStates.Single(static state => state.CellIndex == 3).IsSpentFuel);
        Assert.Equal(recordingSinks.DebugVisualStates[2], consumer.DebugVisualCells[3]);

        string logToken = Assert.Single(logSink.InfoMessages);
        Assert.Contains("wildfire_timberborn_delta_consumer_completed", logToken);
        Assert.Contains("debug_visual_updated_cells=3", logToken);
        Assert.Contains("visual_effect_events=3", logToken);
        Assert.Contains("visual_effect_failures=0", logToken);
        Assert.Contains("gameplay_consequences=2", logToken);
        Assert.Contains("building_burnout_considered_deltas=0", logToken);
        Assert.Contains("alerts=2", logToken);
    }

    [Fact]
    public void ConsumeIsolatesVisualSinkExceptionsAndStillRoutesConsequences()
    {
        RecordingFireDeltaSinks recordingSinks = new();
        RecordingFireLogSink logSink = new();
        TimberbornFireDeltaConsumer consumer = new(
            logSink,
            new TimberbornFireDeltaConsumerSinks(
                visualEffectSink: new ThrowingVisualEffectSink(),
                gameplayConsequenceSink: recordingSinks,
                alertSink: recordingSinks));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            27,
            [new CellDelta(1, Cell(fuel: 3, heat: 8), Cell(fuel: 3, heat: 10, burningLevel: 1))]);

        Assert.Equal(1, summary.VisualEffectEventCount);
        Assert.Equal(1, summary.VisualEffectFailureCount);
        Assert.Equal(1, summary.GameplayConsequenceCount);
        Assert.Equal(1, summary.AlertCount);
        Assert.Equal([TimberbornFireGameplayConsequenceKind.FireStarted],
            recordingSinks.GameplayConsequences.Select(static consequence => consequence.Kind).ToArray());
        Assert.Equal([TimberbornFireAlertKind.FireStarted],
            recordingSinks.AlertEvents.Select(static alertEvent => alertEvent.Kind).ToArray());
        Assert.Contains(
            logSink.WarningMessages,
            message => message.Contains("wildfire_timberborn_visual_effect_sink_failed tick=27 stage=update"));
        Assert.Contains(
            logSink.InfoMessages,
            message => message.Contains(
                "wildfire_timberborn_delta_consumer_completed tick=27") &&
                message.Contains("visual_effect_failures=1") &&
                message.Contains("gameplay_consequences=1") &&
                message.Contains("alerts=1"));
    }

    [Fact]
    public void ConsumeRoutesChangedCellsToBuildingBurnoutSink()
    {
        RecordingBuildingBurnoutConsequenceApi consequenceApi = new(
            static consequence => new TimberbornBuildingBurnoutConsequenceResult(
                MatchedBuildingCell: consequence.CellIndex is 3 or 4,
                AppliedConsequence: consequence.CellIndex == 3 && consequence.ShouldApplyBurnout));
        RecordingFireLogSink logSink = new();
        TimberbornFireDeltaConsumer consumer = new(
            logSink,
            new TimberbornFireDeltaConsumerSinks(
                buildingBurnoutConsequenceSink: new TimberbornBuildingBurnoutConsequenceSink(consequenceApi)));

        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            26,
            [
                new CellDelta(2, Cell(fuel: 5, heat: 1), Cell(fuel: 4, heat: 1)),
                new CellDelta(3, Cell(fuel: 2, heat: 10), Cell(fuel: 0, heat: 10)),
                new CellDelta(4, Cell(fuel: 2, heat: 5), Cell(fuel: 2, heat: 6)),
            ]);

        Assert.Equal(3, summary.BuildingBurnoutConsideredDeltaCount);
        Assert.Equal(2, summary.BuildingBurnoutMatchedCellCount);
        Assert.Equal(1, summary.BuildingBurnoutAppliedConsequenceCount);
        Assert.Equal([2, 3, 4], consequenceApi.Consequences.Select(static consequence => consequence.CellIndex).ToArray());
        Assert.False(consequenceApi.Consequences[0].ShouldApplyBurnout);
        Assert.True(consequenceApi.Consequences[1].ShouldApplyBurnout);
        Assert.False(consequenceApi.Consequences[2].ShouldApplyBurnout);

        string logToken = Assert.Single(logSink.InfoMessages);
        Assert.Contains("building_burnout_considered_deltas=3", logToken);
        Assert.Contains("building_burnout_matched_cells=2", logToken);
        Assert.Contains("building_burnout_applied_consequences=1", logToken);
    }

    [Fact]
    public void ConsumeFiltersDeltasWithoutVisualStateChanges()
    {
        RecordingFireDeltaSinks recordingSinks = new();
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                debugVisualSink: recordingSinks,
                visualEffectSink: recordingSinks,
                gameplayConsequenceSink: recordingSinks,
                alertSink: recordingSinks));

        ushort unchangedCell = Cell(fuel: 3, heat: 4, water: 1);
        TimberbornFireDeltaConsumerSummary summary = consumer.Consume(
            23,
            [new CellDelta(4, unchangedCell, unchangedCell)]);

        Assert.Equal(1, summary.ChangedCellCount);
        Assert.Equal(0, summary.DebugVisualUpdatedCellCount);
        Assert.Equal(0, summary.DebugVisualCellCount);
        Assert.Empty(consumer.DebugVisualCells);
        Assert.Empty(recordingSinks.DebugVisualStates);
        Assert.Empty(recordingSinks.VisualEffectEvents);
        Assert.Empty(recordingSinks.GameplayConsequences);
        Assert.Empty(recordingSinks.AlertEvents);
    }

    [Fact]
    public void ConsumeRetainsLastPositiveWaterChangeEvidenceAfterZeroWaterDispatch()
    {
        TimberbornFireDeltaConsumer consumer = new(new RecordingFireLogSink());
        ushort dryCell = Cell(fuel: 4, heat: 2, water: 0);
        ushort wetCell = Cell(fuel: 4, heat: 2, water: 3);

        consumer.Consume(31, [new CellDelta(4, dryCell, wetCell)]);
        TimberbornFireDeltaConsumerSummary zeroWaterSummary =
            consumer.Consume(32, [new CellDelta(5, dryCell, Cell(fuel: 4, heat: 5, water: 0))]);

        Assert.Equal(0, zeroWaterSummary.WaterChangedCount);
        Assert.Equal(31u, consumer.LastPositiveWaterChangedTick);
        Assert.Equal(1, consumer.LastPositiveWaterChangedCount);
    }

    [Fact]
    public void ConsumeRetainsLastPositiveBuildingBurnoutEvidenceAfterZeroAppliedDispatch()
    {
        RecordingBuildingBurnoutConsequenceApi consequenceApi = new(
            static consequence => new TimberbornBuildingBurnoutConsequenceResult(
                MatchedBuildingCell: true,
                AppliedConsequence: consequence.ShouldApplyBurnout));
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(
                buildingBurnoutConsequenceSink: new TimberbornBuildingBurnoutConsequenceSink(consequenceApi)));

        consumer.Consume(41, [new CellDelta(6, Cell(fuel: 2, heat: 10), Cell(fuel: 0, heat: 10))]);
        TimberbornFireDeltaConsumerSummary zeroAppliedSummary =
            consumer.Consume(42, [new CellDelta(7, Cell(fuel: 4, heat: 1), Cell(fuel: 3, heat: 1))]);

        Assert.Equal(0, zeroAppliedSummary.BuildingBurnoutAppliedConsequenceCount);
        Assert.Equal(41u, consumer.LastPositiveBuildingBurnoutAppliedTick);
        Assert.Equal(1, consumer.LastPositiveBuildingBurnoutAppliedCount);
    }

    [Fact]
    public void ConsumeRetainsLastPositiveBurnDamageEvidenceAfterZeroAppliedDispatch()
    {
        ScriptedBurnDamageSink burnDamageSink = new(
            [
                new TimberbornBurnDamageApplySummary(
                    Tick: 0,
                    ConsideredCellCount: 1,
                    DamageCandidateCellCount: 1,
                    ResolvedTargetCellCount: 1,
                    UnresolvedCellCount: 0,
                    DuplicateCellSuppressedCount: 0,
                    DamageAppliedTargetCount: 1,
                    TotalDamageApplied: 3,
                    PersistenceWriteCount: 1),
                TimberbornBurnDamageApplySummary.Empty,
            ]);
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(burnDamageSink: burnDamageSink));

        consumer.Consume(51, [new CellDelta(6, Cell(fuel: 12, heat: 15), Cell(fuel: 9, heat: 15))]);
        TimberbornFireDeltaConsumerSummary zeroAppliedSummary =
            consumer.Consume(52, [new CellDelta(7, Cell(fuel: 4, heat: 1), Cell(fuel: 3, heat: 1))]);

        Assert.Equal(0, zeroAppliedSummary.BurnDamageAppliedTargetCount);
        Assert.Equal(51u, consumer.LastPositiveBurnDamageAppliedTick);
        Assert.Equal(1, consumer.LastPositiveBurnDamageAppliedTargetCount);
        Assert.Equal(3, consumer.LastPositiveBurnDamageTotalDamageApplied);
    }

    [Fact]
    public void ConsumeRetainsConstructionPhaseStructureEvidenceAfterLaterSkippedNoDamageDispatch()
    {
        ScriptedStructureBurnDamageRollbackSink structureRollbackSink = new(
            [
                new TimberbornStructureBurnDamageRollbackSummary(
                    ConsideredDeltaCount: 1,
                    MatchedStructureCellCount: 1,
                    DuplicateStructureTargetSuppressedCount: 0,
                    ZeroBurnableCapacityTargetCount: 0,
                    MaterialValueLost: 3,
                    ClosedStructureCount: 0,
                    RepairBlockedCount: 1,
                    RepairEligibleCount: 0,
                    ScorchedStageCount: 0,
                    PartialConstructionStageCount: 0,
                    UnfinishedStageCount: 1,
                    VisualRollbackAppliedCount: 1,
                    ConstructionPhaseEnteredCount: 1,
                    SkippedNativeConstructionApiCount: 0,
                    SkippedNoSafeApiCount: 0,
                    TotalDamageApplied: 3),
                new TimberbornStructureBurnDamageRollbackSummary(
                    ConsideredDeltaCount: 1,
                    MatchedStructureCellCount: 1,
                    DuplicateStructureTargetSuppressedCount: 0,
                    ZeroBurnableCapacityTargetCount: 0,
                    MaterialValueLost: 0,
                    ClosedStructureCount: 0,
                    RepairBlockedCount: 0,
                    RepairEligibleCount: 0,
                    ScorchedStageCount: 0,
                    PartialConstructionStageCount: 0,
                    UnfinishedStageCount: 1,
                    VisualRollbackAppliedCount: 1,
                    ConstructionPhaseEnteredCount: 0,
                    SkippedNativeConstructionApiCount: 1,
                    SkippedNoSafeApiCount: 1,
                    TotalDamageApplied: 0),
            ]);
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(structureBurnDamageRollbackSink: structureRollbackSink));

        TimberbornFireDeltaConsumerSummary appliedSummary =
            consumer.Consume(61, [new CellDelta(6, Cell(fuel: 12, heat: 15), Cell(fuel: 9, heat: 15))]);
        TimberbornFireDeltaConsumerSummary zeroAppliedSummary =
            consumer.Consume(62, [new CellDelta(7, Cell(fuel: 4, heat: 1), Cell(fuel: 3, heat: 1))]);

        Assert.Equal(1, appliedSummary.StructureBurnDamageRollbackConstructionPhaseEnteredCount);
        Assert.Equal(1, zeroAppliedSummary.StructureBurnDamageRollbackMatchedStructureCellCount);
        Assert.Equal(1, zeroAppliedSummary.StructureBurnDamageRollbackUnfinishedStageCount);
        Assert.Equal(0, zeroAppliedSummary.StructureBurnDamageRollbackConstructionPhaseEnteredCount);
        Assert.Equal(1, zeroAppliedSummary.StructureBurnDamageRollbackSkippedNativeConstructionApiCount);
        Assert.Equal(1, zeroAppliedSummary.StructureBurnDamageRollbackSkippedNoSafeApiCount);
        Assert.Equal(61u, consumer.LastPositiveStructureBurnDamageRollbackTick);
        Assert.Equal(1, consumer.LastPositiveStructureBurnDamageRollbackUnfinishedStageCount);
        Assert.Equal(1, consumer.LastPositiveStructureBurnDamageRollbackConstructionPhaseEnteredCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackSkippedNativeConstructionApiCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackSkippedNoSafeApiCount);
        Assert.Equal(3, consumer.LastPositiveStructureBurnDamageRollbackTotalDamageApplied);
    }

    [Fact]
    public void ConsumeRetainsSkippedStructureEvidenceWhenNoConstructionSuccessExists()
    {
        ScriptedStructureBurnDamageRollbackSink structureRollbackSink = new(
            [
                new TimberbornStructureBurnDamageRollbackSummary(
                    ConsideredDeltaCount: 1,
                    MatchedStructureCellCount: 1,
                    DuplicateStructureTargetSuppressedCount: 0,
                    ZeroBurnableCapacityTargetCount: 0,
                    MaterialValueLost: 0,
                    ClosedStructureCount: 0,
                    RepairBlockedCount: 1,
                    RepairEligibleCount: 0,
                    ScorchedStageCount: 0,
                    PartialConstructionStageCount: 0,
                    UnfinishedStageCount: 1,
                    VisualRollbackAppliedCount: 1,
                    ConstructionPhaseEnteredCount: 0,
                    SkippedNativeConstructionApiCount: 1,
                    SkippedNoSafeApiCount: 1,
                    TotalDamageApplied: 0),
                TimberbornStructureBurnDamageRollbackSummary.Empty,
            ]);
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(structureBurnDamageRollbackSink: structureRollbackSink));

        consumer.Consume(71, [new CellDelta(6, Cell(fuel: 12, heat: 15), Cell(fuel: 9, heat: 15))]);
        consumer.Consume(72, [new CellDelta(7, Cell(fuel: 4, heat: 1), Cell(fuel: 3, heat: 1))]);

        Assert.Equal(71u, consumer.LastPositiveStructureBurnDamageRollbackTick);
        Assert.Equal(1, consumer.LastPositiveStructureBurnDamageRollbackUnfinishedStageCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackConstructionPhaseEnteredCount);
        Assert.Equal(1, consumer.LastPositiveStructureBurnDamageRollbackSkippedNativeConstructionApiCount);
        Assert.Equal(1, consumer.LastPositiveStructureBurnDamageRollbackSkippedNoSafeApiCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackTotalDamageApplied);
    }

    [Fact]
    public void ConsumeUpdatesOnlyLatestOverlayStatesForRoutedChangedCells()
    {
        RecordingFireDeltaSinks recordingSinks = new();
        TimberbornFireDeltaConsumer consumer = new(
            new RecordingFireLogSink(),
            new TimberbornFireDeltaConsumerSinks(debugVisualSink: recordingSinks));

        ushort firstOverlayCell = Cell(fuel: 3, heat: 10);
        ushort secondOverlayCell = Cell(fuel: 3, heat: 10, water: 1);
        ushort unchangedCell = Cell(fuel: 5, heat: 1);

        TimberbornFireDeltaConsumerSummary firstSummary = consumer.Consume(
            24,
            [new CellDelta(9, Cell(fuel: 3, heat: 8), firstOverlayCell)]);
        TimberbornFireDeltaConsumerSummary secondSummary = consumer.Consume(
            25,
            [
                new CellDelta(9, firstOverlayCell, secondOverlayCell),
                new CellDelta(10, unchangedCell, unchangedCell),
            ]);

        Assert.Equal(1, firstSummary.DebugVisualUpdatedCellCount);
        Assert.Equal(1, firstSummary.DebugVisualCellCount);
        Assert.Equal(1, secondSummary.DebugVisualUpdatedCellCount);
        Assert.Equal(1, secondSummary.DebugVisualCellCount);
        Assert.Equal([9, 9], recordingSinks.DebugVisualStates.Select(static state => state.CellIndex).ToArray());
        Assert.False(consumer.DebugVisualCells.ContainsKey(10));

        TimberbornFireDebugVisualCellState currentState = consumer.DebugVisualCells[9];
        Assert.Equal(25u, currentState.Tick);
        Assert.Equal(secondOverlayCell, currentState.PackedCellValue);
        Assert.Equal(1, currentState.Water);
    }

    [Fact]
    public void ResetClearsDebugVisualStateAndSummary()
    {
        TimberbornFireDeltaConsumer consumer = new(new RecordingFireLogSink());
        consumer.Consume(3, [new CellDelta(1, Cell(fuel: 3, heat: 8), Cell(fuel: 3, heat: 10))]);

        consumer.Reset();

        Assert.Empty(consumer.DebugVisualCells);
        Assert.Equal(TimberbornFireDeltaConsumerSummary.Empty, consumer.LastSummary);
        Assert.Equal(0u, consumer.LastPositiveWaterChangedTick);
        Assert.Equal(0, consumer.LastPositiveWaterChangedCount);
        Assert.Equal(0u, consumer.LastPositiveBurnDamageAppliedTick);
        Assert.Equal(0, consumer.LastPositiveBurnDamageAppliedTargetCount);
        Assert.Equal(0, consumer.LastPositiveBurnDamageTotalDamageApplied);
        Assert.Equal(0u, consumer.LastPositiveStructureBurnDamageRollbackTick);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackUnfinishedStageCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackConstructionPhaseEnteredCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackSkippedNativeConstructionApiCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackSkippedNoSafeApiCount);
        Assert.Equal(0, consumer.LastPositiveStructureBurnDamageRollbackTotalDamageApplied);
    }

    private static ushort Cell(int fuel, int heat, int flammability = 3, int water = 0, int terrain = 1, int burningLevel = 0)
    {
        return PackedCell.Pack(fuel, heat, flammability, water, terrain, burningLevel);
    }

    private sealed class RecordingFireDeltaSinks :
        ITimberbornFireDebugVisualSink,
        ITimberbornFireVisualEffectSink,
        ITimberbornFireGameplayConsequenceSink,
        ITimberbornFireAlertSink
    {
        public List<TimberbornFireDebugVisualCellState> DebugVisualStates { get; } = [];

        public List<TimberbornFireVisualEffectEvent> VisualEffectEvents { get; } = [];

        public List<TimberbornFireGameplayConsequence> GameplayConsequences { get; } = [];

        public List<TimberbornFireAlertEvent> AlertEvents { get; } = [];

        public void UpdateDebugVisualState(TimberbornFireDebugVisualCellState state)
        {
            DebugVisualStates.Add(state);
        }

        public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
        {
            VisualEffectEvents.Add(effectEvent);
        }

        public void ApplyConsequence(TimberbornFireGameplayConsequence consequence)
        {
            GameplayConsequences.Add(consequence);
        }

        public void PublishAlert(TimberbornFireAlertEvent alertEvent)
        {
            AlertEvents.Add(alertEvent);
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public List<string> InfoMessages { get; } = [];

        public List<string> WarningMessages { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
            WarningMessages.Add(message);
        }
    }

    private sealed class ThrowingVisualEffectSink : ITimberbornFireVisualEffectSink
    {
        public void UpdateVisualEffect(TimberbornFireVisualEffectEvent effectEvent)
        {
            throw new InvalidOperationException("visual boom");
        }
    }

    private sealed class RecordingBuildingBurnoutConsequenceApi(
        Func<TimberbornBuildingBurnoutConsequence, TimberbornBuildingBurnoutConsequenceResult> apply)
        : ITimberbornBuildingBurnoutConsequenceApi
    {
        public List<TimberbornBuildingBurnoutConsequence> Consequences { get; } = [];

        public TimberbornBuildingBurnoutConsequenceResult ApplyConsequence(
            TimberbornBuildingBurnoutConsequence consequence)
        {
            Consequences.Add(consequence);
            return apply(consequence);
        }
    }

    private sealed class ScriptedBurnDamageSink(IReadOnlyList<TimberbornBurnDamageApplySummary> summaries)
        : ITimberbornBurnDamageSink
    {
        private int _index;

        public TimberbornBurnDamageApplySummary ApplyDamage(
            uint tick,
            IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
        {
            TimberbornBurnDamageApplySummary summary = _index < summaries.Count
                ? summaries[_index]
                : TimberbornBurnDamageApplySummary.Empty;
            _index++;
            return summary with { Tick = tick };
        }
    }

    private sealed class ScriptedStructureBurnDamageRollbackSink(
        IReadOnlyList<TimberbornStructureBurnDamageRollbackSummary> summaries)
        : ITimberbornStructureBurnDamageRollbackSink
    {
        private int _index;

        public TimberbornStructureBurnDamageRollbackSummary ApplyConsequences(
            uint tick,
            IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
        {
            TimberbornStructureBurnDamageRollbackSummary summary = _index < summaries.Count
                ? summaries[_index]
                : TimberbornStructureBurnDamageRollbackSummary.Empty;
            _index++;
            return summary;
        }
    }
}
