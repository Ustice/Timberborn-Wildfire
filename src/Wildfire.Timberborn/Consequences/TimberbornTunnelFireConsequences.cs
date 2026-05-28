using System.Reflection;
using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Consequences;

public readonly record struct TimberbornTunnelFireConsequence(
    int CellIndex,
    uint Tick,
    int Heat,
    bool IsBurning)
{
    public bool ShouldApplyTunnelConsequence => Heat > 0 || IsBurning;

    public static TimberbornTunnelFireConsequence FromDecision(uint tick, TimberbornFireCellDeltaDecision decision)
    {
        return new TimberbornTunnelFireConsequence(
            decision.CellIndex,
            tick,
            decision.NewHeat,
            decision.IsBurning);
    }
}

public sealed record TimberbornTunnelFireTarget(
    string StableId,
    int CellIndex,
    int BottomLevel,
    bool CanMarkUnstable,
    bool CanExplodeNative,
    bool CanRecover);

public readonly record struct TimberbornTunnelFireSettings(
    bool TunnelFireBehaviorEnabled,
    bool TunnelTerrainDestructionEnabled)
{
    public static TimberbornTunnelFireSettings FromSnapshot(WildfireReleaseSettingsSnapshot snapshot)
    {
        return new TimberbornTunnelFireSettings(
            snapshot.IsTunnelFireBehaviorEnabled,
            snapshot.IsTunnelTerrainDestructionEnabled);
    }
}

public enum TimberbornTunnelNativeExplodeStatus
{
    SkippedSettingDisabled,
    Applied,
}

public readonly record struct TimberbornTunnelNativeExplodeResult(
    TimberbornTunnelNativeExplodeStatus Status,
    bool RecoverabilityPreserved);

public readonly record struct TimberbornTunnelFireSummary(
    int ConsideredDeltaCount,
    int MatchedTargetCellCount,
    int DuplicateTargetSuppressedCount,
    int UnstableTargetCount,
    int NativeExplodeAttemptedCount,
    int NativeExplodeAppliedCount,
    int DestructionDeferredCount,
    int SkippedSettingDisabledCount,
    int RecoverabilityPreservedCount,
    int RecoverabilityUnknownCount)
{
    public static readonly TimberbornTunnelFireSummary Empty = new(
        ConsideredDeltaCount: 0,
        MatchedTargetCellCount: 0,
        DuplicateTargetSuppressedCount: 0,
        UnstableTargetCount: 0,
        NativeExplodeAttemptedCount: 0,
        NativeExplodeAppliedCount: 0,
        DestructionDeferredCount: 0,
        SkippedSettingDisabledCount: 0,
        RecoverabilityPreservedCount: 0,
        RecoverabilityUnknownCount: 0);

    public string ToLogToken(uint tick)
    {
        return "wildfire_timberborn_tunnel_fire_applied " +
            $"tick={tick} " +
            $"considered_deltas={ConsideredDeltaCount} " +
            $"matched_target_cells={MatchedTargetCellCount} " +
            $"duplicate_targets_suppressed={DuplicateTargetSuppressedCount} " +
            $"unstable_targets={UnstableTargetCount} " +
            $"native_explode_attempted={NativeExplodeAttemptedCount} " +
            $"native_explode_applied={NativeExplodeAppliedCount} " +
            $"destruction_deferred={DestructionDeferredCount} " +
            $"skipped_setting_disabled={SkippedSettingDisabledCount} " +
            $"recoverability_preserved={RecoverabilityPreservedCount} " +
            $"recoverability_unknown={RecoverabilityUnknownCount}";
    }
}

public interface ITimberbornTunnelFireSink
{
    TimberbornTunnelFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions);
}

public interface ITimberbornTunnelFireTargetApi
{
    TimberbornTunnelFireTarget? ResolveTarget(TimberbornTunnelFireConsequence consequence);

    TimberbornTunnelNativeExplodeResult ExplodeNative(TimberbornTunnelFireTarget target);
}

public sealed class TimberbornTunnelFireSink : ITimberbornTunnelFireSink
{
    private readonly Func<TimberbornTunnelFireSettings> _settingsProvider;
    private readonly ITimberbornTunnelFireTargetApi _targetApi;
    private readonly ITimberbornFireLogSink _logSink;
    private readonly HashSet<string> _unstableTargets = new(StringComparer.Ordinal);
    private readonly HashSet<string> _explodedTargets = new(StringComparer.Ordinal);

