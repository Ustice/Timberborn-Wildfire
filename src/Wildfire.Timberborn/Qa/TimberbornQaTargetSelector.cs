using Wildfire.Core;
using static Wildfire.Timberborn.Qa.TimberbornQaStimulusValues;

namespace Wildfire.Timberborn.Qa;

/// <summary>Resolves imported and live consequence targets without registering simulator inputs.</summary>
internal sealed class TimberbornQaTargetSelector
{
    private readonly Func<IReadOnlyList<TimberbornImportedFieldTarget>> _readTargets;
    private readonly ITimberbornFireLogSink _logSink;
    private IReadOnlyList<TimberbornImportedFieldTarget> _importedTargets => _readTargets();

    internal TimberbornQaTargetSelector(Func<IReadOnlyList<TimberbornImportedFieldTarget>> readTargets, ITimberbornFireLogSink logSink)
    {
        _readTargets = readTargets;
        _logSink = logSink;
    }

    internal static TimberbornQaDeltaStimulusTargetSelection? FindCropDeltaStimulusTarget(
        FireGrid grid,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>? burnDamageTargets,
        TimberbornQaSelectedCropTarget? selectedCropTarget)
    {
        TimberbornBurnDamageTargetState[] states = burnDamageTargets?.Values.ToArray() ??
            Array.Empty<TimberbornBurnDamageTargetState>();
        TimberbornBurnDamageTargetState[] cropTargets = states
            .Where(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable)
            .ToArray();
        int cropOwnedCellCount = cropTargets
            .SelectMany(static state => state.OwnedCellIndices)
            .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
            .Distinct()
            .Count();

        if (selectedCropTarget is { } selectedTarget)
        {
            return new TimberbornQaDeltaStimulusTargetSelection(
                new TimberbornCellCoordinates(selectedTarget.X, selectedTarget.Y, selectedTarget.Z),
                selectedTarget.TargetSource,
                states.Length,
                cropTargets.Length,
                cropOwnedCellCount);
        }

        TimberbornCellCoordinates center = new(grid.Width / 2, grid.Height / 2, grid.Depth / 2);
        TimberbornCellCoordinates? cropTarget = cropTargets
            .SelectMany(static state => state.OwnedCellIndices)
            .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
            .Distinct()
            .Select(cellIndex =>
            {
                (int x, int y, int z) = grid.FromIndex(cellIndex);
                return new TimberbornCellCoordinates(x, y, z);
            })
            .OrderBy(coordinates => Distance(coordinates, center))
            .ThenBy(static coordinates => coordinates.Z)
            .ThenBy(static coordinates => coordinates.Y)
            .ThenBy(static coordinates => coordinates.X)
            .Select(static coordinates => (TimberbornCellCoordinates?)coordinates)
            .FirstOrDefault();

        return cropTarget.HasValue
            ? new TimberbornQaDeltaStimulusTargetSelection(
                cropTarget.Value,
                "registered_crop_target",
                states.Length,
                cropTargets.Length,
                cropOwnedCellCount)
            : null;
    }

    internal static int Distance(TimberbornCellCoordinates coordinates, TimberbornCellCoordinates target)
    {
        return Math.Abs(coordinates.X - target.X) +
            Math.Abs(coordinates.Y - target.Y) +
            Math.Abs(coordinates.Z - target.Z);
    }

    internal TimberbornImportedFieldTarget FindImportedTarget(
        Func<TimberbornImportedFieldTarget, bool> predicate,
        string notFoundMessage)
    {
        return FindImportedTarget(
            predicate,
            static targets => targets.OrderBy(static target => target.CellIndex),
            notFoundMessage);
    }

    internal TimberbornImportedFieldTarget FindImportedTarget(
        Func<TimberbornImportedFieldTarget, bool> predicate,
        Func<IEnumerable<TimberbornImportedFieldTarget>, IOrderedEnumerable<TimberbornImportedFieldTarget>> order,
        string notFoundMessage)
    {
        IEnumerable<TimberbornImportedFieldTarget> candidates = _importedTargets.Where(predicate);
        return order(candidates)
            .Select(static target => (TimberbornImportedFieldTarget?)target)
            .FirstOrDefault() ?? throw new InvalidOperationException(notFoundMessage);
    }

