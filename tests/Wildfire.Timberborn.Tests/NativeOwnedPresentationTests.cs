using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeOwnedPresentationTests
{
    [Fact]
    public void OwnedTreeConstructionDoesNotEnumerateOrCharNativeLeftovers()
    {
        using var native = new NativeManagedTestContext();
        var registryType = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityRegistry")!;
        var registry = RuntimeHelpers.GetUninitializedObject(registryType); // No dictionary: any legacy world scan would fail.
        var type = native.LoadMod().GetType("Wildfire.Timberborn.Visuals.TimberbornTextureTreeBurnConsequenceApi")!;
        Assert.NotNull(type.GetMethod("CreateOwned")!.Invoke(null,[registry,null,null]));
    }
    [Theory]
    [InlineData("Tree")][InlineData("Crop")]
    public void PresentationOnlyNativePathReturnsNotLiveForMissingOriginalEntity(string family)
    {
        using var native = new NativeManagedTestContext();
        var registryType = native.LoadNative("Timberborn.EntitySystem").GetType("Timberborn.EntitySystem.EntityRegistry")!;
        var registry = Activator.CreateInstance(registryType)!;
        var mod = native.LoadMod();
        var type = mod.GetType($"Wildfire.Timberborn.Visuals.TimberbornTexture{family}BurnConsequenceApi")!;
        var api = RuntimeHelpers.GetUninitializedObject(type);
        type.GetField("_entities",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(api,registry);
        var desiredType=mod.GetType("Wildfire.Timberborn.Persistence.OwnedCharredPresentation")!;
        var result=type.GetMethod("RehydrateOwnedPresentation",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(api,[Guid.NewGuid(),Enum.Parse(desiredType,"BurnedLeftover")]);
        Assert.Equal("NotLive",result!.ToString());
        // Executes actual native registry lookup. Positive renderer/model restoration requires Unity.
    }
}
