using Wildfire.Core;
using Wildfire.Unity;

namespace Wildfire.Core.Tests;

public sealed class FireVisualFieldTests
{
    [Fact]
    public void VisualFieldUsesFloat4CellStride()
    {
        Assert.Equal(4, FireVisualField.ChannelCount);
        Assert.Equal(sizeof(float) * 4, FireVisualField.StrideBytes);
        Assert.Equal(FireVisualField.StrideBytes, ComputeBufferGrid.VisualFieldStrideBytes);
    }

    [Fact]
    public void VisualSampleDerivesFireSmokeAndVisibilityFromPackedBurningCell()
    {
        ushort cell = PackedCell.Pack(fuel: 8, heat: 12, flammability: 3, water: 0, terrain: 1, burningLevel: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0.89f, sample.Fire, precision: 4);
        Assert.Equal(0.312f, sample.Smoke, precision: 4);
        Assert.Equal(0f, sample.Ash);
        Assert.Equal(sample.Fire, sample.Visibility);
    }

    [Fact]
    public void VisualSampleUsesHeatForSmokeOnFueledCells()
    {
        ushort cell = PackedCell.Pack(fuel: 15, heat: 9, flammability: 3, water: 0, terrain: 1, burningLevel: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0.78f, sample.Fire, precision: 4);
        Assert.Equal(0.264f, sample.Smoke, precision: 4);
        Assert.Equal(0f, sample.Ash);
        Assert.True(sample.Smoke > 0f);
    }

    [Fact]
    public void VisualSampleUsesRuntimeParameters()
    {
        ushort cell = PackedCell.Pack(fuel: 15, heat: 9, flammability: 3, water: 0, terrain: 1, burningLevel: 1);
        FireSimParameters parameters = FireSimParameters.Default with
        {
            VisualSmokeHeatWeight = 0.1f,
            VisualFireHeatWeight = 0.25f,
        };

        FireVisualSample sample = FireVisualField.FromPackedCell(cell, parameters);

        Assert.Equal(0.6f, sample.Fire, precision: 4);
        Assert.Equal(0.18f, sample.Smoke, precision: 4);
        Assert.Equal(sample.Fire, sample.Visibility);
    }

    [Fact]
    public void VisualSampleShowsLightSmokeForLowHeatFuelBeforeIgnition()
    {
        ushort cell = PackedCell.Pack(fuel: 12, heat: 4, flammability: 2, water: 0, terrain: 1, burningLevel: 0);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0f, sample.Fire);
        Assert.Equal(0.184f, sample.Smoke, precision: 4);
        Assert.Equal(0.1656f, sample.Visibility, precision: 4);
    }

    [Fact]
    public void VisualSampleDoesNotShowSmokeForHotWetTerrainWithoutFuel()
    {
        ushort cell = PackedCell.Pack(fuel: 0, heat: 4, flammability: 2, water: 2, terrain: 1, burningLevel: 0);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0f, sample.Smoke);
    }

    [Fact]
    public void VisualSampleApproximatesAshOnlyFromTerrainLowFuelAndResidualHeat()
    {
        ushort spentTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        ushort coldBareTerrain = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, burningLevel: 0);
        ushort nonTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 0, burningLevel: 0);

        FireVisualSample spentSample = FireVisualField.FromPackedCell(spentTerrain);

        Assert.Equal(0.808f, spentSample.Ash, precision: 4);
        Assert.Equal(0.6464f, spentSample.Visibility, precision: 4);
        Assert.Equal(0f, FireVisualField.FromPackedCell(coldBareTerrain).Ash);
        Assert.Equal(0f, FireVisualField.FromPackedCell(nonTerrain).Ash);
    }

    [Fact]
    public void ShaderSourceExposesRuntimeWiring()
    {
        string shader = ReadFireSimShader();

        Assert.Contains("#pragma kernel ApplyExternalChanges", shader);
        Assert.Contains("#pragma kernel SimulateFullGrid", shader);
        Assert.Contains("RWStructuredBuffer<float4> VisualFields;", shader);
        Assert.Contains("RWStructuredBuffer<uint> CurrentAtmosphericFields;", shader);
        Assert.Contains("RWStructuredBuffer<uint> NextAtmosphericFields;", shader);
        Assert.Contains("RWStructuredBuffer<uint> CurrentCells;", shader);
        Assert.Contains("RWStructuredBuffer<uint> NextCells;", shader);
        Assert.Contains("StructuredBuffer<uint> CompanionFields;", shader);
        Assert.Contains("StructuredBuffer<FireSimChangeGpu> ExternalChanges;", shader);
        Assert.Contains("AppendStructuredBuffer<CellDeltaGpu> Deltas;", shader);
        Assert.Contains("float4 BuildVisualSample(uint cell)", shader);
        Assert.Contains("uint BuildAtmosphericField(uint index, uint3 coordinate, uint oldCell, uint newCell)", shader);
        Assert.Contains("uint SteamSourceFromMoistureAndHeat(uint cell)", shader);
        Assert.Contains("uint steamSource = SteamSourceFromMoistureAndHeat(newCell);", shader);
        Assert.Contains("uint StepCell", shader);
        Assert.Contains("uint FireCellStepIntervalTicks;", shader);
        Assert.Contains("bool shouldStepReaction = fireStepInterval == 1u || (Tick % fireStepInterval) == 0u;", shader);
        Assert.Contains("uint newCell = StepCell(index, id, oldCell, shouldStepReaction);", shader);
        Assert.Contains("if (shouldStepReaction)", shader);
        Assert.Contains("float EffectiveWindStrength()", shader);
        Assert.Contains("uint WindWeightedNeighborHeat(uint neighborHeat, float directionX, float directionY)", shader);
        Assert.Contains("uint CompanionMaterialClass(uint companion)", shader);
        Assert.Contains("uint BurningLevel(uint cell)", shader);
        Assert.Contains("WriteVisualField(index, newCell, atmospheric);", shader);

        typeof(FireSimParameters)
            .GetProperties()
            .Select(property => $"{ShaderTypeName(property.PropertyType)} {property.Name};")
            .ToList()
            .ForEach(declaration => Assert.Contains(declaration, shader));

        Assert.Contains("uint IgnitionThreshold(uint flammability, uint water)", shader);
        Assert.Contains("if (water > 0u && heat > 0u)", shader);
        Assert.Contains("bool canBurn = terrain == 1u && fuel > 0u;", shader);
        Assert.Contains("if (canBurn && (burningLevel > 0u || heat >= ignitionThreshold))", shader);
        Assert.DoesNotContain("FireWaterFuelLock", shader);
        Assert.DoesNotContain("FireWaterEvaporationHeat", shader);
        Assert.DoesNotContain("FireFlammabilityBurnPressure", shader);
        Assert.DoesNotContain("FireWaterBurnPressurePenalty", shader);
        Assert.DoesNotContain("FireCoolingBase", shader);
    }

    private static string ShaderTypeName(Type type) =>
        type == typeof(float) ? "float" :
        type == typeof(uint) ? "uint" :
        throw new NotSupportedException($"No shader type mapping exists for {type.Name}.");

    private static string ReadFireSimShader()
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(directory.FullName, "src", "Wildfire.Unity", "FireSim.compute"))
            .First(File.Exists);

        return File.ReadAllText(path);
    }

    private static IEnumerable<DirectoryInfo> SelfAndParents(DirectoryInfo directory)
    {
        return directory.Parent is null
            ? [directory]
            : new[] { directory }.Concat(SelfAndParents(directory.Parent));
    }
}
