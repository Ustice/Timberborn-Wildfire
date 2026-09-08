using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenNaturalFillTests
{
    [Fact]
    public void ActualConverterAndNativeProducedAccountingConsumeOnlyCleanQuantum()
    {
        using var f = new F();
        Assert.Equal(.2f, f.Quantum);
        f.Credited(.4f, .125f);
        int commits = 0;
        Assert.Equal("Filled", f.Fill(commit: () => { commits++; f.RequireSaveExcluded(); }));
        Assert.Equal((.4f - f.Quantum, .125f), f.Buffer);
        Assert.Equal(1, f.Native.Quantity(f.Native.Source)); Assert.Equal(1, commits);
        Assert.Equal(1, f.Native.Call(f.Native.Balance, "GetProduction", "Water"));
        Assert.Equal(0, f.Native.Call(f.Native.Balance, "GetConsumption", "Water"));
        Assert.Equal(0, f.Pending);
        Assert.Equal("Unavailable", f.Fill()); Assert.Equal(1, commits);
        Assert.False(f.Native.Poisoned);
    }
    [Theory]
    [InlineData(0f)]
    [InlineData(.19999999f)]
    public void InsufficientNativeCreditQueuesOneFullDemandWithoutDebitOrProduct(float clean)
    {
        using var f = new F(); f.Credited(clean, .125f);
        Assert.Equal("AwaitingCredit", f.Fill(commit: () => throw new Exception("not committed")));
        Assert.Equal((clean, .125f), f.Buffer);
        Assert.Equal(0, f.Native.Quantity(f.Native.Source)); Assert.Equal(1, f.Pending);
        var change = ((IList)NativeShorelineWaterFixture.Get(f.Changes, "_waterChanges")!)[0]!;
        Assert.Equal(-f.Quantum, f.Native.Get(change, "DepthChange"));
        Assert.False(f.Native.Poisoned);
    }
    [Theory]
    [InlineData("full")]
    [InlineData("disabled")]
    [InlineData("capacity")]
    [InlineData("foreign")]
    public void UnavailableBucketNeverDemandsOrDebits(string state)
    {
        using var f = new F(); f.Credited(.4f, .125f);
        if (state == "full") f.Native.Give(f.Native.Source);
        if (state == "disabled") f.Native.Call(f.Native.Source, "Disable");
        if (state == "capacity") f.Native.Call(f.Native.Source, "ReserveCapacity", f.Native.Amount());
        if (state == "foreign") f.Native.Call(NativeShorelineWaterFixture.Get(f.Native.Source, "_storage")!, "Add", f.Native.Amount("FertileAsh"));
        Assert.Equal("Unavailable", f.Fill());
        Assert.Equal((.4f, .125f), f.Buffer); Assert.Equal(0, f.Pending); Assert.False(f.Native.Poisoned);
    }
    [Theory]
    [InlineData("InventoryChanged", 0)]
    [InlineData("InventoryStockChanged", 1)]
    public void ActualNativeCallbackFailureDoesNotInventCompletedProduction(string eventName, int recorded)
    {
        using var f = new F(); f.Credited(.2f, .125f); var cause = new IOException(eventName);
        f.Native.On(f.Native.Source, eventName, () => throw cause);
        var error = Assert.Throws<TargetInvocationException>(() => f.Fill());
        Assert.Same(cause, error.GetBaseException()); Assert.Equal((0f, .125f), f.Buffer);
        Assert.Equal(1, f.Native.Quantity(f.Native.Source));
        Assert.Equal(recorded, f.Native.Call(f.Native.Balance, "GetProduction", "Water"));
        Assert.True(f.Native.Poisoned); Assert.Throws<TargetInvocationException>(() => f.Fill());
    }
    [Theory]
    [InlineData("phase")]
    [InlineData("buffer")]
    [InlineData("stock")]
    public void CommitFailureOrDriftPoisonsWithoutRefund(string change)
    {
        using var f = new F(); f.Credited(.2f, .125f);
        Assert.Throws<TargetInvocationException>(() => f.Fill(commit: () =>
        {
            if (change == "phase") throw new IOException("phase failed");
            if (change == "buffer") f.Native.Call(f.Input, "AddWater", .2f, 0f);
            if (change == "stock") f.Native.Call(f.Native.Source, "TakeExisting", f.Native.Amount());
        }));
        Assert.True(f.Native.Poisoned);
        Assert.Equal(1, f.Native.Call(f.Native.Balance, "GetProduction", "Water"));
        Assert.Throws<TargetInvocationException>(() => f.Native.Call(f.Native.Resources, "ThrowIfSaveUnsafe"));
    }
    [Fact]
    public void RequiredCallbacksRejectBeforeNativeUnityReadAndMissingMapCannotBeAdmitted()
    {
        using var f = new F();
        foreach (var args in new object?[][] { [null, null, (Action)(() => { })], [null, (Action)(() => { }), null] })
            Assert.IsType<ArgumentNullException>(Assert.Throws<TargetInvocationException>(() => f.Native.Call(f.Native.Equipment, "TryFillFromNaturalSource", args)).InnerException);
        var method = f.Contract.GetType().GetMethod("TryCaptureCleanIntake", NativeWardenReturnFixture.Flags)!;
        object?[] capture = [f.Input, f.CreditService, null];
        Assert.False((bool)method.Invoke(f.Contract, capture)!);
        Assert.Equal(0, f.Pending); Assert.False(f.Native.Poisoned);
    }

    private sealed class F : IDisposable
    {
        internal readonly NativeWardenReturnFixture Native = new();
        internal readonly object Input, Changes, CreditService, Contract;
        internal F()
        {
            Contract = Activator.CreateInstance(Native.Mod("Compatibility.TimberbornWaterCreditContract"), true)!;
            Changes = Native.New("Timberborn.WaterSystem.WaterChangeService");
            CreditService = Native.New("Timberborn.WaterBuildings.WaterInputService", Changes);
            var service = Native.New("Timberborn.WaterSystem.WaterService", null, Changes, null, null);
            Input = Native.New("Timberborn.WaterBuildings.WaterInput", service, null, CreditService);
            var fixedInput = RuntimeHelpers.GetUninitializedObject(Native.T("Timberborn.WaterBuildings.WaterInputFixedCoordinates"));
            NativeWardenReturnFixture.Set(fixedInput, "<Coordinates>k__BackingField", Native.New("UnityEngine.Vector3Int", 1, 2, 3));
            NativeWardenReturnFixture.Set(Input, "_inputCoordinates", fixedInput);
        }
        // Explicit already-credited precondition for this arithmetic seam; no fake receipt dictionary or fluid-production claim.
        internal void Credited(float clean, float dirty) => Native.Call(Input, "AddWater", clean, dirty);
        internal float Quantum => (float)Native.Get(Contract, "WaterUnit")!;
        internal (float, float) Buffer => ((float, float))Native.Call(Contract, "Buffer", Input)!;
        internal int Pending => ((IList)NativeShorelineWaterFixture.Get(Changes, "_waterChanges")!).Count;
        internal string Fill(Action? validate = null, Action? commit = null)
        {
            object? result = null;
            Native.Transfer(() => result = Native.Mod("FireResponse.WardenEquipment").GetMethod("ConvertCreditedUnit", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [Native.Source, Input, Contract, validate ?? (() => { }), commit ?? (() => { })]));
            return result!.ToString()!;
        }
        internal void RequireSaveExcluded() => Assert.Throws<TargetInvocationException>(() => Native.Call(Native.Resources, "ThrowIfSaveUnsafe"));
        public void Dispose() => Native.Dispose();
    }
}
