using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornBurnDamageService : ITimberbornBurnDamageSink, ITimberbornBurnDamageTargetStateProvider
{
    private readonly TimberbornBurnDamageDescriptorCatalog _descriptorCatalog;
    private readonly TimberbornBurnDamageCapacityCalculator _capacityCalculator;
    private readonly ITimberbornFireLogSink _logSink;
    private Dictionary<string, TimberbornBurnDamageDescriptor> _dynamicDescriptorsBySpecId =
        new(StringComparer.Ordinal);
    private Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> _states = new();
    private Dictionary<TimberbornBurnDamageTargetKey, RegisteredTarget> _registrations = new();
    private FireGrid? _grid;
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

        Dictionary<string, TimberbornBurnDamageDescriptor> descriptors = new(_dynamicDescriptorsBySpecId, StringComparer.Ordinal);
        foreach (TimberbornBurnDamageDescriptor descriptor in dynamicDescriptors)
        {
            descriptors[descriptor.SpecId] = descriptor;
        }

        Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> states = new();
        Dictionary<TimberbornBurnDamageTargetKey, RegisteredTarget> targets = new();
        foreach (TimberbornBurnDamageTargetRegistration target in registrations)
        {
            TargetStateBuildResult result = BuildState(grid, target, descriptors);
            // Duplicate identities are invalid even when their cells do not overlap.
            states.Add(target.TargetKey, result.State);
            targets.Add(target.TargetKey, CreateRegistration(target, result));
        }

        PublishRegistration(grid, targets, states, Array.Empty<TimberbornBurnDamageTargetKey>(), reset: true);
        _dynamicDescriptorsBySpecId = descriptors;
        return LastRegistrationSummary;
    }

    /// <summary>
    /// Adds a new identity or updates an existing identity's footprint/priority and resumes eligibility.
    /// Existing material/accounting profiles must match exactly: changing capacity or spec requires
    /// an explicit accounting policy, not an ownership refresh that might heal or refill a target.
    /// </summary>
    public void UpsertTarget(FireGrid grid, TimberbornBurnDamageTargetRegistration target)
    {
        if (_grid.HasValue && _grid.Value != grid)
        {
            throw new ArgumentException("Incremental registration must use the initialized fire grid.", nameof(grid));
        }

        TargetStateBuildResult result = BuildState(grid, target, _dynamicDescriptorsBySpecId);
        TimberbornBurnDamageTargetState updated = result.State;
        if (_states.TryGetValue(target.TargetKey, out TimberbornBurnDamageTargetState previous))
        {
            RequireUnchangedProfile(previous, updated, _registrations[target.TargetKey].Descriptor, result.Descriptor);
            updated = updated with { DamageTaken = previous.DamageTaken, LastDamagedTick = previous.LastDamagedTick };
        }

        Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> states = new(_states)
        {
            [target.TargetKey] = updated,
        };
        Dictionary<TimberbornBurnDamageTargetKey, RegisteredTarget> targets = new(_registrations)
        {
            [target.TargetKey] = CreateRegistration(target, result),
        };
        PublishRegistration(grid, targets, states, new[] { target.TargetKey });
    }

    /// <summary>Removes cell eligibility while retaining damage and registration for a later upsert.</summary>
    public bool SuspendTarget(TimberbornBurnDamageTargetKey targetKey)
    {
        if (!_registrations.TryGetValue(targetKey, out RegisteredTarget target) || !target.IsActive)
        {
            return false;
        }

        Dictionary<TimberbornBurnDamageTargetKey, RegisteredTarget> targets = new(_registrations)
        {
            [targetKey] = target with { IsActive = false },
        };
        PublishRegistration(_grid!.Value, targets, _states, new[] { targetKey });
        return true;
    }

    /// <summary>Retires a deleted identity and its damage; underlying overlapping owners become eligible.</summary>
    public bool RemoveTarget(TimberbornBurnDamageTargetKey targetKey)
    {
        if (!_registrations.ContainsKey(targetKey))
        {
            return false;
        }

        Dictionary<TimberbornBurnDamageTargetKey, RegisteredTarget> targets = new(_registrations);
        Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> states = new(_states);
        targets.Remove(targetKey);
        states.Remove(targetKey);
        PublishRegistration(_grid!.Value, targets, states, new[] { targetKey });
        return true;
    }

    private static RegisteredTarget CreateRegistration(
        TimberbornBurnDamageTargetRegistration target,
        TargetStateBuildResult result)
    {
        return new RegisteredTarget(
            target.OwnershipPriority,
            IsActive: true,
            result.IsUnknownSpec,
            DuplicateCellCount: target.OwnedCells.Count - result.State.OwnedCellIndices.Count,
            result.Descriptor);
    }

    private void PublishRegistration(
        FireGrid grid,
        Dictionary<TimberbornBurnDamageTargetKey, RegisteredTarget> targets,
        Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> states,
        IReadOnlyCollection<TimberbornBurnDamageTargetKey> invalidatedEventKeys,
        bool reset = false)
    {
        // Project all derived state before publication. Retaining every contributor makes removal
        // and suspension reveal the same deterministic owner as initial registration.
        IGrouping<int, RegisteredCell>[] cellsByIndex = targets
            .Where(static pair => pair.Value.IsActive)
            .SelectMany(pair => states[pair.Key].OwnedCellIndices.Select(cell =>
                new RegisteredCell(cell, pair.Key, pair.Value.OwnershipPriority)))
            .GroupBy(static cell => cell.CellIndex)
            .ToArray();
        Dictionary<int, TimberbornBurnDamageTargetKey> owners = cellsByIndex.ToDictionary(
            static group => group.Key,
            static group => group.OrderByDescending(static cell => cell.OwnershipPriority)
                .ThenBy(static cell => cell.TargetKey.StableId, StringComparer.Ordinal).First().TargetKey);
        TimberbornBurnDamageRegistrationSummary summary = new(
            TargetCount: targets.Count,
            OwnedCellCount: owners.Count,
            DuplicateOwnedCellCount: targets.Values.Sum(static target => target.DuplicateCellCount),
            OverlappingCellCount: cellsByIndex.Count(static group => group.Count() > 1),
            UnknownSpecCount: targets.Values.Count(static target => target.IsUnknownSpec),
            MissingResourceCount: states.Values.SelectMany(static state => state.MissingResourceIds).Distinct(StringComparer.Ordinal).Count(),
            TotalDamageCapacity: states.Values.Sum(static state => state.DamageCapacity),
            MaxDamageCapacity: states.Values.Select(static state => state.DamageCapacity).DefaultIfEmpty(0).Max(),
            ZeroCapacityTargetCount: states.Values.Count(static state => state.DamageCapacity == 0));
        Dictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageAppliedEvent> events = reset
            ? new()
            : _lastAppliedEventsByTargetKey.Where(pair => !invalidatedEventKeys.Contains(pair.Key))
                .ToDictionary(static pair => pair.Key, static pair => pair.Value);

        _grid = grid;
        _registrations = targets;
        _states = states;
        _targetKeyByCellIndex = owners;
        _lastAppliedEventsByTargetKey = events;
        LastRegistrationSummary = summary;
        // LastApplySummary remains historical telemetry for the last ApplyDamage call. It is not
        // recomputed from the event cache, whose stale target entries are invalidated above.
        if (reset)
        {
            LastApplySummary = TimberbornBurnDamageApplySummary.Empty;
        }
        _logSink.Info(summary.ToLogToken());
    }

    private static void RequireUnchangedProfile(
        TimberbornBurnDamageTargetState previous,
        TimberbornBurnDamageTargetState updated,
        TimberbornBurnDamageDescriptor previousDescriptor,
        TimberbornBurnDamageDescriptor updatedDescriptor)
    {
        if (previous.SpecId != updated.SpecId || previous.TargetKind != updated.TargetKind ||
            previous.MaterialKind != updated.MaterialKind || previous.DamageCapacity != updated.DamageCapacity ||
            previous.FuelValue != updated.FuelValue || previous.Flammability != updated.Flammability ||
            !previous.MissingResourceIds.SequenceEqual(updated.MissingResourceIds) ||
            !previous.AccountedResourceIds.SequenceEqual(updated.AccountedResourceIds) ||
            previousDescriptor.BurnableProfile != updatedDescriptor.BurnableProfile ||
            !OrderedStacks(previousDescriptor.ResourceYields).SequenceEqual(OrderedStacks(updatedDescriptor.ResourceYields)) ||
            !OrderedStacks(previousDescriptor.ConstructionResources).SequenceEqual(OrderedStacks(updatedDescriptor.ConstructionResources)))
        {
            throw new InvalidOperationException(
                $"Target {previous.TargetKey} material/accounting profile changed. Incremental ownership refresh cannot change its spec or capacity.");
        }
    }

    private static IEnumerable<TimberbornBurnDamageResourceStack> OrderedStacks(
        IEnumerable<TimberbornBurnDamageResourceStack> stacks)
    {
        return stacks.OrderBy(static stack => stack.ResourceId, StringComparer.Ordinal).ThenBy(static stack => stack.Amount);
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
        return ApplyHits(tick, decisions.Count, candidateHits.Length, resolvedHits);
    }

    /// <summary>Applies preflighted original-owner decisions. Never resolves a current cell occupant.</summary>
    internal TimberbornBurnDamageApplySummary ApplyOwnedDamage(
        uint tick, IReadOnlyList<TimberbornOwnedBurnDecision> decisions)
    {
        var hits = decisions.Select(item => CreateHit(item.Decision) with { TargetKey = item.TargetKey })
            .Where(hit => hit.DamageUnits > 0).ToArray();
        if (hits.Any(hit => !_states.ContainsKey(hit.TargetKey)))
            throw new InvalidOperationException("An owned burn registration disappeared after batch preflight.");
        return ApplyHits(tick, decisions.Count, hits.Length, hits);
    }

    private TimberbornBurnDamageApplySummary ApplyHits(uint tick, int consideredCount, int candidateCount,
        TimberbornBurnDamageCellHit[] resolvedHits)
    {
        // A coherent GPU step has one local material slot per cell. Repeated reports of
        // that cell are duplicates; different cells of the same body are independent burns.
        var distinctCells = resolvedHits.GroupBy(static hit => (hit.TargetKey, hit.CellIndex))
            .Select(static group => group
                .OrderByDescending(static hit => hit.DamageUnits)
                .ThenByDescending(static hit => hit.Heat)
                .First())
            .ToArray();
        TimberbornBurnDamageAppliedEvent[] appliedEvents = distinctCells
            .GroupBy(static hit => hit.TargetKey)
            .Select(static group =>
            {
                var representative = group.OrderByDescending(hit => hit.DamageUnits)
                    .ThenByDescending(hit => hit.Heat).ThenBy(hit => hit.CellIndex).First();
                // Only the body's capacity can be spent; saturate before converting the sum.
                return representative with { DamageUnits = (int)Math.Min(int.MaxValue, group.Sum(hit => (long)hit.DamageUnits)) };
            })
            .Select(hit => ApplyResolvedHit(tick, hit))
            .Where(static appliedEvent => appliedEvent.DamageApplied > 0)
            .ToArray();
        _lastAppliedEventsByTargetKey = appliedEvents.ToDictionary(static appliedEvent => appliedEvent.TargetKey);

        LastApplySummary = new TimberbornBurnDamageApplySummary(
            Tick: tick,
            ConsideredCellCount: consideredCount,
            DamageCandidateCellCount: candidateCount,
            ResolvedTargetCellCount: resolvedHits.Length,
            UnresolvedCellCount: candidateCount - resolvedHits.Length,
            DuplicateCellSuppressedCount: resolvedHits.Length - distinctCells.Length,
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

    private TargetStateBuildResult BuildState(
        FireGrid grid,
        TimberbornBurnDamageTargetRegistration target,
        IReadOnlyDictionary<string, TimberbornBurnDamageDescriptor> dynamicDescriptors)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        if (string.IsNullOrWhiteSpace(target.TargetKey.StableId))
        {
            throw new ArgumentException("Registration requires a non-empty target identity.", nameof(target));
        }

        bool isKnownSpec;
        TimberbornBurnDamageDescriptor descriptor;
        if (target.DescriptorOverride is not null)
        {
            descriptor = target.DescriptorOverride;
            isKnownSpec = true;
        }
        else
        {
            isKnownSpec = dynamicDescriptors.TryGetValue(target.SpecId, out descriptor) ||
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
            IsUnknownSpec: !isKnownSpec,
            descriptor);
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

    private sealed record RegisteredTarget(
        int OwnershipPriority,
        bool IsActive,
        bool IsUnknownSpec,
        int DuplicateCellCount,
        TimberbornBurnDamageDescriptor Descriptor);

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
        bool IsUnknownSpec,
        TimberbornBurnDamageDescriptor Descriptor);
}
