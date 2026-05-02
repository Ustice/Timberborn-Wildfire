using Wildfire.Core;
using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornFireDeltaConsumerTests
{
    [Fact]
    public void FromDeltaClassifiesBurningTransitionsAndFuelDepletion()
    {
        TimberbornFireCellDeltaDecision started = TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(4, Cell(fuel: 3, heat: 8), Cell(fuel: 3, heat: 10)));
        TimberbornFireCellDeltaDecision spent = TimberbornFireCellDeltaDecision.FromDelta(
            new CellDelta(5, Cell(fuel: 2, heat: 10), Cell(fuel: 0, heat: 10)));

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
                new CellDelta(1, Cell(fuel: 3, heat: 8), Cell(fuel: 3, heat: 10)),
                new CellDelta(2, Cell(fuel: 4, heat: 2, water: 0), Cell(fuel: 4, heat: 2, water: 2)),
                new CellDelta(3, Cell(fuel: 2, heat: 10), Cell(fuel: 0, heat: 10)),
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
        Assert.Equal(2, summary.GameplayConsequenceCount);
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
        Assert.Contains("gameplay_consequences=2", logToken);
        Assert.Contains("alerts=2", logToken);
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
    }

    private static ushort Cell(int fuel, int heat, int flammability = 3, int water = 0, int terrain = 1, int heatLoss = 0)
    {
        return PackedCell.Pack(fuel, heat, flammability, water, terrain, heatLoss);
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

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
        }
    }
}
