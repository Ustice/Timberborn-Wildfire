namespace Wildfire.Timberborn.Mapping;

public static class TimberbornInfrastructureNameClassifier
{
    public static bool IsAnyInfrastructureName(string name)
    {
        return IsPathInfrastructureName(name) ||
            IsPowerInfrastructureName(name) ||
            IsWaterInfrastructureName(name) ||
            name.Contains("Dynamite", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPathInfrastructureName(string name)
    {
        return name.Contains("Path", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Slope", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Platform", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Bridge", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Stair", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Fence", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPowerInfrastructureName(string name)
    {
        return name.Contains("Power", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Shaft", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Gear", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mechanical", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Wheel", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Engine", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWaterInfrastructureName(string name)
    {
        return name.Contains("Pump", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dam", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Levee", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Floodgate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Valve", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Sluice", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Irrigation", StringComparison.OrdinalIgnoreCase);
    }
}
