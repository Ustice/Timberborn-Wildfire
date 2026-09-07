using Timberborn.EntitySystem;
using Timberborn.MapStateSystem;
using Timberborn.WorkSystem;
using UnityEngine;
using Wildfire.Timberborn.FireSafety;
using Wildfire.Timberborn.Qa;

namespace Wildfire.Timberborn.FireBell;

/// <summary>Runs synchronously from the existing native singleton QA update, never a console bypass.</summary>
public sealed class BorrowedDutyQaApi : ITimberbornQaBorrowedDuty
{
    private readonly BorrowedDutyFixture _fixture;
    private readonly EntityRegistry _entities;
    private readonly MapSize _map;
    private readonly FireSafetyField _field;
    public BorrowedDutyQaApi(BorrowedDutyFixture fixture, EntityRegistry entities, MapSize map, FireSafetyField field)
    { _fixture = fixture; _entities = entities; _map = map; _field = field; }

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
