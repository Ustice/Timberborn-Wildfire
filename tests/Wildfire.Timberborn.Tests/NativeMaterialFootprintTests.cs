using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeMaterialFootprintTests
{
    [Fact]
    public void NativeSparseFootprintPreservesLocalIdentityThroughAllRotationsAndFlips()
    {
        using var fixture = new NativeFootprintFixture();
        var grid = new FireGrid(20, 20, 5);
        var registry = new TimberbornNativeMaterialRegistry(grid, Array.Empty<int>());
        Guid entity = Guid.NewGuid();
        Dictionary<TimberbornCellCoordinates, TimberbornMaterialOwner>? original = null;
        var placements = new HashSet<string>();
        foreach (string rotation in new[] { "Cw0", "Cw90", "Cw180", "Cw270" })
        foreach (bool flipped in new[] { false, true })
        {
            var footprint = fixture.Project(rotation, flipped, grid);
            Assert.Equal(4, footprint.Count); // Native size3x2 with two deliberately unoccupied blocks.
            registry.Reconcile(new[] { new TimberbornMaterialProjection(entity, footprint.Reverse(),
                new[] { TimberbornMaterialPart.Building("LumberMill.Folktails") }) }, Array.Empty<Guid>());
            var current = footprint.ToDictionary(slot => slot.LocalCoordinates, slot => registry.ResolveCell(slot.CellIndex).Owner!.Value);
            original ??= current;
            foreach (var slot in current) Assert.Equal(original[slot.Key], slot.Value);
            Assert.Single(current.Values.Select(owner => owner.TargetId).Distinct());
            Assert.Equal(4, current.Values.Select(owner => owner.SlotId).Distinct().Count());
            placements.Add(string.Join(",", footprint.OrderBy(slot => slot.LocalCoordinates.X).ThenBy(slot => slot.LocalCoordinates.Y).Select(slot => slot.CellIndex)));
        }
        Assert.Equal(8, placements.Count); // The test actually exercises distinct native transforms.
    }

    [Fact]
    public void NativeOutOfGridPlacementFailsAsAWholeInsteadOfClippingSlots()
    {
        using var fixture = new NativeFootprintFixture();
        var failure = Assert.Throws<TargetInvocationException>(() => fixture.Project("Cw0", false, new FireGrid(2, 2, 1)));
        Assert.IsType<ArgumentOutOfRangeException>(failure.InnerException);
    }

    internal sealed class NativeFootprintFixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        private readonly object _blocks;
        private readonly Type _vector;
        private readonly Type _placement;
        private readonly Type _orientation;
        private readonly Type _flip;
        private readonly MethodInfo _project;
        internal NativeFootprintFixture()
        {
            var blockAssembly = _native.LoadNative("Timberborn.BlockSystem");
            var coordinateAssembly = _native.LoadNative("Timberborn.Coordinates");
            _vector = _native.LoadNative("UnityEngine.CoreModule").GetType("UnityEngine.Vector3Int")!;
            _placement = coordinateAssembly.GetType("Timberborn.Coordinates.Placement")!;
            _orientation = coordinateAssembly.GetType("Timberborn.Coordinates.Orientation")!;
            _flip = coordinateAssembly.GetType("Timberborn.Coordinates.FlipMode")!;
            var specType = blockAssembly.GetType("Timberborn.BlockSystem.BlockObjectSpec")!;
            var blockSpecType = blockAssembly.GetType("Timberborn.BlockSystem.BlockSpec")!;
            var spec = Activator.CreateInstance(specType)!;
            specType.GetProperty("Size")!.SetValue(spec, Activator.CreateInstance(_vector, 3, 2, 1));
            var entries = Array.CreateInstance(blockSpecType, 6);
            for (int index = 0; index < 6; index++)
            {
                var block = Activator.CreateInstance(blockSpecType)!;
                var occupations = blockSpecType.GetProperty("Occupations")!;
                occupations.SetValue(block, Enum.Parse(occupations.PropertyType, index is 1 or 4 ? "None" : "All"));
                entries.SetValue(block, index);
            }
            var createRange = typeof(ImmutableArray).GetMethods().Single(method => method.Name == "CreateRange" && method.GetParameters().Length == 1 &&
                method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            specType.GetProperty("Blocks")!.SetValue(spec, createRange.MakeGenericMethod(blockSpecType).Invoke(null, new object[] { entries }));
            _blocks = blockAssembly.GetType("Timberborn.BlockSystem.Blocks")!.GetMethod("From")!.Invoke(null, new[] { spec })!;
            _project = _native.LoadMod().GetType("Wildfire.Timberborn.Mapping.TimberbornNativeMaterialFootprint")!.GetMethod("Project")!;
        }
        internal IReadOnlyList<TimberbornMaterialFootprintSlot> Project(string rotation, bool flipped, FireGrid grid)
        {
            var placement = Activator.CreateInstance(_placement, Activator.CreateInstance(_vector, 8, 8, 2),
                Enum.Parse(_orientation, rotation), Activator.CreateInstance(_flip, new object[] { flipped }));
            var result = (IEnumerable)_project.Invoke(null, new[] { _blocks, placement, (object)grid })!;
            return result.Cast<object>().Select(slot =>
            {
                var local = slot.GetType().GetProperty("LocalCoordinates")!.GetValue(slot)!;
                int Read(string name) => (int)local.GetType().GetProperty(name)!.GetValue(local)!;
                return new TimberbornMaterialFootprintSlot(new(Read("X"), Read("Y"), Read("Z")),
                    (int)slot.GetType().GetProperty("CellIndex")!.GetValue(slot)!);
            }).ToArray();
        }
        public void Dispose() => _native.Dispose();
    }
}
