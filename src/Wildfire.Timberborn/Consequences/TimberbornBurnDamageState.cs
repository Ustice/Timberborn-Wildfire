using Wildfire.Core;

namespace Wildfire.Timberborn;

public enum TimberbornBurnDamageTargetKind
{
    Unknown,
    Crop,
    Tree,
    Resource,
    Structure,
    Storage,
    Infrastructure,
}

public enum TimberbornBurnMaterialKind
{
    Unknown,
    Organic,
    Wood,
    Constructed,
    StoredGood,
    NonBurnable,
}

public readonly record struct TimberbornBurnDamageTargetKey
{
    public TimberbornBurnDamageTargetKey(string stableId)
    {
        if (string.IsNullOrWhiteSpace(stableId))
        {
            throw new ArgumentException("Burn damage target identity must be stable and non-empty.", nameof(stableId));
        }

        StableId = stableId.Trim();
    }

    public string StableId { get; }

    public override string ToString()
    {
        return StableId;
    }
}

public readonly record struct TimberbornBurnDamageResourceStack
{
    public TimberbornBurnDamageResourceStack(string resourceId, int amount)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ArgumentException("Resource id must be non-empty.", nameof(resourceId));
        }

        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Resource amount cannot be negative.");
        }

        ResourceId = resourceId.Trim();
        Amount = amount;
    }

    public string ResourceId { get; }

    public int Amount { get; }
}

public sealed record TimberbornBurnDamageDescriptor
{
    public TimberbornBurnDamageDescriptor(
        string specId,
        TimberbornBurnDamageTargetKind targetKind,
        TimberbornBurnMaterialKind materialKind,
        IReadOnlyList<TimberbornBurnDamageResourceStack>? resourceYields = null,
        IReadOnlyList<TimberbornBurnDamageResourceStack>? constructionResources = null,
        TimberbornBurnableProfile? burnableProfile = null)
    {
        if (string.IsNullOrWhiteSpace(specId))
        {
            throw new ArgumentException("Burn damage descriptor spec id must be non-empty.", nameof(specId));
        }

        SpecId = specId.Trim();
        TargetKind = targetKind;
        MaterialKind = materialKind;
        ResourceYields = (resourceYields ?? Array.Empty<TimberbornBurnDamageResourceStack>()).ToArray();
        ConstructionResources = (constructionResources ?? Array.Empty<TimberbornBurnDamageResourceStack>()).ToArray();
        BurnableProfile = burnableProfile;
    }

    public string SpecId { get; }

    public TimberbornBurnDamageTargetKind TargetKind { get; }

    public TimberbornBurnMaterialKind MaterialKind { get; }

    public IReadOnlyList<TimberbornBurnDamageResourceStack> ResourceYields { get; }

    public IReadOnlyList<TimberbornBurnDamageResourceStack> ConstructionResources { get; }

    public TimberbornBurnableProfile? BurnableProfile { get; }

    public bool HasResourceAccounting => ResourceYields.Count > 0 || ConstructionResources.Count > 0;
}

public sealed class TimberbornBurnDamageDescriptorCatalog
{
    private readonly IReadOnlyDictionary<string, TimberbornBurnDamageDescriptor> _descriptorsBySpecId;

    public TimberbornBurnDamageDescriptorCatalog(IEnumerable<TimberbornBurnDamageDescriptor> descriptors)
    {
        if (descriptors is null)
        {
            throw new ArgumentNullException(nameof(descriptors));
        }

        _descriptorsBySpecId = descriptors.ToDictionary(
            static descriptor => descriptor.SpecId,
            static descriptor => descriptor,
            StringComparer.Ordinal);
    }

    public bool TryLookup(string? specId, out TimberbornBurnDamageDescriptor descriptor)
    {
        string normalizedSpecId = specId?.Trim() ?? "";
        if (normalizedSpecId.Length == 0)
        {
            descriptor = CreateUnknownDescriptor(normalizedSpecId);
            return false;
        }

        if (_descriptorsBySpecId.TryGetValue(normalizedSpecId, out descriptor!))
        {
            return true;
        }

        descriptor = CreateUnknownDescriptor(normalizedSpecId);
        return false;
    }

