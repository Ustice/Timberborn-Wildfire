using System.Runtime.CompilerServices;
using Timberborn.BlockSystem;
using Timberborn.Growing;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public sealed class TimberbornGrowableAshGrowthAdapter : ITimberbornAshGrowthAdapter
{
    public const float MaxProgressBonusPerApplication = 0.001f;

    private readonly IBlockService _blockService;
    private readonly Func<FireGrid?> _gridProvider;
    private readonly ITimberbornFireLogSink _logSink;

    public TimberbornGrowableAshGrowthAdapter(
        IBlockService blockService,
        Func<FireGrid?> gridProvider,
        ITimberbornFireLogSink? logSink = null)
    {
        _blockService = blockService ?? throw new ArgumentNullException(nameof(blockService));
        _gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        _logSink = logSink ?? NullTimberbornFireLogSink.Instance;
    }

    public TimberbornAshGrowthApplicationResult ApplyGrowthBonuses(
        uint tick,
        IReadOnlyList<TimberbornAshGrowthBonusRequest> requests)
    {
        if (requests is null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        FireGrid? grid = _gridProvider();
        if (!grid.HasValue)
        {
            return new TimberbornAshGrowthApplicationResult(
                CandidateGrowableCount: requests.Count,
                AppliedGrowableCount: 0,
                SkippedUnsafeApiCount: requests.Count,
                SkippedUnsupportedGrowableCount: 0);
        }

        AshGrowthApplicationCounter counter = new();
        HashSet<int> appliedGrowableIds = new();
        requests.ToList().ForEach(request => ApplyRequest(tick, grid.Value, request, appliedGrowableIds, counter));

        if (counter.AppliedGrowables > 0 || counter.SkippedUnsafeApis > 0)
        {
            _logSink.Info(
                "wildfire_timberborn_ash_growth_adapter_applied " +
                $"tick={tick} " +
                $"requests={requests.Count} " +
                $"candidate_growables={counter.CandidateGrowables} " +
                $"applied_growables={counter.AppliedGrowables} " +
                $"skipped_unsafe_apis={counter.SkippedUnsafeApis} " +
                $"skipped_unsupported_growables={counter.SkippedUnsupportedGrowables}");
        }

        return new TimberbornAshGrowthApplicationResult(
            CandidateGrowableCount: counter.CandidateGrowables,
            AppliedGrowableCount: counter.AppliedGrowables,
            SkippedUnsafeApiCount: counter.SkippedUnsafeApis,
            SkippedUnsupportedGrowableCount: counter.SkippedUnsupportedGrowables);
    }

    private void ApplyRequest(
        uint tick,
        FireGrid grid,
        TimberbornAshGrowthBonusRequest request,
        HashSet<int> appliedGrowableIds,
        AshGrowthApplicationCounter counter)
    {
        Growable[] growables;
        try
        {
            (int x, int y, int z) = grid.FromIndex(request.CellIndex);
            Vector3Int coordinates = new(x, y, z);
            Growable[] directGrowables = _blockService
                .GetObjectsWithComponentAt<Growable>(coordinates)
                .ToArray();
            Growable[] blockObjectGrowables = _blockService
                .GetObjectsWithComponentAt<BlockObject>(coordinates)
                .SelectMany(FindGrowables)
                .ToArray();
            growables = directGrowables
                .Concat(blockObjectGrowables)
                .GroupBy(static growable => RuntimeHelpers.GetHashCode(growable))
                .Select(static group => group.First())
                .ToArray();
        }
        catch (Exception exception)
        {
            counter.SkippedUnsafeApis++;
            _logSink.Warning(
                "wildfire_timberborn_ash_growth_lookup_failed " +
                $"tick={tick} " +
                $"cell_index={request.CellIndex} " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
            return;
        }

        if (growables.Length == 0)
        {
            counter.SkippedUnsupportedGrowables++;
            return;
        }

        growables.ToList().ForEach(growable =>
            ApplyGrowableBonus(tick, request, growable, appliedGrowableIds, counter));
    }

    private static Growable[] FindGrowables(BlockObject blockObject)
    {
        return blockObject.GetComponentsAllocating<Growable>()
            .GroupBy(static growable => RuntimeHelpers.GetHashCode(growable))
            .Select(static group => group.First())
            .ToArray();
    }

    private void ApplyGrowableBonus(
        uint tick,
        TimberbornAshGrowthBonusRequest request,
        Growable growable,
        HashSet<int> appliedGrowableIds,
        AshGrowthApplicationCounter counter)
    {
        int growableId = RuntimeHelpers.GetHashCode(growable);
        if (appliedGrowableIds.Contains(growableId))
        {
            return;
        }

        counter.CandidateGrowables++;
        try
        {
            if (growable.IsGrown || !growable.GrowthInProgress)
            {
                counter.SkippedUnsupportedGrowables++;
                return;
            }

            float progressBonus = ProgressBonus(request);
            if (progressBonus <= 0f)
            {
                counter.SkippedUnsupportedGrowables++;
                return;
            }

            growable.IncreaseGrowthProgress(progressBonus);
            appliedGrowableIds.Add(growableId);
            counter.AppliedGrowables++;
        }
        catch (Exception exception)
        {
            counter.SkippedUnsafeApis++;
            _logSink.Warning(
                "wildfire_timberborn_ash_growth_apply_failed " +
                $"tick={tick} " +
                $"cell_index={request.CellIndex} " +
                $"growable_id={growableId} " +
                $"message={TimberbornQaCommandBridge.FormatToken(exception.GetType().Name)}");
        }
    }

    private static float ProgressBonus(TimberbornAshGrowthBonusRequest request)
    {
        float multiplierBonus = Math.Clamp(request.GrowthMultiplier - 1f, 0f, 0.10f);
        return MaxProgressBonusPerApplication * (multiplierBonus / 0.10f);
    }

    private sealed class AshGrowthApplicationCounter
    {
        public int CandidateGrowables { get; set; }
        public int AppliedGrowables { get; set; }
        public int SkippedUnsafeApis { get; set; }
        public int SkippedUnsupportedGrowables { get; set; }
    }
}
