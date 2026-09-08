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
        Assert.Same(f.Growable, Lookup(f, blocks, 2).Single());

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

    [Fact]
    public void UpperOccupiedCellIsNotTheNativePlantSoilAnchor()
    {
        var f = new NativeAshGrowthFixture();
        var (blocks, _, _) = Blocks(f);
        var plan = f.Call(Adapter(f, blocks), "Prepare", new FireGrid(1, 1, 4), .01f, Requests(f, 2))!;
        var result = f.Call(plan, "Apply")!;
        Assert.Equal(0, f.Get(result, "CandidateGrowableCount"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BothActualNativeBaseAndColumnCeilingAreRequired(int requestedZ)
    {
        var f = new NativeAshGrowthFixture();
        var (blocks, _, _) = Blocks(f);
        var soil = new NativeAshGrowthSoilFixture(f);
        soil.SetColumn(4, 0, 2); // z1 has the plant base but no ceiling; z2 has a ceiling but not its base.
        var plan = f.Call(Adapter(f, blocks, soil), "Prepare", new FireGrid(1, 1, 4), .01f, Requests(f, requestedZ))!;
        var result = f.Call(plan, "Apply")!;
        Assert.Equal(0, f.Get(result, "CandidateGrowableCount"));
    }

    [Theory]
    [InlineData("base")]
    [InlineData("ceiling")]
    [InlineData("column_identity")]
    public void NativeCompletionCallbackCanInvalidatePreparedSpatialEligibility(string change)
    {
        var f = new NativeAshGrowthFixture();
        var (blocks, _, _) = Blocks(f);
        var soil = new NativeAshGrowthSoilFixture(f);
        var plan = f.Call(Adapter(f, blocks, soil), "Prepare", new FireGrid(1, 1, 4), .01f, Requests(f, change == "column_identity" ? 3 : 1))!;
        var candidate = ((Array)plan.GetType().GetField("_candidates", NativeAshGrowthFixture.Flags)!.GetValue(plan)!).GetValue(0)!;
        var spatial = plan.GetType().GetMethod("StillAtSoilBase", NativeAshGrowthFixture.Flags)!;
        Assert.Equal(true, spatial.Invoke(plan, [candidate]));
        // Actual native HasGrown event of another resource changes the candidate between phases.
        var signal = new NativeAshGrowthFixture();
        signal.Completion = () =>
        {
            if (change == "base")
            {
                object body = candidate.GetType().GetProperty("Body")!.GetValue(candidate)!;
                NativeAshGrowthFixture.Set(body, "<CoordinatesAtBaseZ>k__BackingField", Coordinates(f, 2));
            }
            else if (change == "ceiling") soil.SetColumn(4, 0, 2);
            else { soil.SetColumn(4, 0, 3); soil.Counts[4] = 1; }
        };
        signal.Call(signal.Growable, "IncreaseGrowthProgress", 1f);
        Assert.Equal(false, spatial.Invoke(plan, [candidate]));
        var result = f.Call(plan, "Apply")!;
        Assert.Equal(0, f.Get(result, "AppliedGrowableCount"));
        Assert.Equal(0f, f.Progress);
        // Spatial predicate is exercised directly because positive entity/Unity liveness is an engine boundary.
    }

    private static object Adapter(NativeAshGrowthFixture f, object blocks, NativeAshGrowthSoilFixture? soil = null)
    {
        soil ??= new(f);
        return Activator.CreateInstance(f.Native.LoadMod().GetType("Wildfire.Timberborn.Ash.TimberbornGrowableAshGrowthAdapter")!,
            NativeAshGrowthFixture.Flags, null, [blocks, soil.Indices, soil.Columns], null)!;
    }

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
        NativeAshGrowthFixture.Set(body, "<CoordinatesAtBaseZ>k__BackingField", Coordinates(f, 1));
        f.AttachCache(f.Growable, f.Living, f.Dying, entity, body);
        var upper = new NativeAshGrowthFixture();
        var upperBody = f.New("Timberborn.BlockSystem", "BlockObject");
        NativeAshGrowthFixture.Set(upperBody, "<CoordinatesAtBaseZ>k__BackingField", Coordinates(f, 3));
        var upperEntity = f.New("Timberborn.EntitySystem", "EntityComponent");
        NativeAshGrowthFixture.Set(upperEntity, "<EntityId>k__BackingField", Guid.NewGuid());
        upper.AttachCache(upper.Growable, upper.Living, upper.Dying, upperBody, upperEntity);
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
        values.SetValue(Cell(body), 0, 0, 2);
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
