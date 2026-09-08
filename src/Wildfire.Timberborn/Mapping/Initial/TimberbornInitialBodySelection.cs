namespace Wildfire.Timberborn.Mapping;

// Transient internal compilation choices, never native quantity authority or saved history.
internal enum TimberbornInitialAccountingBasis { NativeResourceAmounts = 1, CatalogBodyProfile = 2 }
internal enum TimberbornInitialYieldUse { Excluded = 1, Actual = 2, Declared = 3 }
internal enum TimberbornInitialInventoryUse { Excluded = 1, PhysicalStock = 2 }
internal sealed record TimberbornInitialYieldSelection(string ComponentName, TimberbornCapturedYieldRole Role,
    TimberbornInitialYieldUse Use);
internal sealed record TimberbornInitialInventorySelection(TimberbornInventoryDeclaration Declaration,
    TimberbornInitialInventoryUse Accounting);

internal sealed class TimberbornInitialBodySelection
{
    internal TimberbornInitialBodySelection(Guid entityId, TimberbornInitialAccountingBasis accounting,
        IEnumerable<TimberbornInitialYieldSelection> yields, IEnumerable<TimberbornInitialInventorySelection> inventories)
    {
        if (entityId == Guid.Empty || !Enum.IsDefined(typeof(TimberbornInitialAccountingBasis), accounting))
            throw new ArgumentException("Initial accounting requires an exact captured Guid and explicit basis.");
        var named = yields?.ToArray() ?? throw new ArgumentNullException(nameof(yields));
        var stock = inventories?.ToArray() ?? throw new ArgumentNullException(nameof(inventories));
        if (named.Any(item => item is null || string.IsNullOrWhiteSpace(item.ComponentName) ||
                !Enum.IsDefined(typeof(TimberbornCapturedYieldRole), item.Role) || !Enum.IsDefined(typeof(TimberbornInitialYieldUse), item.Use)) ||
            named.Select(item => item.ComponentName).Distinct(StringComparer.Ordinal).Count() != named.Length ||
            stock.Any(item => item is null || item.Declaration is null ||
                !Enum.IsDefined(typeof(TimberbornInitialInventoryUse), item.Accounting)) ||
            stock.Select(item => item.Declaration.Role).Distinct().Count() != stock.Length ||
            stock.Select(item => item.Declaration.ComponentName).Distinct(StringComparer.Ordinal).Count() != stock.Length)
            throw new ArgumentException("Initial role selections must be explicit, valid and unique.");
        EntityId = entityId; Accounting = accounting;
        Yields = Array.AsReadOnly(named); Inventories = Array.AsReadOnly(stock);
    }
    internal Guid EntityId { get; }
    internal TimberbornInitialAccountingBasis Accounting { get; }
    internal IReadOnlyList<TimberbornInitialYieldSelection> Yields { get; }
    internal IReadOnlyList<TimberbornInitialInventorySelection> Inventories { get; }
}
