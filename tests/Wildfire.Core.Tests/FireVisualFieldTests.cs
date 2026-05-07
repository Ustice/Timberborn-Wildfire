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
        ushort cell = PackedCell.Pack(fuel: 8, heat: 12, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0.89f, sample.Fire, precision: 4);
        Assert.Equal(0.312f, sample.Smoke, precision: 4);
        Assert.Equal(0f, sample.Ash);
        Assert.Equal(sample.Fire, sample.Visibility);
    }

    [Fact]
    public void VisualSampleUsesHeatForSmokeOnFueledCells()
    {
        ushort cell = PackedCell.Pack(fuel: 15, heat: 9, flammability: 3, water: 0, terrain: 1, heatLoss: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0.78f, sample.Fire, precision: 4);
        Assert.Equal(0.264f, sample.Smoke, precision: 4);
        Assert.Equal(0f, sample.Ash);
        Assert.True(sample.Smoke > 0f);
    }

    [Fact]
    public void VisualSampleUsesRuntimeParameters()
    {
        ushort cell = PackedCell.Pack(fuel: 15, heat: 9, flammability: 3, water: 0, terrain: 1, heatLoss: 1);
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
        ushort cell = PackedCell.Pack(fuel: 12, heat: 4, flammability: 2, water: 0, terrain: 1, heatLoss: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0f, sample.Fire);
        Assert.Equal(0.184f, sample.Smoke, precision: 4);
        Assert.Equal(0.1656f, sample.Visibility, precision: 4);
    }

    [Fact]
    public void VisualSampleDoesNotShowSmokeForHotWetTerrainWithoutFuel()
    {
        ushort cell = PackedCell.Pack(fuel: 0, heat: 4, flammability: 2, water: 2, terrain: 1, heatLoss: 1);

        FireVisualSample sample = FireVisualField.FromPackedCell(cell);

        Assert.Equal(0f, sample.Smoke);
    }

    [Fact]
    public void VisualSampleApproximatesAshOnlyFromTerrainLowFuelAndResidualHeat()
    {
        ushort spentTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 1, heatLoss: 2);
        ushort coldBareTerrain = PackedCell.Pack(fuel: 0, heat: 0, flammability: 0, water: 0, terrain: 1, heatLoss: 2);
        ushort nonTerrain = PackedCell.Pack(fuel: 0, heat: 6, flammability: 0, water: 0, terrain: 0, heatLoss: 2);

        FireVisualSample spentSample = FireVisualField.FromPackedCell(spentTerrain);

        Assert.Equal(0.808f, spentSample.Ash, precision: 4);
        Assert.Equal(0.6464f, spentSample.Visibility, precision: 4);
        Assert.Equal(0f, FireVisualField.FromPackedCell(coldBareTerrain).Ash);
        Assert.Equal(0f, FireVisualField.FromPackedCell(nonTerrain).Ash);
    }

    [Fact]
    public void ShaderSourceWritesVisualFieldFromPackedSimulationOutput()
    {
        string shader = ReadFireSimShader();

        Assert.Contains("RWStructuredBuffer<float4> VisualFields;", shader);
        Assert.Contains("RWStructuredBuffer<uint> CurrentAtmosphericFields;", shader);
        Assert.Contains("RWStructuredBuffer<uint> NextAtmosphericFields;", shader);
        Assert.Contains("float4 BuildVisualSample(uint cell)", shader);
        Assert.Contains("uint BuildAtmosphericField(uint index, uint3 coordinate, uint oldCell, uint newCell)", shader);
        Assert.Contains("uint SteamSourceFromMoistureAndHeat(uint cell)", shader);
        Assert.Contains("return water > 0u && heat > 0u ? min(7u, max(1u, ((heat * 7u) + 14u) / 15u)) : 0u;", shader);
        Assert.Contains("uint steamSource = SteamSourceFromMoistureAndHeat(newCell);", shader);
        Assert.DoesNotContain("water * heat", shader);
        Assert.Contains("uint smokeSource = hotFuel ? min(5u, 1u + (Heat(newCell) / 3u)) : 0u;", shader);
        Assert.DoesNotContain("uint steamSource = min(7u, waterDrop * 3u);", shader);
        Assert.Contains("float VisualFireBaseIntensity;", shader);
        Assert.Contains("float VisualSmokeFuelWeight;", shader);
        Assert.Contains("float VisualAshBaseIntensity;", shader);
        Assert.Contains("float VisualVisibilitySmokeWeight;", shader);
        Assert.Contains("uint FireCoolingBase;", shader);
        Assert.Contains("uint FireFuelHeatWeight;", shader);
        Assert.Contains("uint lockedFuel = min(fuel, water * FireWaterFuelLock);", shader);
        Assert.Contains("uint effectiveFuel = fuel - lockedFuel;", shader);
        Assert.Contains("bool canBurn = terrain == 1u && effectiveFuel > 0u;", shader);
        Assert.Contains("bool wasIgnitedBeforeNeighborExchange = terrain == 1u && fuel > 0u && heat >= ignitionThreshold;", shader);
        Assert.Contains("if (wasIgnitedBeforeNeighborExchange && heat < ignitionThreshold)", shader);
        Assert.Contains("if (wasIgnitedBeforeNeighborExchange && fuel > 0u && heat < ignitionThreshold)", shader);
        Assert.Contains("uint fuelHeat = ((effectiveFuel * FireFuelHeatWeight) + 14u) / 15u;", shader);
        Assert.Contains("heat = min(15u, heat + FireBurnHeatBase + flammability + fuelHeat);", shader);
        Assert.Contains("float EffectiveWindStrength()", shader);
        Assert.Contains("return saturate(WindStrength * 0.5f);", shader);
        Assert.Contains("uint WindWeightedNeighborHeat(uint neighborHeat, float directionX, float directionY)", shader);
        Assert.Contains("uint CompanionMaterialClass(uint companion)", shader);
        Assert.Contains("if (CompanionMaterialClass(companion) == 1u)", shader);
        Assert.Contains("int distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);", shader);
        Assert.Contains("distanceSquared == 0 || distanceSquared > 4", shader);
        Assert.Contains("float weight = 1.0f / max(1.0f, distance);", shader);
        Assert.Contains("heat = min(15u, (uint)round(((float)heat + windWeightedNeighborHeatSum) / (1.0f + neighborWeightSum)));", shader);
        Assert.DoesNotContain("heat = heat > suppression ? heat - suppression : 0u;", shader);
        Assert.DoesNotContain("uint neighborHeat = max(maxNeighborHeat", shader);
        Assert.DoesNotContain("FireBurningNeighborHeatBonus", shader);
        Assert.DoesNotContain("FireRetainedHeatWeight", shader);
        Assert.DoesNotContain("FireSpreadHeatWeight", shader);
        Assert.DoesNotContain("FireBurningNeighborDirectHeat", shader);
        Assert.Contains("uint FireHeatLossCoolingDivisor;", shader);
        Assert.Contains("WriteVisualField(index, newCell, atmospheric);", shader);
        Assert.Contains("if (canBurn && heat >= ignitionThreshold)", shader);
        Assert.DoesNotContain("IgnitionPressure", shader);
        Assert.DoesNotContain("ignitionPressure", shader);
        Assert.DoesNotContain("Flame", shader);
    }

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
