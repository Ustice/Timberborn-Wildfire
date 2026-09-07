namespace Wildfire.Timberborn.Consequences;

internal readonly record struct TimberbornStoredGoodFuelSelection(TimberbornStoredGoodStack[] Stacks, int RemainingBudget);

internal readonly record struct TimberbornStoredGoodFuelStack(TimberbornStoredGoodStack Stack, byte FuelValue);

/// <summary>Existing per-owner/resource fractional fuel policy. Transient; not a saved goods/history ledger.</summary>
internal sealed class TimberbornStoredGoodFuelBudget
{
    private readonly Dictionary<string, int> _partialFuel = new(StringComparer.Ordinal);
    internal bool HasCredit => _partialFuel.Count != 0;

    internal TimberbornStoredGoodFuelSelection Select(string stableId, IEnumerable<TimberbornStoredGoodFuelStack> stacks, int burnBudget)
    {
        int remainingBudget = Math.Max(0, burnBudget);
        var selected = stacks.Select(stack =>
        {
            int fuelValue = Math.Max(1, (int)stack.FuelValue);
            string key = $"{stableId}:{stack.Stack.ResourceId}";
            int partial = _partialFuel.GetValueOrDefault(key);
            int spendable = partial + remainingBudget;
            int amount = Math.Min(stack.Stack.Amount, spendable / fuelValue);
            int consumed = amount * fuelValue;
            remainingBudget = Math.Max(0, remainingBudget - Math.Max(0, consumed - partial));
            int nextPartial = spendable - consumed;
            if (amount >= stack.Stack.Amount)
            {
                _partialFuel.Remove(key);
                remainingBudget = nextPartial;
            }
            else if (nextPartial > 0)
            {
                _partialFuel[key] = nextPartial;
                remainingBudget = 0;
            }
            else _partialFuel.Remove(key);
            return new TimberbornStoredGoodStack(stack.Stack.ResourceId, amount);
        }).Where(stack => stack.Amount > 0).ToArray();
        return new TimberbornStoredGoodFuelSelection(selected, remainingBudget);
    }
}
