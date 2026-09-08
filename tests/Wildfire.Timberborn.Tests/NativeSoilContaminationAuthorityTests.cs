using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Timberborn.Ash;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeSoilContaminationAuthorityTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.8f)]
    public void NativeRenderHookCannotReportAuthoritativePoisoning(float originalContamination)
    {
        using SoilFixture native = new(originalContamination);
        Assembly mod = native.Context.LoadMod();
        // The removed wrapper's method-name check reported true for this installed render method.
        Assert.Null(mod.GetType("Wildfire.Timberborn.Ash.TimberbornSoilContaminationPoisoningApi"));
        MethodInfo render = native.Service.GetType().GetMethod("UpdateContamination",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        byte[] il = render.GetMethodBody()!.GetILAsByteArray()!;
        Assert.Equal(20, il.Length);
        Assert.Equal("_terrainMaterialMap", render.Module.ResolveField(BitConverter.ToInt32(il, 2))!.Name);
        Assert.Equal("GetMapSoilContamination", render.Module.ResolveMethod(BitConverter.ToInt32(il, 10))!.Name);
        Assert.Equal("SetSoilContamination", render.Module.ResolveMethod(BitConverter.ToInt32(il, 15))!.Name);

        Assert.Equal(originalContamination, native.Contamination(3));
        TimberbornTaintedAshSoilPoisoningSummary summary =
            UnavailableTimberbornTaintedAshSoilPoisoningAdapter.Instance.ApplyPoisoning(
                1, [new TimberbornTaintedAshSoilPoisoningCandidate(7, 3)]);
        Assert.Equal(TimberbornTaintedAshSoilPoisoningOutcome.Unavailable, summary.Outcome);
        Assert.Equal(0, summary.AppliedCellCount);
        Assert.Equal(originalContamination, native.Contamination(3));
    }

    private sealed class SoilFixture : IDisposable
    {
        internal readonly NativeManagedTestContext Context = new();
        internal readonly object Service;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal SoilFixture(float contamination)
        {
            Type serviceType = Context.LoadNative("Timberborn.SoilContaminationSystem").GetType("Timberborn.SoilContaminationSystem.SoilContaminationService")!;
            Service = RuntimeHelpers.GetUninitializedObject(serviceType);
            Set(Service, "_threadSafeContaminationLevels", Enumerable.Repeat(contamination, 8).ToArray());
        }

        internal float Contamination(int index) => (float)Service.GetType().GetMethod("Contamination", Flags)!.Invoke(Service, [index])!;
        private static void Set(object owner, string name, object? value) => owner.GetType().GetField(name, Flags)!.SetValue(owner, value);
        public void Dispose() => Context.Dispose();
    }
}
