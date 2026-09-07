using System.Reflection;
using System.Security.Cryptography;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.FireBell;

/// <summary>Temporary ordering for an explicit donor; restores the prior neighbors when disarmed.</summary>
internal sealed class BorrowedDutyWorkplaceOrder
{
    private static readonly Lazy<FieldInfo> Field = new(Verify);
    private List<WorkplaceBehavior>? _list;
    private WorkplaceBehavior? _previous;
    private WorkplaceBehavior? _next;
    internal void Update(Workplace workplace, WorkplaceBehavior offer, bool armed)
    {
        if (!armed) { Restore(offer); return; }
        var list = (List<WorkplaceBehavior>)Field.Value.GetValue(workplace)!;
        if (list.Count == 0) return; // Native PostLoadEntity has not populated the list yet.
        if (ReferenceEquals(_list, list) && ReferenceEquals(list[0], offer)) return;
        Restore(offer);
        int index = list.IndexOf(offer);
        if (index < 0) throw new InvalidOperationException("Borrowed duty offer is not registered at its donor.");
        if (index == 0) return;
        _previous = list[index - 1]; _next = index + 1 < list.Count ? list[index + 1] : null;
        _list = list;
        list.RemoveAt(index); list.Insert(0, offer);
    }
    private void Restore(WorkplaceBehavior offer)
    {
        var list = _list;
        if (list is null) return;
        _list = null;
        if (!list.Remove(offer)) return;
        int next = _next is null ? -1 : list.IndexOf(_next);
        int previous = _previous is null ? -1 : list.IndexOf(_previous);
        list.Insert(next >= 0 ? next : previous >= 0 ? previous + 1 : list.Count, offer);
        _previous = null; _next = null;
    }
    private static FieldInfo Verify()
    {
        using var stream = File.OpenRead(typeof(Workplace).Assembly.Location);
        using var sha = SHA256.Create();
        var hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if (hash != "e1b32c9b6c2c97223c9e3ca1d95c60e72f2afe7aa276175f5819b810fc69ca7e")
            throw new InvalidOperationException("Borrowed workplace ordering is not reviewed for this native build.");
        var field = typeof(Workplace).GetField("_workplaceBehaviors", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.FieldType != typeof(List<WorkplaceBehavior>)) throw new InvalidOperationException("Native workplace list changed.");
        return field;
    }
}