    private static TimberbornBurnDamageDescriptor CreateUnknownDescriptor(string specId)
    {
        return new TimberbornBurnDamageDescriptor(
            specId.Length == 0 ? "Unknown" : specId,
            TimberbornBurnDamageTargetKind.Unknown,
            TimberbornBurnMaterialKind.Unknown);
    }
}

public sealed record TimberbornBurnDamageCapacity(
    int Capacity,
    byte FuelValue,
    byte Flammability,
    IReadOnlyList<string> MissingResourceIds,
    IReadOnlyList<string> AccountedResourceIds);

public sealed class TimberbornBurnDamageCapacityCalculator
{
    private const byte LogBurnDamageFuelValue = 12;
    private const byte ProcessedWoodBurnDamageFuelValue = 3;
    private const byte OrganicProduceBurnDamageFuelValue = 3;
    private static readonly HashSet<string> InertZeroFuelResources = new(StringComparer.Ordinal)
    {
        "Badwater",
        "Dirt",
        "MetalBlock",
        "ScrapMetal",
        "Water",
    };

    private readonly TimberbornResourceFuelCatalog _resourceFuelCatalog;

    public TimberbornBurnDamageCapacityCalculator()
        : this(TimberbornResourceFuelCatalog.Default)
    {
    }

    public TimberbornBurnDamageCapacityCalculator(TimberbornResourceFuelCatalog resourceFuelCatalog)
    {
        _resourceFuelCatalog = resourceFuelCatalog ?? throw new ArgumentNullException(nameof(resourceFuelCatalog));
    }

    public TimberbornBurnDamageCapacity Calculate(TimberbornBurnDamageDescriptor descriptor)
    {
        if (descriptor is null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        if (descriptor.BurnableProfile is { } burnableProfile &&
            burnableProfile.Known &&
            descriptor.TargetKind is not TimberbornBurnDamageTargetKind.Tree and
                not TimberbornBurnDamageTargetKind.Crop and
                not TimberbornBurnDamageTargetKind.Resource)
        {
            return new TimberbornBurnDamageCapacity(
                Capacity: burnableProfile.DamageCapacity,
                FuelValue: burnableProfile.FuelValue,
                Flammability: burnableProfile.Flammability,
                MissingResourceIds: Array.Empty<string>(),
                AccountedResourceIds: new[] { burnableProfile.SpecId });
        }

        TimberbornBurnDamageResourceStack[] stacks = descriptor.ResourceYields
            .Concat(descriptor.ConstructionResources)
            .Where(static stack => stack.Amount > 0)
            .ToArray();
        TimberbornBurnDamageResourceStack[] knownStacks = stacks
            .Where(stack => _resourceFuelCatalog.Contains(stack.ResourceId))
            .ToArray();
        string[] missingResourceIds = stacks
            .Where(stack => !_resourceFuelCatalog.Contains(stack.ResourceId))
            .Select(static stack => stack.ResourceId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static resourceId => resourceId)
            .ToArray();
        TimberbornResourceFuelProfile[] profiles = knownStacks
            .Select(stack => EffectiveProfile(descriptor, _resourceFuelCatalog.Lookup(stack.ResourceId)))
            .ToArray();
        int capacity = knownStacks
            .Select(stack => stack.Amount * EffectiveProfile(descriptor, _resourceFuelCatalog.Lookup(stack.ResourceId)).FuelValue)
            .Sum();
        int fuelValue = profiles
            .Select(static profile => (int)profile.FuelValue)
            .DefaultIfEmpty(0)
            .Max();
        int flammability = profiles
            .Select(static profile => (int)profile.Flammability)
            .DefaultIfEmpty(0)
            .Max();

        return new TimberbornBurnDamageCapacity(
            Capacity: capacity,
            FuelValue: (byte)Math.Clamp(fuelValue, 0, 15),
            Flammability: (byte)Math.Clamp(flammability, 0, 3),
            MissingResourceIds: missingResourceIds,
            AccountedResourceIds: knownStacks
                .Select(static stack => stack.ResourceId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static resourceId => resourceId)
                .ToArray());
    }

    private static TimberbornResourceFuelProfile EffectiveProfile(
        TimberbornBurnDamageDescriptor descriptor,
        TimberbornResourceFuelProfile profile)
    {
        if (profile.ResourceId == "Log")
        {
            return profile with
            {
                FuelValue = LogBurnDamageFuelValue,
                Flammability = AtLeast(profile.Flammability, 1),
            };
        }

        if (profile.ResourceId is "Plank" or "TreatedPlank")
        {
            return profile with
            {
                FuelValue = AtLeast(profile.FuelValue, ProcessedWoodBurnDamageFuelValue),
                Flammability = AtLeast(profile.Flammability, 1),
            };
        }

        if (descriptor.MaterialKind == TimberbornBurnMaterialKind.Organic &&
            descriptor.TargetKind is TimberbornBurnDamageTargetKind.Crop or TimberbornBurnDamageTargetKind.Resource &&
            profile.FuelValue == 0 &&
            !InertZeroFuelResources.Contains(profile.ResourceId))
        {
            return profile with
            {
                FuelValue = OrganicProduceBurnDamageFuelValue,
                Flammability = AtLeast(profile.Flammability, 1),
            };
        }

        return profile;
    }

    private static byte AtLeast(byte value, byte minimum)
    {
        return value < minimum ? minimum : value;
    }
}

public sealed record TimberbornBurnDamageTargetRegistration
{
    public TimberbornBurnDamageTargetRegistration(
        TimberbornBurnDamageTargetKey targetKey,
        string specId,
        IReadOnlyList<TimberbornCellCoordinates> ownedCells,
        int ownershipPriority = 0,
        TimberbornBurnDamageDescriptor? descriptorOverride = null)
    {
        if (string.IsNullOrWhiteSpace(specId))
        {
            throw new ArgumentException("Burn damage target spec id must be non-empty.", nameof(specId));
        }

        if (ownedCells is null)
        {
            throw new ArgumentNullException(nameof(ownedCells));
        }

        TargetKey = targetKey;
        SpecId = specId.Trim();
        OwnedCells = ownedCells.ToArray();
        OwnershipPriority = ownershipPriority;
        DescriptorOverride = descriptorOverride;
    }

