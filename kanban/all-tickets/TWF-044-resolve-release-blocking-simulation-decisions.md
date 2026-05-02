---
ticket: TWF-044
agent_level: Medium
role: researcher
requires_qa: false
doc_only: true
dependencies:
   - TWF-042
write_scope:
   - docs/DESIGN.md
   - docs/ARCHITECTURE.md
   - docs/TEST_PLAN.md
   - kanban/all-tickets/TWF-044-resolve-release-blocking-simulation-decisions.md
---

# TWF-044: Resolve Release Blocking Simulation Decisions

## Goal

Turn the remaining release-relevant open design questions into explicit decisions or deferred non-release items.

## Why

The design currently keeps several questions open: tick cadence, diagonal spread, wind, ash storage, vertical building mapping, water semantics, heat-loss source, and full-grid versus active-frontier dispatch. Release needs the decisions that affect visible behavior to be intentional and documented.

## Requirements

- Review the open questions in `docs/DESIGN.md`.
- Decide or explicitly defer each release-relevant question.
- Keep the initial release conservative unless live evidence strongly supports more complexity.
- Document accepted decisions in `docs/DESIGN.md`.
- Update `docs/ARCHITECTURE.md` only if a boundary changes.
- Update `docs/TEST_PLAN.md` with validation expectations for accepted decisions.
- Add follow-up ticket notes only when a deferred decision needs future implementation.

## Dependencies

- `TWF-042` provides the first player-facing fire loop, making the decisions concrete enough to evaluate.

## Role

- Researcher.
- Follow [researcher.md](../roles/researcher.md).

## Verification

- Run `git diff --check`.
- No runtime validation is required for doc-only decision work.

## Notes

- Prefer "defer for release" over speculative complexity when the current loop can ship without it.

2026-05-02 researcher pass on `codex/TWF-044`:

- Short answer: the first release should keep the current conservative simulator shape. Accepted release behavior is one-second fixed cadence, 6-neighbor spread, no wind, derived ash, explicit vertical footprint mapping, bounded water suppression/wetness, material-driven heat loss, and full-grid dispatch unless `TWF-051` changes that after `TWF-046`.
- Confirmed evidence:

   - `docs/DESIGN.md` already described 6-neighbor spread, deterministic hash behavior, derived visual fields, fixed-cadence Timberborn integration, and full-grid-first dispatch.
   - `src/Wildfire.Timberborn/TimberbornFireSystem.cs` defines `TimberbornFireCadence.Default = FromSeconds(1)` and dispatches only when accumulated elapsed time reaches that interval.
   - `src/Wildfire.Unity/FireSim.compute` reads left, right, north, south, below, and above neighbors only; there is no wind input.
   - `src/Wildfire.Timberborn/TimberbornCellMapping.cs` expands `TimberbornCellFootprint` across `x`, `y`, and `z`, selects material-driven heat-loss bands, and treats water/wetness as a packed water overlay.
   - `TWF-034` live profiling recommended keeping `TWF-011` deferred after acceptable full-grid dispatch timings on the observed `128x128x23` save.
   - `TWF-038` live evidence proves a queued `SetWater=3` suppression change through the GPU path, and `TWF-041` accepted derived ash without packed-cell storage changes.

- Docs updated:

   - `docs/DESIGN.md` now replaces open questions with explicit release simulation decisions.
   - `docs/TEST_PLAN.md` now records validation expectations for the accepted conservative release decisions.

- `docs/ARCHITECTURE.md` was not changed because these decisions confirm the existing ownership boundaries: core contracts and packed data stay host-agnostic, Unity/compute owns fire rules, and Timberborn remains an adapter.
- Deferred follow-up notes:

   - Diagonal spread, wind, persistent ash storage, biome/weather heat loss, and active-frontier dispatch remain future mechanics/optimizations, not release blockers.
   - `TWF-051` still owns the final active-frontier release-scope decision after `TWF-046` supplies coherent live-loop evidence.

- Confidence: high for the initial-release recommendation because it matches current source, tests, live evidence notes, and ticket dependencies. Medium for whether cadence should remain player-configurable later; that belongs to `TWF-048`, not this doc-only decision ticket.