    internal TimberbornQaDirectConsequenceTarget FindDirectConsequenceTarget(
        FireGrid grid,
        string selector,
        ITimberbornExplosiveInfrastructureTargetApi? explosiveInfrastructureTargetApi,
        ITimberbornDetonatorFireSafetyTargetApi? detonatorFireSafetyTargetApi,
        ITimberbornTunnelFireTargetApi? tunnelFireTargetApi)
    {
        if (selector == TimberbornQaFieldTargetSelectors.Dynamite && explosiveInfrastructureTargetApi is null)
        {
            throw new InvalidOperationException(
                "QA dynamite stimulus requires the explosive infrastructure target API.");
        }

        if (selector == TimberbornQaFieldTargetSelectors.Detonator && detonatorFireSafetyTargetApi is null)
        {
            throw new InvalidOperationException(
                "QA detonator stimulus requires the detonator fire-safety target API.");
        }

        if (selector == TimberbornQaFieldTargetSelectors.Tunnel && tunnelFireTargetApi is null)
        {
            throw new InvalidOperationException(
                "QA tunnel stimulus requires the tunnel fire target API.");
        }

        Func<int, TimberbornQaDirectConsequenceTarget?> resolveTarget = selector switch
        {
            TimberbornQaFieldTargetSelectors.Dynamite => cellIndex =>
                ResolveDynamiteQaTarget(grid, cellIndex, explosiveInfrastructureTargetApi),
            TimberbornQaFieldTargetSelectors.Detonator => cellIndex =>
                ResolveDetonatorQaTarget(grid, cellIndex, detonatorFireSafetyTargetApi),
            TimberbornQaFieldTargetSelectors.Tunnel => cellIndex =>
                ResolveTunnelQaTarget(grid, cellIndex, tunnelFireTargetApi),
            _ => throw new ArgumentException($"Unsupported direct consequence QA selector '{selector}'.", nameof(selector)),
        };

        int[] candidateCellIndices = _importedTargets
            .Where(static target => target.MaterialClass == WildfireMaterialClass.Infrastructure)
            .Select(static target => target.CellIndex)
            .Concat(Enumerable.Range(0, grid.CellCount))
            .Distinct()
            .OrderBy(static cellIndex => cellIndex)
            .ToArray();
        TimberbornQaDirectConsequenceTarget? target = candidateCellIndices
            .Select<int, TimberbornQaDirectConsequenceTarget?>((cellIndex, offset) =>
            {
                TimberbornQaDirectConsequenceTarget? resolvedTarget;
                try
                {
                    resolvedTarget = resolveTarget(cellIndex);
                }
                catch (Exception exception)
                {
                    _logSink.Warning(
                        "wildfire_timberborn_qa_direct_consequence_target_unresolved " +
                        $"selector={TimberbornQaCommandBridge.FormatToken(selector)} " +
                        $"cell_index={cellIndex} " +
                        $"exception_type={exception.GetType().Name}");
                    resolvedTarget = null;
                }

                return resolvedTarget is null
                    ? (TimberbornQaDirectConsequenceTarget?)null
                    : resolvedTarget.Value with { ScannedCellCount = offset + 1 };
            })
            .Where(static resolvedTarget => resolvedTarget is not null)
            .FirstOrDefault();

        return target ?? throw new InvalidOperationException(
            $"No placed Timberborn target was found for QA selector '{selector}'.");
    }

