namespace Wildfire.Timberborn.Mapping;

// Declared native capability, independent of physical stock and admitted material/effect roles.
public enum TimberbornNativeInventoryRole { Stockpile, SimpleOutput, GoodStack, Manufactory, RecoveredGoodStack }

public sealed record TimberbornInventoryDeclaration
{
    public TimberbornInventoryDeclaration(TimberbornNativeInventoryRole role, string componentName)
    {
        if (!Enum.IsDefined(typeof(TimberbornNativeInventoryRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (string.IsNullOrWhiteSpace(componentName)) throw new ArgumentException("Native inventory requires its exact component name.", nameof(componentName));
        Role = role; ComponentName = componentName;
    }
    public TimberbornNativeInventoryRole Role { get; }
    public string ComponentName { get; }
}
