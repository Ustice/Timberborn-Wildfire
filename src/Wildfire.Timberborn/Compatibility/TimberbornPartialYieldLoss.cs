using System.Reflection;
using System.Security.Cryptography;
using Timberborn.Goods;
using Timberborn.Yielding;

namespace Wildfire.Timberborn.Compatibility;

internal enum TimberbornPartialYieldLossStatus
{
    Applied, NotLive, DeferredReserved, DeferredInactive, NoYield, DepletionRequired, Unavailable,
}
internal readonly record struct TimberbornPartialYieldLossResult(TimberbornPartialYieldLossStatus Status, int Removed = 0);

/// <summary>
/// Reviewed positive-partial loss of authoritative native yield. Caller owns exact entity/family validation
/// and the existing outer resource guard. Never raises the native harvest event or changes future yield.
/// </summary>
internal static class TimberbornPartialYieldLoss
{
    private static readonly Lazy<FieldInfo> YieldField = new(VerifyNativeField);
    internal static void Verify() => _ = YieldField.Value;

    internal static Yielder SelectNamed(IEnumerable<Yielder> yielders, string componentName)
    {
        var matches = yielders.Where(value => value.ComponentName == componentName).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Native resource must resolve one exact named yielder.");
        return matches[0];
    }

    internal static TimberbornPartialYieldLossResult Apply(Yielder yielder, string expectedComponentName,
        string expectedGoodId, int requested)
    {
        if (yielder is null) throw new ArgumentNullException(nameof(yielder));
        if (requested <= 0) throw new ArgumentOutOfRangeException(nameof(requested));
        Verify(); // Fail compatibility before touching quantities, reservations or Enabled.
        if (string.IsNullOrEmpty(expectedComponentName) || yielder.ComponentName != expectedComponentName)
            throw new InvalidOperationException("Native yield component name does not match the selected resource.");
        var before = (GoodAmount)YieldField.Value.GetValue(yielder)!;
        if (string.IsNullOrEmpty(expectedGoodId) || before.GoodId != expectedGoodId || before.Amount < 0)
            throw new InvalidOperationException("Native yield quantity does not match the selected resource.");
        if (yielder.Reservable is null) return new(TimberbornPartialYieldLossStatus.Unavailable);
        if (yielder.Reservable.Reserved) return new(TimberbornPartialYieldLossStatus.DeferredReserved);
        if (!yielder.Enabled) return new(TimberbornPartialYieldLossStatus.DeferredInactive);
        if (before.Amount == 0) return new(TimberbornPartialYieldLossStatus.NoYield);
        if (requested >= before.Amount) return new(TimberbornPartialYieldLossStatus.DepletionRequired);

        // There are no callbacks between admission and this one native field assignment. _initialYield,
        // growth, native stacks and reservations retain their own state. Zero needs a separate lifecycle.
        var after = new GoodAmount(before.GoodId, before.Amount - requested);
        YieldField.Value.SetValue(yielder, after);
        var observed = (GoodAmount)YieldField.Value.GetValue(yielder)!;
        if (observed.GoodId != after.GoodId || observed.Amount != after.Amount)
            throw new InvalidOperationException("Native partial yield write did not produce its exact receipt.");
        return new(TimberbornPartialYieldLossStatus.Applied, requested);
    }

    private static FieldInfo VerifyNativeField()
    {
        string directory = Path.GetDirectoryName(typeof(Yielder).Assembly.Location)!;
        foreach (var pair in Fingerprints)
        {
            using var stream = File.OpenRead(Path.Combine(directory, pair.Key));
            using var sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            RequireFingerprint(pair.Key, pair.Value, actual);
        }
        var field = typeof(Yielder).GetField("_yield", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field is null || field.FieldType != typeof(GoodAmount) || field.IsStatic || field.IsInitOnly)
            throw new InvalidOperationException("Native current-yield field shape changed.");
        return field;
    }
    internal static void RequireFingerprint(string assembly, string expected, string actual)
    {
        if (actual != expected)
            throw new InvalidOperationException($"Native partial yield loss is not reviewed for {assembly} ({actual}).");
    }
    private static readonly IReadOnlyDictionary<string, string> Fingerprints = new Dictionary<string, string>
    {
        ["Timberborn.Yielding.dll"] = "de35358244b4b6c3ee880596ee1fc09b0d7a5928c5f481a360e2a3b6db3916d5",
        ["Timberborn.Gathering.dll"] = "fa592667f30dd8e206030d954cdf2f68e73bce1ed20bbf995fd5b04971aeadfa",
        ["Timberborn.Cutting.dll"] = "cd57d0a80d5964130228ad5df79bab15aeb94c527a9cef26ae893b85083fdbcd",
        ["Timberborn.Goods.dll"] = "18a2514e0984bfb4105e2deb918b149b6cacf20b379caef0366df22587e8da7b",
        ["Timberborn.ReservableSystem.dll"] = "49d6a259ba27c0c3008d783f5b608b13082a0e1cf61b495c11a7534ab7ee9a8c",
        ["Timberborn.BaseComponentSystem.dll"] = "0d479b9533880e1368b5df144f8d83f30d9c8774cfc8f96f071ac53d5851318d",
    };
}
