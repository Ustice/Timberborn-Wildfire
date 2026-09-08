using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeAshGrowthMappingTests
{
    [Fact]
    public void ActualNativeBlockLookupUsesExactZAndDeduplicatesTheSameComponentReference()
    {
        var f = new NativeAshGrowthFixture();
        var (blocks, lower, upper) = Blocks(f);
        Assert.Same(f.Growable, Lookup(f, blocks, 1).First());
        Assert.Same(upper, Lookup(f, blocks, 3).Single());
        Assert.Empty(Lookup(f, blocks, 0));
        Assert.Empty(Lookup(f, blocks, 2));

        var adapter = Adapter(f, blocks);
        // Duplicate native entries plus repeated request must still describe exactly one target.
        var plan = f.Call(adapter, "Prepare", new FireGrid(1, 1, 4), .01f, Requests(f, 1, 1))!;
        var result = f.Call(plan, "Apply")!;
        Assert.Equal(1, f.Get(result, "CandidateGrowableCount"));
        Assert.Equal(0, f.Get(result, "AppliedGrowableCount"));
        Assert.Equal(1, f.Get(result, "UnsupportedGrowableCount"));
        Assert.Equal(false, f.Get(lower, "Initialized")); // Real native gate: no fake positive Unity entity.
        Assert.Equal(0f, f.Progress);
        Assert.Equal(0, f.Completed);
    }

    [Fact]
    public void UnknownOwnerRejectsDuringReadPreparationWithoutPoison()
    {
        var f = new NativeAshGrowthFixture();
        var (blocks, entity, _) = Blocks(f);
        NativeAshGrowthFixture.Set(entity, "<EntityId>k__BackingField", Guid.Empty);
        var adapter = Adapter(f, blocks);
        var coordinator = Activator.CreateInstance(f.Native.LoadMod().GetType("Wildfire.Timberborn.Resources.NativeResourceCoordinator")!)!;
        var capture = coordinator.GetType().GetMethod("CaptureAtRest")!.MakeGenericMethod(typeof(object));
        var error = Assert.Throws<TargetInvocationException>(() => capture.Invoke(coordinator,
            [(Func<object>)(() => f.Call(adapter, "Prepare", new FireGrid(1, 1, 4), .01f, Requests(f, 1))!)]));
        Assert.Contains("native identity", error.GetBaseException().Message);
        f.Call(coordinator, "ThrowIfSaveUnsafe");
        Assert.Equal(false, f.Get(coordinator, "IsIndeterminate"));
        Assert.Equal(0f, f.Progress);
    }

    private static object Adapter(NativeAshGrowthFixture f, object blocks) => Activator.CreateInstance(
        f.Native.LoadMod().GetType("Wildfire.Timberborn.Ash.TimberbornGrowableAshGrowthAdapter")!,
        NativeAshGrowthFixture.Flags, null, [blocks], null)!;

    private static Array Requests(NativeAshGrowthFixture f, params int[] cells)
    {
        var type = f.Native.LoadMod().GetType("Wildfire.Timberborn.Ash.TimberbornAshGrowthBonusRequest")!;
        var result = Array.CreateInstance(type, cells.Length);
        for (int i = 0; i < cells.Length; i++) result.SetValue(Activator.CreateInstance(type, cells[i], 1.1f, WildfireAshQuality.Fertile, 3), i);
        return result;
    }

    private static object[] Lookup(NativeAshGrowthFixture f, object blocks, int z)
    {
        var method = blocks.GetType().GetMethod("GetObjectsWithComponentAt")!.MakeGenericMethod(f.Growable.GetType());
        return ((IEnumerable)method.Invoke(blocks, [Coordinates(f, z)])!).Cast<object>().ToArray();
    }
    private static object Coordinates(NativeAshGrowthFixture f, int z) => Activator.CreateInstance(
        f.Native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!, 0, 0, z)!;

    private static (object Blocks, object Entity, object Upper) Blocks(NativeAshGrowthFixture f)
    {
        var entity = f.New("Timberborn.EntitySystem", "EntityComponent");
        NativeAshGrowthFixture.Set(entity, "<EntityId>k__BackingField", Guid.NewGuid());
        var body = f.New("Timberborn.BlockSystem", "BlockObject");
        f.AttachCache(f.Growable, f.Living, f.Dying, entity, body);
        var upper = new NativeAshGrowthFixture();
        var upperBody = f.New("Timberborn.BlockSystem", "BlockObject");
        upper.AttachCache(upper.Growable, upper.Living, upper.Dying, upperBody);
        var worldBlock = f.T("Timberborn.BlockSystem", "WorldBlock");
        object Cell(params object[] entries)
        {
            object cell = Activator.CreateInstance(worldBlock)!;
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(body.GetType()))!;
            foreach (var entry in entries) list.Add(entry);
            NativeAshGrowthFixture.Set(cell, "_blockObjects", list);
            return cell;
        }
        // Native lookup reads this supplied native Array3D. No claim of full placement/template initialization.
        var values = Array.CreateInstance(worldBlock, 1, 1, 4);
        values.SetValue(Cell(body, body), 0, 0, 1);
        values.SetValue(Cell(upperBody), 0, 0, 3);
        var array = RuntimeHelpers.GetUninitializedObject(f.T("Timberborn.Common", "Array3D`1").MakeGenericType(worldBlock));
        NativeAshGrowthFixture.Set(array, "_values", values);
        var size = Activator.CreateInstance(Coordinates(f, 0).GetType(), 1, 1, 4)!;
        NativeAshGrowthFixture.Set(array, "_size", size);
        var blocks = f.New("Timberborn.BlockSystem", "BlockService");
        NativeAshGrowthFixture.Set(blocks, "_blocks", array);
        return (blocks, entity, upper.Growable);
    }
}
