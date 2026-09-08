using System.IO;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Persistence;

/// <summary>WF2's single paired fire payload. Explicit little-endian primitives; no property-reflection serialization.</summary>
internal static partial class TimberbornOwnedMaterialCodec
{
    internal static string Encode(TimberbornOwnedMaterialSnapshot owned)
    {
        var simulation = owned.CaptureSimulation();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(owned.History is null ? 1 : owned.History.NativeDefinitions is null ? 2 : owned.History.NativeDefinitions.HasInventoryDeclarations ? 4 : 3); // Paired native envelope schema.
        writer.Write(simulation.Version);
        writer.Write(simulation.Grid.Width); writer.Write(simulation.Grid.Height); writer.Write(simulation.Grid.Depth);
        writer.Write(simulation.Tick); writer.Write(simulation.Seed);
        WriteParameters(writer, simulation.Parameters);
        WriteArray(writer, simulation.Cells, (output, value) => output.Write(value));
        foreach (var values in new[] { simulation.TransportFields, simulation.CompanionFields, simulation.TargetIds, simulation.SlotIds })
            WriteArray(writer, values, (output, value) => output.Write(value));
        writer.Write(simulation.MaterialAuthority.LastAttemptToken);
        WriteArray(writer, simulation.MaterialAuthority.KnownSlots, WriteIdentity);
        WriteArray(writer, simulation.MaterialAuthority.Archives, (output, archive) =>
        {
            WriteIdentity(output, archive.Identity); output.Write(archive.CaptureToken); output.Write(archive.SourceCellIndex);
            output.Write(archive.PackedCell); output.Write(archive.Companion);
        });
        WriteArray(writer, simulation.PendingChanges, WriteChange);
        writer.Write(owned.Bindings.Version); writer.Write(owned.Bindings.NextTargetId);
        WriteArray(writer, owned.Bindings.Entities, (output, entity) =>
        {
            output.Write(entity.EntityId.ToByteArray()); output.Write(entity.TargetId); output.Write(entity.NextSlotId);
            WriteArray(output, entity.Slots, (slotOutput, slot) =>
            {
                slotOutput.Write(slot.LocalCoordinates.X); slotOutput.Write(slot.LocalCoordinates.Y); slotOutput.Write(slot.LocalCoordinates.Z);
                slotOutput.Write(slot.SlotId);
            });
        });
        if (owned.History is { } history) WriteHistory(writer, history);
        return Convert.ToBase64String(stream.ToArray());
    }

