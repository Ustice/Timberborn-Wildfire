---
ticket: TWF-165
agent_level: High
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-069
  - TWF-142
write_scope:
  - src/Wildfire.Core/**
  - src/Wildfire.Unity/**
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/DESIGN.md
  - docs/ARCHITECTURE.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-165-add-rare-drought-aware-ignition-sources.md
---

# TWF-165: Add Rare Drought Aware Ignition Sources

## Goal

Add deterministic, very rare spontaneous ignition for appropriate high-risk sources: fire-using buildings and dry dead vegetation, especially during drought.

## Why

Release fires need believable ignition sources beyond QA commands. The player should be able to understand why a fire started: a fire-using building was risky, or very dry dead vegetation sparked during drought. Ordinary buildings should not randomly ignite.

## Requirements

- Use deterministic hash-based randomness keyed by cell, tick, season or drought state, and simulation seed. Do not use runtime-global random state.
- Keep spontaneous ignition rare by default and tunable through release or behavior settings.
- Only fire-using or heat-producing buildings should be eligible for spontaneous building ignition.
- Buildings that do not use fire or heat must not spontaneously burn.
- Dead bushes, dead trees, and dry vegetation can be eligible, but should still ignite rarely.
- Drought season or dry environmental state should increase ignition risk through adapter-provided material inputs, not by moving fire rules into Timberborn.
- Wet, irrigated, or water-suppressed cells should strongly reduce or prevent spontaneous ignition.
- Emit clear telemetry for ignition candidates, eligible fire-using buildings, eligible dead vegetation, drought-risk multiplier, ignition rolls, ignitions started, and skipped ineligible sources.
- Player-facing feedback should route through `TWF-080` so rare ignition is explained instead of feeling like hidden punishment.
- Add deterministic tests that prove same seed and tick produce the same ignition results, and changed seed or drought state can change the result.

## Dependencies

- `TWF-069` tunes visible fire behavior and spread pacing with recordings.
- `TWF-083` remains the broader deferred surface-fuel design reference.
- `TWF-142` provides runtime fire simulation parameters and preset binding.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Treat this as an ignition source adapter feeding the simulator, not as Timberborn mutating packed cells behind the simulator's back.
- Start with a conservative allowlist for fire-using buildings, such as campfire-like, grill-like, brazier-like, forge-like, or engine-like categories discovered through blueprint or runtime probes.
- Dead bushes and dead trees can be represented as adapter material states with elevated flammability during drought.
- Avoid map-wide scans if a bounded candidate registry or changed-entity input can provide the same evidence.

## Verification

- Run `git diff --check`.
- Run `dotnet test Wildfire.slnx --no-restore`.
- Run `bun run typecheck`.
- Live QA must capture rare-ignition telemetry using a forced high-test-rate preset, then confirm the release/default preset remains rare and bounded.

## Notes

- This promotes a focused part of the broader `TWF-083` fuel-load idea into release scope. General overgrowth and surface fuel remain deferred unless separately promoted.
