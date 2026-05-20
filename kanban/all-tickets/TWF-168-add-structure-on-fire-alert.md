---
ticket: TWF-168
agent_level: Medium
role: worker
requires_qa: true
doc_only: false
dependencies:
  - TWF-077
  - TWF-080
  - TWF-163
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - docs/reference/assets/**
  - kanban/all-tickets/TWF-168-add-structure-on-fire-alert.md
---

# TWF-168: Add Structure On Fire Alert

## Goal

Add a bounded player-facing alert for structures that are actively on fire, distinct from the generic active-fire alert and from later aggregate consequence summaries.

## Why

`TWF-042` added the first generic Wildfire alert, and `TWF-080` owns broader consequence aggregation. Structures on fire are urgent enough to deserve a recognizable player alert class so the player can understand that a building, not just terrain or vegetation, is burning.

## Requirements

- Detect structure-burning events from the existing burn-damage or compact-delta consequence path.
- Aggregate structure-burning events so one fire front does not spam one alert per building cell.
- Prefer Timberborn-native quick notification, alert, or status patterns.
- Keep the alert distinct from generic new-fire, plant/resource loss, and repaired/burned-out aftermath messages.
- Include a structure-on-fire icon if the native alert surface supports custom icons.
- When using image generation for the icon, use the combined Timberborn sheets in `docs/reference/assets/` as style anchors, especially:
  - `docs/reference/assets/menu-icons/composite.png`
  - `docs/reference/assets/goods-icons/composite.png`
- Use nearby fire/building style references where useful:
  - `docs/reference/assets/menu-icons/CampfireIcon.png`
  - `docs/reference/assets/menu-icons/GrillIcon.png`
  - `docs/reference/assets/menu-icons/BrazierIcon.png`
- Preserve Timberborn icon conventions: square readable silhouette, no text, simple material shading, restrained outline, and no off-style warning badge.
- Expose QA/status counters for structure-on-fire events received, events coalesced, notifications sent, notifications throttled, and presentation failures.
- Add deterministic tests for aggregation, throttling, message/class selection, and presentation-failure isolation.
- Document live QA expectations in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-077` owns structure burn damage and closure behavior.
- `TWF-080` owns broader player feedback aggregation.
- `TWF-163` owns release icon assets and binding conventions.

## Role

- Worker.
- Follow [worker.md](../roles/worker.md).

## Implementation Notes

- Do not reopen the generic `TWF-042` alert as the final UI surface. This ticket should either add a structure-specific class to the aggregate feedback lane or document why the native UI cannot support the distinction safely.
- If `TWF-080` has not landed yet, implement behind the smallest structure-specific aggregation surface that can later be folded into `TWF-080`.
- Avoid one notification per affected cell, footprint tile, or per-tick update.

## Verification

- Run `git diff --check`.
- Run `dotnet test`.
- Run `dotnet build Wildfire.slnx`.
- Live QA must trigger a burning structure, capture copied `Player.log`, command/status output, and a screenshot or recording showing the alert or the exact native-surface blocker.

## Notes

- Created on 2026-05-20 after Jason asked whether the board had a structure-burning alert ticket.
