---
ticket: TWF-108
agent_level: Medium
role: reviewer
requires_qa: false
doc_only: false
dependencies:
  - TWF-050
write_scope:
  - src/Wildfire.Timberborn/**
  - tests/Wildfire.Core.Tests/**
  - docs/TEST_PLAN.md
  - kanban/all-tickets/TWF-108-define-release-log-noise-policy.md
---

# TWF-108: Define Release Log Noise Policy

## Goal

Reduce noisy release logging while preserving actionable failure and compatibility evidence.

## Why

Wildfire needs useful diagnostics for support, but release logs should not be flooded by normal fire activity or per-entity consequence spam.

## Requirements

- Inventory current Wildfire log tokens and QA/status outputs.
- Classify which tokens are release errors, warnings, diagnostics, QA-only, or too noisy.
- Reduce noisy logs while preserving actionable failure and compatibility tokens.
- Add tests for logging or status-token behavior where practical.
- Document the release logging policy in `docs/TEST_PLAN.md`.

## Dependencies

- `TWF-050` defines safe GPU/asset failure behavior.

## Parent Reference

- Parent gate: `TWF-060`.

## Role

- Reviewer.
- Follow [tech-lead.md](../roles/tech-lead.md).

## Verification

- Run `git diff --check`.
- Run `dotnet test` if production code changes.

## Notes

- Player alert aggregation belongs to `TWF-080`.
- 2026-05-05 reviewer: added `TimberbornReleaseLogNoisePolicy`, tests for token classification and quiet consequence dispatch behavior, and the release log-noise policy in `docs/TEST_PLAN.md`. Empty per-dispatch summaries for stored goods, path infrastructure, power infrastructure, water infrastructure, and structure rollback now stay out of `Info` logs unless a target matched or an actionable outcome occurred; status and `qa-readiness` counters remain the detailed evidence surface.
