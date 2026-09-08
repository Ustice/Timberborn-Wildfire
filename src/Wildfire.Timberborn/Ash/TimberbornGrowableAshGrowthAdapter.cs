using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Growing;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Ash;

internal sealed class TimberbornGrowableAshGrowthAdapter
{
    private readonly IBlockService _blocks;
    internal TimberbornGrowableAshGrowthAdapter(IBlockService blocks) =>
        _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));

    // Read-only preparation under CaptureAtRest; no native quantities or completion callbacks change.
    internal PreparedGrowth Prepare(FireGrid grid, float elapsedDays, IReadOnlyList<TimberbornAshGrowthBonusRequest> requests)
    {
        TimberbornAshGrowthRate.Validate(elapsedDays, 1, 1);
        var candidates = new List<Candidate>();
        foreach (var request in requests)
        {
            if (request.Quality != WildfireAshQuality.Fertile || request.Strength <= 0 || request.Strength > TimberbornAshFieldService.MaxStrength)
                throw new InvalidOperationException("Native growth requires a derived clean-ash request.");
            TimberbornAshGrowthRate.Validate(elapsedDays, request.GrowthMultiplier, 1);
            var (x, y, z) = grid.FromIndex(request.CellIndex);
            var coordinates = new Vector3Int(x, y, z);
            foreach (var growable in _blocks.GetObjectsWithComponentAt<Growable>(coordinates))
            {
                float duration = TimberbornAshGrowthRate.ReadDuration(growable);
                TimberbornAshGrowthRate.Validate(elapsedDays, request.GrowthMultiplier, duration);
                var entity = growable.GetComponent<EntityComponent>() ??
                    throw new InvalidOperationException("Ash growth candidate lacks its native entity.");
                if (entity.EntityId == Guid.Empty)
                    throw new InvalidOperationException("Ash growth candidate lacks a native identity.");
                // Exact references, never integer hash identity. A repeated physical target gets one bonus.
                int existing = candidates.FindIndex(candidate => ReferenceEquals(candidate.Growable, growable));
                if (existing >= 0) continue;
                candidates.Add(new(growable, entity, entity.EntityId, coordinates,
                    duration, request.GrowthMultiplier));
            }
        }
        return new(_blocks, elapsedDays, candidates.ToArray());
    }

    internal sealed record Candidate(Growable Growable, EntityComponent Entity, Guid Id,
        Vector3Int Coordinates, float Duration, float Multiplier);

    internal sealed class PreparedGrowth
    {
        private readonly IBlockService _blocks;
        private readonly float _elapsedDays;
        private readonly Candidate[] _candidates;
        private bool _used;
        internal PreparedGrowth(IBlockService blocks, float elapsedDays, Candidate[] candidates) =>
            (_blocks, _elapsedDays, _candidates) = (blocks, elapsedDays, candidates);

        // One synchronous use inside the same coordinator's mutation guard. Never persisted or retried.
        internal TimberbornAshGrowthApplicationResult Apply()
        {
            if (_used) throw new InvalidOperationException("An ash growth interval cannot be replayed.");
            _used = true;
            int applied = 0;
            foreach (var candidate in _candidates)
            {
                var entity = candidate.Entity;
                if (entity.Deleted || !entity.Initialized || !entity) continue;
                if (entity.EntityId != candidate.Id || !ReferenceEquals(candidate.Growable.GetComponent<EntityComponent>(), entity))
                    throw new InvalidOperationException("Ash growth ownership changed after preparation.");
                if (!_blocks.GetObjectsWithComponentAt<Growable>(candidate.Coordinates)
                    .Any(current => ReferenceEquals(current, candidate.Growable))) continue;
                if (entity.Deleted || !entity.Initialized || !entity) continue;
                if (candidate.Growable.GrowthTimeInDays != candidate.Duration)
                    throw new InvalidOperationException("Native growth duration changed after preparation.");
                if (TimberbornAshGrowthRate.Advance(candidate.Growable, _elapsedDays, candidate.Multiplier)) applied++;
            }
            return new(_candidates.Length, applied, 0, _candidates.Length - applied);
        }
    }
}