    public TimberbornTunnelFireSink(
        Func<TimberbornTunnelFireSettings> settingsProvider,
        ITimberbornTunnelFireTargetApi targetApi,
        ITimberbornFireLogSink? logSink = null)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _targetApi = targetApi ?? throw new ArgumentNullException(nameof(targetApi));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornTunnelFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        TimberbornTunnelFireSettings settings = _settingsProvider();
        TimberbornTunnelFireConsequence[] consequences = decisions
            .Select(decision => TimberbornTunnelFireConsequence.FromDecision(tick, decision))
            .Where(static consequence => consequence.ShouldApplyTunnelConsequence)
            .ToArray();

        if (!settings.TunnelFireBehaviorEnabled)
        {
            TimberbornTunnelFireSummary disabledSummary = new(
                ConsideredDeltaCount: consequences.Length,
                MatchedTargetCellCount: 0,
                DuplicateTargetSuppressedCount: 0,
                UnstableTargetCount: 0,
                NativeExplodeAttemptedCount: 0,
                NativeExplodeAppliedCount: 0,
                DestructionDeferredCount: 0,
                SkippedSettingDisabledCount: consequences.Length,
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
        TunnelTargetResult[] results = uniqueTargets
            .Select(resolvedTarget => ApplyTarget(settings, resolvedTarget))
            .ToArray();

        TimberbornTunnelFireSummary summary = new(
            ConsideredDeltaCount: consequences.Length,
            MatchedTargetCellCount: matchedTargets.Length,
            DuplicateTargetSuppressedCount: matchedTargets.Length - uniqueTargets.Length,
            UnstableTargetCount: results.Count(static result => result.MarkedUnstable),
            NativeExplodeAttemptedCount: results.Count(static result => result.NativeAttempted),
            NativeExplodeAppliedCount: results.Count(static result =>
                result.NativeStatus == TimberbornTunnelNativeExplodeStatus.Applied),
            DestructionDeferredCount: results.Count(static result =>
                result.NativeStatus == TimberbornTunnelNativeExplodeStatus.SkippedSettingDisabled),
            SkippedSettingDisabledCount: 0,
            RecoverabilityPreservedCount: results.Count(static result => result.RecoverabilityPreserved),
            RecoverabilityUnknownCount: results.Count(static result =>
                !result.RecoverabilityPreserved));
        _logSink.Info(summary.ToLogToken(tick));
        return summary;
    }

    private ResolvedTarget ResolveTarget(TimberbornTunnelFireConsequence consequence)
    {
        return new ResolvedTarget(consequence, _targetApi.ResolveTarget(consequence));
    }

    private TunnelTargetResult ApplyTarget(
        TimberbornTunnelFireSettings settings,
        ResolvedTarget resolvedTarget)
    {
        TimberbornTunnelFireTarget target = resolvedTarget.Target ??
            throw new InvalidOperationException("Resolved tunnel target cannot be null during fire application.");
        bool markedUnstable = target.CanMarkUnstable && _unstableTargets.Add(target.StableId);

        if (!settings.TunnelTerrainDestructionEnabled)
        {
            return new TunnelTargetResult(
                MarkedUnstable: markedUnstable,
                NativeAttempted: false,
                NativeStatus: TimberbornTunnelNativeExplodeStatus.SkippedSettingDisabled,
                RecoverabilityPreserved: target.CanRecover);
        }

        if (!target.CanExplodeNative || _explodedTargets.Contains(target.StableId))
        {
            throw new InvalidOperationException($"Tunnel target cannot explode natively: {target.StableId}.");
        }

        TimberbornTunnelNativeExplodeResult result = _targetApi.ExplodeNative(target);
        if (result.Status != TimberbornTunnelNativeExplodeStatus.Applied)
        {
            throw new InvalidOperationException($"Tunnel native explode did not apply for {target.StableId}.");
        }

        _explodedTargets.Add(target.StableId);
        return new TunnelTargetResult(
            MarkedUnstable: markedUnstable,
            NativeAttempted: true,
            NativeStatus: result.Status,
            RecoverabilityPreserved: result.RecoverabilityPreserved);
    }

    private readonly record struct ResolvedTarget(
        TimberbornTunnelFireConsequence Consequence,
        TimberbornTunnelFireTarget? Target);

    private readonly record struct TunnelTargetResult(
        bool MarkedUnstable,
        bool NativeAttempted,
        TimberbornTunnelNativeExplodeStatus NativeStatus,
        bool RecoverabilityPreserved);
}

public sealed class NullTimberbornTunnelFireSink : ITimberbornTunnelFireSink
{
    public static readonly NullTimberbornTunnelFireSink Instance = new();

