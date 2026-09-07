using System.IO;
using System.Text;

namespace Wildfire.Timberborn.Persistence;

internal static partial class TimberbornOwnedMaterialCodec
{
    private static void WriteHistory(BinaryWriter writer, TimberbornOwnedConsequenceSnapshot history)
    {
        WriteArray(writer, history.Owners, (output, owner) =>
        {
            output.Write(owner.EntityId.ToByteArray()); output.Write((int)owner.Family); output.Write((int)owner.Retention);
            if (owner.Profile is { } profile) WriteProfile(output, profile);
        });
        WriteArray(writer, history.Natural, (output, progress) =>
        {
            output.Write(progress.EntityId.ToByteArray()); output.Write(progress.AppliedYieldLoss);
            output.Write(progress.DryRequestSatisfied); output.Write(progress.DeathRequestSatisfied); output.Write(progress.LeftoverRequestSatisfied);
            output.Write((int)progress.DesiredPresentation);
        });
        WriteArray(writer, history.StorageCredits, (output, credit) =>
        {
            output.Write(credit.EntityId.ToByteArray()); WriteText(output, credit.ResourceId);
            output.Write(credit.FractionalBudget); output.Write(credit.FuelValue);
        });
    }
    private static TimberbornOwnedConsequenceSnapshot ReadHistory(BinaryReader reader)
    {
        var owners = ReadArray(reader, input =>
        {
            var id = new Guid(input.ReadBytes(16)); var family = (NativeBurnTargetFamily)input.ReadInt32();
            var retention = (OwnedBodyRetention)input.ReadInt32();
            return new OwnedConsequenceOwner(id, family, retention, retention == OwnedBodyRetention.RetainedBody ? ReadProfile(input) : null);
        }, 24);
        var natural = ReadArray(reader, input => new OwnedNaturalProgress(new Guid(input.ReadBytes(16)), input.ReadInt32(),
            ReadFlag(input), ReadFlag(input), ReadFlag(input), (OwnedCharredPresentation)input.ReadInt32()), 27, owners.Length);
        var credits = ReadArray(reader, input => new OwnedStorageCredit(new Guid(input.ReadBytes(16)), ReadText(input),
            input.ReadInt32(), input.ReadByte()), 25);
        return new(owners, natural, credits);
    }
    private static void WriteProfile(BinaryWriter writer, OwnedBodyAccountingProfile profile)
    {
        WriteText(writer, profile.SpecId); writer.Write((int)profile.TargetKind); writer.Write((int)profile.MaterialKind);
        writer.Write(profile.Capacity); writer.Write(profile.FuelValue); writer.Write(profile.Flammability);
        WriteArray(writer, profile.MissingResources, WriteText); WriteArray(writer, profile.AccountedResources, WriteText);
        writer.Write(profile.BurnableProfile.HasValue);
        if (profile.BurnableProfile is { } burnable)
        {
            WriteText(writer, burnable.SpecId); WriteText(writer, burnable.Type); writer.Write(burnable.FuelValue);
            writer.Write(burnable.DestructionThreshold); writer.Write(burnable.Flammability);
            writer.Write(burnable.Explosive); writer.Write(burnable.Contaminated); writer.Write(burnable.Known);
        }
        WriteArray(writer, profile.ResourceYields, WriteStack); WriteArray(writer, profile.ConstructionResources, WriteStack);
    }
    private static OwnedBodyAccountingProfile ReadProfile(BinaryReader reader)
    {
        string spec = ReadText(reader); var kind = (TimberbornBurnDamageTargetKind)reader.ReadInt32();
        var material = (TimberbornBurnMaterialKind)reader.ReadInt32(); int capacity = reader.ReadInt32();
        byte fuel = reader.ReadByte(), flammability = reader.ReadByte();
        var missing = ReadArray(reader, ReadText, 4); var accounted = ReadArray(reader, ReadText, 4);
        TimberbornBurnableProfile? burnable = ReadFlag(reader) ? new(ReadText(reader), ReadText(reader), reader.ReadByte(),
            reader.ReadInt32(), reader.ReadByte(), ReadFlag(reader), ReadFlag(reader), ReadFlag(reader)) : null;
        return new(spec, kind, material, capacity, fuel, flammability, missing, accounted, burnable,
            ReadArray(reader, ReadStack, 8), ReadArray(reader, ReadStack, 8));
    }
    private static void WriteStack(BinaryWriter writer, TimberbornBurnDamageResourceStack stack)
    { WriteText(writer, stack.ResourceId); writer.Write(stack.Amount); }
    private static TimberbornBurnDamageResourceStack ReadStack(BinaryReader reader) => new(ReadText(reader), reader.ReadInt32());
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static void WriteText(BinaryWriter writer, string value)
    { var bytes = StrictUtf8.GetBytes(value); writer.Write(bytes.Length); writer.Write(bytes); }
    private static string ReadText(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > reader.BaseStream.Length - reader.BaseStream.Position) throw new FormatException("Invalid history string length.");
        return StrictUtf8.GetString(reader.ReadBytes(count));
    }
    private static bool ReadFlag(BinaryReader reader) => reader.ReadByte() switch
    { 0 => false, 1 => true, _ => throw new FormatException("Invalid history flag.") };
}
