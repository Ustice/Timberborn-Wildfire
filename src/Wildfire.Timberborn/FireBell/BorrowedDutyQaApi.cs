using Timberborn.EntitySystem;
using Timberborn.MapStateSystem;
using Timberborn.InventorySystem;
using Timberborn.WorkSystem;
using UnityEngine;
using Wildfire.Timberborn.FireSafety;
using Wildfire.Timberborn.Qa;

namespace Wildfire.Timberborn.FireBell;

/// <summary>Runs synchronously from the existing native singleton QA update, never a console bypass.</summary>
internal sealed class BorrowedDutyQaApi : ITimberbornQaBorrowedDuty
{
    private readonly BorrowedDutyFixture _fixture;
    private readonly EntityRegistry _entities;
    private readonly MapSize _map;
    private readonly FireSafetyField _field;
    private readonly NaturalWaterSourceQaFactory _sources;
    public BorrowedDutyQaApi(BorrowedDutyFixture fixture, EntityRegistry entities, MapSize map, FireSafetyField field, NaturalWaterSourceQaFactory sources)
    { _fixture = fixture; _entities = entities; _map = map; _field = field; _sources = sources; }

    public BorrowedDutyQaStatus Arm(BorrowedDutyQaArmRequest request)
    {
        if (!_fixture.AdmissionsEnabled) throw new InvalidOperationException($"Borrowed duty requires {BorrowedDutyFixture.OptInSwitch}.");
        if (!_field.Ready) throw new InvalidOperationException("Borrowed duty requires an available enabled fire field.");
        if (request.DonorId == Guid.Empty || !float.IsFinite(request.X) || !float.IsFinite(request.Y) || !float.IsFinite(request.Z))
            throw new ArgumentException("An explicit donor and finite destination are required.");
        var size = _map.TotalSize; // Timberborn grid x/y are horizontal; z is height.
        if (request.X < 0 || request.Z < 0 || request.Y < 0 || request.X >= size.x || request.Z >= size.y || request.Y >= size.z)
            throw new ArgumentException("Borrowed duty destination is outside the map.");
        var donor = ResolveDonor(_entities, request.DonorId);
        _fixture.Arm(donor, new Vector3(request.X, request.Y, request.Z));
        return Status();
    }
    public BorrowedDutyQaStatus CreateSource(BorrowedSourceQaRequest request)
    {
        RequireWaterAdmission();
        var source = _sources.Create(new Vector3Int(request.X, request.Y, request.Z), Position(request.Shore));
        return Status() with { CreatedSourceId = source.Entity.EntityId };
    }
    public BorrowedDutyQaStatus ArmWater(BorrowedWaterQaRequest request)
    {
        RequireWaterAdmission();
        var donor = ResolveDonor(_entities, request.DonorId);
        var entity = _entities.GetEntity(request.SourceId);
        if (entity is null || !entity || entity.Deleted || entity.GetComponent<TimberbornNaturalWaterSource>() is not { } source)
            throw new ArgumentException("The source must be a live explicitly created natural-water source.");
        var returnOwner = _entities.GetEntity(request.ReturnOwnerId);
        if (returnOwner is null || !returnOwner || returnOwner.Deleted) throw new ArgumentException("Return inventory owner is unavailable.");
        var inventories = new List<Inventory>();
        returnOwner.GetComponents(inventories);
        var matches = inventories.Where(inventory => inventory.ComponentName == request.InventoryComponent).ToArray();
        if (matches.Length != 1) throw new ArgumentException("Select one exact native return inventory ComponentName.");
        var trip = new BorrowedWaterTrip(_sources, source, Position(request.Shore), request.FireCell, Position(request.Approach), matches[0]);
        if (!trip.SourceUsable || !trip.TargetUsable(_field)) throw new ArgumentException("The source/shore or burning target/approach is unavailable.");
        _fixture.ArmWater(donor, trip);
        return Status();
    }
    private void RequireWaterAdmission()
    {
        if (!_fixture.AdmissionsEnabled || !_field.Ready)
            throw new InvalidOperationException("Finite water admission requires the borrowed-duty opt-in and a ready fire field.");
    }
    private static Vector3 Position(BorrowedDutyQaPoint point) => new(point.X, point.Y, point.Z);
    internal static Workplace ResolveDonor(EntityRegistry entities, Guid id)
    {
        var entity = entities.GetEntity(id);
        if (entity is null || entity.Deleted || !entity) throw new ArgumentException("Borrowed duty donor does not exist or was deleted.");
        var donor = entity.GetComponent<Workplace>();
        if (donor is null || !donor || !donor.Enabled) throw new ArgumentException("Borrowed duty donor must be a live enabled workplace.");
        return donor;
    }
    public BorrowedDutyQaStatus Cancel() { _fixture.Cancel(); return Status(); }
    public BorrowedDutyQaStatus Status() => _fixture.CaptureQaStatus();
}
