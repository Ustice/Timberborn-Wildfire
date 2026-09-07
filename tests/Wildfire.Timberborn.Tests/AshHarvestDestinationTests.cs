using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class AshHarvestDestinationTests
{
    [Fact]
    public void LostDistrictAssignmentDeclinesFallbackBeforeReadingNavigationOrInventories()
    {
        using var native = new NativeManagedTestContext();
        var citizenType = native.LoadNative("Timberborn.GameDistricts").GetType("Timberborn.GameDistricts.Citizen")!;
        var citizen = RuntimeHelpers.GetUninitializedObject(citizenType);
        // A reservation callback may clear assignment before fallback. Execute the real native
        // null-assignment getter and production decline path; no live GameObject or route is claimed.
        Assert.False((bool)citizenType.GetProperty("HasAssignedDistrict")!.GetValue(citizen)!);
        var executorType = native.LoadMod().GetType("Wildfire.Timberborn.Ash.AshHarvestExecutor")!;
        var executor = RuntimeHelpers.GetUninitializedObject(executorType);
        executorType.GetField("_citizen", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(executor, citizen);
        Assert.False((bool)executorType.GetMethod("TryOtherDistrictInventories", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(executor, [null])!);
    }
}
