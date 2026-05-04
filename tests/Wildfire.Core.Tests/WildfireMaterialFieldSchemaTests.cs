using System.Text.Json;
using Wildfire.Core;

namespace Wildfire.Core.Tests;

public sealed class WildfireMaterialFieldSchemaTests
{
    [Fact]
    public void DefaultSchemaContainsExpectedMaterialClasses()
    {
        Assert.Equal(
            new[]
            {
                WildfireMaterialClass.Empty,
                WildfireMaterialClass.Terrain,
                WildfireMaterialClass.Vegetation,
                WildfireMaterialClass.Crop,
                WildfireMaterialClass.Tree,
                WildfireMaterialClass.Building,
                WildfireMaterialClass.Storage,
                WildfireMaterialClass.Infrastructure,
                WildfireMaterialClass.Water,
                WildfireMaterialClass.Badwater,
                WildfireMaterialClass.Unknown,
            },
            WildfireMaterialFieldSchema.Default.Profiles.Select(static profile => profile.MaterialClass));
    }

    [Fact]
    public void UnknownMaterialFailsClosed()
    {
        WildfireMaterialFieldProfile profile = WildfireMaterialFieldSchema.Default.Lookup((WildfireMaterialClass)255);

        Assert.Equal(WildfireMaterialClass.Unknown, profile.MaterialClass);
        Assert.Equal(0, profile.Fuel);
        Assert.Equal(0, profile.BurnCapacity);
        Assert.Equal(WildfireConsequenceTargetKind.None, profile.ConsequenceTargetKind);
        Assert.Equal(WildfireContaminationBehavior.FailClosed, profile.ContaminationBehavior);
        Assert.Equal(WildfireResourcePolicy.FailClosed, profile.ResourcePolicy);
    }

    [Fact]
    public void JsonFixtureMatchesDefaultSchema()
    {
        MaterialFieldSchemaJson fixture = ReadFixture();
        IReadOnlyList<WildfireMaterialFieldProfile> expected = WildfireMaterialFieldSchema.Default.Profiles;

        Assert.Equal(WildfireMaterialFieldSchema.CurrentFormatVersion, fixture.FormatVersion);
        Assert.Equal(expected.Count, fixture.Profiles.Length);
        expected
            .Zip(fixture.Profiles, static (profile, json) => (profile, json))
            .ToList()
            .ForEach(pair => AssertMatches(pair.profile, pair.json));
    }

    private static void AssertMatches(WildfireMaterialFieldProfile profile, MaterialFieldProfileJson json)
    {
        Assert.Equal(ToJsonName(profile.MaterialClass), json.MaterialClass);
        Assert.Equal(profile.Fuel, json.Fuel);
        Assert.Equal(profile.Flammability, json.Flammability);
        Assert.Equal(profile.HeatLoss, json.HeatLoss);
        Assert.Equal(profile.Terrain, json.Terrain);
        Assert.Equal(profile.Water, json.Water);
        Assert.Equal(profile.BurnCapacity, json.BurnCapacity);
        Assert.Equal(ToJsonName(profile.ConsequenceTargetKind), json.ConsequenceTargetKind);
        Assert.Equal(ToJsonName(profile.AshQuality), json.AshQuality);
        Assert.Equal(ToJsonName(profile.ContaminationBehavior), json.ContaminationBehavior);
        Assert.Equal(ToJsonName(profile.ResourcePolicy), json.ResourcePolicy);
    }

    private static MaterialFieldSchemaJson ReadFixture()
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Wildfire.Core",
            "MaterialFieldSchema.v1.json"));
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<MaterialFieldSchemaJson>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private static string ToJsonName(WildfireMaterialClass value) => value switch
    {
        WildfireMaterialClass.Empty => "empty",
        WildfireMaterialClass.Terrain => "terrain",
        WildfireMaterialClass.Vegetation => "vegetation",
        WildfireMaterialClass.Crop => "crop",
        WildfireMaterialClass.Tree => "tree",
        WildfireMaterialClass.Building => "building",
        WildfireMaterialClass.Storage => "storage",
        WildfireMaterialClass.Infrastructure => "infrastructure",
        WildfireMaterialClass.Water => "water",
        WildfireMaterialClass.Badwater => "badwater",
        WildfireMaterialClass.Unknown => "unknown",
        _ => "unknown",
    };

    private static string ToJsonName(WildfireConsequenceTargetKind value) => value switch
    {
        WildfireConsequenceTargetKind.None => "none",
        WildfireConsequenceTargetKind.Crop => "crop",
        WildfireConsequenceTargetKind.Tree => "tree",
        WildfireConsequenceTargetKind.Structure => "structure",
        WildfireConsequenceTargetKind.Storage => "storage",
        WildfireConsequenceTargetKind.Infrastructure => "infrastructure",
        WildfireConsequenceTargetKind.Water => "water",
        _ => "none",
    };

    private static string ToJsonName(WildfireAshQuality value) => value switch
    {
        WildfireAshQuality.None => "none",
        WildfireAshQuality.Fertile => "fertile",
        WildfireAshQuality.Spent => "spent",
        WildfireAshQuality.Tainted => "tainted",
        _ => "none",
    };

    private static string ToJsonName(WildfireContaminationBehavior value) => value switch
    {
        WildfireContaminationBehavior.None => "none",
        WildfireContaminationBehavior.TaintIfSourceContaminated => "taint-if-source-contaminated",
        WildfireContaminationBehavior.TaintedSource => "tainted-source",
        WildfireContaminationBehavior.SuppressesWithoutCleaning => "suppresses-without-cleaning",
        WildfireContaminationBehavior.FailClosed => "fail-closed",
        _ => "none",
    };

    private static string ToJsonName(WildfireResourcePolicy value) => value switch
    {
        WildfireResourcePolicy.Fixed => "fixed",
        WildfireResourcePolicy.UseResourceCatalog => "use-resource-catalog",
        WildfireResourcePolicy.FailClosed => "fail-closed",
        _ => "fixed",
    };

    private sealed record MaterialFieldSchemaJson(int FormatVersion, MaterialFieldProfileJson[] Profiles);

    private sealed record MaterialFieldProfileJson(
        string MaterialClass,
        int Fuel,
        int Flammability,
        int HeatLoss,
        int Terrain,
        int Water,
        int BurnCapacity,
        string ConsequenceTargetKind,
        string AshQuality,
        string ContaminationBehavior,
        string ResourcePolicy);
}