    internal static TimberbornQaDirectConsequenceTarget? ResolveDynamiteQaTarget(
        FireGrid grid,
        int cellIndex,
        ITimberbornExplosiveInfrastructureTargetApi? targetApi)
    {
        if (targetApi is null)
        {
            throw new InvalidOperationException(
                "QA dynamite stimulus requires the explosive infrastructure target API.");
        }

        TimberbornExplosiveInfrastructureTarget? target = targetApi.ResolveTarget(
            new TimberbornExplosiveInfrastructureConsequence(
                cellIndex,
                Tick: 0,
                Heat: QaIgnitionHeat,
                IsBurning: true));
        return target is null
            ? null
            : CreateDirectTarget(grid, cellIndex, "dynamite", target.StableId);
    }

    internal static TimberbornQaDirectConsequenceTarget? ResolveDetonatorQaTarget(
        FireGrid grid,
        int cellIndex,
        ITimberbornDetonatorFireSafetyTargetApi? targetApi)
    {
        if (targetApi is null)
        {
            throw new InvalidOperationException(
                "QA detonator stimulus requires the detonator fire-safety target API.");
        }

        TimberbornDetonatorFireSafetyTarget? target = targetApi.ResolveTarget(
            new TimberbornDetonatorFireSafetyConsequence(
                cellIndex,
                Tick: 0,
                Heat: QaIgnitionHeat,
                IsBurning: true));
        return target is null ||
            IsUnavailablePseudoTarget(target.StableId, TimberbornDetonatorFireSafetyStableIds.UnavailablePrefix)
            ? null
            : CreateDirectTarget(grid, cellIndex, "detonator", target.StableId);
    }

    internal static TimberbornQaDirectConsequenceTarget? ResolveTunnelQaTarget(
        FireGrid grid,
        int cellIndex,
        ITimberbornTunnelFireTargetApi? targetApi)
    {
        if (targetApi is null)
        {
            throw new InvalidOperationException(
                "QA tunnel stimulus requires the tunnel fire target API.");
        }

        TimberbornTunnelFireTarget? target = targetApi.ResolveTarget(
            new TimberbornTunnelFireConsequence(
                cellIndex,
                Tick: 0,
                Heat: QaIgnitionHeat,
                IsBurning: true));
        return target is null || IsUnavailablePseudoTarget(target.StableId, "tunnel-unavailable:")
            ? null
            : CreateDirectTarget(grid, cellIndex, "tunnel", target.StableId);
    }

    internal static bool IsUnavailablePseudoTarget(string stableId, string prefix)
    {
        return stableId.StartsWith(prefix, StringComparison.Ordinal);
    }

    internal static TimberbornQaDirectConsequenceTarget CreateDirectTarget(
        FireGrid grid,
        int cellIndex,
        string kind,
        string stableId)
    {
        (int x, int y, int z) = grid.FromIndex(cellIndex);
        ushort initialCell = PackedCell.Pack(
            fuel: QaIgnitionFuel,
            heat: 0,
            flammability: QaIgnitionFlammability,
            water: QaIgnitionWater,
            terrain: QaIgnitionTerrain,
            burningLevel: 0);

        return new TimberbornQaDirectConsequenceTarget(
            kind,
            stableId,
            cellIndex,
            x,
            y,
            z,
            initialCell,
            ScannedCellCount: 0);
    }

    internal TimberbornQaBurnDamageProbeTarget FindBurnDamageProbeTarget(
        FireGrid grid,
        string selector,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState>? burnDamageTargets)
    {
        if (burnDamageTargets is null || burnDamageTargets.Count == 0)
        {
            throw new InvalidOperationException(
                $"QA burn-damage selector '{selector}' requires registered TWF-075 burn-damage targets.");
        }

        TimberbornBurnDamageTargetState[] candidateStates = burnDamageTargets.Values
            .Where(state => MatchesBurnDamageProbeTargetKind(state.TargetKind, selector))
            .Where(state => MatchesBurnDamageProbeSelector(selector, state))
            .Where(state => HasEnoughRemainingCapacityForBurnDamageProbe(selector, state))
            .Where(static state => state.OwnedCellIndices.Count > 0)
            .OrderByDescending(state => BurnDamageProbeRemainingCapacitySortKey(selector, state))
            .ThenBy(static state => state.TargetKey.StableId, StringComparer.Ordinal)
            .ThenBy(static state => state.SpecId, StringComparer.Ordinal)
            .ToArray();
        TimberbornQaBurnDamageProbeTarget? target = candidateStates
            .SelectMany(state => state.OwnedCellIndices
                .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
                .OrderBy(static cellIndex => cellIndex)
                .Where(cellIndex => !RequiresImportedBurnDamageProbeTarget(selector) ||
                    _importedTargets.Any(importedTarget => importedTarget.CellIndex == cellIndex))
                .Select(cellIndex => (TimberbornQaBurnDamageProbeTarget?)CreateBurnDamageProbeTarget(grid, state, cellIndex)))
            .FirstOrDefault();

        return target ?? throw new InvalidOperationException(
            $"No TWF-075 burn-damage owned target was found for QA selector '{selector}'.");
    }

