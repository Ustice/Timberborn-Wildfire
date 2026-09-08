using Timberborn.BaseComponentSystem;
using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Persistence;
using Timberborn.WaterBuildings;
using Timberborn.WorldPersistence;
using UnityEngine;
using Wildfire.Timberborn.Compatibility;
using Wildfire.Timberborn.Resources;

namespace Wildfire.Timberborn.FireBell;

// Created only by explicit QA source setup; persistent native ownership is not a public responder policy.
internal sealed class TimberbornNaturalWaterSource : BaseComponent,
    IAwakableComponent, IPersistentEntity, IDeletableEntity, IPrePlacementChangeListener
{
    private readonly TimberbornWaterCreditBoundary boundary;
    internal TimberbornNaturalWaterSource(TimberbornWaterCreditBoundary boundary) => this.boundary = boundary;
    private static readonly ComponentKey Key = new("Wildfire.NaturalWaterOwnership");
    private static readonly PropertyKey<int> Version = new("Version");
    private static readonly PropertyKey<bool> Taint = new("Tainted");
    private static readonly PropertyKey<string> Owner = new("Owner");
    private static readonly PropertyKey<int> X = new("X"), Y = new("Y"), Z = new("Z");
    internal WaterInput Input { get; private set; } = null!;
    internal EntityComponent Entity { get; private set; } = null!;
    private BlockObject _block = null!;
    private Guid _owner;
    private Vector3Int _coordinate;
    private bool _loaded, _armed, _tainted, _restorePermit;
    internal bool Tainted => _tainted;
    internal bool Armed => _armed;
    internal Vector3Int Coordinate => _coordinate;

    public void Awake()
    {
        Entity = GetComponent<EntityComponent>();
        _block = GetComponent<BlockObject>();
        Input = GetComponent<WaterInput>();
        boundary.Track(this);
    }
    // Called after native entity creation returns, never from a sibling PostInitialize callback.
    internal bool TryArmNewSource()
    {
        if (_loaded || _armed || _tainted || !boundary.CanRead || !boundary.Install()) return false;
        var contract = boundary.Contract!;
        if (!contract.ZeroBuffer(Input) || !Active || !NativeIdentity(contract) || !boundary.Exclusive(this, Input.Coordinates))
        { Refuse(); return false; }
        _owner = Entity.EntityId;
        _coordinate = Input.Coordinates;
        _armed = true;
        return true;
    }
    internal bool Active => _block.IsFinished && _block.AddedToService;
    internal bool Ready => _armed && !_tainted && boundary.CanRead && boundary.Installed &&
        Active && MatchesIdentity(boundary.Contract!) && boundary.Exclusive(this, _coordinate);
    internal bool TryCaptureCleanIntake(NativeResourceCoordinator resources, out TimberbornWaterCreditContract.CleanIntake intake)
    {
        intake = default;
        return boundary.CanFill(resources) && Ready && boundary.TryCaptureIntake(this, out intake);
    }
    internal bool MatchesCleanIntake(NativeResourceCoordinator resources, TimberbornWaterCreditContract.CleanIntake intake) =>
        boundary.CanFill(resources) && Ready && ReferenceEquals(Input, intake.Input) && boundary.MatchesIntake(intake);
    private bool NativeIdentity(TimberbornWaterCreditContract contract) =>
        Entity && Entity.Initialized && !Entity.Deleted && _block &&
        GetComponentsAllocating<WaterInput>().Count == 1 && ReferenceEquals(GetComponent<WaterInput>(), Input) &&
        contract.Fixed(Input) && _block.TransformCoordinates(GetComponent<WaterInputSpec>().WaterInputCoordinates) == Input.Coordinates;
    internal bool MatchesIdentity(TimberbornWaterCreditContract contract) => NativeIdentity(contract) &&
        Entity.EntityId == _owner && Input.Coordinates == _coordinate && contract.ValidBuffer(Input);
    internal void Refuse() => _tainted = true; // Monotonic ownership invalidation, no native resource write/callback.
    public void OnPrePlacementChanged() { if (_armed) Refuse(); }
    public void DeleteEntity() { Refuse(); boundary.Forget(this); }

    public void Load(IEntityLoader loader)
    {
        if (_loaded || _armed) { Refuse(); throw new InvalidOperationException("Water ownership was loaded twice."); }
        _loaded = true;
        if (!boundary.CanRestore(this, loader) || !loader.TryGetComponent(Key, out var saved)) { Refuse(); return; }
        if (saved.Get(Version) != 1 || !Guid.TryParse(saved.Get(Owner), out _owner))
            throw new InvalidOperationException("Unsupported or malformed native water ownership witness.");
        _tainted |= saved.Get(Taint);
        _coordinate = new(saved.Get(X), saved.Get(Y), saved.Get(Z));
        _restorePermit = boundary.Contract!.SavedBufferPresent(loader);
        if (!_restorePermit) Refuse();
    }
    internal void CompleteRestore(bool inNativeLoad)
    {
        bool permitted = _restorePermit;
        _restorePermit = false; // Single native PostLoad fence, never a reusable load permission.
        if (!permitted || _tainted) return;
        if (!inNativeLoad || !boundary.Installed || !Active || !MatchesIdentity(boundary.Contract!) || !boundary.Exclusive(this, _coordinate))
        { Refuse(); return; }
        _armed = true;
    }
    public void Save(IEntitySaver saver)
    {
        boundary.CheckSave();
        if (!_armed || !boundary.Installed || !MatchesIdentity(boundary.Contract!)) Refuse();
        var saved = saver.GetComponent(Key);
        saved.Set(Version, 1); saved.Set(Taint, _tainted);
        saved.Set(Owner, (_armed ? _owner : Entity.EntityId).ToString("D"));
        var coordinate = _coordinate;
        saved.Set(X, coordinate.x); saved.Set(Y, coordinate.y); saved.Set(Z, coordinate.z);
    }
}
