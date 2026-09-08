using System.Reflection;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeFertilizerDistrictLifecycleTests
{
    [Fact]
    public void CitizenFirstDeathFailureDoesNotAbortLaterNativeDeathSubscribers()
    {
        using var f = Ready();
        int removed = 0;
        var cause = new IOException("district observer during native Citizen.OnDied");
        f.On(f.District, "InventoryUnregistered", () => { removed++; throw cause; });
        bool laterDeath = false;
        f.On(f.Character, "Died", () => laterDeath = true);
        f.Call(f.Character, "KillCharacter");
        Assert.True(laterDeath);
        Assert.False((bool)f.Get(f.Character, "Alive")!);
        Assert.True(f.Poisoned);
        Assert.Equal(1, removed);
        Assert.Equal(0, f.RegisteredProcessors);
        var warning = Assert.Single(f.Warnings);
        Assert.Contains("operation=unregister_district", warning);
        Assert.Contains(cause.ToString(), warning);
        f.Call(f.Satchel, "DeleteEntity");
        Assert.Equal(1, removed);
        Assert.Single(f.Warnings);
        Unspent(f);
    }

    [Fact]
    public void NativeDeleteContinuesPastSatchelCleanupFailure()
    {
        using var f = Ready();
        int removed = 0;
        f.On(f.District, "InventoryUnregistered", () => { removed++; throw new IOException("remove observer"); });
        bool deleted = false;
        f.DeleteThroughNativeEntity(() => deleted = true);
        Assert.True(deleted);
        Assert.True(f.Poisoned);
        Assert.Equal(1, removed);
        f.Call(f.Satchel, "DeleteEntity");
        Assert.Equal(1, removed);
        Assert.Single(f.Warnings);
        Unspent(f);
    }

    [Theory]
    [InlineData("capture", true)]
    [InlineData("transfer", true)]
    [InlineData("poison", true)]
    [InlineData("capture", false)]
    [InlineData("transfer", false)]
    [InlineData("poison", false)]
    public void BusyOrPoisonedNativeTeardownContinuesWithoutUnguardedCleanup(string boundary, bool death)
    {
        using var f = Ready();
        int removed = 0;
        bool laterDeath = false;
        f.On(f.District, "InventoryUnregistered", () => removed++);
        f.On(f.Character, "Died", () => laterDeath = true);
        void Teardown()
        {
            if (death) f.Call(f.Character, "KillCharacter");
            else f.DeleteThroughNativeEntity(() => laterDeath = true);
        }
        if (boundary == "poison")
        {
            Assert.Throws<TargetInvocationException>(() => f.Transfer(() => throw new IOException("prior failure")));
            Teardown();
        }
        else if (boundary == "capture")
            Assert.Throws<TargetInvocationException>(() => f.Capture(Teardown));
        else
            Assert.Throws<TargetInvocationException>(() => f.Transfer(Teardown));
        Assert.True(laterDeath);
        Assert.True(f.Poisoned);
        Assert.Equal(0, removed);
        Assert.Equal(1, f.RegisteredProcessors);
        f.Call(f.Satchel, "DeleteEntity");
        Assert.Equal(0, removed);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
        Unspent(f);
    }

    [Fact]
    public void AppliedNativeConsumptionThenCitizenFirstDeathCannotReturnACompleteStep()
    {
        using var f = Ready();
        bool laterDeath = false;
        int removed = 0;
        f.On(f.Character, "Died", () => laterDeath = true);
        f.On(f.District, "InventoryUnregistered", () => removed++);
        var error = Assert.Throws<TargetInvocationException>(() =>
            f.Apply(() => f.Call(f.Character, "KillCharacter")));
        Assert.Equal(Wildfire.Core.FireSimStepInputOutcome.Indeterminate,
            Assert.IsType<Wildfire.Core.FireSimStepInputException>(error.InnerException).Outcome);
        Assert.True(laterDeath);
        Assert.True(f.Poisoned);
        Assert.Equal(0, removed); // Death never bypasses the already-entered application guard.
        Assert.Equal(1, f.RegisteredProcessors);
        Assert.Equal(0, f.Quantity(f.Inventory));
        Assert.Equal(1, f.Consumption);
        Assert.Equal(0, f.Production);
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "RequireAshApplicationCommit"));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
    }

    [Fact]
    public void CaughtLivingDistrictReentryCannotPublishASafeCapture()
    {
        using var f = Ready();
        int removed = 0;
        f.On(f.District, "InventoryUnregistered", () => removed++);
        Assert.Throws<TargetInvocationException>(() => f.Capture(() =>
        {
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Citizen, "UnassignDistrict"));
        }));
        Assert.True(f.Poisoned);
        Assert.Equal(0, removed);
        Assert.Equal(1, f.RegisteredProcessors);
        Unspent(f);
    }

    [Theory]
    [InlineData("InitializeEntity")]
    [InlineData("PostLoadEntity")]
    public void DeathInsideNativeEnableDoesNotContinueRegistrationOrReturnSuccess(string lifecycle)
    {
        using var f = Ready();
        f.Call(f.Inventory, "Disable");
        int removed = 0;
        bool laterDeath = false;
        f.On(f.District, "InventoryUnregistered", () => removed++);
        f.On(f.Character, "Died", () => laterDeath = true);
        f.On(f.Inventory, "InventoryEnabled", () => f.Call(f.Character, "KillCharacter"));
        Assert.Throws<TargetInvocationException>(() => f.Call(f.Satchel, lifecycle));
        Assert.True(laterDeath);
        Assert.True(f.Poisoned);
        Assert.Equal(0, removed);
        Assert.Equal(1, f.RegisteredProcessors);
        Unspent(f);
    }

    [Fact]
    public void ExitedSatchelCannotReenableOnPostLoadOrRepeatCleanup()
    {
        using var f = Ready();
        int enabled = 0, removed = 0;
        f.On(f.District, "InventoryUnregistered", () => removed++);
        f.Call(f.Character, "KillCharacter");
        f.Call(f.Inventory, "Disable");
        f.On(f.Inventory, "InventoryEnabled", () => enabled++);
        f.Call(f.Satchel, "PostLoadEntity");
        f.Call(f.Satchel, "DeleteEntity");
        Assert.Equal(0, enabled);
        Assert.Equal(1, removed);
        Assert.Equal(0, f.RegisteredProcessors);
        Assert.False(f.Poisoned);
        Unspent(f);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingOrReentrantDiagnosticCannotAbortNativeTeardown(bool death)
    {
        using var f = Ready();
        bool warnedAfterPoison = false, continued = false;
        f.On(f.District, "InventoryUnregistered", () => throw new IOException("original district failure"));
        f.OnWarning = _ =>
        {
            warnedAfterPoison = f.Poisoned;
            f.Call(f.Satchel, "DeleteEntity");
            throw new IOException("diagnostic failure");
        };
        if (death)
        {
            f.On(f.Character, "Died", () => continued = true);
            f.Call(f.Character, "KillCharacter");
        }
        else f.DeleteThroughNativeEntity(() => continued = true);
        Assert.True(continued);
        Assert.True(warnedAfterPoison);
        Assert.Single(f.Warnings);
        Unspent(f);
    }

    [Fact]
    public void ActualCharacterStartsAliveAndBinditoStillSelectsSolePublicConstructor()
    {
        using var f = Ready();
        Assert.True((bool)f.Get(f.Character, "Alive")!);
        using var native = new NativeManagedTestContext();
        var type = native.LoadMod().GetType("Wildfire.Timberborn.Fertilizer.FertilizerSatchel")!;
        var retriever = Activator.CreateInstance(native.LoadNative("Bindito.Core")
            .GetType("Bindito.Core.Internal.ConstructorRetriever")!)!;
        var constructor = (ConstructorInfo)retriever.GetType().GetMethod("GetEligibleConstructor")!.Invoke(retriever, [type])!;
        Assert.True(constructor.IsPublic);
        Assert.Equal("Wildfire.Timberborn.Resources.NativeResourceCoordinator", Assert.Single(constructor.GetParameters()).ParameterType.FullName);
    }

    private static NativeFertilizerSatchelFixture Ready()
    {
        var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.ModelDistrictRegistration();
        return f;
    }
    private static void Unspent(NativeFertilizerSatchelFixture f)
    {
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
        Assert.Equal(0, f.Production);
    }
}
