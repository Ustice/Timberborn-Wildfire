using System.IO;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Persistence;

internal static partial class TimberbornOwnedMaterialCodec
{
    private static void WriteNativeDefinitions(BinaryWriter writer, OwnedNativeDefinitionSet definitions) =>
        WriteArray(writer, definitions.Definitions, (output, value) =>
        {
            output.Write(value.EntityId.ToByteArray());
            WriteText(output, value.SpecId);
            output.Write((int)value.Shape);
            var profile = value.BodyProfile;
            WriteText(output, profile.SpecId);
            WriteText(output, profile.Type);
            output.Write(profile.FuelValue);
            output.Write(profile.DestructionThreshold);
            output.Write(profile.Flammability);
            output.Write(profile.Explosive);
            output.Write(profile.Contaminated);
            output.Write(profile.Known);
            WriteArray(output, value.LocalFootprint, (slotWriter, coordinates) =>
            {
                slotWriter.Write(coordinates.X);
                slotWriter.Write(coordinates.Y);
                slotWriter.Write(coordinates.Z);
            });
            WriteArray(output, value.Yields, (o, y) =>
            {
                o.Write((int)y.Role);
                WriteText(o, y.ComponentName);
                WriteText(o, y.GoodId);
                o.Write(y.Amount);
                o.Write(y.RemoveOnCut);
            });
            output.Write(value.BuildingCost is not null);
            if (value.BuildingCost is { } cost)
                WriteArray(output, cost, WriteStack);
        });
    private static OwnedNativeDefinitionSet ReadNativeDefinitions(BinaryReader reader, int maxOwners) => new(
        ReadArray(reader, input =>
        {
            var id = new Guid(input.ReadBytes(16));
            string spec = ReadText(input);
            var shape = (TimberbornInitialBodyShape)input.ReadInt32();
            var profile = new TimberbornBurnableProfile(ReadText(input), ReadText(input), input.ReadByte(), input.ReadInt32(),
                input.ReadByte(), ReadFlag(input), ReadFlag(input), ReadFlag(input));
            var cells = ReadArray(input, r => new TimberbornCellCoordinates(r.ReadInt32(), r.ReadInt32(), r.ReadInt32()), 12);
            var yields = ReadArray(input, r => new OwnedNamedYieldDefinition((TimberbornCapturedYieldRole)r.ReadInt32(),
                ReadText(r), ReadText(r), r.ReadInt32(), ReadFlag(r)), 17);
            var cost = ReadFlag(input) ? ReadArray(input, ReadStack, 8) : null;
            return new OwnedNativeDefinitionWitness(id, spec, shape, profile, cells, yields, cost);
        }, 46, maxOwners));
}
