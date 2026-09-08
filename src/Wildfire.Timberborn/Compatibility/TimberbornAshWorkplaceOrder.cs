using System.Reflection;
using System.Security.Cryptography;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.Compatibility;

/// <summary>Reviewed order seam: native gathering falls back to other yields when the prioritized field has no entity.</summary>
internal sealed class TimberbornAshWorkplaceOrder
{
    private static readonly Lazy<FieldInfo> Behaviors = new(Verify);
    internal void EnsureFirst(Workplace workplace, WorkplaceBehavior owner)
    {
        // PostLoadEntity fills this same native list. Check its current first entry so subsequent rebuilds are handled.
        if (workplace.WorkplaceBehaviors.Count == 0 || ReferenceEquals(workplace.WorkplaceBehaviors[0], owner)) return;
        var list = Behaviors.Value.GetValue(workplace) as List<WorkplaceBehavior> ??
            throw new InvalidOperationException("Native workplace behavior storage changed.");
        int index = list.IndexOf(owner);
        if (index < 0) throw new InvalidOperationException("Ash behavior is absent from its workplace.");
        list.RemoveAt(index); list.Insert(0, owner);
    }
    private static FieldInfo Verify()
    {
        using var stream = File.OpenRead(typeof(Workplace).Assembly.Location);
        using var sha = SHA256.Create();
        var fingerprint = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if (fingerprint != "e1b32c9b6c2c97223c9e3ca1d95c60e72f2afe7aa276175f5819b810fc69ca7e")
            throw new InvalidOperationException("Native ash workplace ordering is not reviewed for this game build.");
        var field = typeof(Workplace).GetField("_workplaceBehaviors", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.FieldType != typeof(List<WorkplaceBehavior>)) throw new InvalidOperationException("Native workplace behavior signature changed.");
        return field;
    }
}
