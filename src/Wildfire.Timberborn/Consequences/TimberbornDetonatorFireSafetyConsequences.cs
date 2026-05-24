using System.Runtime.CompilerServices;
using System.Reflection;
using Timberborn.BlockSystem;
using Timberborn.Explosions;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornDetonatorFireSafetyConsequence(
    int CellIndex,
    uint Tick,
    int Heat,
    bool IsBurning)
{
    public bool ShouldApplySafety => Heat > 0 || IsBurning;

    public static TimberbornDetonatorFireSafetyConsequence FromDecision(
        uint tick,
        TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornDetonatorFireSafetyConsequence(
            decision.CellIndex,
            tick,
            decision.NewHeat,
            decision.IsBurning);
    }
}

public sealed record TimberbornDetonatorFireSafetyTarget(
    string StableId,
    int CellIndex,
    bool CanDisable,
    bool CanPreserveAutomationState);

public static class TimberbornDetonatorFireSafetyStableIds
{
    public const string UnavailablePrefix = "detonator-unavailable:";
    public const string DynamiteControlPrefix = "detonator-dynamite-control:";

    public static string CreateDynamiteControlStableId(object dynamite)
    {
        if (dynamite is null)
        {
            throw new ArgumentNullException(nameof(dynamite));
        }

        return $"{DynamiteControlPrefix}{RuntimeHelpers.GetHashCode(dynamite)}";
    }
}

public enum TimberbornDetonatorFireSafetyDisableStatus
{
    SkippedNoSafeApi,
    Disabled,
}

public readonly record struct TimberbornDetonatorFireSafetyDisableResult(
    TimberbornDetonatorFireSafetyDisableStatus Status,
    bool RecoverabilityPreserved);

public readonly record struct TimberbornDetonatorFireSafetySummary(
    int ConsideredDeltaCount,
    int MatchedTargetCellCount,
    int DuplicateTargetSuppressedCount,
    int DisabledTargetCount,
    int ArmedTargetCount,
    int SkippedSettingDisabledCount,
    int SkippedNoSafeApiCount,
    int RecoverabilityPreservedCount,
    int RecoverabilityUnknownCount)
{
    public static readonly TimberbornDetonatorFireSafetySummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedTargetCellCount: 0,
        DuplicateTargetSuppressedCount: 0,
        DisabledTargetCount: 0,
        ArmedTargetCount: 0,
        SkippedSettingDisabledCount: 0,
        SkippedNoSafeApiCount: 0,
        RecoverabilityPreservedCount: 0,
        RecoverabilityUnknownCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_detonator_fire_safety_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_target_cells={MatchedTargetCellCount} " +
            $"duplicate_targets_suppressed={DuplicateTargetSuppressedCount} " +
            $"disabled_targets={DisabledTargetCount} " +
            $"armed_targets={ArmedTargetCount} " +
            $"skipped_setting_disabled={SkippedSettingDisabledCount} " +
            $"skipped_no_safe_api={SkippedNoSafeApiCount} " +
            $"recoverability_preserved={RecoverabilityPreservedCount} " +
            $"recoverability_unknown={RecoverabilityUnknownCount}";
    }
}

