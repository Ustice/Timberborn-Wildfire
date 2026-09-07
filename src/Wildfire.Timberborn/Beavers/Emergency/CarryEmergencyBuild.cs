using System.Security.Cryptography;

namespace Wildfire.Timberborn.Beavers.Emergency;

/// <summary>Reviewed macOS build only. New builds require IL review, never an optimistic signature fallback.</summary>
public static class CarryEmergencyBuild
{
    private static readonly IReadOnlyDictionary<string, string> Fingerprints = new Dictionary<string, string>
    {
        ["Timberborn.BehaviorSystem.dll"] = "d49c74ab361748bb8bd815a2fade378246224565fdbc3ed048e633f4704ba8a8",
        ["Timberborn.TickSystem.dll"] = "5df66413408a54fc616343668600f114fd33056fafe0bf4b2465be084c55cb9d",
        ["Timberborn.WalkingSystem.dll"] = "458ef206ef59c4af6cff0a11c050abc188376e412ca170da2762cd8f91672061",
        ["Timberborn.Carrying.dll"] = "0c1c05da9f2b0701c6ec8bedaeb8b9b631ca7e6fc0f44164f1dd708a7ffbe08c",
        ["Timberborn.InventorySystem.dll"] = "ba109660d20d82d88c8348965957a4c2fdb3d172a8e60033a2afd794b9d12f8f",
        ["Timberborn.CharacterMovementSystem.dll"] = "e39989352ceaaa6571ae403d06d5ab73ca60d627ffd35ae015b3c83e747a5aa4",
        ["Timberborn.EntitySystem.dll"] = "c198cf97f6c57a50117c2593a686aa7874cd8eeb567fd25f3409e18d29a0635e",
        ["Timberborn.EnterableSystem.dll"] = "3a25b059edaebac237b089724233527d3c33ff29ccedbccee3cbc01f860c1a6a",
    };

    public static void VerifyDirectory(string managedDirectory)
    {
        foreach (var entry in Fingerprints)
        {
            using var stream = File.OpenRead(Path.Combine(managedDirectory, entry.Key));
            using var sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            if (actual != entry.Value)
                throw new InvalidOperationException($"Carrying emergency is not reviewed for {entry.Key} ({actual}).");
        }
    }
}