    internal TimberbornQaBurnDamageProbeTarget FindCropBurnDamageProbeTarget(
        FireGrid grid,
        string selector,
        IReadOnlyDictionary<TimberbornBurnDamageTargetKey, TimberbornBurnDamageTargetState> burnDamageTargets,
        TimberbornQaSelectedCropTarget? selectedCropTarget)
    {
        TimberbornBurnDamageTargetState[] candidateStates = burnDamageTargets.Values
            .Where(TimberbornCropBurnTargetClassifier.IsCropOrHarvestable)
            .Where(state => MatchesCropBurnDamageProbeSelector(selector, state))
            .Where(static state => state.RemainingCapacity > 0)
            .Where(static state => state.OwnedCellIndices.Count > 0)
            .OrderBy(static state => state.TargetKey.StableId, StringComparer.Ordinal)
            .ThenBy(static state => state.SpecId, StringComparer.Ordinal)
            .ToArray();

        if (selectedCropTarget is { } selectedTarget)
        {
            int selectedCellIndex = grid.ToIndex(selectedTarget.X, selectedTarget.Y, selectedTarget.Z);
            TimberbornBurnDamageTargetState? selectedState = candidateStates
                .Where(state => state.OwnedCellIndices.Contains(selectedCellIndex))
                .Select(static state => (TimberbornBurnDamageTargetState?)state)
                .FirstOrDefault();
            if (selectedState is { } foundSelectedState)
            {
                return CreateBurnDamageProbeTarget(grid, foundSelectedState, selectedCellIndex);
            }
        }

        TimberbornCellCoordinates center = new(grid.Width / 2, grid.Height / 2, grid.Depth / 2);
        TimberbornQaBurnDamageProbeTarget? target = candidateStates
            .SelectMany(state => state.OwnedCellIndices
                .Where(cellIndex => cellIndex >= 0 && cellIndex < grid.CellCount)
                .Distinct()
                .Select(cellIndex =>
                {
                    (int x, int y, int z) = grid.FromIndex(cellIndex);
                    return new
                    {
                        State = state,
                        CellIndex = cellIndex,
                        Coordinates = new TimberbornCellCoordinates(x, y, z),
                    };
                }))
            .OrderBy(item => Distance(item.Coordinates, center))
            .ThenBy(static item => item.Coordinates.Z)
            .ThenBy(static item => item.Coordinates.Y)
            .ThenBy(static item => item.Coordinates.X)
            .Select(item => (TimberbornQaBurnDamageProbeTarget?)CreateBurnDamageProbeTarget(
                grid,
                item.State,
                item.CellIndex))
            .FirstOrDefault();

        return target ?? throw new InvalidOperationException(
            "No crop or harvestable burn-damage target was found for QA crop stimulus.");
    }

    internal static bool MatchesCropBurnDamageProbeSelector(string selector, TimberbornBurnDamageTargetState state)
    {
        return TimberbornQaFieldTargetSelectors.Normalize(selector) switch
        {
            TimberbornQaFieldTargetSelectors.Bush => state.TargetKind == TimberbornBurnDamageTargetKind.Resource,
            _ => true,
        };
    }