    public TimberbornBurnDamageTargetKey TargetKey { get; }

    public string SpecId { get; }

    public IReadOnlyList<TimberbornCellCoordinates> OwnedCells { get; }

    public int OwnershipPriority { get; }

    public TimberbornBurnDamageDescriptor? DescriptorOverride { get; }

    public static TimberbornBurnDamageTargetRegistration FromFootprint(
        TimberbornBurnDamageTargetKey targetKey,
        string specId,
        TimberbornCellFootprint footprint,
        int ownershipPriority = 0)
    {
        return new TimberbornBurnDamageTargetRegistration(
            targetKey,
            specId,
            footprint.EnumerateCoordinates().ToArray(),
            ownershipPriority);
    }
}

public sealed record TimberbornBurnDamageStateSnapshot(
    TimberbornBurnDamageTargetKey TargetKey,
    string SpecId,
    int DamageCapacity,
    byte FuelValue,
    byte Flammability,
    int DamageTaken,
    uint LastDamagedTick,
    IReadOnlyList<int> OwnedCellIndices,
    IReadOnlyList<string> MissingResourceIds,
    IReadOnlyList<string> AccountedResourceIds);

public sealed record TimberbornBurnDamageTargetState(
    TimberbornBurnDamageTargetKey TargetKey,
    string SpecId,
    TimberbornBurnDamageTargetKind TargetKind,
    TimberbornBurnMaterialKind MaterialKind,
    int DamageCapacity,
    byte FuelValue,
    byte Flammability,
    int DamageTaken,
    uint LastDamagedTick,
    IReadOnlyList<int> OwnedCellIndices,
    IReadOnlyList<string> MissingResourceIds,
    IReadOnlyList<string> AccountedResourceIds)
{
    public int RemainingCapacity => Math.Max(0, DamageCapacity - DamageTaken);

    public bool IsFullyDamaged => DamageCapacity > 0 && DamageTaken >= DamageCapacity;

    public TimberbornBurnDamageStateSnapshot ToSnapshot()
    {
        return new TimberbornBurnDamageStateSnapshot(
            TargetKey,
            SpecId,
            DamageCapacity,
            FuelValue,
            Flammability,
            DamageTaken,
            LastDamagedTick,
            OwnedCellIndices.ToArray(),
            MissingResourceIds.ToArray(),
            AccountedResourceIds.ToArray());
    }
}

public sealed record TimberbornBurnDamageAppliedEvent(
    TimberbornBurnDamageTargetKey TargetKey,
    string SpecId,
    int SourceCellIndex,
    int DamageApplied,
    int DamageTaken,
    int DamageCapacity,
    uint Tick);

public sealed record TimberbornBurnDamageApplySummary(
    uint Tick,
    int ConsideredCellCount,
    int DamageCandidateCellCount,
    int ResolvedTargetCellCount,
    int UnresolvedCellCount,
    int DuplicateCellSuppressedCount,
    int DamageAppliedTargetCount,
    int TotalDamageApplied,
    int PersistenceWriteCount)
{
    public static readonly TimberbornBurnDamageApplySummary Empty = new(
        Tick: 0,
        ConsideredCellCount: 0,
        DamageCandidateCellCount: 0,
        ResolvedTargetCellCount: 0,
        UnresolvedCellCount: 0,
        DuplicateCellSuppressedCount: 0,
        DamageAppliedTargetCount: 0,
        TotalDamageApplied: 0,
        PersistenceWriteCount: 0);

    public string ToLogToken()
    {
        return "wildfire_timberborn_burn_damage_applied " +
            $"tick={Tick} " +
            $"considered_cells={ConsideredCellCount} " +
            $"damage_candidate_cells={DamageCandidateCellCount} " +
            $"resolved_target_cells={ResolvedTargetCellCount} " +
            $"unresolved_cells={UnresolvedCellCount} " +
            $"duplicate_cells_suppressed={DuplicateCellSuppressedCount} " +
            $"damage_applied_targets={DamageAppliedTargetCount} " +
            $"total_damage_applied={TotalDamageApplied} " +
            $"persistence_writes={PersistenceWriteCount}";
    }
}

public sealed record TimberbornBurnDamageRegistrationSummary(
    int TargetCount,
    int OwnedCellCount,
    int DuplicateOwnedCellCount,
    int OverlappingCellCount,
    int UnknownSpecCount,
    int MissingResourceCount,
    int TotalDamageCapacity,
    int MaxDamageCapacity,
    int ZeroCapacityTargetCount)
{
    public static readonly TimberbornBurnDamageRegistrationSummary Empty = new(
        TargetCount: 0,
        OwnedCellCount: 0,
        DuplicateOwnedCellCount: 0,
        OverlappingCellCount: 0,
        UnknownSpecCount: 0,
        MissingResourceCount: 0,
        TotalDamageCapacity: 0,
        MaxDamageCapacity: 0,
        ZeroCapacityTargetCount: 0);

    public string ToLogToken()
    {
        return "wildfire_timberborn_burn_damage_targets_registered " +
            $"targets={TargetCount} " +
            $"owned_cells={OwnedCellCount} " +
            $"duplicate_owned_cells={DuplicateOwnedCellCount} " +
            $"overlapping_cells={OverlappingCellCount} " +
            $"unknown_specs={UnknownSpecCount} " +
            $"missing_resources={MissingResourceCount} " +
            $"burn_capacity_total={TotalDamageCapacity} " +
            $"burn_capacity_max={MaxDamageCapacity} " +
            $"burn_capacity_zero_targets={ZeroCapacityTargetCount}";
    }
}

public interface ITimberbornBurnDamageSink
{
    TimberbornBurnDamageApplySummary ApplyDamage(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornBurnDamageTargetStateProvider
{
    bool TryGetStateForCell(int cellIndex, out TimberbornBurnDamageTargetState state);

    bool TryGetState(TimberbornBurnDamageTargetKey targetKey, out TimberbornBurnDamageTargetState state);

    bool TryGetAppliedEvent(TimberbornBurnDamageTargetKey targetKey, out TimberbornBurnDamageAppliedEvent appliedEvent);
}

public sealed class NullTimberbornBurnDamageSink : ITimberbornBurnDamageSink
{
    public static readonly NullTimberbornBurnDamageSink Instance = new();

    private NullTimberbornBurnDamageSink()
    {
    }

    public TimberbornBurnDamageApplySummary ApplyDamage(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornBurnDamageApplySummary.Empty with { Tick = tick };
    }
}

public sealed class TimberbornBurnDamageService : ITimberbornBurnDamageSink, ITimberbornBurnDamageTargetStateProvider
{
    private readonly TimberbornBurnDamageDescriptorCatalog _descriptorCatalog;
    private readonly TimberbornBurnDamageCapacityCalculator _capacityCalculator;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly Dictionary<string, TimberbornBurnDamageDescriptor> _dynamicDescriptorsBySpecId =
        new(StringComparer.Ordinal);
    private readonly Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> _states = new();
    private IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageAppliedEvent> _lastAppliedEventsByTargetKey =
        new Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageAppliedEvent>();
    private IReadOnlyDictionary<int, TimberbornBurnDamageTargetKey> _targetKeyByCellIndex =
        new Dictionary<int, TimberbornBurnDamageTargetKey>();

    public TimberbornBurnDamageService(
        TimberbornBurnDamageDescriptorCatalog descriptorCatalog,
        TimberbornBurnDamageCapacityCalculator? capacityCalculator = null,
        ITimberbornFireLogSink? logSink = null)
    {
        _descriptorCatalog = descriptorCatalog ?? throw new ArgumentNullException(nameof(descriptorCatalog));
        _capacityCalculator = capacityCalculator ?? new TimberbornBurnDamageCapacityCalculator();
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
        LastRegistrationSummary = TimberbornBurnDamageRegistrationSummary.Empty;
        LastApplySummary = TimberbornBurnDamageApplySummary.Empty;
    }

    public TimberbornBurnDamageRegistrationSummary LastRegistrationSummary { get; private set; }

    public TimberbornBurnDamageApplySummary LastApplySummary { get; private set; }

    public IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> States => _states;

    public IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageAppliedEvent> LastAppliedEventsByTargetKey =>
        _lastAppliedEventsByTargetKey;

    public IReadOnlyDictionary<int, TimberbornBurnDamageTargetKey> TargetKeyByCellIndex => _targetKeyByCellIndex;

    public bool TryGetStateForCell(int cellIndex, out TimberbornBurnDamageTargetState state)
    {
        if (_targetKeyByCellIndex.TryGetValue(cellIndex, out TimberbornBurnDamageTargetKey targetKey) &&
            _states.TryGetValue(targetKey, out state!))
        {
            return true;
        }

        state = default!;
        return false;
    }

    public bool TryGetState(TimberbornBurnDamageTargetKey targetKey, out TimberbornBurnDamageTargetState state)
    {
        return _states.TryGetValue(targetKey, out state!);
    }

    public bool TryGetAppliedEvent(
        TimberbornBurnDamageTargetKey targetKey,
        out TimberbornBurnDamageAppliedEvent appliedEvent)
    {
        return _lastAppliedEventsByTargetKey.TryGetValue(targetKey, out appliedEvent!);
    }

    public TimberbornBurnDamageRegistrationSummary RegisterTargets(
        FireGrid grid,
        IEnumerable<TimberbornBurnDamageTargetRegistration> registrations)
    {
        return RegisterTargets(grid, registrations, Array.Empty<TimberbornBurnDamageDescriptor>());
    }

    public TimberbornBurnDamageRegistrationSummary RegisterTargets(
        FireGrid grid,
        IEnumerable<TimberbornBurnDamageTargetRegistration> registrations,
        IEnumerable<TimberbornBurnDamageDescriptor> dynamicDescriptors)
    {
        if (registrations is null)
        {
            throw new ArgumentNullException(nameof(registrations));
        }

        if (dynamicDescriptors is null)
        {
            throw new ArgumentNullException(nameof(dynamicDescriptors));
        }

        dynamicDescriptors
            .ToList()
            .ForEach(descriptor => _dynamicDescriptorsBySpecId[descriptor.SpecId] = descriptor);
        TimberbornBurnDamageTargetRegistration[] targets = registrations.ToArray();
        RegisteredCell[] registeredCells = targets
            .SelectMany(target => target.OwnedCells
                .Select(cell => new RegisteredCell(
                    grid.ToIndex(cell.X, cell.Y, cell.Z),
                    target.TargetKey,
                    target.OwnershipPriority)))
            .ToArray();
        int duplicateOwnedCellCount = registeredCells
            .GroupBy(static cell => new { cell.TargetKey, cell.CellIndex })
            .Select(static group => Math.Max(0, group.Count() - 1))
            .Sum();
        IGrouping<int, RegisteredCell>[] cellsByIndex = registeredCells
            .GroupBy(static cell => cell.CellIndex)
            .ToArray();
        int overlappingCellCount = cellsByIndex.Count(static group =>
            group.Select(static cell => cell.TargetKey).Distinct().Count() > 1);

        _targetKeyByCellIndex = cellsByIndex.ToDictionary(
            static group => group.Key,
            static group => group
                .OrderByDescending(static cell => cell.OwnershipPriority)
                .ThenBy(static cell => cell.TargetKey.StableId, StringComparer.Ordinal)
                .First()
                .TargetKey);

        _states.Clear();
        TargetStateBuildResult[] stateBuildResults = targets
            .Select(target => BuildState(grid, target))
            .ToArray();
        stateBuildResults
            .ToList()
            .ForEach(result => _states[result.State.TargetKey] = result.State);

        LastRegistrationSummary = new TimberbornBurnDamageRegistrationSummary(
            TargetCount: targets.Length,
            OwnedCellCount: _targetKeyByCellIndex.Count,
            DuplicateOwnedCellCount: duplicateOwnedCellCount,
            OverlappingCellCount: overlappingCellCount,
            UnknownSpecCount: stateBuildResults.Count(static result => result.IsUnknownSpec),
            MissingResourceCount: stateBuildResults
                .SelectMany(static result => result.State.MissingResourceIds)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            TotalDamageCapacity: stateBuildResults.Sum(static result => result.State.DamageCapacity),
            MaxDamageCapacity: stateBuildResults
                .Select(static result => result.State.DamageCapacity)
                .DefaultIfEmpty(0)
                .Max(),
            ZeroCapacityTargetCount: stateBuildResults.Count(static result => result.State.DamageCapacity == 0));
        _logSink.Info(LastRegistrationSummary.ToLogToken());

        return LastRegistrationSummary;
    }

    public TimberbornBurnDamageApplySummary ApplyDamage(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        if (decisions is null)
        {
            throw new ArgumentNullException(nameof(decisions));
        }

        TimberbornBurnDamageCellHit[] candidateHits = decisions
            .Select(CreateHit)
            .Where(static hit => hit.DamageUnits > 0)
            .ToArray();
        TimberbornBurnDamageCellHit[] resolvedHits = candidateHits
            .Where(hit => _targetKeyByCellIndex.ContainsKey(hit.CellIndex))
            .Select(hit => hit with { TargetKey = _targetKeyByCellIndex[hit.CellIndex] })
            .ToArray();
        TimberbornBurnDamageAppliedEvent[] appliedEvents = resolvedHits
            .GroupBy(static hit => hit.TargetKey)
            .Select(static group => group
                .OrderByDescending(static hit => hit.DamageUnits)
                .ThenByDescending(static hit => hit.Heat)
                .ThenBy(static hit => hit.CellIndex)
                .First())
            .Select(hit => ApplyResolvedHit(tick, hit))
            .Where(static appliedEvent => appliedEvent.DamageApplied > 0)
            .ToArray();
        _lastAppliedEventsByTargetKey = appliedEvents.ToDictionary(static appliedEvent => appliedEvent.TargetKey);

        LastApplySummary = new TimberbornBurnDamageApplySummary(
            Tick: tick,
            ConsideredCellCount: decisions.Count,
            DamageCandidateCellCount: candidateHits.Length,
            ResolvedTargetCellCount: resolvedHits.Length,
            UnresolvedCellCount: candidateHits.Length - resolvedHits.Length,
            DuplicateCellSuppressedCount: resolvedHits.Length - resolvedHits
                .Select(static hit => hit.TargetKey)
                .Distinct()
                .Count(),
            DamageAppliedTargetCount: appliedEvents.Length,
            TotalDamageApplied: appliedEvents.Sum(static appliedEvent => appliedEvent.DamageApplied),
            PersistenceWriteCount: appliedEvents.Length);
        _logSink.Info(LastApplySummary.ToLogToken());

        return LastApplySummary;
    }

    public IReadOnlyList<TimberbornBurnDamageStateSnapshot> CaptureState()
    {
        return _states.Values
            .OrderBy(static state => state.TargetKey.StableId)
            .Select(static state => state.ToSnapshot())
            .ToArray();
    }

    public void RestoreState(IEnumerable<TimberbornBurnDamageStateSnapshot> snapshots)
    {
        if (snapshots is null)
        {
            throw new ArgumentNullException(nameof(snapshots));
        }

        snapshots
            .ToList()
            .ForEach(snapshot =>
            {
                if (_states.TryGetValue(snapshot.TargetKey, out TimberbornBurnDamageTargetState state))
                {
                    _states[snapshot.TargetKey] = state with
                    {
                        DamageTaken = Math.Clamp(snapshot.DamageTaken, 0, state.DamageCapacity),
                        LastDamagedTick = snapshot.LastDamagedTick,
                    };
                }
            });
    }

    private TargetStateBuildResult BuildState(FireGrid grid, TimberbornBurnDamageTargetRegistration target)
    {
        bool isKnownSpec;
        TimberbornBurnDamageDescriptor descriptor;
        if (target.DescriptorOverride is not null)
        {
            descriptor = target.DescriptorOverride;
            isKnownSpec = true;
        }
        else
        {
            isKnownSpec = _dynamicDescriptorsBySpecId.TryGetValue(target.SpecId, out descriptor) ||
                _descriptorCatalog.TryLookup(target.SpecId, out descriptor);
        }

        TimberbornBurnDamageCapacity capacity = _capacityCalculator.Calculate(descriptor);
        int[] ownedCellIndices = target.OwnedCells
            .Select(cell => grid.ToIndex(cell.X, cell.Y, cell.Z))
            .Distinct()
            .OrderBy(static cellIndex => cellIndex)
            .ToArray();

        return new TargetStateBuildResult(
            new TimberbornBurnDamageTargetState(
                target.TargetKey,
                descriptor.SpecId,
                descriptor.TargetKind,
                descriptor.MaterialKind,
                capacity.Capacity,
                capacity.FuelValue,
                capacity.Flammability,
                DamageTaken: 0,
                LastDamagedTick: 0,
                OwnedCellIndices: ownedCellIndices,
                MissingResourceIds: capacity.MissingResourceIds.ToArray(),
                AccountedResourceIds: capacity.AccountedResourceIds.ToArray()),
            IsUnknownSpec: !isKnownSpec);
    }

    private TimberbornBurnDamageAppliedEvent ApplyResolvedHit(uint tick, TimberbornBurnDamageCellHit hit)
    {
        if (!_states.TryGetValue(hit.TargetKey, out TimberbornBurnDamageTargetState state))
        {
            return new TimberbornBurnDamageAppliedEvent(hit.TargetKey, "", hit.CellIndex, 0, 0, 0, tick);
        }

        int damageApplied = Math.Min(hit.DamageUnits, state.RemainingCapacity);
        if (damageApplied <= 0)
        {
            return new TimberbornBurnDamageAppliedEvent(
                hit.TargetKey,
                state.SpecId,
                hit.CellIndex,
                0,
                state.DamageTaken,
                state.DamageCapacity,
                tick);
        }

        TimberbornBurnDamageTargetState updatedState = state with
        {
            DamageTaken = state.DamageTaken + damageApplied,
            LastDamagedTick = tick,
        };
        _states[hit.TargetKey] = updatedState;

        return new TimberbornBurnDamageAppliedEvent(
            hit.TargetKey,
            state.SpecId,
            hit.CellIndex,
            damageApplied,
            updatedState.DamageTaken,
            updatedState.DamageCapacity,
            tick);
    }

    private static TimberbornBurnDamageCellHit CreateHit(TimberbornFireCellDeltaDecision decision)
    {
        int damageUnits = Math.Max(0, decision.OldFuel - decision.NewFuel);

        return new TimberbornBurnDamageCellHit(
            decision.CellIndex,
            default,
            DamageUnits: damageUnits,
            Heat: decision.NewHeat);
    }

    private readonly record struct RegisteredCell(
        int CellIndex,
        TimberbornBurnDamageTargetKey TargetKey,
        int OwnershipPriority);

    private readonly record struct TimberbornBurnDamageCellHit(
        int CellIndex,
        TimberbornBurnDamageTargetKey TargetKey,
        int DamageUnits,
        int Heat);

    private sealed record TargetStateBuildResult(
        TimberbornBurnDamageTargetState State,
        bool IsUnknownSpec);
}
