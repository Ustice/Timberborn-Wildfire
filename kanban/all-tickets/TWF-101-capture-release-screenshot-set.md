---
ticket: TWF-101
agent_level: Medium
role: qa
requires_qa: true
doc_only: false
dependencies:
  - TWF-066
  - TWF-067
  - TWF-068
  - TWF-070
  - TWF-074
write_scope:
  - release/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-101-capture-release-screenshot-set.md
---

# TWF-101: Capture Release Screenshot Set

## Goal

Capture real live gameplay screenshots for release media.

## Why

Screenshots should prove what the current build actually does. They should not be stale, staged beyond current behavior, or copied from debug-only states.

## Requirements

- Capture at least one coherent gameplay-loop screenshot.
- Capture additional screenshots for fire, smoke, ash, steam, beaver behavior, or aftermath only when those behaviors are QA-proven.
- Preserve screenshot source path, save name, commands, logs, and artifact metadata.
- Exclude screenshots contaminated by desktop apps or debug-only panels unless explicitly labeled as development material.
- Store selected release screenshots in `release/` or a documented durable path.
- Update `docs/TEST_PLAN.md` with screenshot evidence paths.

## Dependencies

- `TWF-066`, `TWF-067`, `TWF-068`, and `TWF-070` provide visual baselines.
- `TWF-074` validates beaver behavior recordings before screenshots imply beaver behavior.

## Parent Reference

- Parent gate: `TWF-055`.

## Role

- QA.
- Follow [qa.md](../roles/qa.md).

## Verification

- Run `git diff --check`.
- Passing evidence requires screenshots from current live behavior with supporting logs or command output.

## Notes

- Thumbnail/key graphic work belongs to `TWF-100`.