    internal TimberbornQaBurnDamageProbeTarget CreateBurnDamageProbeTarget(
        FireGrid grid,
        TimberbornBurnDamageTargetState state,
        int cellIndex)
    {
        TimberbornImportedFieldTarget fieldTarget = _importedTargets
            .Where(target => target.CellIndex == cellIndex)
            .Select(static target => (TimberbornImportedFieldTarget?)target)
            .FirstOrDefault() ??
            CreateSyntheticBurnDamageFieldTarget(grid, state, cellIndex);
        int spendUnits = BurnDamageProbeSpendUnits(state);
        byte probeFuel = (byte)Math.Clamp(Math.Max(Math.Max(1, (int)state.FuelValue), spendUnits), 1, 15);
        byte spendFuel = (byte)Math.Max(0, probeFuel - spendUnits);
        byte probeFlammability = (byte)Math.Clamp(Math.Max(1, (int)state.Flammability), 1, 3);

        return new TimberbornQaBurnDamageProbeTarget(
            state,
            fieldTarget,
            probeFuel,
            spendFuel,
            probeFlammability);
    }

    internal static bool RequiresImportedBurnDamageProbeTarget(string selector)
    {
        return TimberbornQaFieldTargetSelectors.Normalize(selector) == TimberbornQaFieldTargetSelectors.Lodge;
    }

    internal static int BurnDamageProbeSpendUnits(TimberbornBurnDamageTargetState state)
    {
        int remainingCapacity = Math.Max(0, state.RemainingCapacity);
        if (remainingCapacity <= 0)
        {
            return 1;
        }

        int requestedSpend = state.TargetKind == TimberbornBurnDamageTargetKind.Structure
            ? Math.Max(
                QaStructureBurnDamageAcceptanceDamage,
                TimberbornStructureBurnDamageRollbackSink.MinimumUnfinishedDamage(state.DamageCapacity) - state.DamageTaken)
            : 1;

        return Math.Clamp(requestedSpend, 1, Math.Min(15, remainingCapacity));
    }

    internal static TimberbornImportedFieldTarget CreateSyntheticBurnDamageFieldTarget(
        FireGrid grid,
        TimberbornBurnDamageTargetState state,
        int cellIndex)
    {
        (int x, int y, int z) = grid.FromIndex(cellIndex);
        byte probeFuel = (byte)Math.Clamp(Math.Max(1, (int)state.FuelValue), 1, 15);
        byte probeFlammability = (byte)Math.Clamp(Math.Max(1, (int)state.Flammability), 1, 3);
        ushort initialCell = PackedCell.Pack(
            fuel: probeFuel,
            heat: 0,
            flammability: probeFlammability,
            water: 0,
            terrain: 1,
            burningLevel: 0);

        return new TimberbornImportedFieldTarget(
            cellIndex,
            x,
            y,
            z,
            MaterialClassForBurnDamageKind(state.TargetKind),
            CompanionTargetId: 0,
            initialCell);
    }

    internal static bool MatchesBurnDamageProbeTargetKind(
        TimberbornBurnDamageTargetKind targetKind,
        string selector)
    {
        return TimberbornQaFieldTargetSelectors.Normalize(selector) switch
        {
            TimberbornQaFieldTargetSelectors.Building => targetKind == TimberbornBurnDamageTargetKind.Structure,
            TimberbornQaFieldTargetSelectors.Lodge => targetKind == TimberbornBurnDamageTargetKind.Structure,
            TimberbornQaFieldTargetSelectors.DistrictCenter => targetKind == TimberbornBurnDamageTargetKind.Structure,
            TimberbornQaFieldTargetSelectors.Storage => targetKind is TimberbornBurnDamageTargetKind.Storage or
                TimberbornBurnDamageTargetKind.Structure,
            TimberbornQaFieldTargetSelectors.Infrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            TimberbornQaFieldTargetSelectors.PathInfrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            TimberbornQaFieldTargetSelectors.PowerInfrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            TimberbornQaFieldTargetSelectors.WaterInfrastructure => targetKind == TimberbornBurnDamageTargetKind.Infrastructure,
            _ => false,
        };
    }

