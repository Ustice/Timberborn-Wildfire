using System.Reflection;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeDispatchScopeLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeDeathBetweenOperationsCanCleanUpButInnerReentryStillPoisons(bool insideTransfer)
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.ModelDistrictRegistration();
        int unregisters = 0;
        bool laterNativeDeath = false;
        f.On(f.District, "InventoryUnregistered", () => unregisters++);
        f.On(f.Character, "Died", () => laterNativeDeath = true);
        Action death = () => f.Call(f.Character, "KillCharacter");
        var error = Record.Exception(() => f.Call(f.Resources, "ExcludeSavesDuringDispatch", (Action)(() =>
        {
            if (insideTransfer) f.Transfer(death);
            else death();
        })));
        Assert.True(laterNativeDeath);
        Assert.Equal(insideTransfer, f.Poisoned);
        Assert.Equal(insideTransfer ? 0 : 1, unregisters);
        Assert.Equal(insideTransfer ? 1 : 0, f.RegisteredProcessors);
        if (insideTransfer) Assert.IsType<TargetInvocationException>(error);
        else
        {
            Assert.Null(error);
            f.Call(f.Resources, "ThrowIfSaveUnsafe");
        }
        f.Call(f.Satchel, "DeleteEntity");
        Assert.Equal(insideTransfer ? 0 : 1, unregisters);
        Assert.Equal(1, f.Quantity(f.Inventory));
        Assert.Equal(0, f.Consumption);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttachedApplicationKeepsExactCommitPrivilegeAndNativeQuantityInsideSaveScope(bool rejected)
    {
        using var f = new NativeFertilizerSatchelFixture();
        f.Give(f.Inventory);
        f.AttachApplicationSimulator(rejected);
        int commits = 0;
        f.Call(f.Resources, "ExcludeSavesDuringDispatch", (Action)(() =>
        {
            f.Call(f.Resources, "TryApplyCleanAsh", new FireSimAshApplicationInput(0, 2),
                (Action<FireSimAshApplicationReceipt>)(_ =>
                {
                    commits++;
                    f.Consume();
                }));
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "RequireAshApplicationCommit"));
            Assert.Throws<TargetInvocationException>(() => f.Call(f.Resources, "ThrowIfSaveUnsafe"));
        }));
        Assert.Equal(rejected ? 0 : 1, commits);
        Assert.Equal(rejected ? 1 : 0, f.Quantity(f.Inventory));
        Assert.Equal(rejected ? 0 : 1, f.Consumption);
        Assert.Equal(0, f.Production);
        Assert.False(f.Poisoned);
        f.Call(f.Resources, "ThrowIfSaveUnsafe");
    }
}