public interface ITimberbornDetonatorFireSafetySink
{
    TimberbornDetonatorFireSafetySummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornDetonatorFireSafetyTargetApi
{
    TimberbornDetonatorFireSafetyTarget? ResolveTarget(
        TimberbornDetonatorFireSafetyConsequence consequence);

    TimberbornDetonatorFireSafetyDisableResult DisableTarget(
        TimberbornDetonatorFireSafetyTarget target);
}

public sealed class TimberbornDetonatorFireSafetySink : ITimberbornDetonatorFireSafetySink
{
    private readonly Func<bool> _isEnabled;
    private readonly ITimberbornDetonatorFireSafetyTargetApi _targetApi;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornDetonatorFireSafetySink(
        Func<bool> isEnabled,
        ITimberbornDetonatorFireSafetyTargetApi targetApi,
        ITimberbornFireLogSink? logSink = null)
    {
        _isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornDetonatorFireSafetySummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornDetonatorFireSafetyConsequence[] consequences = decisions
            .Select(decision => TimberbornDetonatorFireSafetyConsequence.FromDecision(tick, decision))
            .Where(static consequence => consequence.ShouldApplySafety)
            .ToArray();

        if (!_isEnabled())
        {
            TimberbornDetonatorFireSafetySummary disabledSummary = new(
                ConsideredDeltaCount: consequences.Length,
                MatchedTargetCellCount: 0,
                DuplicateTargetSuppressedCount: 0,
                DisabledTargetCount: 0,
                ArmedTargetCount: 0,
                SkippedSettingDisabledCount: consequences.Length,
                SkippedNoSafeApiCount: 0,
                RecoverabilityPreservedCount: 0,
                RecoverabilityUnknownCount: 0);
            _logSink.Info(disabledSummary.ToLogToken(tick));
            return disabledSummary;
        }

        ResolvedTarget[] resolvedTargets = consequences
            .Select(ResolveTarget)
            .ToArray();
        ResolvedTarget[] matchedTargets = resolvedTargets
            .Where(static resolvedTarget => resolvedTarget.Target is not null)
            .ToArray();
        ResolvedTarget[] uniqueTargets = matchedTargets
            .GroupBy(static resolvedTarget => resolvedTarget.Target!.StableId, StringComparer.Ordinal)
            .Select(static group => group
                .OrderByDescending(static resolvedTarget => resolvedTarget.Consequence.Heat)
                .ThenBy(static resolvedTarget => resolvedTarget.Consequence.CellIndex)
                .First())
            .ToArray();
        TimberbornDetonatorFireSafetyDisableResult[] results = uniqueTargets
            .Select(DisableTarget)
            .ToArray();

        TimberbornDetonatorFireSafetySummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedTargetCellCount: matchedTargets.Length,
            DuplicateTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            DisabledTargetCount: results.Count(static result =>
                result.Status == TimberbornDetonatorFireSafetyDisableStatus.Disabled),
            ArmedTargetCount: 0,
            SkippedSettingDisabledCount: 0,
            SkippedNoSafeApiCount: results.Count(static result =>
                result.Status == TimberbornDetonatorFireSafetyDisableStatus.SkippedNoSafeApi),
            RecoverabilityPreservedCount: results.Count(static result => result.RecoverabilityPreserved),
            RecoverabilityUnknownCount: results.Count(static result =>
                !result.RecoverabilityPreserved));
        _logSink.Info(summary.ToLogToken(tick));
        return summary;
    }

    private ResolvedTarget ResolveTarget(TimberbornDetonatorFireSafetyConsequence consequence)
    {
        return new ResolvedTarget(consequence, _targetApi.ResolveTarget(consequence));
    }

    private TimberbornDetonatorFireSafetyDisableResult DisableTarget(ResolvedTarget resolvedTarget)
    {
        TimberbornDetonatorFireSafetyTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved detonator target cannot be null during safety application.");
        if (!target.CanDisable)
        {
            throw new InvalidOperationException($"Detonator target cannot be disabled: {target.StableId}.");
        }

        return _targetApi.DisableTarget(target);
    }

    private readonly record struct ResolvedTarget(
        TimberbornDetonatorFireSafetyConsequence Consequence,
        TimberbornDetonatorFireSafetyTarget? Target);
}

public sealed class NullTimberbornDetonatorFireSafetySink : ITimberbornDetonatorFireSafetySink
{
    public static readonly NullTimberbornDetonatorFireSafetySink Instance = new();

    private NullTimberbornDetonatorFireSafetySink()
    {
    }

    public TimberbornDetonatorFireSafetySummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornDetonatorFireSafetySummary.Empty;
    }
}

