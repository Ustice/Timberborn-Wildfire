using System.Text.Json.Nodes;
using Wildfire.Cli;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class ShaderSnapshotHarnessTests
{
    [Fact]
    public void FixtureLoaderReadsCliFixtureExportAndCreatesBufferGrid()
    {
        Scenario scenario = ScenarioCatalog.Build(CliOptions.Parse(
        [
            "--scenario=mixed-terrain",
            "--seed=42",
            "--width=4",
            "--height=3",
            "--depth=2",
            "--layer=1",
        ]));
        string json = FixtureExporter.Export(scenario, selectedLayer: 1);

        ShaderSnapshotFixture fixture = ShaderSnapshotFixtureLoader.Load(json, "mixed-terrain.fixture.json");

        Assert.Equal(1, fixture.FormatVersion);
        Assert.Equal("mixed-terrain", fixture.Scenario);
        Assert.Equal(42u, fixture.Seed);
        Assert.Equal(new ComputeGridDimensions(4, 3, 2), fixture.Grid);
        Assert.Equal(new ShaderSnapshotLayer(1, 12, 12), fixture.SelectedLayer);
        Assert.Equal(scenario.Cells, fixture.InitialCells);

        RecordingComputeBufferAllocator allocator = new();
        using ComputeBufferGrid grid = fixture.CreateBufferGrid(allocator);

        Assert.Equal(fixture.Grid, grid.Dimensions);
        Assert.Equal(scenario.Cells.Select(static cell => (uint)cell).ToArray(), allocator.CurrentCells.UploadedValues);
    }

    [Fact]
    public void HarnessCapturesFixtureDrivenSnapshotAndWritesReviewableJson()
    {
        ShaderSnapshotFixture fixture = new(
            FormatVersion: 1,
            Scenario: "single-ignition",
            Seed: 9,
            Grid: new ComputeGridDimensions(2, 1, 1),
            SelectedLayer: new ShaderSnapshotLayer(0, 0, 2),
            InitialCells: [0x1001, 0x1002]);
        ShaderSnapshotCapture capture = new(
            Scenario: fixture.Scenario,
            Seed: fixture.Seed,
            Grid: fixture.Grid,
            TickCount: 2,
            FinalPackedCells: [0x1001, 0x1003],
            Ticks:
            [
                new ShaderSnapshotTick(1, 1, [new ShaderSnapshotDelta(1, 0x1002, 0x1003)]),
                new ShaderSnapshotTick(2, 0, []),
            ],
            Visual: new ShaderSnapshotVisual(Checksum: "heat:0000002A"));
        ShaderSnapshotHarness harness = new(new RecordingShaderSnapshotExecutor(capture));

        ShaderSnapshotCapture actual = harness.Capture(fixture, tickCount: 2);
        string json = ShaderSnapshotJson.Serialize(actual);
        JsonNode snapshot = JsonNode.Parse(json) ?? throw new InvalidOperationException("Snapshot JSON did not parse.");

        Assert.Equal(capture, actual);
        Assert.Equal(1, (int?)snapshot["formatVersion"]);
        Assert.Equal("single-ignition", (string?)snapshot["scenario"]);
        Assert.Equal(2, (int?)snapshot["tickCount"]);
        Assert.Equal(2, snapshot["finalPackedCells"]?.AsArray().Count);
        Assert.Equal(1, (int?)snapshot["perTickDeltaCounts"]?[0]);
        Assert.Equal(0, (int?)snapshot["perTickDeltaCounts"]?[1]);
        Assert.Equal(1, (int?)snapshot["perTickDeltas"]?[0]?["deltas"]?[0]?["cellIndex"]);
        Assert.Equal("heat:0000002A", (string?)snapshot["visual"]?["checksum"]);
    }

    [Fact]
    public void ComparisonReportsActionableCellTickAndVisualDifferences()
    {
        ShaderSnapshotCapture expected = CreateCapture(
            finalPackedCells: [0x1001, 0x1002],
            ticks: [new ShaderSnapshotTick(1, 1, [new ShaderSnapshotDelta(1, 0x1001, 0x1002)])],
            visualChecksum: "heat:expected");
        ShaderSnapshotCapture actual = CreateCapture(
            finalPackedCells: [0x1001, 0x2002],
            ticks: [new ShaderSnapshotTick(1, 1, [new ShaderSnapshotDelta(1, 0x1001, 0x2002)])],
            visualChecksum: "heat:actual");

        ShaderSnapshotComparison comparison = ShaderSnapshotComparison.Create(expected, actual);

        Assert.False(comparison.Matches);
        Assert.Contains("finalPackedCells[1] expected 0x1002, got 0x2002.", comparison.Differences);
        Assert.Contains("ticks[1].deltas[0] expected cell 1 0x1001->0x1002, got cell 1 0x1001->0x2002.", comparison.Differences);
        Assert.Contains("visual.checksum expected heat:expected, got heat:actual.", comparison.Differences);
    }

    [Fact]
    public void BlockedExecutorMakesCurrentShaderExecutionBlockerExplicit()
    {
        ShaderSnapshotHarness harness = new(new BlockedShaderSnapshotExecutor(ShaderSnapshotExecutionBlocker.CurrentRepository));
        ShaderSnapshotFixture fixture = new(
            FormatVersion: 1,
            Scenario: "water-barrier",
            Seed: 1,
            Grid: new ComputeGridDimensions(1, 1, 1),
            SelectedLayer: new ShaderSnapshotLayer(0, 0, 1),
            InitialCells: [0]);

        ShaderSnapshotExecutionBlockedException exception = Assert.Throws<ShaderSnapshotExecutionBlockedException>(
            () => harness.Capture(fixture, tickCount: 1));

        Assert.Contains("no Unity batchmode project", exception.Blocker.Reason);
        Assert.Contains("IShaderSnapshotExecutor", exception.Blocker.Enablement);
    }

    private static ShaderSnapshotCapture CreateCapture(
        ushort[] finalPackedCells,
        ShaderSnapshotTick[] ticks,
        string? visualChecksum = null)
    {
        return new ShaderSnapshotCapture(
            Scenario: "single-ignition",
            Seed: 5,
            Grid: new ComputeGridDimensions(2, 1, 1),
            TickCount: ticks.Length,
            FinalPackedCells: finalPackedCells,
            Ticks: ticks,
            Visual: new ShaderSnapshotVisual(Checksum: visualChecksum));
    }

    private sealed class RecordingShaderSnapshotExecutor(ShaderSnapshotCapture capture) : IShaderSnapshotExecutor
    {
        public ShaderSnapshotCapture Capture(ShaderSnapshotFixture fixture, int tickCount)
        {
            Assert.Equal(capture.Scenario, fixture.Scenario);
            Assert.Equal(capture.Seed, fixture.Seed);
            Assert.Equal(capture.Grid, fixture.Grid);
            Assert.Equal(capture.TickCount, tickCount);
            return capture;
        }
    }

    private sealed class RecordingComputeBufferAllocator : IComputeBufferAllocator
    {
        public RecordingComputeBufferHandle CurrentCells { get; private set; } = null!;

        public IComputeBufferHandle Allocate(string name, int count, int strideBytes)
        {
            RecordingComputeBufferHandle handle = new(name, count, strideBytes);
            if (name == "wildfire.current_cells")
            {
                CurrentCells = handle;
            }

            return handle;
        }

        public IAppendComputeBufferHandle AllocateAppend(string name, int count, int strideBytes)
        {
            return new RecordingComputeBufferHandle(name, count, strideBytes);
        }
    }

    private sealed class RecordingComputeBufferHandle(string name, int count, int strideBytes) : IAppendComputeBufferHandle
    {
        public string Name { get; } = name;

        public int Count { get; } = count;

        public int StrideBytes { get; } = strideBytes;

        public uint[] UploadedValues { get; private set; } = [];

        public void Upload(ReadOnlySpan<uint> values)
        {
            UploadedValues = values.ToArray();
        }

        public void ResetAppendCounter()
        {
        }

        public int ReadAppendCounter()
        {
            return 0;
        }

        public uint[] ReadAppendedData(int elementCount)
        {
            return [];
        }

        public void Dispose()
        {
        }
    }
}
