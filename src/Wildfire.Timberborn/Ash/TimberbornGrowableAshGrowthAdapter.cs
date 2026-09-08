using Timberborn.BlockSystem;
using Timberborn.EntitySystem;
using Timberborn.Growing;
using Timberborn.MapIndexSystem;
using Timberborn.TerrainSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn.Ash;

internal sealed class TimberbornGrowableAshGrowthAdapter
{
    private readonly IBlockService _blocks;
    private readonly MapIndexService _indices;
    private readonly IThreadSafeColumnTerrainMap _columns;
    internal TimberbornGrowableAshGrowthAdapter(IBlockService blocks, MapIndexService indices, IThreadSafeColumnTerrainMap columns) =>
        (_blocks, _indices, _columns) = (blocks ?? throw new ArgumentNullException(nameof(blocks)),
            indices ?? throw new ArgumentNullException(nameof(indices)), columns ?? throw new ArgumentNullException(nameof(columns)));

    private bool TrySoilColumn(BlockObject body, Vector3Int coordinates, out int column)
    {
        column = -1;
        if (body.CoordinatesAtBaseZ != coordinates) return false;
        int horizontal = _indices.CellToIndex(new Vector2Int(coordinates.x, coordinates.y));
        return _columns.TryGetIndexAtCeiling(horizontal, coordinates.z, out column) && body.CoordinatesAtBaseZ == coordinates;
    }

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
                var body = growable.GetComponent<BlockObject>() ??
                    throw new InvalidOperationException("Ash growth candidate lacks its native block object.");
                if (!TrySoilColumn(body, coordinates, out int column)) continue;
                float duration = TimberbornAshGrowthRate.ReadDuration(growable);
                TimberbornAshGrowthRate.Validate(elapsedDays, request.GrowthMultiplier, duration);
                var entity = growable.GetComponent<EntityComponent>() ??
                    throw new InvalidOperationException("Ash growth candidate lacks its native entity.");
                if (entity.EntityId == Guid.Empty)
                    throw new InvalidOperationException("Ash growth candidate lacks a native identity.");
                // Exact references, never integer hash identity. A repeated physical target gets one bonus.
                int existing = candidates.FindIndex(candidate => ReferenceEquals(candidate.Growable, growable));
                if (existing >= 0) continue;
                candidates.Add(new(growable, body, entity, entity.EntityId, coordinates, column,
                    duration, request.GrowthMultiplier));
            }
        }
        return new(this, elapsedDays, candidates.ToArray());
    }

    internal sealed record Candidate(Growable Growable, BlockObject Body, EntityComponent Entity, Guid Id,
        Vector3Int Coordinates, int SoilColumn, float Duration, float Multiplier);

    internal sealed class PreparedGrowth
    {
        private readonly TimberbornGrowableAshGrowthAdapter _source;
        private readonly float _elapsedDays;
        private readonly Candidate[] _candidates;
        private bool _used;
        internal PreparedGrowth(TimberbornGrowableAshGrowthAdapter source, float elapsedDays, Candidate[] candidates) =>
            (_source, _elapsedDays, _candidates) = (source, elapsedDays, candidates);

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
                if (!StillAtSoilBase(candidate)) continue;
                if (!_source._blocks.GetObjectsWithComponentAt<Growable>(candidate.Coordinates)
                    .Any(current => ReferenceEquals(current, candidate.Growable))) continue;
                if (entity.Deleted || !entity.Initialized || !entity) continue;
                if (entity.EntityId != candidate.Id || !ReferenceEquals(candidate.Growable.GetComponent<EntityComponent>(), entity))
                    throw new InvalidOperationException("Ash growth ownership changed during spatial revalidation.");
                if (candidate.Growable.GrowthTimeInDays != candidate.Duration)
                    throw new InvalidOperationException("Native growth duration changed after preparation.");
                if (!StillAtSoilBase(candidate)) continue;
                if (entity.Deleted || !entity.Initialized || !entity) continue;
                if (TimberbornAshGrowthRate.Advance(candidate.Growable, _elapsedDays, candidate.Multiplier)) applied++;
            }
            return new(_candidates.Length, applied, 0, _candidates.Length - applied);
        }

        private bool StillAtSoilBase(Candidate candidate) =>
            ReferenceEquals(candidate.Growable.GetComponent<BlockObject>(), candidate.Body) &&
            _source.TrySoilColumn(candidate.Body, candidate.Coordinates, out int column) && column == candidate.SoilColumn;
    }
}
