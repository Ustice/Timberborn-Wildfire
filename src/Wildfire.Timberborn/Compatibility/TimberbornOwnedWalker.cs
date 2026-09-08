using System.Reflection;
using System.Security.Cryptography;
using Timberborn.WalkingSystem;

namespace Wildfire.Timberborn.Compatibility;

/// <summary>Stops a reviewed native walker without leaving its destination and path out of sync.</summary>
public sealed class TimberbornOwnedWalker
{
    private readonly Walker _walker;
    private readonly WalkerMover _mover;
    private bool _pausedByOwner;
    private static readonly Lazy<MethodInfo> StopMethod = new(VerifyNativeStop);
    private static readonly Lazy<FieldInfo> DestinationField = new(VerifyDestinationField);
    internal IDestination? CurrentDestination => (IDestination?)DestinationField.Value.GetValue(_walker);

    public TimberbornOwnedWalker(Walker walker, WalkerMover mover) { _walker = walker; _mover = mover; }
    public static void Verify() => _ = StopMethod.Value;

    /// <summary>Safe inside StartedNewPath: disable the late mover, retaining the path FindPath still reads.</summary>
    public void RejectRoute()
    {
        Verify();
        if (!_mover.Enabled) return;
        _mover.DisableComponent();
        _pausedByOwner = true;
    }
    /// <summary>Call after GoTo/RefreshPath returns, never inside a normally returning StartedNewPath callback.</summary>
    public void Stop()
    {
        RejectRoute();
        StopMethod.Value.Invoke(_walker, null);
    }
    /// <summary>Only after an owned validated launch, or after a successful stop when releasing to native roots.</summary>
    public void ReleasePause()
    {
        if (!_pausedByOwner) return;
        _mover.EnableComponent();
        _pausedByOwner = false;
    }
    private static FieldInfo VerifyDestinationField()
    {
        Verify();
        var field = typeof(Walker).GetField("_currentDestination", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field?.FieldType != typeof(IDestination) || field.IsStatic)
            throw new InvalidOperationException("Native Walker destination identity field changed.");
        return field;
    }
    private static MethodInfo VerifyNativeStop()
    {
        string managed = Path.GetDirectoryName(typeof(Walker).Assembly.Location)!;
        foreach (var entry in Fingerprints)
        {
            using var stream = File.OpenRead(Path.Combine(managed, entry.Key));
            using var sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (actual != entry.Value) throw new InvalidOperationException($"Native owned-walk stopping is not reviewed for {entry.Key} ({actual}).");
        }
        var method = typeof(Walker).GetMethod("StopMoving", BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, types: Type.EmptyTypes, modifiers: null);
        if (method is null || method.ReturnType != typeof(void))
            throw new InvalidOperationException("Native Walker.StopMoving signature changed.");
        return method;
    }
    private static readonly IReadOnlyDictionary<string, string> Fingerprints = new Dictionary<string, string>
    {
        ["Timberborn.WalkingSystem.dll"] = "458ef206ef59c4af6cff0a11c050abc188376e412ca170da2762cd8f91672061",
        ["Timberborn.CharacterMovementSystem.dll"] = "e39989352ceaaa6571ae403d06d5ab73ca60d627ffd35ae015b3c83e747a5aa4",
        ["Timberborn.BaseComponentSystem.dll"] = "0d479b9533880e1368b5df144f8d83f30d9c8774cfc8f96f071ac53d5851318d",
        ["Timberborn.TickSystem.dll"] = "5df66413408a54fc616343668600f114fd33056fafe0bf4b2465be084c55cb9d",
    };
}
