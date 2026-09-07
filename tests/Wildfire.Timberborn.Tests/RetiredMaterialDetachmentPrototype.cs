using System.Runtime.ExceptionServices;
using Wildfire.Core;
using Wildfire.Timberborn.Mapping;
using Wildfire.Timberborn.Persistence;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.Tests;

// Disposable orchestration proof. No runtime registration or independent material authority.
internal static class RetiredMaterialDetachmentPrototype
{
    internal enum Result { NoWork, CapacityBlocked, Applied }

    internal static FireSimMaterialHandoffBatch? Plan(TimberbornNativeMaterialRegistry registry,
        IReadOnlyDictionary<Guid, OwnedBodyRetention> owners, FireSimSnapshot snapshot,
        IFireSimMaterialHandoffSimulator simulator)
    {
        snapshot = FireSimSnapshotValidation.ValidateAndClone(snapshot);
        _ = new TimberbornOwnedMaterialSnapshot(snapshot, registry.CaptureBindings());
        var active = snapshot.TargetIds.Select((target, cell) => (id: new FireSimMaterialIdentity(target, snapshot.SlotIds[cell]), cell))
            .Where(pair => pair.id.IsOwned).ToDictionary(pair => pair.id, pair => pair.cell);
        var pending = new Queue<int>();
        foreach (var pair in active)
        {
            if (!registry.TryResolveOrigin(pair.Key.TargetId, out var nativeId) || !owners.TryGetValue(nativeId, out var retention))
                throw new InvalidOperationException("Active material has no canonical native owner.");
            if (!Enum.IsDefined(retention)) throw new InvalidOperationException("Unknown native retention.");
            if (retention == OwnedBodyRetention.RetiredNativeOwner) pending.Enqueue(pair.Value);
        }
        if (pending.Count == 0) return null;
        var requests = new Dictionary<int, FireSimMaterialHandoffRequest>();
        while (pending.TryDequeue(out int cell))
        {
            if (requests.ContainsKey(cell)) continue;
            var expected = new FireSimMaterialIdentity(snapshot.TargetIds[cell], snapshot.SlotIds[cell]);
            var desired = registry.ResolveCell(cell);
            if (desired.Owner is not { } owner)
            {
                requests.Add(cell, FireSimMaterialHandoffRequest.Remove(cell, expected, PackedCell.Terrain(desired.PackedDefinition) != 0));
                continue;
            }
            if (!owners.TryGetValue(owner.EntityId, out var retention) || retention != OwnedBodyRetention.RetainedBody)
                throw new InvalidOperationException("Desired projection does not belong to a retained canonical body.");
            var incoming = new FireSimMaterialIdentity(owner.TargetId, owner.SlotId);
            if (incoming == expected) throw new InvalidOperationException("Captured source would remain active at its old cell.");
            if (!simulator.IsSlotKnown(incoming)) requests.Add(cell, registry.CreateFirstActivationRequest(cell, expected, simulator));
            else if (simulator.TryGetMaterialArchive(incoming, out var archive))
                requests.Add(cell, FireSimMaterialHandoffRequest.RestoreArchived(cell, expected, archive));
            else if (active.TryGetValue(incoming, out int source))
            {
                requests.Add(cell, FireSimMaterialHandoffRequest.RestoreCaptured(cell, expected, incoming, source));
                pending.Enqueue(source); // Include every active source that this whole transition must relinquish.
            }
            else throw new InvalidOperationException("Known incoming material has neither active source nor exact archive.");
        }
        return new(checked(snapshot.MaterialAuthority.LastAttemptToken + 1), requests.Values);
    }

    internal static Result Flush(NativeResourceTransaction guard, IFireSimMaterialHandoffSimulator simulator,
        FireSimMaterialHandoffBatch? batch, Action<GpuFireStepResult> consumeRaw)
    {
        guard.ThrowIfSaveUnsafe();
        if (batch is null) return Result.NoWork;
        Result outcome = Result.CapacityBlocked;
        FireSimStepInputException? notApplied = null;
        guard.TransferInventory(() =>
        {
            FireSimMaterialHandoffReceipt? receipt = null;
            GpuFireStepResult? step;
            try { step = simulator.TryHandoffMaterial(batch, value => receipt = value); }
            catch (FireSimStepInputException exception) when (exception.Outcome == FireSimStepInputOutcome.NotApplied)
            { notApplied = exception; return; } // Explicit no-apply receipt, not a guessed rollback.
            if (step is null) return;
            if (receipt is null) throw new InvalidOperationException("Completed step has no material receipt.");
            if (!receipt.Accepted) throw new InvalidOperationException("Material reconciliation was rejected after simulation; reload a valid snapshot.");
            consumeRaw(step.Value); // Deliberately raw: actual public owned Consumer currently starts its own guard.
            outcome = Result.Applied;
        });
        if (notApplied is not null) ExceptionDispatchInfo.Capture(notApplied).Throw();
        return outcome;
    }

    internal static void RequireNormalStepReady(NativeResourceTransaction guard, Func<FireSimMaterialHandoffBatch?> pending)
    {
        guard.ThrowIfSaveUnsafe();
        if (pending() is not null) throw new InvalidOperationException("Retired material detachment must finish before a normal step.");
    }
}