public sealed class TimberbornDetonatorFireSafetyTargetApi : ITimberbornDetonatorFireSafetyTargetApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;
    private readonly Type? _detonatorType =
        Type.GetType("Timberborn.AutomationBuildings.Detonator, Timberborn.AutomationBuildings");
    private readonly Dictionary<string, object> _disarmTargetsByStableId = new(StringComparer.Ordinal);

    public TimberbornDetonatorFireSafetyTargetApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornDetonatorFireSafetyTarget? ResolveTarget(
        TimberbornDetonatorFireSafetyConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        FindDetonatorResult findResult = FindDetonatorAt(coordinates);

        if (findResult.Detonator is null)
        {
            FindDynamiteControlResult dynamiteControlResult = FindDynamiteControlAt(coordinates);
            if (dynamiteControlResult.Dynamite is null)
            {
                return null;
            }

            string dynamiteControlStableId =
                TimberbornDetonatorFireSafetyStableIds.CreateDynamiteControlStableId(dynamiteControlResult.Dynamite);
            _disarmTargetsByStableId[dynamiteControlStableId] = dynamiteControlResult.Dynamite;

            return new TimberbornDetonatorFireSafetyTarget(
                dynamiteControlStableId,
                consequence.CellIndex,
                CanDisable: true,
                CanPreserveAutomationState: true);
        }

        string stableId = $"detonator:{RuntimeHelpers.GetHashCode(findResult.Detonator)}";
        _disarmTargetsByStableId[stableId] = findResult.Detonator;

        return new TimberbornDetonatorFireSafetyTarget(
            stableId,
            consequence.CellIndex,
            CanDisable: true,
            CanPreserveAutomationState: true);
    }

    public TimberbornDetonatorFireSafetyDisableResult DisableTarget(
        TimberbornDetonatorFireSafetyTarget target)
    {
        if (!_disarmTargetsByStableId.TryGetValue(target.StableId, out object? disarmTarget))
        {
            throw new InvalidOperationException($"Detonator target was not registered: {target.StableId}.");
        }

        return TimberbornDetonatorFireSafetyNativeWrapper.DisableTarget(
            disarmTarget,
            target.CanPreserveAutomationState);
    }

    private FindDetonatorResult FindDetonatorAt(Vector3Int coordinates)
    {
        if (_detonatorType is null)
        {
            throw new InvalidOperationException("Detonator type is unavailable.");
        }

        MethodInfo? method = typeof(IBlockService)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(static candidate => candidate.Name == nameof(IBlockService.GetObjectsWithComponentAt))
            .Where(static candidate => candidate.IsGenericMethodDefinition)
            .Where(static candidate => candidate.GetParameters().Length == 1)
            .FirstOrDefault();
        if (method is null)
        {
            throw new InvalidOperationException("IBlockService.GetObjectsWithComponentAt API is unavailable.");
        }

        try
        {
            object? result = method
                .MakeGenericMethod(_detonatorType)
                .Invoke(_blockService, new object[] { coordinates });
            if (result is not System.Collections.IEnumerable enumerable)
            {
                throw new InvalidOperationException("Detonator lookup returned a non-enumerable result.");
            }

            return new FindDetonatorResult(
                enumerable
                    .Cast<object>()
                    .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
                    .FirstOrDefault(),
                SafeApiUnavailable: false);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("Detonator lookup failed.", exception);
        }
    }

    private FindDynamiteControlResult FindDynamiteControlAt(Vector3Int coordinates)
    {
        try
        {
            return new FindDynamiteControlResult(
                _blockService
                    .GetObjectsWithComponentAt<Dynamite>(coordinates)
                    .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
                    .FirstOrDefault(),
                SafeApiUnavailable: false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Dynamite control lookup failed.", exception);
        }
    }

    private readonly record struct FindDetonatorResult(
        object? Detonator,
        bool SafeApiUnavailable)
    {
        public static readonly FindDetonatorResult SafeUnavailable = new(
            Detonator: null,
            SafeApiUnavailable: true);
    }

    private readonly record struct FindDynamiteControlResult(
        Dynamite? Dynamite,
        bool SafeApiUnavailable)
    {
        public static readonly FindDynamiteControlResult SafeUnavailable = new(
            Dynamite: null,
            SafeApiUnavailable: true);
    }
}

public static class TimberbornDetonatorFireSafetyNativeWrapper
{
    public static TimberbornDetonatorFireSafetyDisableResult DisableTarget(
        object detonator,
        bool canPreserveAutomationState)
    {
        if (detonator is null)
        {
            throw new ArgumentNullException(nameof(detonator));
        }

        MethodInfo? disarmMethod = detonator
            .GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static method => method.Name == "Disarm")
            .Where(static method => method.GetParameters().Length == 0)
            .FirstOrDefault();
        if (disarmMethod is null)
        {
            throw new InvalidOperationException(
                $"Detonator Disarm API is unavailable on {detonator.GetType().FullName}.");
        }

        try
        {
            disarmMethod.Invoke(detonator, Array.Empty<object>());
            return new TimberbornDetonatorFireSafetyDisableResult(
                TimberbornDetonatorFireSafetyDisableStatus.Disabled,
                RecoverabilityPreserved: canPreserveAutomationState);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Detonator disarm failed.", exception);
        }
    }
}
