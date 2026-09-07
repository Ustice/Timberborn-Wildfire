using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Wildfire.Core;
using Wildfire.Timberborn.Runtime;
using Wildfire.Timberborn.Simulation;

public static class MaterialSnapshotProbe
{
    public static void Run()
    {
        try
        {
            var output = Environment.GetEnvironmentVariable("WILDFIRE_SNAPSHOT_PROBE_OUTPUT");
            AssetDatabase.ImportAsset("Assets/FireSim.compute", ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/FireSim.compute");
            Require(shader != null, "shader missing");
            var log = new UnityTimberbornFireLogSink();
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
            using (var original = TimberbornComputeFireSimulator.CreateFromSnapshot(fixture, shader, log))
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
                try { using var invalid = TimberbornComputeFireSimulator.CreateFromSnapshot(malformed, shader, log); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "active slot alias accepted");
                Require(original.CaptureSnapshot().Cells.SequenceEqual(saved.Cells), "failed new construction changed original");
            }
            var decoded = JsonConvert.DeserializeObject<FireSimSnapshot>(File.ReadAllText(Path.Combine(output, "captured.json")));
            using (var restored = TimberbornComputeFireSimulator.CreateFromSnapshot(decoded, shader, log))
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
            Debug.Log("WILDFIRE_MATERIAL_SNAPSHOT_PROBE_PASS backend=native new_simulator=true serialized=true archived_slots=2 restored_fuel=3,0 restored_history=5,9 pending_water_once=true stale_token_rejected=true malformed_alias_rejected=true");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
    private static uint Companion(byte history) => new WildfireMaterialFieldState(WildfireMaterialClass.Tree, 9, history, 3,
        WildfireAshQuality.Fertile, WildfireContaminationBehavior.None, 6).Pack();
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