    internal static TimberbornOwnedMaterialSnapshot Decode(string encoded)
    {
        try
        {
            using var stream = new MemoryStream(Convert.FromBase64String(encoded), writable: false);
            using var reader = new BinaryReader(stream);
            int envelope = reader.ReadInt32();
            if (envelope is not (1 or 2 or 3 or 4)) throw new FormatException("Unsupported owned-material envelope schema.");
            int version = reader.ReadInt32();
            var grid = new FireGrid(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            if (grid.Width <= 0 || grid.Height <= 0 || grid.Depth <= 0) throw new FormatException("Invalid material grid dimensions.");
            int cellCount = checked(grid.Width * grid.Height * grid.Depth);
            uint tick = reader.ReadUInt32(), seed = reader.ReadUInt32();
            var parameters = ReadParameters(reader);
            var cells = ReadArray(reader, input => input.ReadUInt16(), sizeof(ushort), cellCount, exactCount: true);
            var transport = ReadArray(reader, input => input.ReadUInt32(), sizeof(uint), cellCount, exactCount: true);
            var companions = ReadArray(reader, input => input.ReadUInt32(), sizeof(uint), cellCount, exactCount: true);
            var targets = ReadArray(reader, input => input.ReadUInt32(), sizeof(uint), cellCount, exactCount: true);
            var slots = ReadArray(reader, input => input.ReadUInt32(), sizeof(uint), cellCount, exactCount: true);
            uint token = reader.ReadUInt32();
            var known = ReadArray(reader, ReadIdentity, 8);
            var archives = ReadArray(reader, input => new FireSimMaterialArchiveSnapshot(ReadIdentity(input), input.ReadUInt32(),
                input.ReadInt32(), input.ReadUInt32(), input.ReadUInt32()), 24, known.Length);
            var changes = ReadArray(reader, ReadChange, 20);
            int bindingVersion = reader.ReadInt32();
            uint nextTarget = reader.ReadUInt32();
            var entities = ReadArray(reader, input => new TimberbornMaterialEntityBinding(new Guid(input.ReadBytes(16)),
                input.ReadUInt32(), input.ReadUInt32(), ReadArray(input, slotInput => new TimberbornMaterialSlotBinding(
                    new(slotInput.ReadInt32(), slotInput.ReadInt32(), slotInput.ReadInt32()), slotInput.ReadUInt32()), 16)), 28);
            var history = envelope >= 2 ? ReadHistory(reader, envelope) : null;
            if (stream.Position != stream.Length) throw new FormatException("Trailing data in owned-material payload.");
            return new TimberbornOwnedMaterialSnapshot(new FireSimSnapshot(version, grid, tick, parameters, seed, cells,
                transport, companions, targets, slots, new FireSimMaterialAuthoritySnapshot(token, known, archives), changes),
                new TimberbornMaterialBindingSnapshot(bindingVersion, nextTarget, entities), history);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or OverflowException)
        { throw new FormatException("Malformed or inconsistent paired material snapshot.", exception); }
    }

    private static void WriteIdentity(BinaryWriter writer, FireSimMaterialIdentity identity)
    { writer.Write(identity.TargetId); writer.Write(identity.SlotId); }
    private static FireSimMaterialIdentity ReadIdentity(BinaryReader reader) => new(reader.ReadUInt32(), reader.ReadUInt32());

    private static void WriteArray<T>(BinaryWriter writer, IReadOnlyList<T> values, Action<BinaryWriter, T> write)
    {
        writer.Write(values.Count);
        foreach (var value in values) write(writer, value);
    }
    private static T[] ReadArray<T>(BinaryReader reader, Func<BinaryReader, T> read, int minimumBytes, int maximumCount = int.MaxValue, bool exactCount = false)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > maximumCount || (exactCount && count != maximumCount) ||
            count > (reader.BaseStream.Length - reader.BaseStream.Position) / minimumBytes)
            throw new FormatException("Invalid array count in owned-material payload.");
        var values = new T[count];
        for (int index = 0; index < count; index++) values[index] = read(reader);
        return values;
    }

    private static void WriteParameters(BinaryWriter writer, FireSimParameters parameters)
    {
        foreach (float value in new[] { parameters.VisualFireBaseIntensity, parameters.VisualFireHeatWeight,
            parameters.VisualSmokeBaseIntensity, parameters.VisualSmokeFuelWeight, parameters.VisualSmokeHeatWeight,
            parameters.AshPresentationBaseIntensity, parameters.AshPresentationFuelWeight, parameters.AshPresentationHeatWeight,
            parameters.VisualVisibilityHeatWeight, parameters.VisualVisibilitySmokeWeight, parameters.AshPresentationVisibilityWeight }) writer.Write(value);
        foreach (uint value in new[] { parameters.IgnitionPoint, parameters.FireWaterIgnitionPenalty, parameters.FireBurnHeatBase,
            parameters.FireFuelHeatWeight, parameters.FireFuelBurnDownPressureNumerator, parameters.FireFuelBurnDownPressureDenominator,
            parameters.FireFuelBurnDownRollSeed }) writer.Write(value);
    }
    private static FireSimParameters ReadParameters(BinaryReader reader) => new(
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(),
        reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadUInt32(),
        reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32(), reader.ReadUInt32());

    private static void WriteChange(BinaryWriter writer, FireSimChange change)
    {
        writer.Write(change.CellIndex);
        writer.Write(change.SetCell.HasValue);
        if (change.SetCell.HasValue) writer.Write(change.SetCell.Value);
        foreach (byte? value in new[] { change.AddHeat, change.AddFuel, change.AddAsh, change.RemoveAsh, change.SetAsh,
            change.SetAshContamination, change.SetWater, change.SetFuel, change.SetHeat, change.SetFlammability,
            change.SetBurningLevel, change.SetTerrain, change.SetSmoke, change.SetSmokeContamination, change.AddWater })
        { writer.Write(value.HasValue); if (value.HasValue) writer.Write(value.Value); }
    }
    private static FireSimChange ReadChange(BinaryReader reader) => new(reader.ReadInt32(),
        SetCell: ReadPresence(reader) ? reader.ReadUInt16() : null,
        AddHeat: ReadOptionalByte(reader), AddFuel: ReadOptionalByte(reader), AddAsh: ReadOptionalByte(reader),
        RemoveAsh: ReadOptionalByte(reader), SetAsh: ReadOptionalByte(reader), SetAshContamination: ReadOptionalByte(reader),
        SetWater: ReadOptionalByte(reader), SetFuel: ReadOptionalByte(reader), SetHeat: ReadOptionalByte(reader),
        SetFlammability: ReadOptionalByte(reader), SetBurningLevel: ReadOptionalByte(reader), SetTerrain: ReadOptionalByte(reader),
        SetSmoke: ReadOptionalByte(reader), SetSmokeContamination: ReadOptionalByte(reader), AddWater: ReadOptionalByte(reader));
    private static byte? ReadOptionalByte(BinaryReader reader) => ReadPresence(reader) ? reader.ReadByte() : null;
    private static bool ReadPresence(BinaryReader reader) => reader.ReadByte() switch
    {
        0 => false, 1 => true, _ => throw new FormatException("Optional material input presence must be zero or one."),
    };
}
