using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Wildfire.Core;
using System.Reflection;
using System.Runtime.ExceptionServices;

public static class MaterialSnapshotProbe
{
    public static void Run()
    {
        try
        {
            var output = Environment.GetEnvironmentVariable("WILDFIRE_SNAPSHOT_PROBE_OUTPUT");
            Debug.Log("WILDFIRE_GPU_RESOURCE_LIMITS backend=" + SystemInfo.graphicsDeviceType + " device=" + SystemInfo.graphicsDeviceName +
                " supportedRandomWriteTargetCount=" + SystemInfo.supportedRandomWriteTargetCount +
                " maxComputeBufferInputsCompute=" + SystemInfo.maxComputeBufferInputsCompute);
            AssetDatabase.ImportAsset("Assets/FireSim.compute", ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/FireSim.compute");
            Require(shader != null, "shader missing");
            var a0 = new FireSimMaterialIdentity(1, 11);
            var a1 = new FireSimMaterialIdentity(1, 12);
            var b0 = new FireSimMaterialIdentity(2, 21);
            var b1 = new FireSimMaterialIdentity(2, 22);
            var profile = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 9,
                WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 9, 2, 1);
            var fixture = new FireSimSnapshot(1, new FireGrid(2, 1, 1), 0, FireSimParameters.Default, 89,
                new ushort[] { PackedCell.Pack(3, 0, 2, 1, 1, 0), PackedCell.Pack(0, 0, 2, 2, 1, 0) },
                new uint[] { 0x18, 0x400 }, new uint[] { Companion(5), Companion(9) },
                new uint[] { 1, 1 }, new uint[] { 11, 12 },
                new FireSimMaterialAuthoritySnapshot(0, new[] { a0, a1 }, new FireSimMaterialArchiveSnapshot[0]),
                new FireSimChange[0]);
            FireSimSnapshot saved;
            using (var original = NativeSimulator.Create(fixture, shader))
            {
                var hide = new FireSimMaterialHandoffBatch(1, new[]
                {
                    FireSimMaterialHandoffRequest.Fresh(0, a0, b0, profile),
                    FireSimMaterialHandoffRequest.Fresh(1, a1, b1, profile),
                });
                original.TryHandoffMaterial(hide, receipt => Require(receipt.Accepted, "hide rejected"));
                original.RegisterChange(new FireSimChange(0, AddWater: 1));
                saved = original.CaptureSnapshot();
                Require(saved.MaterialAuthority.Archives.Length == 2, "GPU archives missing");
                Require(saved.PendingChanges.Length == 1, "pending water was lost");
                Require(saved.MaterialAuthority.LastAttemptToken == 1, "attempt token missing");
                File.WriteAllText(Path.Combine(output, "captured.json"), JsonConvert.SerializeObject(saved, Formatting.Indented));
                var malformed = saved with { SlotIds = new uint[] { 21, 21 } };
                bool rejected = false;
                try { using var invalid = NativeSimulator.Create(malformed, shader); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "active slot alias accepted");
                Require(original.CaptureSnapshot().Cells.SequenceEqual(saved.Cells), "failed new construction changed original");
            }
            var decoded = JsonConvert.DeserializeObject<FireSimSnapshot>(File.ReadAllText(Path.Combine(output, "captured.json")));
            using (var restored = NativeSimulator.Create(decoded, shader))
            {
                var recaptured = restored.CaptureSnapshot();
                Require(saved.Cells.SequenceEqual(recaptured.Cells), "restored cells differ");
                Require(saved.TransportFields.SequenceEqual(recaptured.TransportFields), "restored transport differs");
                Require(saved.CompanionFields.SequenceEqual(recaptured.CompanionFields), "restored companions differ");
                Require(saved.TargetIds.SequenceEqual(recaptured.TargetIds) && saved.SlotIds.SequenceEqual(recaptured.SlotIds), "restored identities differ");
                Require(recaptured.Tick == saved.Tick && recaptured.Seed == 89 && recaptured.Parameters == saved.Parameters, "tick/seed/parameters differ");
                Require(recaptured.PendingChanges.SequenceEqual(saved.PendingChanges), "pending ordering differs");
                Require(restored.TryGetMaterialArchive(a0, out var first) && restored.TryGetMaterialArchive(a1, out _), "restored archives unavailable");
                restored.TryGetMaterialArchive(a1, out var exhausted);
                Require((first.PackedCell & 15) == 3 && (exhausted.PackedCell & 15) == 0, "retained fuel changed");
                var stale = new FireSimMaterialHandoffBatch(1, new[] { FireSimMaterialHandoffRequest.RestoreArchived(0, b0, first) });
                bool rejected = false;
                try { restored.TryHandoffMaterial(stale, _ => { }); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "old attempt token replayed");
                var show = new FireSimMaterialHandoffBatch(2, new[]
                {
                    FireSimMaterialHandoffRequest.RestoreArchived(0, b0, first),
                    FireSimMaterialHandoffRequest.RestoreArchived(1, b1, exhausted),
                });
                restored.TryHandoffMaterial(show, receipt =>
                {
                    Require(receipt.Accepted, "re-exposure rejected");
                    Require((receipt.Cells[0].AppliedCell & 15) == 3 && (receipt.Cells[1].AppliedCell & 15) == 0, "re-exposure refueled");
                    Require(((receipt.Cells[0].AppliedCompanion >> 12) & 15) == 5 && ((receipt.Cells[1].AppliedCompanion >> 12) & 15) == 9, "history changed");
                    Require(((receipt.Cells[0].PriorCell >> 10) & 3) == 2, "pending water was not applied before handoff");
                    File.WriteAllText(Path.Combine(output, "restored-receipt.json"), JsonConvert.SerializeObject(receipt, Formatting.Indented));
                });
                Require(!restored.TryGetMaterialArchive(a0, out _), "consumed archive still available");
                var final = restored.CaptureSnapshot();
                Require((final.Cells[1] & 15) == 0 && final.TargetIds.SequenceEqual(new uint[] { 1, 1 }), "final ownership or exhaustion differs");
                File.WriteAllText(Path.Combine(output, "recaptured.json"), JsonConvert.SerializeObject(final, Formatting.Indented));
            }
            ProveGpuExhaustion(shader, output);
            ProveFirstSlotActivation(shader, output);
            Debug.Log("WILDFIRE_MATERIAL_SNAPSHOT_PROBE_PASS backend=native new_simulator=true serialized=true archived_slots=2 restored_fuel=3,0 restored_history=5,9 pending_water_once=true stale_token_rejected=true malformed_alias_rejected=true");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static void ProveGpuExhaustion(ComputeShader shader, string output)
    {
        var owner = new FireSimMaterialIdentity(70, 701);
        var replacement = new FireSimMaterialIdentity(80, 801);
        var profile = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 9,
            WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 9, 2, 1);
        var hot = new FireSimSnapshot(1, new FireGrid(1, 1, 1), 0, FireSimParameters.Default.WithFuelBurnDown(16, 1), 89,
            new[] { PackedCell.Pack(3, 15, 3, 0, 1, 0) }, new uint[] { 0 }, new[] { Companion(0) },
            new uint[] { 70 }, new uint[] { 701 },
            new FireSimMaterialAuthoritySnapshot(0, new[] { owner }, new FireSimMaterialArchiveSnapshot[0]), new FireSimChange[0]);
        FireSimSnapshot saved;
        uint burnTicks;
        bool sawFuelDecrease = false;
        using (var original = NativeSimulator.Create(hot, shader))
        {
            for (int tick = 0; tick < 16 && (original.CaptureSnapshot().Cells[0] & 15) != 0; tick++)
            {
                var step = original.Tick();
                sawFuelDecrease |= step.Deltas.Any(delta => delta.TargetId == 70 && (delta.NewCell & 15) < (delta.OldCell & 15));
            }
            var exhausted = original.CaptureSnapshot();
            burnTicks = exhausted.Tick;
            Require(sawFuelDecrease && (exhausted.Cells[0] & 15) == 0, "GPU did not exhaust original fuel");
            original.TryHandoffMaterial(new FireSimMaterialHandoffBatch(1, new[]
                { FireSimMaterialHandoffRequest.Fresh(0, owner, replacement, profile) }), receipt => Require(receipt.Accepted, "burned owner hide rejected"));
            saved = original.CaptureSnapshot();
        }
        var serialized = JsonConvert.SerializeObject(saved, Formatting.Indented);
        File.WriteAllText(Path.Combine(output, "gpu-exhausted-snapshot.json"), serialized);
        using (var restored = NativeSimulator.Create(JsonConvert.DeserializeObject<FireSimSnapshot>(serialized), shader))
        {
            Require(restored.TryGetMaterialArchive(owner, out var archive) && (archive.PackedCell & 15) == 0, "GPU-exhausted archive refueled on restore");
            restored.TryHandoffMaterial(new FireSimMaterialHandoffBatch(2, new[]
                { FireSimMaterialHandoffRequest.RestoreArchived(0, replacement, archive) }), receipt =>
            {
                Require(receipt.Accepted && (receipt.Cells[0].AppliedCell & 15) == 0, "GPU-exhausted owner refueled on re-exposure");
                Require(receipt.Cells[0].AppliedCompanion == archive.Companion, "GPU-captured raw companion changed");
                File.WriteAllText(Path.Combine(output, "gpu-exhausted-reexposed-receipt.json"), JsonConvert.SerializeObject(receipt, Formatting.Indented));
            });
            Require((restored.CaptureSnapshot().Cells[0] & 15) == 0, "exhausted owner refueled after next simulation");
        }
        Debug.Log("WILDFIRE_GPU_EXHAUSTION_RESTORE_PASS initial_fuel=3 final_fuel=0 burn_ticks=" + burnTicks + " owner_delta=true new_simulator=true reexposed_fuel=0");
    }
    private static void ProveFirstSlotActivation(ComputeShader shader, string output)
    {
        var first = new FireSimMaterialIdentity(70, 701);
        var second = new FireSimMaterialIdentity(70, 702);
        var third = new FireSimMaterialIdentity(70, 703);
        var profile = new FireSimMaterialDefinition(WildfireMaterialClass.Tree, 9,
            WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 3, 2, 1);
        var snapshot = new FireSimSnapshot(1, new FireGrid(2, 1, 1), 0, FireSimParameters.Default, 89,
            new[] { PackedCell.Pack(0, 0, 2, 2, 1, 0), PackedCell.Pack(0, 0, 0, 2, 0, 0) },
            new uint[2], new[] { Companion(9), 0u }, new uint[] { 70, 0 }, new uint[] { 701, 0 },
            new FireSimMaterialAuthoritySnapshot(0, new[] { first }, new FireSimMaterialArchiveSnapshot[0]), new FireSimChange[0]);
        FireSimSnapshot saved;
        using (var original = NativeSimulator.Create(snapshot, shader))
        {
            Require(original.IsSlotKnown(first) && !original.IsSlotKnown(second), "wrong initial slot authority");
            original.TryHandoffMaterial(new FireSimMaterialHandoffBatch(1, new[] {
                FireSimMaterialHandoffRequest.Fresh(1, default, second, profile) }), receipt =>
                Require(receipt.Accepted && (receipt.Cells[0].AppliedCell & 15) == 3, "first hidden slot activation rejected"));
            Require(original.IsSlotKnown(second), "accepted new slot did not publish authority");
            original.TryHandoffMaterial(new FireSimMaterialHandoffBatch(2, new[] {
                FireSimMaterialHandoffRequest.Fresh(1, second, third, profile) }), receipt => {
                Require(receipt.Accepted, "same target different slot rejected by GPU");
                File.WriteAllText(Path.Combine(output, "first-slot-receipt.json"), JsonConvert.SerializeObject(receipt, Formatting.Indented));
            });
            saved = original.CaptureSnapshot();
            Require((saved.Cells[0] & 15) == 0 && saved.CompanionFields[0] == snapshot.CompanionFields[0], "known exhausted sibling changed");
        }
        File.WriteAllText(Path.Combine(output, "first-slot-snapshot.json"), JsonConvert.SerializeObject(saved, Formatting.Indented));
        using (var restored = NativeSimulator.Create(JsonConvert.DeserializeObject<FireSimSnapshot>(JsonConvert.SerializeObject(saved)), shader))
        {
            Require(restored.IsSlotKnown(first) && restored.IsSlotKnown(second) && restored.IsSlotKnown(third), "saved known pair authority changed");
            bool rejected = false;
            try { restored.TryHandoffMaterial(new FireSimMaterialHandoffBatch(3, new[] {
                FireSimMaterialHandoffRequest.Fresh(1, third, second, profile) }), _ => { }); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "previously active pair accepted as fresh");
            Require(restored.TryGetMaterialArchive(second, out var archive) && (archive.PackedCell & 15) == 3, "saved same-owner archive missing");
            restored.TryHandoffMaterial(new FireSimMaterialHandoffBatch(3, new[] {
                FireSimMaterialHandoffRequest.RestoreArchived(1, third, archive) }), receipt =>
                Require(receipt.Accepted && (receipt.Cells[0].AppliedCell & 15) == 3, "retained same-owner slot rejected"));
            Require((restored.CaptureSnapshot().Cells[0] & 15) == 0, "exhausted sibling refilled after restore");
        }
        Debug.Log("WILDFIRE_FIRST_SLOT_ACTIVATION_PASS backend=native same_target_new_slot=true same_target_handoff=true known_pair_fresh_rejected=true archived_pair_restored=true exhausted_sibling_fuel=0 serialized=true");
    }
    private static uint Companion(byte history) => new WildfireMaterialFieldState(WildfireMaterialClass.Tree, 9, history, 3,
        WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 6).Pack();
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

// Load the unchanged production DLL lazily. Do not stub any dependency of the exercised factory or GPU backend.
internal sealed class NativeSimulator : IDisposable
{
    private readonly object _native;
    private NativeSimulator(object native) { _native = native; }
    public static NativeSimulator Create(FireSimSnapshot snapshot, ComputeShader shader)
    {
        var assembly = Assembly.LoadFrom(Environment.GetEnvironmentVariable("WILDFIRE_SNAPSHOT_PROBE_NATIVE"));
        var type = assembly.GetType("Wildfire.Timberborn.Simulation.TimberbornComputeFireSimulator", true);
        var log = Activator.CreateInstance(assembly.GetType("Wildfire.Timberborn.Runtime.UnityTimberbornFireLogSink", true));
        try { return new NativeSimulator(type.GetMethod("CreateFromSnapshot").Invoke(null, new object[] { snapshot, shader, log, null })); }
        catch (TargetInvocationException exception) { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
    public void RegisterChange(FireSimChange change) => ((IGpuFireSimulator)_native).RegisterChange(change);
    public GpuFireStepResult Tick() => ((IGpuFireSimulator)_native).Tick();
    public FireSimSnapshot CaptureSnapshot() => ((IFireSimSnapshotSimulator)_native).CaptureSnapshot();
    public bool IsSlotKnown(FireSimMaterialIdentity identity) => ((IFireSimMaterialHandoffSimulator)_native).IsSlotKnown(identity);
    public bool TryGetMaterialArchive(FireSimMaterialIdentity identity, out FireSimMaterialArchive archive)
        => ((IFireSimMaterialHandoffSimulator)_native).TryGetMaterialArchive(identity, out archive);
    public GpuFireStepResult? TryHandoffMaterial(FireSimMaterialHandoffBatch batch, Action<FireSimMaterialHandoffReceipt> commit)
        => ((IFireSimMaterialHandoffSimulator)_native).TryHandoffMaterial(batch, commit);
    public void Dispose() => ((IDisposable)_native).Dispose();
}