    internal static bool MatchesBurnDamageProbeSelector(
        string selector,
        TimberbornBurnDamageTargetState state)
    {
        string normalizedSelector = TimberbornQaFieldTargetSelectors.Normalize(selector);
        if (normalizedSelector == TimberbornQaFieldTargetSelectors.Storage)
        {
            return IsStorageBurnDamageState(state);
        }

        if (normalizedSelector == TimberbornQaFieldTargetSelectors.Building && IsStorageBurnDamageState(state))
        {
            return false;
        }

        if (normalizedSelector == TimberbornQaFieldTargetSelectors.Lodge)
        {
            return IsLodgeBurnDamageState(state);
        }

        if (state.TargetKind != TimberbornBurnDamageTargetKind.Infrastructure)
        {
            return normalizedSelector != TimberbornQaFieldTargetSelectors.DistrictCenter ||
                state.SpecId.Contains("DistrictCenter", StringComparison.OrdinalIgnoreCase);
        }

        string stableId = state.TargetKey.StableId;
        return normalizedSelector switch
        {
            TimberbornQaFieldTargetSelectors.Infrastructure => true,
            TimberbornQaFieldTargetSelectors.PathInfrastructure =>
                stableId.StartsWith("path_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.PowerInfrastructure =>
                stableId.StartsWith("power_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.WaterInfrastructure =>
                stableId.StartsWith("water_infrastructure:", StringComparison.Ordinal),
            _ => true,
        };
    }

    internal static bool IsStorageBurnDamageState(TimberbornBurnDamageTargetState state)
    {
        return state.TargetKind == TimberbornBurnDamageTargetKind.Storage ||
            state.TargetKey.StableId.StartsWith("stockpile:", StringComparison.Ordinal) ||
            state.SpecId.Contains("Warehouse", StringComparison.OrdinalIgnoreCase) ||
            state.SpecId.Contains("Pile", StringComparison.OrdinalIgnoreCase) ||
            state.SpecId.Contains("Tank", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsLodgeBurnDamageState(TimberbornBurnDamageTargetState state)
    {
        return state.TargetKind == TimberbornBurnDamageTargetKind.Structure &&
            (state.SpecId.Contains("Lodge", StringComparison.OrdinalIgnoreCase) ||
                state.TargetKey.StableId.Contains("lodge", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasEnoughRemainingCapacityForBurnDamageProbe(
        string selector,
        TimberbornBurnDamageTargetState state)
    {
        string normalizedSelector = TimberbornQaFieldTargetSelectors.Normalize(selector);
        if (normalizedSelector is TimberbornQaFieldTargetSelectors.Building or TimberbornQaFieldTargetSelectors.Lodge &&
            state.TargetKind == TimberbornBurnDamageTargetKind.Structure)
        {
            return state.RemainingCapacity >= QaStructureBurnDamageAcceptanceDamage;
        }

        return state.RemainingCapacity > 0 || AllowsZeroCapacityBurnDamageProbe(selector, state);
    }

    internal static int BurnDamageProbeRemainingCapacitySortKey(
        string selector,
        TimberbornBurnDamageTargetState state)
    {
        string normalizedSelector = TimberbornQaFieldTargetSelectors.Normalize(selector);
        bool prefersHighestCapacity = normalizedSelector is TimberbornQaFieldTargetSelectors.Building or
            TimberbornQaFieldTargetSelectors.Lodge &&
            state.TargetKind == TimberbornBurnDamageTargetKind.Structure;
        return prefersHighestCapacity
            ? state.RemainingCapacity
            : 0;
    }

    internal static bool AllowsZeroCapacityBurnDamageProbe(
        string selector,
        TimberbornBurnDamageTargetState state)
    {
        if (state.TargetKind != TimberbornBurnDamageTargetKind.Infrastructure)
        {
            return false;
        }

        string stableId = state.TargetKey.StableId;
        return TimberbornQaFieldTargetSelectors.Normalize(selector) switch
        {
            TimberbornQaFieldTargetSelectors.Infrastructure => true,
            TimberbornQaFieldTargetSelectors.PathInfrastructure =>
                stableId.StartsWith("path_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.PowerInfrastructure =>
                stableId.StartsWith("power_infrastructure:", StringComparison.Ordinal),
            TimberbornQaFieldTargetSelectors.WaterInfrastructure =>
                stableId.StartsWith("water_infrastructure:", StringComparison.Ordinal),
            _ => false,
        };
    }

    internal static WildfireMaterialClass MaterialClassForBurnDamageKind(TimberbornBurnDamageTargetKind targetKind)
    {
        return targetKind switch
        {
            TimberbornBurnDamageTargetKind.Structure => WildfireMaterialClass.Building,
            TimberbornBurnDamageTargetKind.Storage => WildfireMaterialClass.Storage,
            TimberbornBurnDamageTargetKind.Infrastructure => WildfireMaterialClass.Infrastructure,
            TimberbornBurnDamageTargetKind.Tree => WildfireMaterialClass.Tree,
            TimberbornBurnDamageTargetKind.Crop => WildfireMaterialClass.Crop,
            _ => WildfireMaterialClass.Empty,
        };
    }

    internal static Func<IEnumerable<TimberbornImportedFieldTarget>, IOrderedEnumerable<TimberbornImportedFieldTarget>> OrderByCenterDistance(
        FireGrid grid)
    {
        float centerX = (grid.Width - 1) / 2f;
        float centerY = (grid.Height - 1) / 2f;
        float centerZ = (grid.Depth - 1) / 2f;
        return targets => targets
            .OrderBy(target => SquaredDistance(target, centerX, centerY, centerZ))
            .ThenBy(static target => target.CellIndex);
    }

    internal static float SquaredDistance(TimberbornImportedFieldTarget target, float centerX, float centerY, float centerZ)
    {
        float dx = target.X - centerX;
        float dy = target.Y - centerY;
        float dz = target.Z - centerZ;
        return dx * dx + dy * dy + dz * dz;
    }

    internal static Func<IEnumerable<TimberbornImportedFieldTarget>, IOrderedEnumerable<TimberbornImportedFieldTarget>>
        OrderByDescendingSoilContamination()
    {
        return targets => targets
            .OrderByDescending(static target => target.SoilContamination)
            .ThenByDescending(static target => PackedCell.Fuel(target.InitialCell))
            .ThenBy(static target => target.CellIndex);
    }

    internal static bool IsBurnableImportedTarget(TimberbornImportedFieldTarget target)
    {
        return PackedCell.Terrain(target.InitialCell) == 1 &&
            PackedCell.Fuel(target.InitialCell) > 0 &&
            PackedCell.Flammability(target.InitialCell) > 0 &&
            target.MaterialClass is WildfireMaterialClass.Tree or
                WildfireMaterialClass.Vegetation or
                WildfireMaterialClass.Crop or
                WildfireMaterialClass.Building or
                WildfireMaterialClass.Storage;
    }

    internal static bool IsWaterSuppressionTarget(TimberbornImportedFieldTarget target, string normalizedSelector)
    {
        if (!IsBurnableImportedTarget(target) ||
            !TimberbornQaFieldTargetSelectors.Matches(target.MaterialClass, normalizedSelector))
        {
            return false;
        }

        return normalizedSelector != TimberbornQaFieldTargetSelectors.ContaminatedTree ||
            target.SoilContamination > 0;
    }

    internal sealed record TimberbornQaDeltaStimulusTargetSelection(
        TimberbornCellCoordinates Coordinates,
        string TargetSource,
        int RegisteredBurnDamageTargetCount,
        int RegisteredCropBurnTargetCount,
        int RegisteredCropBurnOwnedCellCount);
}
