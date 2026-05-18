---
ticket: TWF-080
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-076
  - TWF-077
  - TWF-078
  - TWF-079
  - TWF-084
  - TWF-115
  - TWF-116
  - TWF-085
  - TWF-086
  - TWF-087
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/reference/timberborn-ui.md
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-080-aggregate-world-consequence-feedback.md
---

# TWF-080: Aggregate World Consequence Feedback

## Goal

Add bounded player feedback for world consequences without spamming one alert per burned cell, plant, crop, or tree.

## Why

The design says player feedback should distinguish active fire, building damage, plant or resource loss, beaver danger, and ash aftermath, but it should aggregate consequences instead of flooding the player.

## Requirements

- Aggregate consequence events per dispatch, time window, or affected region.
- Distinguish at least active fire, building damage or closure, plant/crop/resource loss, beaver danger or death, and fertile or tainted ash aftermath when available.
- Use Timberborn-native alert, status, or quick notification patterns where possible.
- Avoid per-entity spam for common burn aftermath.
- Expose QA/status counters for aggregated event classes, notifications sent, skipped notifications, and presentation failures.
- Add deterministic tests for aggregation, throttling, class selection, and presentation-failure isolation.
- Update `docs/reference/timberborn-ui.md` only if a durable native UI or alert pattern is discovered.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-076` implements crop burn consequences.
- `TWF-077` implements structure burn damage rollback.
- `TWF-078` implements persistent ash fields.
- `TWF-079` implements contamination-aware fire consequences.
- `TWF-084` implements tree burn consequences.
- `TWF-115` implements stored-goods loss.
- `TWF-116` defines explosive hazard behavior.
- `TWF-085`, `TWF-086`, and `TWF-087` implement beaver smoke, toxic smoke, and fire variants.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Aggregate by event class and time window before presenting anything to the player.
- The first implementation should prefer a small number of Timberborn-native alert/status patterns over custom UI.
- Include stored-goods loss from `TWF-115`, but collapse common item losses into summaries such as "stored goods burned" instead of listing every stack.
- Beaver death, building closure, explosive hazard, and tainted ash should be higher-priority classes than ordinary plant loss.
- Expected counters include source events received, events coalesced, notifications sent by class, notifications suppressed by throttle, presentation failures, and log-only fallbacks.
- Safe no-op behavior should record feedback events in status telemetry even when no native UI surface is safe.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- QA must capture live evidence that multiple consequences aggregate into bounded player feedback with no critical log failures.

## Notes

- `TWF-042` provided the first fire alert. This ticket broadens feedback after richer consequence lanes exist.
- 2026-05-17 coordinator/code reconciliation: ash and fertile-ash telemetry now exist in `status`/`qa-readiness`, but this ticket is still open because those counters are not yet aggregated into bounded player-facing consequence feedback.
- Relevant design reference: `docs/DESIGN.md` section 20, "Player Feedback".
