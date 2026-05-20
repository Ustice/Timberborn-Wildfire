---
ticket: TWF-164
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-079
  - TWF-157
  - TWF-158
  - TWF-160
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/ash-simulation-model.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-164-add-tainted-ash-decay-and-water-washout.md
---

# TWF-164: Add Tainted Ash Decay And Water Washout

## Goal

Make tainted ash naturally fade over time and wash away when water reaches it, while slightly tainting the affected water instead of silently deleting contamination.

## Why

Tainted ash should be hazardous aftermath, not permanent map litter. Players need a natural cleanup path, but fire and ash must still respect the no-decontamination rule: contamination can move or dilute into water only through an explicit, bounded mechanic.

## Requirements

- Tainted ash decays gradually over simulator time.
- Water contact can wash tainted ash out of a cell and reduce or remove the ash level.
- Washed tainted ash slightly taints the affected water or emits bounded contamination telemetry if Timberborn exposes no safe water-contamination mutation path.
- Clean water washing fertile ash may remove ash but must not create tainted water.
- Badwater or already contaminated water remains contaminated when washing ash.
- The first implementation must be deterministic and reproducible from tick, cell, and seed inputs.
- Visual presentation should show ash receding as ash levels fall.
- Status and QA counters should distinguish tainted ash decayed, tainted ash washed, fertile ash washed, water taint attempts, water taint successes, and skipped unsafe water APIs.
- Save/reload must preserve remaining ash state and avoid resurrecting washed ash.
- Do not treat fire, heat, or ash decay as decontamination of soil, goods, plants, buildings, or beavers.

## Dependencies

- `TWF-079` defines contamination-aware fire consequences.
- `TWF-157`, `TWF-158`, and `TWF-160` align ash authority, queued mutations, and persistence/harvest synchronization with simulator-owned ash state.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Prefer implementing decay and washout in simulator-owned ash transport state after the ash migration tickets land.
- If live Timberborn water contamination mutation is not safe, preserve the ash washout behavior and report `skipped_no_safe_water_taint_api` rather than inventing a hidden native mutation.
- Keep the taint amount intentionally small and bounded so water washout is a cleanup vector with a visible tradeoff, not a catastrophic badwater generator.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Run the Unity shader harness if compute shader ash behavior changes.
- Live QA must capture before/after evidence of tainted ash fading or washing out, with status tokens proving the ash level changed and any water-taint path succeeded or safely skipped.

## Notes

- This ticket answers the release design decision that tainted ash fades over time or is washed away by water, and that washed tainted ash slightly taints the water.