    private NullTimberbornTunnelFireSink()
    {
    }

    public TimberbornTunnelFireSummary ApplyConsequences(
        uint tick,
        IReadOnlyList<TimberbornFireCellDeltaDecision> decisions)
    {
        return TimberbornTunnelFireSummary.Empty;
    }
}

public sealed class TimberbornTunnelFireTargetApi : ITimberbornTunnelFireTargetApi
{
    private readonly FireGrid _grid;
    private readonly IBlockService _blockService;
    private readonly Type? _tunnelType = Type.GetType("Timberborn.Explosions.Tunnel, Timberborn.Explosions");
    private readonly Dictionary<string, object> _tunnelsByStableId = new(StringComparer.Ordinal);

    public TimberbornTunnelFireTargetApi(FireGrid grid, IBlockService blockService)
    {
        _grid = grid;
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
    }

    public TimberbornTunnelFireTarget? ResolveTarget(TimberbornTunnelFireConsequence consequence)
    {
        (int x, int y, int z) = _grid.FromIndex(consequence.CellIndex);
        Vector3Int coordinates = new(x, y, z);
        FindTunnelResult findResult = FindTunnelAt(coordinates);

        if (findResult.Tunnel is null)
        {
            return null;
        }

        string stableId = $"tunnel:{RuntimeHelpers.GetHashCode(findResult.Tunnel)}";
        _tunnelsByStableId[stableId] = findResult.Tunnel;

        return new TimberbornTunnelFireTarget(
            stableId,
            consequence.CellIndex,
            ReadBottomLevel(findResult.Tunnel),
            CanMarkUnstable: true,
            CanExplodeNative: true,
            CanRecover: false);
    }

    public TimberbornTunnelNativeExplodeResult ExplodeNative(TimberbornTunnelFireTarget target)
    {
        if (!_tunnelsByStableId.TryGetValue(target.StableId, out object? tunnel))
        {
            throw new InvalidOperationException($"Tunnel target was not registered: {target.StableId}.");
        }

        try
        {
            MethodInfo? explodeMethod = tunnel.GetType().GetMethod(
                "Explode",
                BindingFlags.Instance | BindingFlags.Public);
            if (explodeMethod is null)
            {
                throw new InvalidOperationException(
                    $"Tunnel Explode API is unavailable on {tunnel.GetType().FullName}.");
            }

            explodeMethod.Invoke(tunnel, Array.Empty<object>());
            return new TimberbornTunnelNativeExplodeResult(
                TimberbornTunnelNativeExplodeStatus.Applied,
                RecoverabilityPreserved: target.CanRecover);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("Tunnel explode failed.", exception);
        }
    }

    private FindTunnelResult FindTunnelAt(Vector3Int coordinates)
    {
        if (_tunnelType is null)
        {
            throw new InvalidOperationException("Tunnel type is unavailable.");
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
                .MakeGenericMethod(_tunnelType)
                .Invoke(_blockService, new object[] { coordinates });
            if (result is not System.Collections.IEnumerable enumerable)
            {
                throw new InvalidOperationException("Tunnel lookup returned a non-enumerable result.");
            }

            return new FindTunnelResult(
                enumerable
                    .Cast<object>()
                    .OrderBy(static candidate => RuntimeHelpers.GetHashCode(candidate))
                    .FirstOrDefault(),
                Failed: false);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException("Tunnel lookup failed.", exception);
        }
    }

    private static int ReadBottomLevel(object tunnel)
    {
        try
        {
            return tunnel.GetType()
                .GetProperty("BottomLevel", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(tunnel) as int? ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private readonly record struct FindTunnelResult(
        object? Tunnel,
        bool Failed);
}
