using Wildfire.Core;
using Wildfire.Timberborn.Mapping;

namespace Wildfire.Timberborn.Persistence;

public enum OwnedBodyRetention { RetainedBody, RetiredNativeOwner }
public enum OwnedCharredPresentation { None, BurnedBody, BurnedLeftover }
public enum OwnedConsequenceHistoryCapability { Unavailable, Complete }

/// <summary>Immutable accounting definition, never current inventory, yield, or remaining GPU fuel.</summary>
public sealed class OwnedBodyAccountingProfile
{
    public string SpecId { get; }
    public TimberbornBurnDamageTargetKind TargetKind { get; }
    public TimberbornBurnMaterialKind MaterialKind { get; }
    public int Capacity { get; }
    public byte FuelValue { get; }
    public byte Flammability { get; }
    public IReadOnlyList<string> MissingResources { get; }
    public IReadOnlyList<string> AccountedResources { get; }
    public TimberbornBurnableProfile? BurnableProfile { get; }
    public IReadOnlyList<TimberbornBurnDamageResourceStack> ResourceYields { get; }
    public IReadOnlyList<TimberbornBurnDamageResourceStack> ConstructionResources { get; }

    public OwnedBodyAccountingProfile(string specId, TimberbornBurnDamageTargetKind kind, TimberbornBurnMaterialKind material,
        int capacity, byte fuelValue, byte flammability, IEnumerable<string> missing, IEnumerable<string> accounted,
        TimberbornBurnableProfile? burnable, IEnumerable<TimberbornBurnDamageResourceStack> yields,
        IEnumerable<TimberbornBurnDamageResourceStack> construction)
    {
        if (string.IsNullOrWhiteSpace(specId) || capacity < 0 || !Enum.IsDefined(typeof(TimberbornBurnDamageTargetKind), kind) ||
            !Enum.IsDefined(typeof(TimberbornBurnMaterialKind), material)) throw new ArgumentException("Invalid owned accounting definition.");
        SpecId = specId; TargetKind = kind; MaterialKind = material; Capacity = capacity; FuelValue = fuelValue; Flammability = flammability;
        MissingResources = Strings(missing); AccountedResources = Strings(accounted); BurnableProfile = burnable;
        ResourceYields = Stacks(yields); ConstructionResources = Stacks(construction);
    }
    private static IReadOnlyList<string> Strings(IEnumerable<string> values)
    {
        var items = values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (items.Any(string.IsNullOrWhiteSpace) || items.Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new ArgumentException("Invalid accounting resource ids.");
        return Array.AsReadOnly(items);
    }
    private static IReadOnlyList<TimberbornBurnDamageResourceStack> Stacks(IEnumerable<TimberbornBurnDamageResourceStack> values)
    {
        var items = values.OrderBy(value => value.ResourceId, StringComparer.Ordinal).ThenBy(value => value.Amount).ToArray();
        if (items.Any(value => string.IsNullOrWhiteSpace(value.ResourceId) || value.Amount < 0)) throw new ArgumentException("Invalid accounting stack.");
        return Array.AsReadOnly(items);
    }
    internal bool Matches(OwnedBodyAccountingProfile other) => SpecId == other.SpecId && TargetKind == other.TargetKind &&
        MaterialKind == other.MaterialKind && Capacity == other.Capacity && FuelValue == other.FuelValue && Flammability == other.Flammability &&
        BurnableProfile == other.BurnableProfile && MissingResources.SequenceEqual(other.MissingResources) &&
        AccountedResources.SequenceEqual(other.AccountedResources) && ResourceYields.SequenceEqual(other.ResourceYields) &&
        ConstructionResources.SequenceEqual(other.ConstructionResources);
}

public sealed class OwnedConsequenceOwner
{
    public Guid EntityId { get; }
    public NativeBurnTargetFamily Family { get; }
    public OwnedBodyRetention Retention { get; }
    public OwnedBodyAccountingProfile? Profile { get; }
    public TimberbornBurnDamageTargetKey TargetKey => new(TimberbornBurnDamageIdentity.ForEntity(EntityId, Family));
    public OwnedConsequenceOwner(Guid entityId, NativeBurnTargetFamily family, OwnedBodyRetention retention, OwnedBodyAccountingProfile? profile)
    {
        if (entityId == Guid.Empty || family is not (NativeBurnTargetFamily.Tree or NativeBurnTargetFamily.Crop or
            NativeBurnTargetFamily.Stockpile or NativeBurnTargetFamily.Structure) ||
            !Enum.IsDefined(typeof(OwnedBodyRetention), retention) || (retention == OwnedBodyRetention.RetainedBody) != (profile is not null))
            throw new ArgumentException("Owned retention requires exactly its supported family and retained body profile.");
        EntityId = entityId; Family = family; Retention = retention; Profile = profile;
    }
}

public sealed class OwnedNaturalProgress
{
    public Guid EntityId { get; }
    public int AppliedYieldLoss { get; }
    public bool DryRequestSatisfied { get; }
    public bool DeathRequestSatisfied { get; }
    public bool LeftoverRequestSatisfied { get; }
    public OwnedCharredPresentation DesiredPresentation { get; }
    public OwnedNaturalProgress(Guid id, int appliedYieldLoss, bool dry, bool death, bool leftover, OwnedCharredPresentation presentation)
    {
        if (id == Guid.Empty || appliedYieldLoss < 0 || !Enum.IsDefined(typeof(OwnedCharredPresentation), presentation))
            throw new ArgumentException("Invalid natural consequence progress.");
        EntityId = id; AppliedYieldLoss = appliedYieldLoss; DryRequestSatisfied = dry; DeathRequestSatisfied = death;
        LeftoverRequestSatisfied = leftover; DesiredPresentation = presentation;
    }
}

public sealed class OwnedStorageCredit
{
    public Guid EntityId { get; }
    public string ResourceId { get; }
    public int FractionalBudget { get; }
    public byte FuelValue { get; }
    public OwnedStorageCredit(Guid entityId, string resourceId, int fractionalBudget, byte fuelValue)
    {
        if (entityId == Guid.Empty || string.IsNullOrWhiteSpace(resourceId) || fractionalBudget <= 0 || fractionalBudget >= fuelValue)
            throw new ArgumentException("Invalid fractional burn credit.");
        EntityId = entityId; ResourceId = resourceId; FractionalBudget = fractionalBudget; FuelValue = fuelValue;
    }
}

public sealed class TimberbornOwnedConsequenceSnapshot
{
    public IReadOnlyList<OwnedConsequenceOwner> Owners { get; }
    public IReadOnlyList<OwnedNaturalProgress> Natural { get; }
    public IReadOnlyList<OwnedStorageCredit> StorageCredits { get; }
    public OwnedNativeDefinitionSet? NativeDefinitions { get; }
    public OwnedNativeCompatibilityCapability NativeCompatibility => NativeDefinitions is null ?
        OwnedNativeCompatibilityCapability.Unavailable : OwnedNativeCompatibilityCapability.Complete;
    public TimberbornOwnedConsequenceSnapshot(IEnumerable<OwnedConsequenceOwner> owners,
        IEnumerable<OwnedNaturalProgress> natural, IEnumerable<OwnedStorageCredit> credits, OwnedNativeDefinitionSet? nativeDefinitions = null)
    {
        var all = owners.OrderBy(owner => owner.EntityId).ToArray();
        if (all.Select(owner => owner.EntityId).Distinct().Count() != all.Length) throw new ArgumentException("Duplicate owned body identity.");
        var map = all.ToDictionary(owner => owner.EntityId);
        var progress = natural.OrderBy(item => item.EntityId).ToArray();
        if (progress.Length != all.Count(owner => owner.Family is NativeBurnTargetFamily.Tree or NativeBurnTargetFamily.Crop) ||
            progress.Select(item => item.EntityId).Distinct().Count() != progress.Length || progress.Any(item =>
            !map.TryGetValue(item.EntityId, out var owner) || owner.Family is not (NativeBurnTargetFamily.Tree or NativeBurnTargetFamily.Crop)))
            throw new ArgumentException("Natural history needs a unique canonical natural owner.");
        var fractions = credits.OrderBy(item => item.EntityId).ThenBy(item => item.ResourceId, StringComparer.Ordinal).ToArray();
        if (fractions.Select(item => (item.EntityId, item.ResourceId)).Distinct().Count() != fractions.Length || fractions.Any(item =>
            !map.TryGetValue(item.EntityId, out var owner) || owner.Family is not (NativeBurnTargetFamily.Stockpile or NativeBurnTargetFamily.Structure)))
            throw new ArgumentException("Storage history needs a unique canonical inventory owner.");
        Owners = Array.AsReadOnly(all); Natural = Array.AsReadOnly(progress); StorageCredits = Array.AsReadOnly(fractions);
        nativeDefinitions?.Validate(Owners); NativeDefinitions=nativeDefinitions;
    }
    internal void ValidateAssociation(FireSimSnapshot simulation, TimberbornMaterialBindingSnapshot bindings,
        TimberbornConsequencePersistenceSnapshot damage)
    {
        var entities = bindings.Entities.ToDictionary(item => item.EntityId);
        var owners = Owners.ToDictionary(item => item.EntityId);
        if(NativeDefinitions is {} definitions)
            foreach(var witness in definitions.Definitions)
                if(!entities.TryGetValue(witness.EntityId,out var binding) ||
                    !binding.Slots.Select(slot=>slot.LocalCoordinates).ToHashSet().SetEquals(witness.LocalFootprint))
                    throw new ArgumentException("Native definition footprint differs from its durable local slot bindings.");
        if (Owners.Any(owner => !entities.ContainsKey(owner.EntityId))) throw new ArgumentException("History has no retained native binding.");
        var knownTargets = simulation.MaterialAuthority.KnownSlots.Select(item => item.TargetId).ToHashSet();
        if (bindings.Entities.Any(entity => knownTargets.Contains(entity.TargetId) && !owners.ContainsKey(entity.EntityId)))
            throw new ArgumentException("Known material origin has no canonical consequence history.");
        var retained = Owners.Where(owner => owner.Retention == OwnedBodyRetention.RetainedBody).ToDictionary(owner => owner.TargetKey.StableId);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in damage.BurnDamageStates)
            if (!seen.Add(entry.TargetKey) || !retained.TryGetValue(entry.TargetKey, out var owner) || entry.DamageTaken < 0 ||
                entry.DamageTaken > owner.Profile!.Capacity || entry.LastDamagedTick > simulation.Tick)
                throw new ArgumentException("Complete body history has duplicate, extra, out-of-range, or future damage.");
        if (seen.Count != retained.Count) throw new ArgumentException("Complete body history is missing a retained owner's damage.");
    }
}
